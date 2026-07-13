using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class GroundExploration : FactoryComponentCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public GroundExploration() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await FactoryCardActions.AddGeneratedCardToHand<MechanicalMaterial>(Owner);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class BiologicalExcision : FactoryComponentCard
{
    private bool _upgraded;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4, ValueProp.Move), new IntVar("MaterialCount", 1)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue }];
    public BiologicalExcision() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var hpBefore = play.Target.CurrentHp;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target).Execute(context);
        if (play.Target.CurrentHp < hpBefore)
            await FactoryCardActions.AddGeneratedCardToHand<CompoundMaterial>(Owner, _upgraded ? 2 : 1);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade()
    {
        _upgraded = true;
        DynamicVars.Damage.UpgradeValueBy(2);
        if (DynamicVars.TryGetValue("MaterialCount", out var count)) count.UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Accumulate : FactoryComponentCard
{
    private int _materials = 2;
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(12, ValueProp.Move), new IntVar("MaterialCount", 2)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public Accumulate() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryCardActions.AddGeneratedCard<MechanicalMaterial>(Owner, PileType.Draw, _materials);
        await FactoryCardActions.AddGeneratedCard<CompoundMaterial>(Owner, PileType.Draw, _materials);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade()
    {
        _materials = 3;
        DynamicVars.Block.UpgradeValueBy(4);
        if (DynamicVars.TryGetValue("MaterialCount", out var count)) count.UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class HandcraftedBlade : FactoryCardTemplate
{
    private bool _upgraded;
    public HandcraftedBlade() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "consumer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!await FactoryCardActions.ExhaustMaterials<MechanicalMaterial>(Owner, 2))
        {
            FactoryCombatState.For(Owner.Creature.CombatState!).Record("手制刀片：缺少两张机械原料");
            return;
        }
        await FactoryCardActions.AddGeneratedCardToHand<ReciprocatingBlade>(Owner);
        if (_upgraded)
        {
            var generated = PileType.Hand.GetPile(Owner).Cards.OfType<ReciprocatingBlade>().LastOrDefault();
            if (generated is { IsUpgradable: true }) CardCmd.Upgrade(generated);
        }
    }
    protected override void OnUpgrade() => _upgraded = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ReciprocatingBlade : FactoryComponentCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue, Hits = 2 }];
    public ReciprocatingBlade() : base(0, CardType.Attack, CardRarity.Status, TargetType.AllEnemies, false, "consumer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        for (var hit = 0; hit < 2; hit++)
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
                .TargetingAllOpponents(Owner.Creature.CombatState!).Execute(context);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class CompoundProtection : FactoryCardTemplate
{
    private bool _upgraded;
    public CompoundProtection() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "consumer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!await FactoryCardActions.ExhaustMaterials<CompoundMaterial>(Owner, 1))
        {
            FactoryCombatState.For(Owner.Creature.CombatState!).Record("化合加护：缺少化合原料");
            return;
        }
        await FactoryCardActions.AddGeneratedCardToHand<BufferPlate>(Owner);
        if (_upgraded)
        {
            var generated = PileType.Hand.GetPile(Owner).Cards.OfType<BufferPlate>().LastOrDefault();
            if (generated is { IsUpgradable: true }) CardCmd.Upgrade(generated);
        }
    }
    protected override void OnUpgrade() => _upgraded = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class BufferPlate : FactoryComponentCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent, CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(10, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public BufferPlate() : base(0, CardType.Skill, CardRarity.Status, TargetType.Self, false, "consumer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(5);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class UniversalReplacement : FactoryComponentCard
{
    private int _count = 1;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("TransformCount", 1)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [FactoryKeywords.PermanentComponent, CardKeyword.Retain]
        : [FactoryKeywords.PermanentComponent];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public UniversalReplacement() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards.Where(card => !ReferenceEquals(card, this)).ToList();
        if (candidates.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_REPLACEMENT"), Math.Min(_count, candidates.Count))
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs);
        foreach (var card in selected)
            await CardCmd.TransformTo<UniversalMaterial>(card, CardPreviewStyle.None);
    }
    protected override void OnUpgrade()
    {
        _count = 2;
        if (DynamicVars.TryGetValue("TransformCount", out var count)) count.UpgradeValueBy(1);
        AddKeyword(CardKeyword.Retain);
    }
}
