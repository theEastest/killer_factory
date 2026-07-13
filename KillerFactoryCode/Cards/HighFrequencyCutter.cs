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
public sealed class HighFrequencyCutter : FactoryComponentCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue, Hits = 3 }];

    public HighFrequencyCutter() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true, "consumer") { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        for (var hit = 0; hit < 3; hit++)
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
                .Targeting(cardPlay.Target).Execute(choiceContext);
        await FactoryFusionService.ResolveFusedEffects(this, choiceContext, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}
