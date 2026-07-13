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

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class MaterialRation : FactoryComponentCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public MaterialRation() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var mechanical = Owner.Creature.CombatState!.CreateCard<MechanicalMaterial>(Owner);
        var compound = Owner.Creature.CombatState!.CreateCard<CompoundMaterial>(Owner);
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_MATERIAL"), 1)
        { Cancelable = false, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, [mechanical, compound], Owner, prefs)).FirstOrDefault();
        if (selected is not null) await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ThermalSmelting : FactoryCardTemplate
{
    public ThermalSmelting() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "decomposer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var scraps = PileType.Hand.GetPile(Owner).Cards.OfType<KillerFactoryScrap>().ToList();
        if (scraps.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE"), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, scraps, Owner, prefs)).FirstOrDefault();
        if (selected is null) return;
        await CardPileCmd.Add(selected, PileType.Exhaust);
        await FactoryCardActions.AddGeneratedCardToHand<UniversalMaterial>(Owner);
        await CardPileCmd.Draw(context, 1, Owner);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ThermalCore : FactoryComponentCard
{
    private bool _choose;
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public ThermalCore() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "decomposer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card.Keywords.Contains(FactoryKeywords.Material) || card is KillerFactoryScrap).ToList();
        if (candidates.Count == 0) return;
        CardModel selected;
        if (_choose)
        {
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_DECOMPOSE"), 1)
            { Cancelable = true, RequireManualConfirmation = true };
            selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs)).FirstOrDefault()!;
            if (selected is null) return;
        }
        else selected = candidates[Random.Shared.Next(candidates.Count)];
        await CardPileCmd.Add(selected, PileType.Exhaust);
        await PlayerCmd.GainEnergy(selected is UniversalMaterial ? 2 : 1, Owner);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => _choose = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class QuickLoad : FactoryComponentCard
{
    private int _draw = 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Draw", 3)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent, CardKeyword.Exhaust];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public QuickLoad() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardPileCmd.Draw(context, _draw, Owner);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade()
    {
        _draw = 4;
        if (DynamicVars.TryGetValue("Draw", out var draw)) draw.UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class EmergencyUnload : FactoryComponentCard
{
    private int _perCard = 3;
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public EmergencyUnload() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self, true, "consumer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var count = PileType.Hand.GetPile(Owner).Cards.Count;
        DynamicVars.Block.BaseValue = count * _perCard;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() { _perCard = 4; DynamicVars.Block.UpgradeValueBy(1); }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class FoldedShield : FactoryComponentCard
{
    public override bool IsFragileComponent => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.PermanentComponent, FactoryKeywords.FragileComponent];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(9, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public FoldedShield() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "component_defend") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class LayeredArmor : FactoryComponentCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(13, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public LayeredArmor() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "component_defend") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PrecisionCuttingCore : FactoryComponentCard
{
    public override bool IsPrecisionComponent => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.PrecisionComponent, FactoryKeywords.PermanentComponent];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue }];
    public PrecisionCuttingCore() : base(1, CardType.Attack, CardRarity.Status, TargetType.AnyEnemy, false, "component_attack") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target).Execute(context);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PrecisionProtectionCore : FactoryComponentCard
{
    public override bool IsPrecisionComponent => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.PrecisionComponent, FactoryKeywords.PermanentComponent];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public PrecisionProtectionCore() : base(1, CardType.Skill, CardRarity.Status, TargetType.Self, false, "component_defend") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class TemporaryCutter : FactoryComponentCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(11, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue }];
    public TemporaryCutter() : base(1, CardType.Attack, CardRarity.Status, TargetType.AnyEnemy, false, "component_attack") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target).Execute(context);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class EmergencyPanel : FactoryComponentCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent, CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(10, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public EmergencyPanel() : base(1, CardType.Skill, CardRarity.Status, TargetType.Self, false, "component_defend") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class StandardUpgrade : FactoryCardTemplate, IFactoryProcedureCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.Procedure];
    public StandardUpgrade() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await ExecuteProcedureAsync(context);
    private List<FactoryComponentCard> GetCandidates() => PileType.Hand.GetPile(Owner).Cards
        .OfType<FactoryComponentCard>().Where(card => card.IsUpgradable).ToList();
    public bool HasLegalTargets(bool fromProcessingTable = false) => GetCandidates().Count > 0;
    public async Task<bool> ExecuteProcedureAsync(
        PlayerChoiceContext context,
        bool fromProcessingTable = false,
        Func<Task<bool>>? commitResources = null)
    {
        var candidates = GetCandidates();
        if (candidates.Count == 0) return false;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_UPGRADE"), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs))
            .OfType<FactoryComponentCard>().FirstOrDefault();
        if (selected is null) return false;
        if (selected.Pile?.Type != PileType.Hand || !selected.IsUpgradable) return false;
        if (commitResources is not null && !await commitResources()) return false;
        if (!await FactoryFusionService.TryBeginProcessingAsync(selected)) return true;
        CardCmd.Upgrade(selected);
        selected.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(_ => { });
        return true;
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Lightweighting : FactoryCardTemplate, IFactoryProcedureCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.Procedure];
    public Lightweighting() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await ExecuteProcedureAsync(context);
    private List<FactoryComponentCard> GetCandidates() => PileType.Hand.GetPile(Owner).Cards.OfType<FactoryComponentCard>()
        .Where(card => card.EnergyCost.GetWithModifiers(CostModifiers.None) > 0).ToList();
    public bool HasLegalTargets(bool fromProcessingTable = false) => GetCandidates().Count > 0;
    public async Task<bool> ExecuteProcedureAsync(
        PlayerChoiceContext context,
        bool fromProcessingTable = false,
        Func<Task<bool>>? commitResources = null)
    {
        var candidates = GetCandidates();
        if (candidates.Count == 0) return false;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_LIGHTWEIGHT"), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs))
            .OfType<FactoryComponentCard>().FirstOrDefault();
        if (selected is null) return false;
        if (selected.Pile?.Type != PileType.Hand || selected.EnergyCost.GetWithModifiers(CostModifiers.None) <= 0) return false;
        if (commitResources is not null && !await commitResources()) return false;
        if (!await FactoryFusionService.TryBeginProcessingAsync(selected)) return true;
        var current = selected.EnergyCost.GetWithModifiers(CostModifiers.None);
        selected.EnergyCost.SetCustomBaseCost(Math.Max(0, current - 1));
        selected.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(_ => { });
        return true;
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
