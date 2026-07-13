using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Mechanics;

public static class FactoryFusionService
{
    public static async Task<bool> TryBeginProcessingAsync(FactoryComponentCard card)
    {
        var state = card.GetOrCreateCapability<FactoryCardStateCapability>();
        if (!card.Keywords.Contains(FactoryKeywords.FragileComponent) || state.ProcessCount <= 0)
            return true;

        await CardPileCmd.Add(card, PileType.Exhaust);
        if (state.IsEfficient)
        {
            await FactoryCardActions.AddGeneratedCardToHand<Cards.UniversalMaterial>(card.Owner);
            if (state.EfficientDraw)
                await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, card.Owner);
        }
        else
        {
            await FactoryCardActions.AddScrapToHand(card.Owner);
        }

        FactoryCombatState.For(card.Owner.Creature.CombatState!).Record("脆弱构件在再次加工前损坏");
        return false;
    }

    public static async Task<bool> FuseAsync(
        FactoryComponentCard body,
        FactoryComponentCard material,
        bool upgradeFirst)
    {
        if (!await TryBeginProcessingAsync(body))
            return false;

        var bodyState = body.GetOrCreateCapability<FactoryCardStateCapability>();

        if (upgradeFirst)
        {
            if (body.IsUpgradable)
                CardCmd.Upgrade(body);
            if (material.IsUpgradable)
                CardCmd.Upgrade(material);
        }

        var materialState = material.GetOrCreateCapability<FactoryCardStateCapability>();
        var effects = material.GetNativeEffectSegments().Concat(materialState.FusedEffects).ToList();
        bodyState.AddFusion(effects);

        if (!body.Keywords.Contains(FactoryKeywords.PrecisionComponent))
        {
            var bodyCost = body.EnergyCost.GetWithModifiers(CostModifiers.None);
            var materialCost = material.EnergyCost.GetWithModifiers(CostModifiers.None);
            body.EnergyCost.SetCustomBaseCost(Math.Max(0, bodyCost + materialCost));
        }

        await CardPileCmd.Add(material, PileType.Exhaust);
        FactoryCombatState.For(body.Owner.Creature.CombatState!).Record(
            $"{body.Title} 已融入 {material.Title}");
        return true;
    }

    public static async Task ResolveFusedEffects(
        FactoryComponentCard card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (!card.TryGetCapability<FactoryCardStateCapability>(out var state))
        {
            await FactoryCardActions.TriggerComponentLoopReturns(card);
            return;
        }
        foreach (var effect in state.FusedEffects)
        {
            switch (effect.Kind)
            {
                case FactoryEffectKind.Damage when cardPlay.Target is not null:
                    for (var hit = 0; hit < Math.Max(1, effect.Hits); hit++)
                    {
                        await DamageCmd.Attack(effect.Amount)
                            .FromCard(card, cardPlay)
                            .Targeting(cardPlay.Target)
                            .Execute(choiceContext);
                    }
                    break;
                case FactoryEffectKind.Block:
                    await CreatureCmd.GainBlock(
                        card.Owner.Creature,
                        new BlockVar(effect.Amount, ValueProp.Move),
                        cardPlay);
                    break;
                case FactoryEffectKind.AddScrap:
                    await FactoryCardActions.AddScrapToHand(card.Owner);
                    break;
            }
        }
        await FactoryCardActions.TriggerComponentLoopReturns(card);
    }
}
