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
public sealed class ReturnPlate : FactoryComponentCard
{
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Block, Amount = (int)DynamicVars.Block.BaseValue }];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(2m, ValueProp.Move)];

    public ReturnPlate() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "consumer")
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await FactoryFusionService.ResolveFusedEffects(this, choiceContext, cardPlay);
        await FactoryCardActions.TryReturnLoopPartner<ReciprocatingCutter>(Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
