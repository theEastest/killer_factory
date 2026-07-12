using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using System.Runtime.CompilerServices;

namespace KillerFactory.Mechanics;

/// <summary>
/// 首轮实现的战斗内工厂状态。原料暂以仓储计数表示，废料仍是实际卡牌。
/// </summary>
public sealed class FactoryCombatState
{
    private static readonly ConditionalWeakTable<ICombatState, FactoryCombatState> States = new();

    public static FactoryCombatState? Current { get; private set; }

    public static void ClearCurrent() => Current = null;

    public int MechanicalMaterial { get; private set; }
    public int CompoundMaterial { get; private set; }
    public bool SimpleArmInstalled { get; private set; }
    public string LastAction { get; private set; } = "产线待机";

    private readonly HashSet<CardModel> _fusedCards = [];

    public static FactoryCombatState For(ICombatState combatState)
    {
        var state = States.GetValue(combatState, static _ => new FactoryCombatState());
        Current = state;
        return state;
    }

    public void InstallSimpleArm()
    {
        SimpleArmInstalled = true;
        LastAction = "简易机械臂已架设";
    }

    public void AddMechanicalMaterial(int amount = 1)
    {
        MechanicalMaterial += amount;
        LastAction = $"机械原料 +{amount}";
    }

    public void AddCompoundMaterial(int amount = 1)
    {
        CompoundMaterial += amount;
        LastAction = $"化合原料 +{amount}";
    }

    public bool IsFused(CardModel card) => _fusedCards.Contains(card);

    public void MarkFused(CardModel card)
    {
        _fusedCards.Add(card);
        LastAction = $"{card.GetType().Name} 完成初步融锻";
    }

    public void Record(string action)
    {
        LastAction = action;
    }
}
