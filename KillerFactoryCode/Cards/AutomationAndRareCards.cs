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
using KillerFactory.Ui;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Filter : FactoryComponentCard
{
    private int _draw = 3;
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public Filter() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardPileCmd.Draw(context, _draw, Owner);
        while (true)
        {
            var candidates = PileType.Hand.GetPile(Owner).Cards.Where(card => !ReferenceEquals(card, this)).ToList();
            if (candidates.Count == 0) break;
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_FILTER_DISCARD"), 1)
            { Cancelable = true, RequireManualConfirmation = true };
            var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs)).FirstOrDefault();
            if (selected is null) break;
            await CardPileCmd.Add(selected, PileType.Discard);
        }
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => _draw = 4;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PortableBattery : FactoryComponentCard
{
    public int Charge { get; private set; } = 2;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public PortableBattery() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    public int ConsumeCharge(int amount) { var paid = Math.Min(amount, Charge); Charge -= paid; return paid; }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade() => Charge = 3;
}

public abstract class FactoryBlueprintCard : FactoryCardTemplate
{
    protected FactoryBlueprintCard(int cost, CardRarity rarity) : base(cost, CardType.Power, rarity, TargetType.Self, true, "producer") { }
    protected abstract IReadOnlyList<BlueprintOptionCard> CreateOptions();
    protected virtual int StartingEnergy => 0;
    protected virtual int ExtraMaxEnergy => 0;
    protected virtual bool OwnRefresh => false;
    protected T CreateOption<T>() where T : BlueprintOptionCard => Owner.Creature.CombatState!.CreateCard<T>(Owner);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state = FactoryCombatState.For(Owner.Creature.CombatState!);
        var options = CreateOptions().ToList();
        if (state.BlueprintExtraOptions > 0)
        {
            var extras = BlueprintOptionCard.CreateAll(Owner).Where(extra => options.All(option => option.GetType() != extra.GetType()))
                .OrderBy(_ => Random.Shared.Next()).Take(state.BlueprintExtraOptions);
            options.AddRange(extras);
        }
        var canRefresh = OwnRefresh || state.BlueprintFreeRefresh;
        if (canRefresh) options.Add(CreateOption<RefreshBlueprintOption>());
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_BLUEPRINT"), 1)
        { Cancelable = false, RequireManualConfirmation = true };
        var option = (await CardSelectCmd.FromSimpleGrid(context, options.Cast<MegaCrit.Sts2.Core.Models.CardModel>().ToList(), Owner, prefs))
            .OfType<BlueprintOptionCard>().FirstOrDefault();
        if (option is RefreshBlueprintOption)
        {
            options = CreateOptions().OrderBy(_ => Random.Shared.Next()).ToList();
            option = (await CardSelectCmd.FromSimpleGrid(context, options.Cast<MegaCrit.Sts2.Core.Models.CardModel>().ToList(), Owner, prefs))
                .OfType<BlueprintOptionCard>().FirstOrDefault();
        }
        if (option is not null) state.InstallMachine(option.MachineName, option.MachineKind, StartingEnergy, 5 + ExtraMaxEnergy);
    }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class BasicSetup : FactoryBlueprintCard
{
    private int _energy;
    protected override IReadOnlyList<BlueprintOptionCard> CreateOptions() =>
        [CreateOption<ArmBlueprintOption>(), CreateOption<StorageBlueprintOption>(), CreateOption<DecomposerBlueprintOption>()];
    protected override int StartingEnergy => _energy;
    public BasicSetup() : base(1, CardRarity.Common) { }
    protected override void OnUpgrade() => _energy = 2;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ProcessingBlueprint : FactoryBlueprintCard
{
    private bool _refresh;
    protected override IReadOnlyList<BlueprintOptionCard> CreateOptions() =>
        [CreateOption<ProcessingTableBlueprintOption>(), CreateOption<UpgradeTableBlueprintOption>(), CreateOption<RepairTableBlueprintOption>()];
    public ProcessingBlueprint() : base(1, CardRarity.Uncommon) { }
    protected override bool OwnRefresh => _refresh;
    protected override void OnUpgrade() => _refresh = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PowerBlueprint : FactoryBlueprintCard
{
    private bool _recharge;
    protected override IReadOnlyList<BlueprintOptionCard> CreateOptions() =>
        [CreateOption<SolarBlueprintOption>(), CreateOption<EnergyStorageBlueprintOption>(), CreateOption<CoilBlueprintOption>()];
    public PowerBlueprint() : base(1, CardRarity.Uncommon) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await base.OnPlay(context, play);
        if (_recharge) FactoryCombatState.For(Owner.Creature.CombatState!).RechargeMachines();
    }
    protected override void OnUpgrade() => _recharge = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PrecisionSetup : FactoryBlueprintCard
{
    private int _energy;
    private int _extraMax;
    protected override IReadOnlyList<BlueprintOptionCard> CreateOptions() =>
        [CreateOption<PrecisionArmBlueprintOption>(), CreateOption<AdvancedTableBlueprintOption>(), CreateOption<SmartStorageBlueprintOption>()];
    protected override int StartingEnergy => _energy;
    protected override int ExtraMaxEnergy => _extraMax;
    public PrecisionSetup() : base(2, CardRarity.Rare) { }
    protected override void OnUpgrade() { _energy = 2; _extraMax = 2; }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class AutoLoadProtocol : FactoryCardTemplate
{
    public AutoLoadProtocol() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { var s = FactoryCombatState.For(Owner.Creature.CombatState!); s.AutoLoadProtocol = true; s.AutoLoadDraw = IsUpgraded; return Task.CompletedTask; }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class AutoStartProtocol : FactoryCardTemplate
{
    public AutoStartProtocol() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { var s = FactoryCombatState.For(Owner.Creature.CombatState!); s.AutoStartProtocol = true; s.AutoStartRefund = IsUpgraded; return Task.CompletedTask; }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class SelfStartingBus : FactoryCardTemplate
{
    public SelfStartingBus() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { FactoryCombatState.For(Owner.Creature.CombatState!).SelfStartingBusThreshold = IsUpgraded ? 2 : 3; return Task.CompletedTask; }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class BlueprintRewrite : FactoryCardTemplate
{
    public BlueprintRewrite() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state = FactoryCombatState.For(Owner.Creature.CombatState!);
        state.BlueprintExtraOptions = 2; state.BlueprintFreeRefresh = true;
        if (!IsUpgraded) return;
        var options = BlueprintOptionCard.CreateAll(Owner).OrderBy(_ => Random.Shared.Next()).Take(3).ToList();
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_BLUEPRINT"), 1)
        { Cancelable = false, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, options.Cast<MegaCrit.Sts2.Core.Models.CardModel>().ToList(), Owner, prefs))
            .OfType<BlueprintOptionCard>().FirstOrDefault();
        if (selected is not null) state.InstallMachine(selected.MachineName, selected.MachineKind);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class VirusMother : FactoryComponentCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.VirusComponent, FactoryKeywords.PermanentComponent];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue }];
    public VirusMother() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true, "component_attack") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target).Execute(context);
        var other = PileType.Hand.GetPile(Owner).Cards.Where(card => !ReferenceEquals(card, this)).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
        if (other is not null)
        {
            await CardCmd.TransformTo<VirusMother>(other, CardPreviewStyle.None);
        }
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class RecursiveProcessing : FactoryCardTemplate
{
    public RecursiveProcessing() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self, true, "process") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { var s = FactoryCombatState.For(Owner.Creature.CombatState!); s.RecursiveProcessing = true; s.RecursiveProcessingThreshold = IsUpgraded ? 2 : 3; return Task.CompletedTask; }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class MasterControlCore : FactoryComponentCard
{
    public override bool IsPrecisionComponent => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.PrecisionComponent, FactoryKeywords.PermanentComponent];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public MasterControlCore() : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var count = FactoryLinePanel.Active is { } panel ? await panel.ActivateAllAsync() : 0;
        if (count > 0)
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(count * 2, ValueProp.Move), play);
        if (IsUpgraded && count >= 2) await CardPileCmd.Draw(context, count / 2, Owner);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() { }
}
