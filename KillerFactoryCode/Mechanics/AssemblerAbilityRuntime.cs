using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using MegaCrit.Sts2.Core.Helpers;
using KillerFactory.Cards;

namespace KillerFactory.Mechanics;

public static class AssemblerAbilityRuntime
{
    private static readonly Dictionary<CardModel, int> TemporaryOriginalCosts = new();

    public static void OnTurnStarted(PlayerTurnStartedEvent evt)
    {
        if (evt.CombatState is not null)
        {
            var state = AssemblerCombatState.For(evt.CombatState);
            if (state.BufferedCard is not null)
            {
                var buffered = state.BufferedCard;
                state.BufferedCard = null;
                TaskHelper.RunSafely(CardPileCmd.AddGeneratedCardToCombat(buffered, PileType.Discard, buffered.Owner));
            }
            state.BypassedScrap = null;
            state.ResetTurnFlags();
        }
    }

    public static void OnTurnEnding(SideTurnEndingEvent evt)
    {
        if (evt.CombatState is null) return;
        var state = AssemblerCombatState.For(evt.CombatState);
        foreach (var (card, originalCost) in TemporaryOriginalCosts.ToList())
            card.EnergyCost.SetCustomBaseCost(originalCost);
        TemporaryOriginalCosts.Clear();
        if (state.BufferedCard is not null)
        {
            var buffered = state.BufferedCard;
            state.BufferedCard = null;
            TaskHelper.RunSafely(RestoreBufferedCard(buffered, false));
            state.BufferedCardCostReduction = false;
        }
        foreach (var replica in state.LegacyReplicas.Where(c => c.Pile?.Type == PileType.Hand).ToList())
            TaskHelper.RunSafely(CardPileCmd.Add(replica, PileType.Exhaust));
    }

    private static async Task RestoreBufferedCard(CardModel card, bool reduceCost)
    {
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, card.Owner);
        AssemblerService.MoveWithinHand(card, int.MaxValue);
        if (reduceCost) ReduceCostUntilPlayed(card, 1);
    }

    public static void OnCardPlayed(CardPlayedEvent evt)
    {
        var card = evt.CardPlay.Card;
        if (TemporaryOriginalCosts.Remove(card, out var originalCost))
            card.EnergyCost.SetCustomBaseCost(originalCost);
        if (evt.CombatState is null) return;
        var state = AssemblerCombatState.For(evt.CombatState);
        if (state.LegacyReplicas.Remove(card)) TaskHelper.RunSafely(CardPileCmd.Add(card, PileType.Exhaust));
    }

    public static void OnCardPlaying(CardPlayingEvent evt)
    {
        if (evt.CombatState is null) return;
        var card = evt.CardPlay.Card;
        var cards = PileType.Hand.GetPile(card.Owner).Cards.ToList();
        var index = cards.IndexOf(card);
        if (index < 0) return;
        AssemblerCombatState.For(evt.CombatState).PositionSnapshots[card] = new HandPositionSnapshot(
            index, cards.Count, index > 0 ? cards[index - 1] : null,
            index + 1 < cards.Count ? cards[index + 1] : null);
    }

    public static void OnCardDrawn(CardDrawnEvent evt)
    {
        if (evt.Card is not AssemblerPartCard) return;
        var state = AssemblerCombatState.For(evt.CombatState);
        if (state.FirstInFirstOut <= 0) return;
        state.PartsDrawnThisTurn++;
        if (state.PartsDrawnThisTurn == 1) AssemblerService.MoveWithinHand(evt.Card, 0);
        else if (state.PartsDrawnThisTurn == 2)
        {
            AssemblerService.MoveWithinHand(evt.Card, int.MaxValue);
            TaskHelper.RunSafely(CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, evt.Card.Owner));
        }
    }

    public static void OnCardExhausted(CardExhaustedEvent evt)
    {
        if (evt.Card is AssemblerPartCard part)
            AssemblerCombatState.For(evt.CombatState).LastExhaustedPart = part;
    }

    public static void OnCardDiscarded(CardDiscardedEvent evt)
    {
        if (evt.CombatState is null) return;
        var state=AssemblerCombatState.For(evt.CombatState);
        state.PartModifiers.Remove(evt.Card);
        state.BatchTags.Remove(evt.Card);
    }

    public static void OnCardMoved(CardMovedBetweenPilesEvent evt)
    {
        if (evt.PreviousPile != PileType.Discard || evt.Card.Pile?.Type != PileType.Hand || evt.Card is not AssemblerPartCard) return;
        if (evt.CombatState is null) return;
        var state = AssemblerCombatState.For(evt.CombatState);
        if (state.ReworkBus <= 0 || state.ReworkBusUsedThisTurn) return;
        state.ReworkBusUsedThisTurn = true;
        ReduceCostUntilPlayed(evt.Card, 1);
        TaskHelper.RunSafely(CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, evt.Card.Owner));
    }

    public static void MakeFreeUntilPlayed(CardModel card)
    {
        if (!TemporaryOriginalCosts.ContainsKey(card))
            TemporaryOriginalCosts[card] = card.EnergyCost.GetWithModifiers(CostModifiers.None);
        card.EnergyCost.SetCustomBaseCost(0);
    }

    public static void ReduceCostUntilPlayed(CardModel card, int amount)
    {
        if (!TemporaryOriginalCosts.ContainsKey(card))
            TemporaryOriginalCosts[card] = card.EnergyCost.GetWithModifiers(CostModifiers.None);
        var current = card.EnergyCost.GetWithModifiers(CostModifiers.None);
        card.EnergyCost.SetCustomBaseCost(Math.Max(0, current - Math.Max(0, amount)));
    }
}
