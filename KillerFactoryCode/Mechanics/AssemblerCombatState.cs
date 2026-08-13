using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using KillerFactory.Cards;
using System.Runtime.CompilerServices;

namespace KillerFactory.Mechanics;

public sealed class AssemblerCombatState
{
    private static readonly ConditionalWeakTable<ICombatState, AssemblerCombatState> States = new();
    public int FullAutoProduction { get; set; }
    public bool PreventiveMaintenance { get; set; }
    public bool PreventiveRepairDraw { get; set; }
    public bool AssemblyUsedThisTurn { get; set; }
    public bool ProductProtectionUsedThisTurn { get; set; }
    public bool RepairDrawUsedThisTurn { get; set; }
    public decimal PendingAssemblerAttackMultiplier { get; set; } = 1m;
    public AssemblerModuleKind? LastAssemblerOutputKind { get; set; }
    public int LastAssemblerOutputAmount { get; set; }
    public Dictionary<CardModel, PartPlayModifier> PartModifiers { get; } = [];
    public Dictionary<CardModel, HandPositionSnapshot> PositionSnapshots { get; } = [];
    public bool AssembledThisTurn { get; set; }
    public AssembledProduct? LatestProduct { get; set; }
    public bool PendingBufferDraw { get; set; }
    public CardModel? BufferedCard { get; set; }
    public bool BufferedCardCostReduction { get; set; }
    public CardModel? BypassedScrap { get; set; }
    public bool BypassAlwaysDraw { get; set; }
    public bool BypassMoveRight { get; set; }
    public Dictionary<CardModel, bool> BatchTags { get; } = [];
    public int InterlockStation { get; set; }
    public int InterlockUsedThisTurn { get; set; }
    public int ModuleRecirculation { get; set; }
    public int ZeroWasteFactory { get; set; }
    public bool ZeroWasteProcessesAnyScrap { get; set; }
    public bool ZeroWasteUsedThisTurn { get; set; }
    public int ClassificationProtocol { get; set; }
    public int HeterogeneousGrowth { get; set; }
    public int HeterogeneousGrowthDraw { get; set; }
    public bool HeterogeneousGrowthUsedThisTurn { get; set; }
    public int Productivity { get; set; }
    public int FirstInFirstOut { get; set; }
    public int PartsDrawnThisTurn { get; set; }
    public int ReworkBus { get; set; }
    public bool ReworkBusUsedThisTurn { get; set; }
    public int BlueprintLibrary { get; set; }
    public bool BlueprintLibraryUsedThisTurn { get; set; }
    public AssemblerPartCard? LastExhaustedPart { get; set; }
    public AssemblerModuleSpec? PendingBlueprintModule { get; set; }
    public bool PendingBlueprintAtFront { get; set; }
    public HashSet<CardModel> LegacyReplicas { get; } = [];
    public bool ProcessedMechanicalThisTurn { get; set; }
    public bool ProcessedBiologicalThisTurn { get; set; }
    public bool ClassificationMechanicalUsedThisTurn { get; set; }
    public bool ClassificationBiologicalUsedThisTurn { get; set; }
    public HashSet<CardModel> ClassificationEnhancedCards { get; } = [];
    public bool ModuleRecirculationUsedThisTurn { get; set; }

    public static AssemblerCombatState For(ICombatState combatState) =>
        States.GetValue(combatState, static _ => new AssemblerCombatState());

    public void ResetTurnFlags()
    {
        AssemblyUsedThisTurn = false;
        ProductProtectionUsedThisTurn = false;
        RepairDrawUsedThisTurn = false;
        PartModifiers.Clear();
        PositionSnapshots.Clear();
        BatchTags.Clear();
        PendingBufferDraw = false;
        BypassedScrap = null;
        BypassAlwaysDraw = false;
        BypassMoveRight = false;
        PendingBlueprintModule = null;
        PendingBlueprintAtFront = false;
        AssembledThisTurn = false;
        LatestProduct = null;
        ProcessedMechanicalThisTurn = false;
        ProcessedBiologicalThisTurn = false;
        ClassificationMechanicalUsedThisTurn = false;
        ClassificationBiologicalUsedThisTurn = false;
        HeterogeneousGrowthUsedThisTurn = false;
        ModuleRecirculationUsedThisTurn = false;
        InterlockUsedThisTurn = 0;
        ZeroWasteUsedThisTurn = false;
        PartsDrawnThisTurn = 0;
        ReworkBusUsedThisTurn = false;
        BlueprintLibraryUsedThisTurn = false;
    }
}

public sealed class PartPlayModifier
{
    public decimal DamageBonus { get; set; }
    public int RepeatPercent { get; set; }
    public bool MakeDamageAll { get; set; }
    public decimal AreaMultiplier { get; set; } = 1m;
}

public sealed record HandPositionSnapshot(int Index, int Count, CardModel? Left, CardModel? Right)
{
    public bool IsLeftEdge => Index == 0;
    public bool IsRightEdge => Index >= 0 && Index == Count - 1;
}
