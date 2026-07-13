using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Helpers;
using KillerFactory.Cards;
using KillerFactory.Ui;
using STS2RitsuLib;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Mechanics;

public static class FactoryAbilityRuntime
{
    private static readonly Dictionary<CardModel, int> TemporaryOriginalCosts = new();
    private static readonly HashSet<CardModel> PendingExternalSpringReturns = [];

    public static void OnTurnStarted(PlayerTurnStartedEvent evt)
    {
        if (evt.CombatState is null) return;
        var state = FactoryCombatState.For(evt.CombatState);
        state.ResetTurnFlags();
        if (!state.AdvancedDrilling) return;
        var task = Random.Shared.Next(2) == 0
            ? FactoryCardActions.AddGeneratedCardToHand<MechanicalMaterial>(evt.Player)
            : FactoryCardActions.AddGeneratedCardToHand<CompoundMaterial>(evt.Player);
        TaskHelper.RunSafely(task);
    }

    public static void OnCardDrawn(CardDrawnEvent evt)
    {
        if (!evt.Card.Keywords.Contains(FactoryKeywords.Material)) return;
        var state = FactoryCombatState.For(evt.CombatState);
        if (state.StrongAttractor)
            TaskHelper.RunSafely(CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, evt.Card.Owner));
        if (state.Attractor && state.AttractorCard is { } attractor && attractor.Pile?.Type == PileType.Discard)
            TaskHelper.RunSafely(CardPileCmd.Add(attractor, PileType.Hand));
    }

    public static void OnCardExhausted(CardExhaustedEvent evt)
    {
        var state = FactoryCombatState.For(evt.CombatState);
        if (!state.DelayedAnalysis) return;
        if (evt.Card is MechanicalMaterial)
            TaskHelper.RunSafely(FactoryCardActions.AddGeneratedCard<CompoundMaterial>(evt.Card.Owner, PileType.Discard));
        else if (evt.Card is CompoundMaterial)
            TaskHelper.RunSafely(FactoryCardActions.AddGeneratedCard<MechanicalMaterial>(evt.Card.Owner, PileType.Discard));
    }

    public static void OnCardMoved(CardMovedBetweenPilesEvent evt)
    {
        if (evt.CombatState is null) return;

        if (PendingExternalSpringReturns.Contains(evt.Card) && evt.PreviousPile == PileType.Play)
        {
            // Mechanical-arm plays are finalized by FactoryLinePanel after AutoPlay returns,
            // otherwise the panel could race this callback and put the card back in its cache.
            if (FactoryLinePanel.Active?.IsExecutingCard(evt.Card) != true)
            {
                PendingExternalSpringReturns.Remove(evt.Card);
                if (evt.Card.Pile?.Type == PileType.Discard &&
                    PileType.Hand.GetPile(evt.Card.Owner).Cards.Count < RitsuLibFramework.GetMaxHandSize(evt.Card.Owner))
                {
                    TaskHelper.RunSafely(CardPileCmd.Add(evt.Card, PileType.Hand));
                }
            }
        }

        if (evt.Card.Pile?.Type != PileType.Hand) return;
        var state = FactoryCombatState.For(evt.CombatState);

        if (evt.Card.TryGetCapability<FactoryCardStateCapability>(out var capability))
        {
            if (capability.IsSticky)
            {
                var copies = PileType.Draw.GetPile(evt.Card.Owner).Cards
                    .Where(card => card.GetType() == evt.Card.GetType()).ToList();
                for (var index = 0; index < copies.Count; index++)
                {
                    var copy = copies[index];
                    if (index == 0 && capability.StickyFirstCopyFree && !TemporaryOriginalCosts.ContainsKey(copy))
                    {
                        TemporaryOriginalCosts[copy] = copy.EnergyCost.GetWithModifiers(CostModifiers.None);
                        copy.EnergyCost.SetCustomBaseCost(0);
                    }
                    TaskHelper.RunSafely(CardPileCmd.Add(copy, PileType.Hand));
                }
            }
        }

        if (state.AutoLoadProtocol && evt.Card is FactoryComponentCard && FactoryLinePanel.Active is { } panel)
        {
            var arm = state.Machines.FirstOrDefault(machine =>
                machine.Kind == FactoryMachineKind.MechanicalArm && machine.CachedCard is null);
            if (arm is not null) panel.QueueLoad(arm, evt.Card);
        }
    }

    public static void OnCardPlayed(CardPlayedEvent evt)
    {
        var card = evt.CardPlay.Card;
        if (TemporaryOriginalCosts.Remove(card, out var originalCost))
            card.EnergyCost.SetCustomBaseCost(originalCost);
        if (!card.TryGetCapability<FactoryCardStateCapability>(out var capability)) return;

        if (capability.IsStreamlined)
        {
            var current = card.EnergyCost.GetWithModifiers(CostModifiers.None);
            card.EnergyCost.SetCustomBaseCost(Math.Max(0, current - 1));
        }

        if (capability.HasExternalSpring)
            PendingExternalSpringReturns.Add(card);
    }

    public static async Task<bool> ResolveExternalSpringAfterMachinePlayAsync(CardModel card)
    {
        if (!PendingExternalSpringReturns.Remove(card)) return false;
        if (!card.TryGetCapability<FactoryCardStateCapability>(out var capability) || !capability.HasExternalSpring)
            return false;

        // Exhaust/removed cards are broken or scrapped and must not be resurrected.
        if (card.Pile?.Type is PileType.Exhaust or null) return true;

        if (PileType.Hand.GetPile(card.Owner).Cards.Count < RitsuLibFramework.GetMaxHandSize(card.Owner))
            await CardPileCmd.Add(card, PileType.Hand);
        else if (card.Pile?.Type != PileType.Discard)
            await CardPileCmd.Add(card, PileType.Discard);

        return true;
    }
}
