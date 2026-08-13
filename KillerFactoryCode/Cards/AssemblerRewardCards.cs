using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using KillerFactory.Mechanics;
using KillerFactory.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Cards;

public abstract class AssemblerAssemblyCard : AssemblerCardTemplate
{
    protected AssemblerAssemblyCard(int cost, CardRarity rarity, string portrait = "process")
        : base(cost, CardType.Skill, rarity, TargetType.Self, true, portrait) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => [AssemblerKeywords.Assembly, CardKeyword.Exhaust];
    protected async Task<AssembledProduct?> Assemble(PlayerChoiceContext context, int count, int cost, int durability, int scrap,
        bool repeat = false)
    {
        var parts = await AssemblerService.SelectConsecutivePartsAsync(context, Owner, count);
        return parts is null ? null : await AssemblerService.AssembleAsync(Owner, parts, cost, durability, scrap, repeat);
    }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ConveyorBelt : AssemblerCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ConveyorBelt() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (IsUpgraded) await CardPileCmd.Draw(context, 1, Owner);
        var cards = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (cards.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_MOVE_CARD"), 1) { Cancelable = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, cards, Owner, prefs)).FirstOrDefault();
        if (selected is null) return;
        cards = PileType.Hand.GetPile(Owner).Cards.ToList();
        var edgePrefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_MOVE_DESTINATION"), 1) { Cancelable = true };
        var edge = (await CardSelectCmd.FromSimpleGrid(context, [cards.First(), cards.Last()], Owner, edgePrefs)).FirstOrDefault();
        if (edge is not null) AssemblerService.MoveWithinHand(selected, ReferenceEquals(edge, cards.First()) ? 0 : cards.Count - 1);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ScrapSorting : AssemblerCardTemplate
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.Any(AssemblerService.IsScrap);
    public ScrapSorting() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "scrap") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var scraps = PileType.Hand.GetPile(Owner).Cards.Where(AssemblerService.IsScrap).ToList();
        var selected = await AssemblerService.SelectUpToAsync(context, Owner, scraps, 1,
            IsUpgraded ? 2 : 1, "KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE");
        foreach (var card in selected.OrderBy(card => PileType.Hand.GetPile(Owner).Cards.ToList().IndexOf(card)).ToList())
            await AssemblerService.ProcessScrapAsync(card);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class QuickAssembly : AssemblerAssemblyCard
{
    protected override bool IsPlayable => base.IsPlayable && AssemblerService.HasConsecutiveParts(Owner, 2);
    public QuickAssembly() : base(0, CardRarity.Common) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var product = await Assemble(context, 2, 0, 1, 1);
        if (product is not null && IsUpgraded) await CardPileCmd.Draw(context, 1, Owner);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class TripleAssembly : AssemblerAssemblyCard
{
    protected override bool IsPlayable => base.IsPlayable && AssemblerService.HasConsecutiveParts(Owner, 3);
    public TripleAssembly() : base(1, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await Assemble(context, 3, IsUpgraded ? 1 : 2, 2, 2);
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class PrecisionAssembly : AssemblerAssemblyCard
{
    protected override bool IsPlayable => base.IsPlayable && AssemblerService.HasConsecutiveParts(Owner, 2);
    public PrecisionAssembly() : base(1, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var parts = await AssemblerService.SelectConsecutivePartsAsync(context, Owner, 2, 3);
        if (parts is not null)
            await AssemblerService.AssembleAsync(Owner, parts, parts.Cards.Count - 1,
                IsUpgraded ? 3 : 2, Math.Max(0, parts.Cards.Count - 2));
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class DiverterValve : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move)];
    public override bool GainsBlock => true;
    public DiverterValve() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "component_defend") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.ConvertNextDamageToAll, BonusAmount = IsUpgraded ? 25 : 0, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await AssemblerService.ExecutePartBlockAsync(this, (int)DynamicVars.Block.BaseValue, context, play);
        AssemblerService.TryMarkRightPart(this, p => p.Type == CardType.Attack, modifier =>
        {
            modifier.MakeDamageAll = true;
            modifier.AreaMultiplier = IsUpgraded ? 1m : 0.75m;
        });
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReinforcedFrame : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8, ValueProp.Move)];
    public override bool GainsBlock => true;
    public ReinforcedFrame() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "component_defend") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.StructureDurability, Amount = 1, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var snapshot = AssemblerService.GetPositionSnapshot(this);
        var amount = (int)DynamicVars.Block.BaseValue + (snapshot.IsRightEdge ? (IsUpgraded ? 4 : 3) : 0);
        await AssemblerService.ExecutePartBlockAsync(this, amount, context, play);
        if (snapshot.IsLeftEdge) await AssemblerService.SelectProductToRepairAsync(context, Owner, IsUpgraded ? 2 : 1);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ScrapSmelting : AssemblerCardTemplate
{
    public override bool GainsBlock => true;
    public ScrapSmelting() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "scrap") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var scraps = PileType.Hand.GetPile(Owner).Cards.Where(AssemblerService.IsScrap).ToList();
        var selected = await AssemblerService.SelectUpToAsync(context, Owner, scraps, 1, 3,
            "KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE");
        foreach (var card in selected) await CardPileCmd.Add(card, PileType.Exhaust);
        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(selected.Count * (IsUpgraded ? 6 : 4), ValueProp.Move), play);
        if (selected.Count >= 2) await PlayerCmd.GainEnergy(1, Owner);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class TemporaryPatch : AssemblerCardTemplate
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>()
        .Any(p => p.GetOrCreateCapability<AssemblerProductCapability>().IsBroken);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public TemporaryPatch() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var products = PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>().Where(p => p.GetOrCreateCapability<AssemblerProductCapability>().IsBroken).Cast<CardModel>().ToList();
        if (products.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_PRODUCT_REPAIR"), 1) { Cancelable = true };
        var product = (await CardSelectCmd.FromSimpleGrid(context, products, Owner, prefs)).OfType<AssembledProduct>().FirstOrDefault();
        if (product is null) return;
        await AssemblerService.RepairProductAsync(product, 1);
        AssemblerAbilityRuntime.MakeFreeUntilPlayed(product);
        product.GetOrCreateCapability<AssemblerProductCapability>().SetPatchedAfterUse(IsUpgraded ? 2 : 1);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReverseConveyor : AssemblerCardTemplate
{
    public ReverseConveyor() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner); var cards = hand.Cards.ToList();
        var selected = await AssemblerService.SelectUpToAsync(context, Owner, cards, 2, 4,
            "KILLER_FACTORY_SELECT_SORTING");
        if (selected.Count < 2) return;
        cards = hand.Cards.ToList(); var indices = selected.Select(card => cards.IndexOf(card)).Order().ToList();
        if (indices.Select((x, i) => x == indices[0] + i).Any(ok => !ok)) return;
        var reversed = selected.OrderByDescending(cards.IndexOf).ToList();
        foreach (var card in reversed) { hand.RemoveInternal(card, true); hand.AddInternal(card, indices[0], false); }
        await CardPileCmd.Draw(context, 1, Owner);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ScrapFilling : AssemblerAssemblyCard
{
    protected override bool IsPlayable => base.IsPlayable && HasValidWindow();
    public ScrapFilling() : base(1, CardRarity.Uncommon, "scrap") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var ordered = hand.Cards.ToList();
        var candidates = ordered.Where(c => c is AssemblerPartCard || AssemblerService.IsScrap(c)).ToList();
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_SORTING"), 3)
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs)).Distinct().ToList();
        if (selected.Count != 3) return;
        ordered = hand.Cards.ToList();
        var indexed = selected.Select(c => (Card:c, Index:ordered.IndexOf(c))).OrderBy(x=>x.Index).ToList();
        if (indexed[0].Index < 0 || indexed.Select((x,i)=>x.Index==indexed[0].Index+i).Any(ok=>!ok)) return;
        var cards = indexed.Select(x=>x.Card).ToList();
        if (cards.Count(c=>c is AssemblerPartCard)!=2 || cards.Count(AssemblerService.IsScrap)!=1) return;
        var scrap = cards.Single(AssemblerService.IsScrap);
        var scrapIndex = cards.IndexOf(scrap);
        var parts = cards.OfType<AssemblerPartCard>().ToList();
        await CardPileCmd.RemoveFromCombat([scrap], false);
        ordered = hand.Cards.ToList();
        var leftIndex = ordered.IndexOf(parts[0]);
        if (leftIndex < 0 || ordered.IndexOf(parts[1]) != leftIndex + 1) return;
        var product = await AssemblerService.AssembleAsync(Owner,
            new AssemblerService.ConsecutiveParts(parts, leftIndex), 1, 2, 0);
        product?.GetOrCreateCapability<AssemblerProductCapability>().InsertModule(
            new AssemblerModuleSpec { Kind = AssemblerModuleKind.ScrapBlock, Amount = IsUpgraded ? 8 : 5, SourceTitle = scrap.Title, SourceType = scrap.Type },
            scrapIndex, -1);
    }
    private bool HasValidWindow()
    {
        var cards = PileType.Hand.GetPile(Owner).Cards.ToList();
        return cards.Count >= 3 && cards.Zip(cards.Skip(1), cards.Skip(2))
            .Any(w => new[] { w.First, w.Second, w.Third }.Count(c=>c is AssemblerPartCard)==2 &&
                      new[] { w.First, w.Second, w.Third }.Count(AssemblerService.IsScrap)==1);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class FullAutoProduction : AssemblerCardTemplate
{
    public FullAutoProduction() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) { AssemblerCombatState.For(Owner.Creature.CombatState!).FullAutoProduction++; return Task.CompletedTask; }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class PreventiveMaintenance : AssemblerCardTemplate
{
    public PreventiveMaintenance() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self, true, "process") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) { var state = AssemblerCombatState.For(Owner.Creature.CombatState!); state.PreventiveMaintenance = true; if (IsUpgraded) state.PreventiveRepairDraw = true; return Task.CompletedTask; }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class SelfRepairCore : AssemblerPartCard
{
    public SelfRepairCore() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self, true, "producer") { }
    public override AssemblerModuleSpec CreateModule() => new() { Kind = AssemblerModuleKind.SelfRepair, Amount = 1, BonusAmount = IsUpgraded ? 1 : 0, SourceTitle = Title };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await AssemblerService.SelectProductToRepairAsync(context, Owner, 2);
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReverseEngineering : AssemblerCardTemplate
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>().Any();
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public ReverseEngineering() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var products = PileType.Hand.GetPile(Owner).Cards.OfType<AssembledProduct>().Cast<CardModel>().ToList();
        if (products.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_PRODUCT_REPAIR"), 1) { Cancelable = true };
        var product = (await CardSelectCmd.FromSimpleGrid(context, products, Owner, prefs)).OfType<AssembledProduct>().FirstOrDefault();
        if (product is null) return;
        var parts = product.GetOrCreateCapability<AssemblerProductCapability>().OriginalParts.ToList();
        if (!IsUpgraded) parts.Reverse();
        await CardPileCmd.RemoveFromCombat([product], false);
        foreach (var part in parts) await CardPileCmd.AddGeneratedCardToCombat(part, PileType.Hand, Owner);
        if (IsUpgraded && parts.Count > 0) AssemblerAbilityRuntime.MakeFreeUntilPlayed(parts[0]);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class IndustrialMiracle : AssemblerAssemblyCard
{
    protected override bool IsPlayable => base.IsPlayable && AssemblerService.HasConsecutiveParts(Owner, 4);
    public IndustrialMiracle() : base(2, CardRarity.Rare, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await Assemble(context, 4, 2, 3, IsUpgraded ? 2 : 3, true);
    protected override void OnUpgrade() { }
}
