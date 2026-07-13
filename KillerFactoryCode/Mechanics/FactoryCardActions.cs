using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;

namespace KillerFactory.Mechanics;

public static class FactoryCardActions
{
    // 新版局部循环已删除“搭档牌从弃牌堆自动返回”的旧机制。
    public static Task TriggerComponentLoopReturns(FactoryComponentCard playedCard) => Task.CompletedTask;

    public static async Task AddGeneratedCardToHand<TCard>(Player owner, int amount = 1)
        where TCard : CardModel
    {
        for (var index = 0; index < amount; index++)
        {
            try
            {
                var card = owner.Creature.CombatState!.CreateCard<TCard>(owner);
                var destination = PileType.Hand.GetPile(owner).Cards.Count < RitsuLibFramework.GetMaxHandSize(owner)
                    ? PileType.Hand
                    : PileType.Discard;
                await CardPileCmd.AddGeneratedCardToCombat(card, destination, owner);
            }
            catch (Exception exception)
            {
                // 生成牌注册错误不应让整张正在结算的牌悬空并卡死行动队列。
                Entry.Logger.Error($"Failed to generate {typeof(TCard).Name}; card play will continue: {exception}");
                FactoryCombatState.For(owner.Creature.CombatState!).Record("生成牌失败，已安全跳过");
                return;
            }
        }
    }

    public static async Task AddGeneratedCard<TCard>(Player owner, PileType destination, int amount = 1)
        where TCard : CardModel
    {
        for (var index = 0; index < amount; index++)
        {
            try
            {
                var card = owner.Creature.CombatState!.CreateCard<TCard>(owner);
                await CardPileCmd.AddGeneratedCardToCombat(card, destination, owner);
            }
            catch (Exception exception)
            {
                Entry.Logger.Error($"Failed to generate {typeof(TCard).Name}: {exception}");
                FactoryCombatState.For(owner.Creature.CombatState!).Record("生成牌失败，已安全跳过");
                return;
            }
        }
    }

    public static async Task<bool> ExhaustMaterials<TCard>(Player owner, int amount)
        where TCard : CardModel
    {
        var cards = PileType.Hand.GetPile(owner).Cards.OfType<TCard>().Take(amount).Cast<CardModel>().ToList();
        if (cards.Count < amount) return false;
        foreach (var card in cards) await CardPileCmd.Add(card, PileType.Exhaust);
        return true;
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
