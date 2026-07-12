using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class RecyclingWorkOrder : FactoryCardTemplate
{
    public RecyclingWorkOrder() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "decomposer")
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!await FactoryCardActions.ExhaustOneScrapFromHand(Owner))
        {
            FactoryCombatState.For(Owner.Creature.CombatState!).Record("回收工单失败：工作区没有废料");
            return;
        }

        await PlayerCmd.GainEnergy(1, Owner);
        await CardPileCmd.Draw(choiceContext, 1, Owner);
        await FactoryCardActions.TryReturnLoopPartner<PlannedObsolescence>(Owner, this);
    }

    protected override void OnUpgrade()
    {
        // 原料目前以仓储计数表示；完整的“分解原料”选择将在加工台阶段接入。
    }
}
