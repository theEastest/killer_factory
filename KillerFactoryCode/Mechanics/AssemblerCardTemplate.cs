using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace KillerFactory.Mechanics;

public abstract class AssemblerCardTemplate : ModCardTemplate
{
    private readonly string _portraitStem;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{_portraitStem}.svg");

    protected AssemblerCardTemplate(int energyCost, CardType cardType, CardRarity rarity,
        TargetType targetType, bool showInLibrary, string portraitStem)
        : base(energyCost, cardType, rarity, targetType, showInLibrary)
    {
        _portraitStem = portraitStem;
    }
}
