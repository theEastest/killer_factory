using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using KillerFactory.Cards;

namespace KillerFactory.Mechanics;

public enum FactoryMaterialKind { None, Mechanical, Compound, Universal }
public sealed record FactoryMaterialRequirement(FactoryMaterialKind Kind, int Amount, string DisplayName);

public static class FactoryProcedureRequirements
{
    public static FactoryMaterialRequirement For(CardModel card) => card switch
    {
        StandardUpgrade => new(FactoryMaterialKind.Universal, 1, "万能原料 ×1"),
        StandardCalibration => new(FactoryMaterialKind.Universal, 1, "万能原料 ×1"),
        Lightweighting => new(FactoryMaterialKind.Compound, 1, "化合原料 ×1"),
        ExternalSpring => new(FactoryMaterialKind.Mechanical, 1, "机械原料 ×1"),
        Solidification => new(FactoryMaterialKind.Universal, 1, "万能原料 ×1"),
        EfficientStructure => new(FactoryMaterialKind.Compound, 1, "化合原料 ×1"),
        Hardening => new(FactoryMaterialKind.Mechanical, 1, "机械原料 ×1"),
        StreamlinedImprovement => new(FactoryMaterialKind.Universal, 1, "万能原料 ×1"),
        StickyCoating => new(FactoryMaterialKind.Compound, 1, "化合原料 ×1"),
        Precisionization => new(FactoryMaterialKind.Universal, 2, "万能原料 ×2"),
        _ => new(FactoryMaterialKind.None, 0, card is ManualAssembly or OverallCasting ? "构件材料（选择界面）" : "无"),
    };

    public static bool CanPay(CardModel procedure)
    {
        var requirement = For(procedure);
        if (requirement.Amount == 0) return true;
        var hand = PileType.Hand.GetPile(procedure.Owner).Cards;
        var dedicated = requirement.Kind switch
        {
            FactoryMaterialKind.Mechanical => hand.OfType<MechanicalMaterial>().Count(),
            FactoryMaterialKind.Compound => hand.OfType<CompoundMaterial>().Count(),
            FactoryMaterialKind.Universal => 0,
            _ => 0,
        };
        var universal = hand.OfType<UniversalMaterial>().Count();
        var stored = FactoryCombatState.Current?.Machines
            .Where(machine => machine.Kind == FactoryMachineKind.Storage && machine.CachedCard is not null)
            .Select(machine => machine.CachedCard!).ToList() ?? [];
        dedicated += requirement.Kind switch
        {
            FactoryMaterialKind.Mechanical => stored.OfType<MechanicalMaterial>().Count(),
            FactoryMaterialKind.Compound => stored.OfType<CompoundMaterial>().Count(),
            _ => 0,
        };
        universal += stored.OfType<UniversalMaterial>().Count();
        return dedicated + universal >= requirement.Amount;
    }

    public static async Task PayAsync(CardModel procedure)
    {
        var requirement = For(procedure);
        var remaining = requirement.Amount;
        var hand = PileType.Hand.GetPile(procedure.Owner).Cards;
        IEnumerable<CardModel> dedicated = requirement.Kind switch
        {
            FactoryMaterialKind.Mechanical => hand.OfType<MechanicalMaterial>(),
            FactoryMaterialKind.Compound => hand.OfType<CompoundMaterial>(),
            _ => [],
        };
        foreach (var material in dedicated.Take(remaining).ToList())
        { await CardPileCmd.Add(material, PileType.Exhaust); remaining--; }
        foreach (var material in hand.OfType<UniversalMaterial>().Take(remaining).ToList())
        { await CardPileCmd.Add(material, PileType.Exhaust); remaining--; }
        if (remaining <= 0 || FactoryCombatState.Current is not { } state) return;
        var storage = state.Machines.Where(machine => machine.Kind == FactoryMachineKind.Storage).ToList();
        bool IsDedicated(CardModel card) => requirement.Kind switch
        {
            FactoryMaterialKind.Mechanical => card is MechanicalMaterial,
            FactoryMaterialKind.Compound => card is CompoundMaterial,
            _ => false,
        };
        foreach (var machine in storage.Where(machine => machine.CachedCard is not null && IsDedicated(machine.CachedCard)).ToList())
        {
            await CardPileCmd.Add(machine.CachedCard!, PileType.Exhaust); machine.CachedCard = null; remaining--;
            if (remaining == 0) return;
        }
        foreach (var machine in storage.Where(machine => machine.CachedCard is UniversalMaterial).ToList())
        {
            await CardPileCmd.Add(machine.CachedCard!, PileType.Exhaust); machine.CachedCard = null; remaining--;
            if (remaining == 0) return;
        }
    }
}
