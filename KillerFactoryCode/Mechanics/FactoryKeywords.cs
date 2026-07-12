using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace KillerFactory.Mechanics;

[RegisterOwnedCardKeyword(nameof(PermanentComponent), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(DisposableComponent), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(FragileComponent), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(PrecisionComponent), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Procedure), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Material), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Waste), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(DamagedProduct), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
public static class FactoryKeywords
{
    public static readonly CardKeyword PermanentComponent = Resolve(nameof(PermanentComponent));
    public static readonly CardKeyword DisposableComponent = Resolve(nameof(DisposableComponent));
    public static readonly CardKeyword FragileComponent = Resolve(nameof(FragileComponent));
    public static readonly CardKeyword PrecisionComponent = Resolve(nameof(PrecisionComponent));
    public static readonly CardKeyword Procedure = Resolve(nameof(Procedure));
    public static readonly CardKeyword Material = Resolve(nameof(Material));
    public static readonly CardKeyword Waste = Resolve(nameof(Waste));
    public static readonly CardKeyword DamagedProduct = Resolve(nameof(DamagedProduct));

    private static CardKeyword Resolve(string stem) =>
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, stem).GetModCardKeyword();
}
