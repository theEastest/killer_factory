using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class FeedPump : FactoryCardTemplate
{
    public override bool GainsBlock => DynamicVars.Block.BaseValue > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(0m, ValueProp.Move)];

    public FeedPump() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer")
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        FactoryCombatState.For(Owner.Creature.CombatState!).AddMechanicalMaterial();
        if (DynamicVars.Block.BaseValue > 0)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await FactoryCardActions.TryReturnLoopPartner<ReactionMold>(Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
