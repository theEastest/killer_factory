using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Mechanics;

public enum FactoryEffectKind
{
    Damage,
    Block,
    AddScrap,
}

public sealed class FactoryEffectSegment
{
    public FactoryEffectKind Kind { get; set; }
    public int Amount { get; set; }
    public int Hits { get; set; } = 1;
}

public sealed class FactoryCardInstanceState
{
    public int ProcessCount { get; set; }
    public List<FactoryEffectSegment> FusedEffects { get; set; } = [];
    public bool HasExternalSpring { get; set; }
    public bool IsEfficient { get; set; }
    public bool EfficientDraw { get; set; }
    public bool IsHard { get; set; }
    public bool IsStreamlined { get; set; }
    public bool IsSticky { get; set; }
    public bool StickyFirstCopyFree { get; set; }
}

[RegisterModelCapability]
public sealed class FactoryCardStateCapability
    : StatefulModelCapability<CardModel, FactoryCardInstanceState>,
      ICardPropertyContributor,
      ICardDescriptionContributor
{
    public int ProcessCount => State.ProcessCount;
    public IReadOnlyList<FactoryEffectSegment> FusedEffects => State.FusedEffects;
    public bool HasExternalSpring => State.HasExternalSpring;
    public bool IsEfficient => State.IsEfficient;
    public bool EfficientDraw => State.EfficientDraw;
    public bool IsHard => State.IsHard;
    public bool IsStreamlined => State.IsStreamlined;
    public bool IsSticky => State.IsSticky;
    public bool StickyFirstCopyFree => State.StickyFirstCopyFree;

    public void ApplyProcessing(Action<FactoryCardInstanceState> mutation)
    {
        var state = CopyState();
        state.ProcessCount++;
        mutation(state);
        SetState(state);
    }

    public void ResetProcessCount()
    {
        var state = CopyState();
        state.ProcessCount = 0;
        SetState(state);
    }

    public void AddFusion(IEnumerable<FactoryEffectSegment> effects)
    {
        var state = CopyState();
        state.ProcessCount++;
        state.FusedEffects.AddRange(effects.Select(CopyEffect));
        SetState(state);
    }

    // Capability models may be cloned from a registered prototype. Never mutate the
    // state payload in place: a shallowly cloned prototype can otherwise make several
    // cards observe the same mutable state object.
    private FactoryCardInstanceState CopyState() => new()
    {
        ProcessCount = State.ProcessCount,
        FusedEffects = State.FusedEffects.Select(CopyEffect).ToList(),
        HasExternalSpring = State.HasExternalSpring,
        IsEfficient = State.IsEfficient,
        EfficientDraw = State.EfficientDraw,
        IsHard = State.IsHard,
        IsStreamlined = State.IsStreamlined,
        IsSticky = State.IsSticky,
        StickyFirstCopyFree = State.StickyFirstCopyFree,
    };

    private static FactoryEffectSegment CopyEffect(FactoryEffectSegment effect) => new()
    {
        Kind = effect.Kind,
        Amount = effect.Amount,
        Hits = effect.Hits,
    };

    public TargetType? GetTargetType(CardModel card) =>
        State.FusedEffects.Any(effect => effect.Kind == FactoryEffectKind.Damage)
            ? TargetType.AnyEnemy
            : null;

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (State.HasExternalSpring)
            yield return new CardDescriptionFragment(new LocString("cards", "KILLER_FACTORY_MOD_EXTERNAL_SPRING"));

        foreach (var effect in State.FusedEffects)
        {
            var key = effect.Kind switch
            {
                FactoryEffectKind.Damage when effect.Hits > 1 => "KILLER_FACTORY_FUSED_MULTI_DAMAGE",
                FactoryEffectKind.Damage => "KILLER_FACTORY_FUSED_DAMAGE",
                FactoryEffectKind.Block => "KILLER_FACTORY_FUSED_BLOCK",
                FactoryEffectKind.AddScrap => "KILLER_FACTORY_FUSED_SCRAP",
                _ => null,
            };
            if (key is null)
                continue;
            var text = new LocString("cards", key);
            text.Add("Amount", effect.Amount);
            text.Add("Hits", effect.Hits);
            yield return new CardDescriptionFragment(text);
        }
    }
}

[RegisterModelCapability]
[RegisterDefaultModelCapability(typeof(FactoryComponentCard))]
public sealed class FactoryFragileProcessDescriptionCapability
    : ModelCapability<CardModel>, ICardDescriptionContributor
{
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (!context.Card.Keywords.Contains(FactoryKeywords.FragileComponent))
            yield break;

        var processCount = context.Card.TryGetCapability<FactoryCardStateCapability>(out var state)
            ? state.ProcessCount
            : 0;
        var processCountKey = processCount <= 0
            ? "KILLER_FACTORY_FRAGILE_PROCESS_COUNT_SAFE"
            : "KILLER_FACTORY_FRAGILE_PROCESS_COUNT_DANGER";
        yield return new CardDescriptionFragment(new LocString("cards", processCountKey));
    }
}

public interface IFactoryComponentDefinition
{
    bool IsPrecisionComponent { get; }
    bool IsFragileComponent { get; }
    IEnumerable<FactoryEffectSegment> GetNativeEffectSegments();
}
