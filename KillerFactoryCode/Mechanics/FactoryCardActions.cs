using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;

namespace KillerFactory.Mechanics;

public static class FactoryCardActions
{
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
        var combat = owner.Creature.CombatState!;
        var scrap = combat.CreateCard<Cards.KillerFactoryScrap>(owner);
        var hand = PileType.Hand.GetPile(owner);
        var destination = hand.Cards.Count < RitsuLibFramework.GetMaxHandSize(owner)
            ? PileType.Hand
            : PileType.Discard;

        await CardPileCmd.AddGeneratedCardToCombat(scrap, destination, owner);
        FactoryCombatState.For(combat).Record(
            destination == PileType.Hand ? "废料进入工作区" : "工作区已满，废料溢出至弃牌堆");
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
