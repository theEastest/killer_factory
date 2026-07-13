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
    private static readonly Dictionary<CardModel, int> SpringOriginalCosts = new();

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
        if (evt.Card.Pile?.Type != PileType.Hand) return;
        var state = FactoryCombatState.For(evt.CombatState);

        if (evt.Card.TryGetCapability<FactoryCardStateCapability>(out var capability))
        {
            if (capability.HasExternalSpring && !SpringOriginalCosts.ContainsKey(evt.Card))
            {
                var cost = evt.Card.EnergyCost.GetWithModifiers(CostModifiers.None);
                SpringOriginalCosts[evt.Card] = cost;
                evt.Card.EnergyCost.SetCustomBaseCost(0);
            }
            if (capability.IsSticky)
            {
                var copies = PileType.Draw.GetPile(evt.Card.Owner).Cards
                    .Where(card => card.GetType() == evt.Card.GetType()).ToList();
                for (var index = 0; index < copies.Count; index++)
                {
                    var copy = copies[index];
                    if (index == 0 && capability.StickyFirstCopyFree && !SpringOriginalCosts.ContainsKey(copy))
                    {
                        SpringOriginalCosts[copy] = copy.EnergyCost.GetWithModifiers(CostModifiers.None);
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
        if (SpringOriginalCosts.Remove(card, out var originalCost))
            card.EnergyCost.SetCustomBaseCost(originalCost);
        if (card.TryGetCapability<FactoryCardStateCapability>(out var capability) && capability.IsStreamlined)
        {
            var current = card.EnergyCost.GetWithModifiers(CostModifiers.None);
            card.EnergyCost.SetCustomBaseCost(Math.Max(0, current - 1));
        }
    }
}
