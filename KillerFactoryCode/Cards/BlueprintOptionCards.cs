using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

public abstract class BlueprintOptionCard : FactoryCardTemplate
{
    public abstract string MachineName { get; }
    public abstract FactoryMachineKind MachineKind { get; }
    protected BlueprintOptionCard() : base(0, CardType.Status, CardRarity.Status, TargetType.None, false, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade() { }

    public static IReadOnlyList<BlueprintOptionCard> CreateAll(Player owner) =>
    [
        owner.Creature.CombatState!.CreateCard<ArmBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<StorageBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<DecomposerBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<ProcessingTableBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<UpgradeTableBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<RepairTableBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<SolarBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<EnergyStorageBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<CoilBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<PrecisionArmBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<AdvancedTableBlueprintOption>(owner),
        owner.Creature.CombatState!.CreateCard<SmartStorageBlueprintOption>(owner),
    ];
}

[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class ArmBlueprintOption : BlueprintOptionCard { public override string MachineName => "简易机械臂"; public override FactoryMachineKind MachineKind => FactoryMachineKind.MechanicalArm; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class StorageBlueprintOption : BlueprintOptionCard { public override string MachineName => "仓储室"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Storage; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class DecomposerBlueprintOption : BlueprintOptionCard { public override string MachineName => "分解室"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Decomposer; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class ProcessingTableBlueprintOption : BlueprintOptionCard { public override string MachineName => "基础加工台"; public override FactoryMachineKind MachineKind => FactoryMachineKind.ProcessingTable; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class UpgradeTableBlueprintOption : BlueprintOptionCard { public override string MachineName => "升级工作台"; public override FactoryMachineKind MachineKind => FactoryMachineKind.ProcessingTable; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class RepairTableBlueprintOption : BlueprintOptionCard { public override string MachineName => "修复工作台"; public override FactoryMachineKind MachineKind => FactoryMachineKind.ProcessingTable; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class SolarBlueprintOption : BlueprintOptionCard { public override string MachineName => "太阳能板"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Power; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class EnergyStorageBlueprintOption : BlueprintOptionCard { public override string MachineName => "储能室"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Power; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class CoilBlueprintOption : BlueprintOptionCard { public override string MachineName => "放电线圈"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Power; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class PrecisionArmBlueprintOption : BlueprintOptionCard { public override string MachineName => "精密机械臂"; public override FactoryMachineKind MachineKind => FactoryMachineKind.MechanicalArm; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class AdvancedTableBlueprintOption : BlueprintOptionCard { public override string MachineName => "高级加工台"; public override FactoryMachineKind MachineKind => FactoryMachineKind.ProcessingTable; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class SmartStorageBlueprintOption : BlueprintOptionCard { public override string MachineName => "智能仓储室"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Storage; }
[RegisterCard(typeof(KillerFactoryCardPool))] public sealed class RefreshBlueprintOption : BlueprintOptionCard { public override string MachineName => "刷新蓝图"; public override FactoryMachineKind MachineKind => FactoryMachineKind.Power; }
