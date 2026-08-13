using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Cards;

public abstract class AssemblerExpansionCard : AssemblerCardTemplate
{
    protected AssemblerExpansionCard(int cost, CardType type, CardRarity rarity, string portrait = "process")
        : base(cost, type, rarity, TargetType.Self, true, portrait) { }

    protected async Task<CardModel?> SelectOne(PlayerChoiceContext context, IEnumerable<CardModel> cards, string key)
    {
        var list = cards.Distinct().ToList();
        if (list.Count == 0) return null;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", key), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        return (await CardSelectCmd.FromSimpleGrid(context, list, Owner, prefs)).FirstOrDefault();
    }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class TemporaryBuffer : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.Count > 0 &&
        AssemblerCombatState.For(Owner.Creature.CombatState!).BufferedCard is null;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public TemporaryBuffer() : base(0, CardType.Skill, CardRarity.Common) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner); var ordered = hand.Cards.ToList();
        var selected = await SelectOne(context, ordered, "KILLER_FACTORY_SELECT_MOVE_CARD");
        if (selected is null) return;
        var index = ordered.IndexOf(selected);
        var state = AssemblerCombatState.For(Owner.Creature.CombatState!);
        if (index > 0 && index + 1 < ordered.Count && ordered[index - 1] is AssemblerPartCard && ordered[index + 1] is AssemblerPartCard)
            state.PendingBufferDraw = true;
        state.BufferedCard = selected;
        state.BufferedCardCostReduction = IsUpgraded;
        await CardPileCmd.RemoveFromCombat([selected], false);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BatchTag : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.OfType<AssemblerPartCard>()
        .Any(p=>!AssemblerCombatState.For(Owner.Creature.CombatState!).BatchTags.ContainsKey(p));
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public BatchTag() : base(0, CardType.Skill, CardRarity.Common) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state=AssemblerCombatState.For(Owner.Creature.CombatState!);
        var part = await SelectOne(context, PileType.Hand.GetPile(Owner).Cards.OfType<AssemblerPartCard>().Where(p=>!state.BatchTags.ContainsKey(p)), "KILLER_FACTORY_SELECT_PART");
        if (part is not null) state.BatchTags[part] = IsUpgraded;
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BypassRail : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && ValidBypassScraps().Any() &&
        AssemblerCombatState.For(Owner.Creature.CombatState!).BypassedScrap is null;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public BypassRail() : base(0, CardType.Skill, CardRarity.Common) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var scrap = await SelectOne(context, ValidBypassScraps(), "KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE");
        if (scrap is null) return;
        var state = AssemblerCombatState.For(Owner.Creature.CombatState!);
        state.BypassedScrap = scrap;
        state.BypassAlwaysDraw = IsUpgraded;
        var hand=PileType.Hand.GetPile(Owner).Cards.ToList();
        var edge=await SelectOne(context,new[]{hand.First(),hand.Last()},"KILLER_FACTORY_SELECT_MOVE_DESTINATION");
        state.BypassMoveRight=ReferenceEquals(edge,hand.Last());
    }
    private IEnumerable<CardModel> ValidBypassScraps()
    {
        var cards = PileType.Hand.GetPile(Owner).Cards.ToList();
        return cards.Where((card,index) => AssemblerService.IsScrap(card) && index > 0 && index + 1 < cards.Count &&
            cards[index - 1] is AssemblerPartCard && cards[index + 1] is AssemblerPartCard);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class RollerAssembly : AssemblerPartCard
{
    public RollerAssembly() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.RollerFilter, Amount = 1, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardPileCmd.Draw(context, IsUpgraded ? 2 : 1, Owner);
        await AssemblerService.PutHandCardOnDrawBottomAsync(context, Owner);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class OutputCounter : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    public override bool GainsBlock => true;
    public OutputCounter() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "component_defend") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.EndBlockDraw, Amount = IsUpgraded ? 6 : 4, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await AssemblerService.ExecutePartBlockAsync(this, (int)DynamicVars.Block.BaseValue, context, play);
        if (AssemblerService.GetPositionSnapshot(this).IsRightEdge && AssemblerCombatState.For(Owner.Creature.CombatState!).AssembledThisTurn)
            await CardPileCmd.Draw(context, 1, Owner);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ProcessReview : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>()
        .Any(p => p.GetOrCreateCapability<AssemblerProductCapability>().Modules.Count >= 2);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ProcessReview() : base(1, CardType.Skill, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var product = await SelectOne(context, PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>(), "KILLER_FACTORY_SELECT_PRODUCT_REPAIR") as AssembledProduct;
        if (product is null) return;
        var cap = product.GetOrCreateCapability<AssemblerProductCapability>();
        if (cap.Modules.Count < 2) return;
        var candidates = Enumerable.Range(0, cap.Modules.Count - 1)
            .Select(i => cap.GetModulePart(i)).Where(p => p is not null).Cast<CardModel>().ToList();
        var selected = await SelectOne(context, candidates, "KILLER_FACTORY_SELECT_PART") as AssemblerPartCard;
        if (selected is null) return;
        var firstIndex = Enumerable.Range(0, cap.Modules.Count - 1)
            .FirstOrDefault(i => ReferenceEquals(cap.GetModulePart(i), selected), -1);
        if (firstIndex < 0) return;
        var firstKind = cap.Modules[firstIndex].Kind;
        var secondKind = cap.Modules[firstIndex + 1].Kind;
        var before = SatisfiesPosition(firstKind, firstIndex, cap.Modules.Count) ||
                     SatisfiesPosition(secondKind, firstIndex + 1, cap.Modules.Count);
        var after = SatisfiesPosition(secondKind, firstIndex, cap.Modules.Count) ||
                    SatisfiesPosition(firstKind, firstIndex + 1, cap.Modules.Count);
        cap.SwapModules(firstIndex, firstIndex + 1);
        if (after && !before) await CardPileCmd.Draw(context, 1, Owner);
    }
    private static bool SatisfiesPosition(AssemblerModuleKind kind, int index, int count) => kind switch
    {
        AssemblerModuleKind.FlankBranch or AssemblerModuleKind.StructureDurability or AssemblerModuleKind.RollerFilter
            => index == 0 || index == count - 1,
        AssemblerModuleKind.EndBlockDraw => index == count - 1,
        _ => false,
    };
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class MaterialEjector : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>()
        .Any(p => { var cap=p.GetOrCreateCapability<AssemblerProductCapability>(); return cap.Modules.Count>0 &&
            (cap.GetModulePart(0) is not null || cap.GetModulePart(cap.Modules.Count-1) is not null); });
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [] : [CardKeyword.Exhaust];
    public MaterialEjector() : base(1, CardType.Skill, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var product = await SelectOne(context, PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>(), "KILLER_FACTORY_SELECT_PRODUCT_REPAIR") as AssembledProduct;
        if (product is null) return;
        var cap = product.GetOrCreateCapability<AssemblerProductCapability>();
        if (cap.Modules.Count == 0) return;
        var edges=new List<(AssemblerPartCard Part,int Index)>();
        if(cap.GetModulePart(0) is {} first)edges.Add((first,0));
        if(cap.GetModulePart(cap.Modules.Count-1) is {} last&&edges.All(e=>!ReferenceEquals(e.Part,last)))edges.Add((last,cap.Modules.Count-1));
        if(edges.Count==0)return;
        var selected = await SelectOne(context, edges.Select(e=>(CardModel)e.Part), "KILLER_FACTORY_SELECT_PART") as AssemblerPartCard;
        if (selected is null) return;
        var moduleIndex=edges.First(e=>ReferenceEquals(e.Part,selected)).Index;
        var takeLast = moduleIndex==cap.Modules.Count-1;
        var part = cap.GetModulePart(moduleIndex);
        cap.RemoveModuleAt(moduleIndex, -1);
        if (part is not null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(part, PileType.Hand, Owner);
            if (takeLast) AssemblerAbilityRuntime.MakeFreeUntilPlayed(part);
            else await CardPileCmd.Draw(context,1,Owner);
        }
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class SecondaryFeeding : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Discard.GetPile(Owner).Cards.OfType<AssemblerPartCard>().Any();
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public SecondaryFeeding() : base(1, CardType.Skill, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var parts = PileType.Discard.GetPile(Owner).Cards.OfType<AssemblerPartCard>().Cast<CardModel>().ToList();
        var selected = await AssemblerService.SelectUpToAsync(context, Owner, parts, 1, IsUpgraded ? 3 : 2, "KILLER_FACTORY_SELECT_PART");
        foreach (var card in selected.AsEnumerable().Reverse()) await CardPileCmd.Add(card, PileType.Draw);
        if (selected.Any(c=>c.Type==CardType.Attack)&&selected.Any(c=>c.Type==CardType.Skill))
            await CardPileCmd.Draw(context, 1, Owner);
        if (IsUpgraded && selected.Count>=2 && selected[0].Type!=selected[^1].Type)
            await CardPileCmd.Draw(context, 1, Owner);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class InterlockStation : AssemblerExpansionCard
{
    public InterlockStation() : base(1, CardType.Power, CardRarity.Uncommon, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) { AssemblerCombatState.For(Owner.Creature.CombatState!).InterlockStation += IsUpgraded ? 2 : 1; return Task.CompletedTask; }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReplenishmentOrder : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && AssemblerCombatState.For(Owner.Creature.CombatState!).AssembledThisTurn;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ReplenishmentOrder() : base(1, CardType.Skill, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state = AssemblerCombatState.For(Owner.Creature.CombatState!); if (!state.AssembledThisTurn || state.LatestProduct is null) return;
        var hand = PileType.Hand.GetPile(Owner).Cards.ToList(); var index = hand.IndexOf(state.LatestProduct);
        var target = index == 0 ? (IsUpgraded ? 6 : 5) : index == hand.Count - 1 ? (IsUpgraded ? 5 : 4) : 0;
        var draw = target > 0 ? Math.Max(0, target - hand.Count) : (IsUpgraded ? 3 : 2);
        if (draw > 0) await CardPileCmd.Draw(context, draw, Owner);
        if (index == hand.Count - 1) await PlayerCmd.GainEnergy(1, Owner);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ModuleRecirculation : AssemblerExpansionCard
{
    public ModuleRecirculation() : base(2, CardType.Power, CardRarity.Rare, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) { AssemblerCombatState.For(Owner.Creature.CombatState!).ModuleRecirculation++; return Task.CompletedTask; }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class FullLineRearrangement : AssemblerExpansionCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public FullLineRearrangement() : base(1, CardType.Skill, CardRarity.Rare) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner); var cards = hand.Cards.ToList();
        var arranged = await AssemblerService.SelectUpToAsync(context, Owner, cards, cards.Count, cards.Count,
            "KILLER_FACTORY_SELECT_SORTING");
        if (arranged.Count != cards.Count) return;
        foreach (var card in cards) hand.RemoveInternal(card, true);
        foreach (var card in arranged) hand.AddInternal(card, hand.Cards.Count, false);
        var pairs = arranged.Zip(arranged.Skip(1)).Count(p => p.First is AssemblerPartCard && p.Second is AssemblerPartCard);
        if (pairs > 0) await CardPileCmd.Draw(context, Math.Min(3, pairs), Owner);
        if (arranged.Count > 1 && arranged[0] is AssemblerPartCard && arranged[^1] is AssemblerPartCard) await PlayerCmd.GainEnergy(1, Owner);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class EndpointRecovery : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards is var cards && cards.Count > 0 &&
        (AssemblerService.IsScrap(cards[0]) || AssemblerService.IsScrap(cards[^1]));
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public EndpointRecovery() : base(0, CardType.Skill, CardRarity.Common, "scrap") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var cards = PileType.Hand.GetPile(Owner).Cards.ToList(); if (cards.Count == 0) return;
        var targets = IsUpgraded ? new[] { cards[0], cards[^1] }.Where(AssemblerService.IsScrap).Distinct().ToList()
            : new[] { cards[0] }.Where(c => c is MechanicalScrap).Concat(new[] { cards[^1] }.Where(c => c is BiologicalScrap)).Distinct().ToList();
        foreach (var target in targets) await AssemblerService.ProcessScrapAsync(target);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ChipEjection : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move)];
    public ChipEjection() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true, "component_attack") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.Damage, Amount = IsUpgraded ? 8 : 6, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var snapshot = AssemblerService.GetPositionSnapshot(this);
        await AssemblerService.ExecutePartAttackAsync(this, (int)DynamicVars.Damage.BaseValue, context, play);
        if (snapshot.Right is MechanicalScrap scrap)
        { await AssemblerService.ProcessScrapAsync(scrap); if (play.Target is not null && !play.Target.IsDead) await DamageCmd.Attack(IsUpgraded ? 5 : 4).FromCard(this, play).Targeting(play.Target).Execute(context); }
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class CultureChamber : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7, ValueProp.Move)]; public override bool GainsBlock => true;
    public CultureChamber() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "component_defend") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.BiologicalBlock, Amount = IsUpgraded ? 8 : 6, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { await AssemblerService.ExecutePartBlockAsync(this, (int)DynamicVars.Block.BaseValue, context, play); var scrap=Owner.Creature.CombatState!.CreateCard<BiologicalScrap>(Owner); await CardPileCmd.AddGeneratedCardToCombat(scrap,PileType.Discard,Owner); }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class MetabolicPump : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)]; public override bool GainsBlock => true;
    public MetabolicPump() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "component_defend") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind=AssemblerModuleKind.Block, Amount=IsUpgraded?7:5, SourceTitle=Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { var snapshot=AssemblerService.GetPositionSnapshot(this); var scraps=new[]{snapshot.Left,snapshot.Right}.OfType<BiologicalScrap>().ToList(); await AssemblerService.ExecutePartBlockAsync(this,(int)DynamicVars.Block.BaseValue+scraps.Count*(IsUpgraded?4:3),context,play); foreach(var s in scraps)await AssemblerService.ProcessScrapAsync(s); }
    protected override void OnUpgrade()=>DynamicVars.Block.UpgradeValueBy(2);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class PolaritySorting : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.Any(c=>c is MechanicalScrap) &&
        PileType.Hand.GetPile(Owner).Cards.Any(c=>c is BiologicalScrap);
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [] : [CardKeyword.Exhaust];
    public PolaritySorting():base(0,CardType.Skill,CardRarity.Common,"scrap"){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    { var hand=PileType.Hand.GetPile(Owner); var m=await SelectOne(context,hand.Cards.OfType<MechanicalScrap>(),"KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE"); var b=await SelectOne(context,hand.Cards.OfType<BiologicalScrap>(),"KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE"); if(m is null||b is null)return; var list=hand.Cards.ToList(); var mi=list.IndexOf(m);var bi=list.IndexOf(b);hand.RemoveInternal(m,true);hand.RemoveInternal(b,true);hand.AddInternal(m,bi,false);hand.AddInternal(b,mi,false);await CardPileCmd.Draw(context,1,Owner); }
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class DualStreamRecovery : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.Any(AssemblerService.IsScrap);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public DualStreamRecovery():base(1,CardType.Skill,CardRarity.Uncommon,"scrap"){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    { var hand=PileType.Hand.GetPile(Owner);var m=await SelectOne(context,hand.Cards.OfType<MechanicalScrap>(),"KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE");var b=await SelectOne(context,hand.Cards.OfType<BiologicalScrap>(),"KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE");if(m is not null)await AssemblerService.ProcessScrapAsync(m);if(b is not null)await AssemblerService.ProcessScrapAsync(b);if(m is not null&&b is not null)await CardPileCmd.Draw(context,1,Owner);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class MechBioCoupler : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && (AssemblerService.GetPositionSnapshot(this).Left is CardModel left && AssemblerService.IsScrap(left) ||
        AssemblerService.GetPositionSnapshot(this).Right is CardModel right && AssemblerService.IsScrap(right));
    public MechBioCoupler():base(1,CardType.Skill,CardRarity.Uncommon,"scrap"){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var snapshot=AssemblerService.GetPositionSnapshot(this);var targets=new[]{snapshot.Left,snapshot.Right}.Where(c=>c is not null&&AssemblerService.IsScrap(c)).Cast<CardModel>().ToList();if(targets.Count==0)return;var mixed=targets.Any(c=>c is MechanicalScrap)&&targets.Any(c=>c is BiologicalScrap);foreach(var s in (mixed?targets:targets.Take(1)))await AssemblerService.ProcessScrapAsync(s);if(mixed){await CardPileCmd.Draw(context,1,Owner);await PlayerCmd.GainEnergy(1,Owner);}}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class RemoteRecycling : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Discard.GetPile(Owner).Cards.Any(AssemblerService.IsScrap);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public RemoteRecycling():base(1,CardType.Skill,CardRarity.Uncommon,"scrap"){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var scraps=PileType.Discard.GetPile(Owner).Cards.Where(AssemblerService.IsScrap).ToList();var selected=await AssemblerService.SelectUpToAsync(context,Owner,scraps,1,IsUpgraded?3:2,"KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE");foreach(var s in selected)await AssemblerService.ProcessScrapAsync(s,PileType.Hand,s is MechanicalScrap?0:int.MaxValue);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ClassificationProtocol : AssemblerExpansionCard
{
    public ClassificationProtocol():base(2,CardType.Power,CardRarity.Uncommon,"producer"){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play){AssemblerCombatState.For(Owner.Creature.CombatState!).ClassificationProtocol++;return Task.CompletedTask;}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class LocalReclamation : AssemblerExpansionCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>().Any(product =>
    { var cards=PileType.Hand.GetPile(Owner).Cards.ToList();var i=cards.IndexOf(product);return i>=0 &&
        (i>0&&AssemblerService.IsScrap(cards[i-1]) || i+1<cards.Count&&AssemblerService.IsScrap(cards[i+1])); });
    public LocalReclamation():base(1,CardType.Skill,CardRarity.Uncommon,"scrap"){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var product=await SelectOne(context,PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>(),"KILLER_FACTORY_SELECT_PRODUCT_REPAIR") as AssembledProduct;if(product is null)return;var hand=PileType.Hand.GetPile(Owner).Cards.ToList();var i=hand.IndexOf(product);var scraps=new[]{i>0?hand[i-1]:null,i+1<hand.Count?hand[i+1]:null}.Where(c=>c is not null&&AssemblerService.IsScrap(c)).Cast<CardModel>().ToList();foreach(var s in scraps)await AssemblerService.ProcessScrapAsync(s);if(scraps.Count>0)await AssemblerService.RepairProductAsync(product,scraps.Count);if(IsUpgraded)AssemblerAbilityRuntime.ReduceCostUntilPlayed(product,1);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ZeroWasteFactory : AssemblerExpansionCard
{
    public ZeroWasteFactory():base(2,CardType.Power,CardRarity.Rare,"producer"){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play){var state=AssemblerCombatState.For(Owner.Creature.CombatState!);state.ZeroWasteFactory++;if(IsUpgraded)state.ZeroWasteProcessesAnyScrap=true;return Task.CompletedTask;}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class HeterogeneousGrowth : AssemblerExpansionCard
{
    public HeterogeneousGrowth():base(2,CardType.Power,CardRarity.Rare,"producer"){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play){var state=AssemblerCombatState.For(Owner.Creature.CombatState!);state.HeterogeneousGrowth++;if(IsUpgraded)state.HeterogeneousGrowthDraw++;return Task.CompletedTask;}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

internal static class ExpansionSelectionHack
{
    public static async Task<CardModel?> SelectVia(this TemporaryBuffer _,PlayerChoiceContext context,MegaCrit.Sts2.Core.Entities.Players.Player owner,IEnumerable<CardModel> cards)
    {var list=cards.ToList();if(list.Count==0)return null;var prefs=new CardSelectorPrefs(new LocString("card_selection","KILLER_FACTORY_SELECT_MOVE_CARD"),1){Cancelable=true};return(await CardSelectCmd.FromSimpleGrid(context,list,owner,prefs)).FirstOrDefault();}
}
