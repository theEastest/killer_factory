using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class RecyclingWorkOrder : FactoryCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("EnergyGain", 1)];
    public RecyclingWorkOrder() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "decomposer") { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var scraps = PileType.Hand.GetPile(Owner).Cards.OfType<KillerFactoryScrap>().ToList();
        if (scraps.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_SCRAP_TO_RECYCLE"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, scraps, Owner, prefs))
            .OfType<KillerFactoryScrap>()
            .FirstOrDefault();
        if (selected is null)
            return;

        await CardCmd.Exhaust(choiceContext, selected);
        DynamicVars.TryGetValue("EnergyGain", out var energyGain);
        await PlayerCmd.GainEnergy((int)(energyGain?.BaseValue ?? 1), Owner);
        await CardPileCmd.Draw(choiceContext, 2, Owner);
    }

    protected override void OnUpgrade()
    {
        if (DynamicVars.TryGetValue("EnergyGain", out var energyGain))
            energyGain.UpgradeValueBy(1);
    }
}
