using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PlannedObsolescence : FactoryCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public PlannedObsolescence() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "decomposer") { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var components = PileType.Hand.GetPile(Owner).Cards.OfType<FactoryComponentCard>().ToList();
        if (components.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_COMPONENT_TO_SCRAP"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, components, Owner, prefs))
            .OfType<FactoryComponentCard>()
            .FirstOrDefault();
        if (selected is null)
            return;

        var energy = selected.EnergyCost.GetWithModifiers(CostModifiers.None);
        await CardCmd.TransformTo<KillerFactoryScrap>(selected, CardPreviewStyle.None);
        if (energy > 0)
            await PlayerCmd.GainEnergy(energy, Owner);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
        AddKeyword(CardKeyword.Retain);
    }
}
