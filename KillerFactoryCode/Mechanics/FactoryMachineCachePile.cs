using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Mechanics;

[RegisterOwnedCardPile(CacheStem, Scope = ModCardPileScope.CombatOnly, Style = ModCardPileUiStyle.Headless)]
public sealed class FactoryMachineCachePile
{
    public const string CacheStem = "MachineCache";

    public static PileType PileType => ModContentRegistry
        .GetQualifiedCardPileId(Entry.ModId, CacheStem)
        .GetModCardPileType();
}
