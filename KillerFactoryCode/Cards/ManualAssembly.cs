using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
[RegisterCharacterStarterCard(typeof(KillerFactoryCharacter), 1)]
public sealed class ManualAssembly : FactoryCardTemplate
{
    public ManualAssembly() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "process")
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var state = FactoryCombatState.For(Owner.Creature.CombatState!);
        var components = PileType.Hand.GetPile(Owner).Cards
            .OfType<FactoryComponentCard>()
            .Where(card => card.IsPrecisionComponent || !state.IsFused(card))
            .Take(2)
            .ToList();

        if (components.Count < 2)
        {
            state.Record("手工装配失败：需要两张可融锻构件");
            return;
        }

        var body = components[0];
        var material = components[1];
        CardCmd.Upgrade(body);
        await CardPileCmd.Add(material, PileType.Exhaust);
        if (!body.IsPrecisionComponent)
            state.MarkFused(body);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
