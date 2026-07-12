using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using KillerFactory.Mechanics;
using KillerFactory.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class CompoundMaterial : FactoryCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable, FactoryKeywords.Material];
    public CompoundMaterial() : base(0, CardType.Status, CardRarity.Status, TargetType.None, false, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
    protected override void OnUpgrade() { }
}
