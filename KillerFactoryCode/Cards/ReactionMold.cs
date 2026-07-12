using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ReactionMold : FactoryCardTemplate
{
    private bool _upgraded;

    public ReactionMold() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer")
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await FactoryCardActions.AddGeneratedCardToHand<CompoundMaterial>(Owner, _upgraded ? 2 : 1);
        await FactoryCardActions.TryReturnLoopPartner<FeedPump>(Owner, this);
    }

    protected override void OnUpgrade()
    {
        _upgraded = true;
    }
}
