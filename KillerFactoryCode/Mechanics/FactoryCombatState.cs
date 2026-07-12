using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using System.Runtime.CompilerServices;

namespace KillerFactory.Mechanics;

public sealed class FactoryMachineState
{
    public required string Name { get; init; }
    public int MaxEnergy { get; init; } = 5;
    public int Energy { get; set; }
    public CardModel? CachedCard { get; set; }

    public bool CanAccept(CardModel card) => card is FactoryComponentCard;
}

public sealed class FactoryCombatState
{
    public const int MaximumMachineSlots = 10;
    private static readonly ConditionalWeakTable<ICombatState, FactoryCombatState> States = new();

    public static FactoryCombatState? Current { get; private set; }
    public static void ClearCurrent() => Current = null;

    public List<FactoryMachineState> Machines { get; } = [];
    public string LastAction { get; private set; } = "产线待机";

    public static FactoryCombatState For(ICombatState combatState)
    {
        var state = States.GetValue(combatState, static _ => new FactoryCombatState());
        Current = state;
        return state;
    }

    public void InstallSimpleArm()
    {
        if (Machines.Any(machine => machine.Name == "简易机械臂"))
            return;
        if (Machines.Count >= MaximumMachineSlots)
        {
            LastAction = "机械槽位已满";
            return;
        }

        Machines.Add(new FactoryMachineState { Name = "简易机械臂" });
        LastAction = "简易机械臂已架设";
    }

    public void RechargeMachines(int amount = 1)
    {
        foreach (var machine in Machines)
            machine.Energy = Math.Min(machine.MaxEnergy, machine.Energy + amount);
        LastAction = $"机械自动充能 +{amount}";
    }

    public void Record(string action) => LastAction = action;
}
