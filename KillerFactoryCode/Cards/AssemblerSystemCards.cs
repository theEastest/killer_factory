using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BiologicalScrap : AssemblerCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    public BiologicalScrap() : base(0, CardType.Status, CardRarity.Status, TargetType.None, false, "scrap") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class SecondaryConveyor : AssemblerCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Ethereal];
    public SecondaryConveyor() : base(0, CardType.Skill, CardRarity.Status, TargetType.Self, false, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state = AssemblerCombatState.For(Owner.Creature.CombatState!);
        var amount = state.ClassificationEnhancedCards.Remove(this) ? 2 : 1;
        await CardPileCmd.Draw(context, amount, Owner);
    }
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class SecondaryEnergyTank : AssemblerCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Ethereal];
    public SecondaryEnergyTank() : base(0, CardType.Skill, CardRarity.Status, TargetType.Self, false, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state = AssemblerCombatState.For(Owner.Creature.CombatState!);
        var amount = state.ClassificationEnhancedCards.Remove(this) ? 2 : 1;
        await PlayerCmd.GainEnergy(amount, Owner);
    }
    protected override void OnUpgrade() { }
}
