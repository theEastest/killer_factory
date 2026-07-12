using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;

namespace KillerFactory.Mechanics;

public static class FactoryCardActions
{
    public static async Task AddGeneratedCardToHand<TCard>(Player owner, int amount = 1)
        where TCard : CardModel
    {
        for (var index = 0; index < amount; index++)
        {
            var card = owner.Creature.CombatState!.CreateCard<TCard>(owner);
            var destination = PileType.Hand.GetPile(owner).Cards.Count < RitsuLibFramework.GetMaxHandSize(owner)
                ? PileType.Hand
                : PileType.Discard;
            await CardPileCmd.AddGeneratedCardToCombat(card, destination, owner);
        }
    }

    public static async Task TryReturnLoopPartner<TPartner>(Player owner, AbstractModel source)
        where TPartner : CardModel
    {
        var hand = PileType.Hand.GetPile(owner);
        if (hand.Cards.Count >= RitsuLibFramework.GetMaxHandSize(owner))
        {
            FactoryCombatState.For(owner.Creature.CombatState!).Record("工作区已满，回路中断");
            return;
        }

        var discard = PileType.Discard.GetPile(owner);
        var partner = discard.Cards.LastOrDefault(static card => card is TPartner);
        if (partner is null)
            return;

        await CardPileCmd.Add(partner, PileType.Hand);
        FactoryCombatState.For(owner.Creature.CombatState!).Record("回路返回一张搭档牌");
    }

    public static async Task AddScrapToHand(Player owner)
    {
        await AddGeneratedCardToHand<Cards.KillerFactoryScrap>(owner);
        FactoryCombatState.For(owner.Creature.CombatState!).Record("获得1张废料");
    }

    public static async Task<bool> ExhaustOneScrapFromHand(Player owner)
    {
        var scrap = PileType.Hand.GetPile(owner).Cards.FirstOrDefault(static card => card is Cards.KillerFactoryScrap);
        if (scrap is null)
            return false;

        await CardPileCmd.Add(scrap, PileType.Exhaust);
        FactoryCombatState.For(owner.Creature.CombatState!).Record("分解一张废料");
        return true;
    }
}
