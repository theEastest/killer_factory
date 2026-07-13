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
public sealed class OverloadPunch : FactoryComponentCard
{
    public override bool IsFragileComponent => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [FactoryKeywords.PermanentComponent, FactoryKeywords.FragileComponent];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue }];

    public OverloadPunch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true, "consumer") { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        await FactoryFusionService.ResolveFusedEffects(this, choiceContext, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6);
}
