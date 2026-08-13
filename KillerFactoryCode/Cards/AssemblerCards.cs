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

public abstract class AssemblerPartCard : AssemblerCardTemplate, IAssemblerPart
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [AssemblerKeywords.Part];

    protected AssemblerPartCard(
        int energyCost,
        CardType cardType,
        CardRarity rarity,
        TargetType targetType,
        bool showInLibrary,
        string portraitStem)
        : base(energyCost, cardType, rarity, targetType, showInLibrary, portraitStem)
    {
    }

    public abstract AssemblerModuleSpec CreateModule();
}

[RegisterCard(typeof(AssemblerCardPool))]
[RegisterCharacterStarterCard(typeof(AssemblerCharacter), 4)]
public sealed class CastStrike : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    public CastStrike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, true, "component_attack") { }

    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.Damage,
        Amount = IsUpgraded ? 7 : 5,
        SourceTitle = Title,
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await AssemblerService.ExecutePartAttackAsync(this, (int)DynamicVars.Damage.BaseValue, context, play);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
[RegisterCharacterStarterCard(typeof(AssemblerCharacter), 2)]
public sealed class GuardPlate : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    public override bool GainsBlock => true;

    public GuardPlate() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "component_defend") { }

    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.Block,
        Amount = IsUpgraded ? 7 : 5,
        SourceTitle = Title,
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await AssemblerService.ExecutePartBlockAsync(this, (int)DynamicVars.Block.BaseValue, context, play);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
[RegisterCharacterStarterCard(typeof(AssemblerCharacter), 1)]
public sealed class Feeder : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("ExtraDrawLimit", 1)];
    public Feeder() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "producer") { }

    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.Draw,
        Amount = 1,
        SourceTitle = Title,
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var adjacentScrap = AssemblerService.CountAdjacentAssemblyScrap(this);
        DynamicVars.TryGetValue("ExtraDrawLimit", out var extraDrawLimit);
        var draw = 1 + Math.Min((int)(extraDrawLimit?.BaseValue ?? 1), adjacentScrap);
        await CardPileCmd.Draw(context, draw, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["ExtraDrawLimit"].UpgradeValueBy(1);
}

[RegisterCard(typeof(AssemblerCardPool))]
[RegisterCharacterStarterCard(typeof(AssemblerCharacter), 1)]
public sealed class FlankPlate : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5, ValueProp.Move),
        new IntVar("HandLeftBonus", 4),
        new IntVar("ModuleBlock", 5),
        new IntVar("ModuleLeftBonus", 4),
    ];
    public override bool GainsBlock => true;

    public FlankPlate() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "component_defend") { }

    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.FlankBranch,
        Amount = (int)DynamicVars["ModuleBlock"].BaseValue,
        BonusAmount = (int)DynamicVars["ModuleLeftBonus"].BaseValue,
        SourceTitle = Title,
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var amount = (int)DynamicVars.Block.BaseValue;
        var snapshot = AssemblerService.GetPositionSnapshot(this);
        if (snapshot.IsLeftEdge) amount += (int)DynamicVars["HandLeftBonus"].BaseValue;
        AssemblerService.RecordBlock(this, amount);
        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(amount, ValueProp.Move), play);
        if (snapshot.IsRightEdge) await CardPileCmd.Draw(context, 1, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["HandLeftBonus"].UpgradeValueBy(1);
        DynamicVars["ModuleBlock"].UpgradeValueBy(2);
        DynamicVars["ModuleLeftBonus"].UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(AssemblerCardPool))]
[RegisterCharacterStarterCard(typeof(AssemblerCharacter), 1)]
public sealed class PositioningArm : AssemblerCardTemplate
{
    public PositioningArm() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "process") { }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var ordered = hand.Cards.ToList();
        if (ordered.Count < 2)
            return;

        var selectCardPrefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_MOVE_CARD"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, ordered, Owner, selectCardPrefs)).FirstOrDefault();
        if (selected is null)
            return;

        ordered = hand.Cards.ToList();
        var start = ordered.IndexOf(selected);
        var destinations = ordered.Where((card, index) =>
                !ReferenceEquals(card, selected) && (IsUpgraded || Math.Abs(index - start) <= 2))
            .ToList();
        if (destinations.Count == 0)
            return;

        var destinationPrefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_MOVE_DESTINATION"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var anchor = (await CardSelectCmd.FromSimpleGrid(context, destinations, Owner, destinationPrefs)).FirstOrDefault();
        if (anchor is null)
            return;

        ordered = hand.Cards.ToList();
        AssemblerService.MoveWithinHand(selected, ordered.IndexOf(anchor));
    }

    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
[RegisterCharacterStarterCard(typeof(AssemblerCharacter), 1)]
public sealed class EmergencyMaintenance : AssemblerCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6, ValueProp.Move), new IntVar("Repair", 1)];
    public override bool GainsBlock => true;

    public EmergencyMaintenance() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "component_defend") { }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await AssemblerService.SelectProductToRepairAsync(context, Owner, (int)DynamicVars["Repair"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Repair"].UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BasicAssembly : AssemblerCardTemplate
{
    protected override bool IsPlayable => base.IsPlayable && AssemblerService.HasAdjacentAssemblyPair(Owner);
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [AssemblerKeywords.Assembly, CardKeyword.Retain, CardKeyword.Exhaust];

    public BasicAssembly() : base(0, CardType.Skill, CardRarity.Status, TargetType.Self, false, "process") { }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var parts = await AssemblerService.SelectAdjacentPartsAsync(context, Owner);
        if (parts is not null)
            await AssemblerService.AssembleAsync(Owner, parts);
    }

    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class AssembledProduct : AssemblerCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [AssemblerKeywords.Product];

    protected override bool IsPlayable => base.IsPlayable &&
        (!this.TryGetCapability<AssemblerProductCapability>(out var state) || !state.IsBroken);

    public AssembledProduct() : base(1, CardType.Skill, CardRarity.Status, TargetType.Self, false, "producer") { }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!this.TryGetCapability<AssemblerProductCapability>(out var state) || state.IsBroken)
            return;

        decimal pendingMultiplier = 1m;
        var makeNextDamageAll = false;
        var makeNextDamageBonus = 0;
        AssemblerModuleKind? previousOutputKind = null;
        var previousOutputAmount = 0;
        var passes = state.ShouldRepeatOutputs() ? 2 : 1;
        for (var pass = 0; pass < passes; pass++)
        for (var moduleIndex = 0; moduleIndex < state.Modules.Count; moduleIndex++)
        {
            var module = state.Modules[moduleIndex];
            if (pass > 0 && module.Kind is not (AssemblerModuleKind.Damage or AssemblerModuleKind.Block or
                AssemblerModuleKind.EdgeBlock or AssemblerModuleKind.FlankBranch or AssemblerModuleKind.Draw or
                AssemblerModuleKind.ScrapBlock or AssemblerModuleKind.BiologicalBlock or AssemblerModuleKind.EndBlockDraw))
                continue;
            if (module.Kind == AssemblerModuleKind.BoostNext)
            {
                pendingMultiplier = Math.Min(2m, pendingMultiplier + module.Amount / 100m);
                continue;
            }
            if (module.Kind == AssemblerModuleKind.ConvertNextDamageToAll)
            {
                makeNextDamageAll = true;
                makeNextDamageBonus = module.BonusAmount;
                continue;
            }
            if (module.Kind == AssemblerModuleKind.StructureDurability)
                continue;
            if (module.Kind == AssemblerModuleKind.SelfRepair)
                continue;
            if (module.Kind == AssemblerModuleKind.RollerFilter)
            {
                if (moduleIndex == 0)
                {
                    await AssemblerService.PutHandCardOnDrawBottomAsync(context, Owner);
                    await CardPileCmd.Draw(context, 1, Owner);
                }
                else if (moduleIndex == state.Modules.Count - 1)
                {
                    await CardPileCmd.Draw(context, 1, Owner);
                    await AssemblerService.PutHandCardOnDrawBottomAsync(context, Owner);
                }
                continue;
            }

            if (module.Kind == AssemblerModuleKind.RepeatPrevious)
            {
                if (previousOutputKind is null)
                    continue;
                var repeatAmount = (int)Math.Floor(previousOutputAmount * module.Amount / 100m);
                await ExecuteOutputAsync(previousOutputKind.Value, repeatAmount, context, play);
                continue;
            }

            var amount = module.Amount;
            if (module.Kind is AssemblerModuleKind.Damage or AssemblerModuleKind.Block or
                AssemblerModuleKind.EdgeBlock or AssemblerModuleKind.FlankBranch or AssemblerModuleKind.ScrapBlock or
                AssemblerModuleKind.BiologicalBlock or AssemblerModuleKind.EndBlockDraw)
                amount += AssemblerCombatState.For(Owner.Creature.CombatState!).Productivity;
            if (module.Kind == AssemblerModuleKind.EdgeBlock &&
                (moduleIndex == 0 || moduleIndex == state.Modules.Count - 1))
                amount += module.BonusAmount;
            if (module.Kind == AssemblerModuleKind.FlankBranch && moduleIndex == 0)
                amount += module.BonusAmount;
            if (module.Kind is AssemblerModuleKind.Damage or AssemblerModuleKind.Block or
                AssemblerModuleKind.EdgeBlock or AssemblerModuleKind.FlankBranch)
            {
                amount = (int)Math.Floor(amount * pendingMultiplier);
                pendingMultiplier = 1m;
            }
            if (module.Kind == AssemblerModuleKind.Damage && makeNextDamageAll)
            {
                amount = (int)Math.Floor(amount * (1m + makeNextDamageBonus / 100m));
                await DamageCmd.Attack(amount).FromCard(this, play)
                    .TargetingAllOpponents(Owner.Creature.CombatState!).Execute(context);
                makeNextDamageAll = false;
                makeNextDamageBonus = 0;
            }
            else await ExecuteOutputAsync(module.Kind, amount, context, play);
            if (module.Kind == AssemblerModuleKind.FlankBranch &&
                moduleIndex == state.Modules.Count - 1 && moduleIndex != 0)
                await CardPileCmd.Draw(context, 1, Owner);
            if (module.Kind == AssemblerModuleKind.EndBlockDraw && moduleIndex == state.Modules.Count - 1)
                await CardPileCmd.Draw(context, 1, Owner);
            previousOutputKind = module.Kind;
            previousOutputAmount = amount;

        }
        var factoryState = AssemblerCombatState.For(Owner.Creature.CombatState!);
        var wasBroken = state.IsBroken;
        var protectedByPower = factoryState.PreventiveMaintenance && !factoryState.ProductProtectionUsedThisTurn;
        if (protectedByPower) factoryState.ProductProtectionUsedThisTurn = true;
        if (!protectedByPower && !state.TrySkipDurabilityLoss()) state.SpendDurability();
        foreach (var selfRepair in state.Modules.Where(module => module.Kind == AssemblerModuleKind.SelfRepair))
        {
            var scraps = PileType.Hand.GetPile(Owner).Cards.Where(AssemblerService.IsScrap).ToList();
            if (scraps.Count == 0) break;
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE"), 1)
            { Cancelable = true, RequireManualConfirmation = true };
            var repairScrap = (await CardSelectCmd.FromSimpleGrid(context, scraps, Owner, prefs)).FirstOrDefault();
            if (repairScrap is null) continue;
            await CardPileCmd.Add(repairScrap, PileType.Exhaust);
            state.Repair(1);
            if (selfRepair.BonusAmount > 0) await PlayerCmd.GainEnergy(1, Owner);
        }
        var biologicalScrapCount = state.Modules.Count(module => module.Kind == AssemblerModuleKind.BiologicalBlock);
        for (var i = 0; i < biologicalScrapCount; i++)
        {
            var bio = Owner.Creature.CombatState!.CreateCard<BiologicalScrap>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(bio, PileType.Discard, Owner);
        }
        if (!wasBroken && state.IsBroken && factoryState.ModuleRecirculation > 0 &&
            !factoryState.ModuleRecirculationUsedThisTurn && state.Modules.Count > 0)
        {
            factoryState.ModuleRecirculationUsedThisTurn = true;
            var edges = new List<(AssemblerPartCard Part,int Index)>();
            if (state.GetModulePart(0) is { } first) edges.Add((first,0));
            if (state.GetModulePart(state.Modules.Count-1) is { } last && edges.All(e=>!ReferenceEquals(e.Part,last)))
                edges.Add((last,state.Modules.Count-1));
            if (edges.Count == 0) goto SkipModuleRecirculation;
            var choices = edges.Select(e=>(CardModel)e.Part).ToList();
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_PART"), 1)
            { Cancelable = false, RequireManualConfirmation = true };
            var selected = (await CardSelectCmd.FromSimpleGrid(context, choices, Owner, prefs)).FirstOrDefault();
            if (selected is not null)
            {
                var moduleIndex = edges.First(e=>ReferenceEquals(e.Part,selected)).Index;
                var recoverLast = moduleIndex == state.Modules.Count - 1;
                var recovered = state.GetModulePart(moduleIndex);
                state.RemoveModuleAt(moduleIndex);
                if (recovered is not null)
                    await CardPileCmd.AddGeneratedCardToCombat(recovered, PileType.Hand, Owner);
                if (recoverLast) state.Repair(1);
                else await CardPileCmd.Draw(context, 1, Owner);
            }
        }
        SkipModuleRecirculation:
        if (state.Modules.Count == 0)
            await CardPileCmd.Add(this, PileType.Exhaust);
        state.MarkUsed();
        if (state.PatchedAfterUse == 1)
            await CardPileCmd.Add(this, PileType.Exhaust);
        else if (state.PatchedAfterUse == 2)
        {
            while (!state.IsBroken) state.SpendDurability();
        }
    }

    private async Task ExecuteOutputAsync(
        AssemblerModuleKind kind,
        int amount,
        PlayerChoiceContext context,
        CardPlay play)
    {
        if (amount <= 0)
            return;
        if (kind == AssemblerModuleKind.Damage && play.Target is not null && !play.Target.IsDead)
        {
            await DamageCmd.Attack(amount).FromCard(this, play).Targeting(play.Target).Execute(context);
        }
        else if (kind is AssemblerModuleKind.Block or AssemblerModuleKind.EdgeBlock or AssemblerModuleKind.FlankBranch or
                 AssemblerModuleKind.BiologicalBlock or AssemblerModuleKind.EndBlockDraw)
        {
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(amount, ValueProp.Move), play);
        }
        else if (kind == AssemblerModuleKind.Draw)
        {
            await CardPileCmd.Draw(context, amount, Owner);
        }
        else if (kind == AssemblerModuleKind.ScrapBlock)
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(amount, ValueProp.Move), play);
    }

    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class MechanicalScrap : AssemblerCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    public MechanicalScrap() : base(0, CardType.Status, CardRarity.Status, TargetType.None, false, "scrap") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class SawBlade : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    public SawBlade() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true, "component_attack") { }
    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.Damage,
        Amount = IsUpgraded ? 9 : 7,
        SourceTitle = Title,
    };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await AssemblerService.ExecutePartAttackAsync(this, (int)DynamicVars.Damage.BaseValue, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class CushionPad : AssemblerPartCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7, ValueProp.Move)];
    public override bool GainsBlock => true;
    public CushionPad() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "component_defend") { }
    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.Block,
        Amount = IsUpgraded ? 8 : 6,
        SourceTitle = Title,
    };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await AssemblerService.ExecutePartBlockAsync(this, (int)DynamicVars.Block.BaseValue, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BoosterPart : AssemblerPartCard
{
    protected override bool IsPlayable => base.IsPlayable &&
        AssemblerService.GetPositionSnapshot(this).Right is AssemblerPartCard { Type: CardType.Attack };
    public BoosterPart() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.BoostNext,
        Amount = 50,
        SourceTitle = Title,
    };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        AssemblerService.TryMarkRightPart(this, p => p.Type == CardType.Attack,
            modifier => modifier.DamageBonus += 0.5m);
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class CouplingPart : AssemblerPartCard
{
    protected override bool IsPlayable => base.IsPlayable &&
        AssemblerService.GetPositionSnapshot(this).Right is AssemblerPartCard part &&
        (part.Type == CardType.Attack || part.GainsBlock);
    public CouplingPart() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    public override AssemblerModuleSpec CreateModule() => new()
    {
        Kind = AssemblerModuleKind.RepeatPrevious,
        Amount = IsUpgraded ? 75 : 50,
        SourceTitle = Title,
    };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        AssemblerService.TryMarkRightPart(this,
            p => p.Type == CardType.Attack || p.GainsBlock,
            modifier => modifier.RepeatPercent += IsUpgraded ? 75 : 50);
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class SimpleRepair : AssemblerCardTemplate
{
    public SimpleRepair() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var brokenProducts = PileType.Hand.GetPile(Owner).Cards
            .OfType<AssembledProduct>()
            .Where(product => product.TryGetCapability<AssemblerProductCapability>(out var state) && state.IsBroken)
            .ToHashSet();
        var product = await AssemblerService.SelectProductToRepairAsync(context, Owner, IsUpgraded ? 2 : 1);
        if (product is not null && brokenProducts.Contains(product))
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(6, ValueProp.Move), play);
    }
    protected override void OnUpgrade() { }
}
