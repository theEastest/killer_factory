using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KillerFactory.Ui;

[HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
internal static class FactoryCardDragPatch
{
    private static bool Prefix(NCardPlay __instance, Creature? target)
    {
        if (__instance is not NMouseCardPlay || FactoryLinePanel.Active is not { } panel)
            return true;

        var card = __instance.Holder?.CardModel;
        var viewport = __instance.GetViewport();
        if (card is null || viewport is null ||
            !panel.TryGetDropMachine(viewport.GetMousePosition(), card, out var machine))
            return true;

        __instance.CancelPlayCard();
        panel.QueueLoad(machine, card);
        return false;
    }
}
