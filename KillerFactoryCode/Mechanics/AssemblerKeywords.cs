using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace KillerFactory.Mechanics;

[RegisterOwnedCardKeyword(nameof(Assembly), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Part), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Product), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
public sealed class AssemblerKeywords
{
    private AssemblerKeywords() { }
    public static readonly CardKeyword Assembly = Resolve(nameof(Assembly));
    public static readonly CardKeyword Part = Resolve(nameof(Part));
    public static readonly CardKeyword Product = Resolve(nameof(Product));

    private static CardKeyword Resolve(string stem) =>
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, stem).GetModCardKeyword();
}
