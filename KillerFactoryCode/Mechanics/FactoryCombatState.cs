using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using System.Runtime.CompilerServices;

namespace KillerFactory.Mechanics;

public enum FactoryMachineKind
{
    MechanicalArm,
    ProcessingTable,
    Storage,
    Decomposer,
    Power,
}

public enum MachineStartFailureReason
{
    None,
    NotPlayerTurn,
    ActionQueueBusy,
    MachineBusy,
    MachineDisabled,
    NoCachedOperation,
    InvalidCachedCard,
    NoLoadedCard,
    InsufficientCharge,
    MissingMaterial,
    MissingTarget,
    NoLegalCardTarget,
    HandFull,
    TargetNoLongerValid,
    CardNoLongerAvailable,
    Unknown,
}

public sealed record MachineStartResult(
    bool Success,
    MachineStartFailureReason FailureReason,
    string Message,
    int RequiredCharge = 0,
    int AvailableCharge = 0,
    string? MissingResourceName = null)
{
    public static MachineStartResult Ok(string message = "可以启动") =>
        new(true, MachineStartFailureReason.None, message);

    public static MachineStartResult Fail(
        MachineStartFailureReason reason,
        string message,
        int requiredCharge = 0,
        int availableCharge = 0,
        string? missingResourceName = null) =>
        new(false, reason, message, requiredCharge, availableCharge, missingResourceName);
}

public sealed class FactoryMachineState
{
    public required string Name { get; init; }
    public FactoryMachineKind Kind { get; init; } = FactoryMachineKind.MechanicalArm;
    public int MaxEnergy { get; init; } = 5;
    public int Energy { get; set; }
    public CardModel? CachedCard { get; set; }
    public string? TemporaryMessage { get; private set; }
    public DateTimeOffset TemporaryMessageUntil { get; private set; }

    public string SlotName => Kind == FactoryMachineKind.ProcessingTable ? "工序槽" : "构件槽";

    public bool CanAccept(CardModel card) => Kind switch
    {
        FactoryMachineKind.ProcessingTable => card.Keywords.Contains(FactoryKeywords.Procedure),
        FactoryMachineKind.Storage => true,
        FactoryMachineKind.Decomposer => card is Cards.KillerFactoryScrap || card.Keywords.Contains(FactoryKeywords.Material),
        FactoryMachineKind.Power => false,
        _ => card is FactoryComponentCard,
    };

    public int GetRequiredCharge(CardModel? card) =>
        Kind == FactoryMachineKind.ProcessingTable ? 1 : card?.EnergyCost.GetAmountToSpend() ?? 0;

    public void ShowMessage(string message, double seconds = 2d)
    {
        TemporaryMessage = message;
        TemporaryMessageUntil = DateTimeOffset.UtcNow.AddSeconds(seconds);
    }

    public string? GetVisibleMessage()
    {
        if (TemporaryMessage is null || DateTimeOffset.UtcNow <= TemporaryMessageUntil)
            return TemporaryMessage;
        TemporaryMessage = null;
        return null;
    }
}

public sealed class FactoryCombatState
{
    public const int MaximumMachineSlots = 10;
    private static readonly ConditionalWeakTable<ICombatState, FactoryCombatState> States = new();

    public static FactoryCombatState? Current { get; private set; }
    public static void ClearCurrent() => Current = null;

    public ICombatState CombatState { get; private set; } = null!;

    public List<FactoryMachineState> Machines { get; } = [];
    public string LastAction { get; private set; } = "产线待机";
    public bool DelayedAnalysis { get; set; }
    public bool AdvancedDrilling { get; set; }
    public bool StrongAttractor { get; set; }
    public bool Attractor { get; set; }
    public CardModel? AttractorCard { get; set; }
    public bool AutoLoadProtocol { get; set; }
    public bool AutoLoadDrawUsed { get; set; }
    public bool AutoStartProtocol { get; set; }
    public bool AutoStartRefund { get; set; }
    public bool AutoStartRefundUsed { get; set; }
    public int SelfStartingBusThreshold { get; set; }
    public int MachineCardsPlayed { get; set; }
    public int RecursiveProcessingThreshold { get; set; }
    public int BlueprintExtraOptions { get; set; }
    public bool BlueprintFreeRefresh { get; set; }
    public bool RecursiveProcessing { get; set; }
    public bool AutoLoadDraw { get; set; }

    public static FactoryCombatState For(ICombatState combatState)
    {
        var state = States.GetValue(combatState, static _ => new FactoryCombatState());
        state.CombatState = combatState;
        Current = state;
        return state;
    }

    public void InstallStarterMachines()
    {
        InstallMachine("简易机械臂", FactoryMachineKind.MechanicalArm);
        InstallMachine("基础加工台", FactoryMachineKind.ProcessingTable);
    }

    public void InstallSimpleArm() => InstallStarterMachines();

    public FactoryMachineState? InstallMachine(string name, FactoryMachineKind kind, int startingEnergy = 0, int maxEnergy = 5)
    {
        if (kind is FactoryMachineKind.MechanicalArm or FactoryMachineKind.ProcessingTable &&
            Machines.Any(machine => machine.Kind == kind && machine.Name == name)) return null;
        if (Machines.Count >= MaximumMachineSlots)
        {
            LastAction = "机械槽位已满";
            return null;
        }

        var machine = new FactoryMachineState { Name = name, Kind = kind, Energy = startingEnergy, MaxEnergy = maxEnergy };
        Machines.Add(machine);
        LastAction = $"{name} 已安装";
        return machine;
    }

    public void RechargeMachines(int amount = 1)
    {
        foreach (var machine in Machines)
            machine.Energy = Math.Min(machine.MaxEnergy, machine.Energy + amount);
        LastAction = $"机械自动充能 +{amount}";
    }

    public void Record(string action) => LastAction = action;

    public void ResetTurnFlags()
    {
        AutoLoadDrawUsed = false;
        AutoStartRefundUsed = false;
    }

    public void ReportFailure(FactoryMachineState machine, MachineStartResult result)
    {
        var message = $"无法启动：{result.Message}";
        machine.ShowMessage(message);
        LastAction = message;
    }
}
