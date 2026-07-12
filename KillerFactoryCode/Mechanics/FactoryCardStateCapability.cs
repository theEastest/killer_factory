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
}

public sealed class FactoryCardInstanceState
{
    public int ProcessCount { get; set; }
    public List<FactoryEffectSegment> FusedEffects { get; set; } = [];
}

[RegisterModelCapability]
public sealed class FactoryCardStateCapability
    : StatefulModelCapability<CardModel, FactoryCardInstanceState>,
      ICardPropertyContributor,
      ICardDescriptionContributor
{
    public int ProcessCount => State.ProcessCount;
    public IReadOnlyList<FactoryEffectSegment> FusedEffects => State.FusedEffects;

    public void AddFusion(IEnumerable<FactoryEffectSegment> effects)
    {
        MutateState(state =>
        {
            state.ProcessCount++;
            state.FusedEffects.AddRange(effects.Select(effect => new FactoryEffectSegment
            {
                Kind = effect.Kind,
                Amount = effect.Amount,
            }));
        });
    }

    public TargetType? GetTargetType(CardModel card) =>
        State.FusedEffects.Any(effect => effect.Kind == FactoryEffectKind.Damage)
            ? TargetType.AnyEnemy
            : null;

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        foreach (var effect in State.FusedEffects)
        {
            var key = effect.Kind switch
            {
                FactoryEffectKind.Damage => "KILLER_FACTORY_FUSED_DAMAGE",
                FactoryEffectKind.Block => "KILLER_FACTORY_FUSED_BLOCK",
                FactoryEffectKind.AddScrap => "KILLER_FACTORY_FUSED_SCRAP",
                _ => null,
            };
            if (key is null)
                continue;
            var text = new LocString("cards", key);
            text.Add("Amount", effect.Amount);
            yield return new CardDescriptionFragment(text);
        }
    }
}

public interface IFactoryComponentDefinition
{
    bool IsPrecisionComponent { get; }
    bool IsFragileComponent { get; }
    IEnumerable<FactoryEffectSegment> GetNativeEffectSegments();
}
