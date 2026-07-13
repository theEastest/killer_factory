using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KillerFactory.Ui;

internal static class FactoryCardDragPatch
{
    [ThreadStatic]
    private static bool _cancellingAcceptedDrop;

    [HarmonyPatch(typeof(NMouseCardPlay), nameof(NMouseCardPlay.Start))]
    [HarmonyPostfix]
    private static void StartPostfix(NMouseCardPlay __instance)
    {
        var card = __instance.Holder?.CardModel;
        if (card is not null) FactoryLinePanel.Active?.BeginCardDrag(card);
    }

    [HarmonyPatch(typeof(NMouseCardPlay), "_ExitTree")]
    [HarmonyPrefix]
    private static void ExitPrefix() => FactoryLinePanel.Active?.EndCardDrag();

    [HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
    [HarmonyPrefix]
    private static bool Prefix(NCardPlay __instance, Creature? target)
    {
        if (__instance is not NMouseCardPlay || FactoryLinePanel.Active is not { } panel)
            return true;

        var card = __instance.Holder?.CardModel;
        var viewport = __instance.GetViewport();
        if (card is null || viewport is null ||
            !panel.TryGetDropMachine(viewport.GetMousePosition(), card, out var machine))
            return true;

        // Stop the vanilla play animation before moving the real card instance into the
        // machine cache.  Suppress our CancelPlayCard hook here so the load is queued once.
        _cancellingAcceptedDrop = true;
        try { __instance.CancelPlayCard(); }
        finally { _cancellingAcceptedDrop = false; }
        panel.QueueLoad(machine, card);
        return false;
    }

    // Dropping a card over combat UI is normally classified as an invalid play and goes
    // straight to CancelPlayCard without ever calling TryPlayCard.  Catch that path too.
    [HarmonyPatch(typeof(NCardPlay), nameof(NCardPlay.CancelPlayCard))]
    [HarmonyPrefix]
    private static void CancelPrefix(NCardPlay __instance)
    {
        if (_cancellingAcceptedDrop || __instance is not NMouseCardPlay ||
            FactoryLinePanel.Active is not { } panel)
            return;

        var card = __instance.Holder?.CardModel;
        var viewport = __instance.GetViewport();
        if (card is not null && viewport is not null &&
            panel.TryGetDropMachine(viewport.GetMousePosition(), card, out var machine))
            panel.QueueLoad(machine, card);
    }
}
