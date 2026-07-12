using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace KillerFactory.Mechanics;

public abstract class FactoryCardTemplate : ModCardTemplate
{
    private readonly string _portraitStem;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{_portraitStem}.svg");

    protected FactoryCardTemplate(
        int energyCost,
        CardType cardType,
        CardRarity rarity,
        TargetType targetType,
        bool showInLibrary,
        string portraitStem)
        : base(energyCost, cardType, rarity, targetType, showInLibrary)
    {
        _portraitStem = portraitStem;
    }
}

public abstract class FactoryComponentCard : FactoryCardTemplate
    , IFactoryComponentDefinition
{
    public virtual bool IsPrecisionComponent => false;
    public virtual bool IsFragileComponent => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.PermanentComponent];

    public abstract IEnumerable<FactoryEffectSegment> GetNativeEffectSegments();

    protected FactoryComponentCard(
        int energyCost,
        CardType cardType,
        CardRarity rarity,
        TargetType targetType,
        bool showInLibrary,
        string portraitStem)
        : base(energyCost, cardType, rarity, targetType, showInLibrary, portraitStem)
    {
    }
}
