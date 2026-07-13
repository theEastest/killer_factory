using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class SmartRetrieval : FactoryCardTemplate
{
    private int _draw = 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Draw", 3)];
    public SmartRetrieval() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var candidates = PileType.Draw.GetPile(Owner).Cards.ToList();
        if (candidates.Count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_STICKY"), 1)
            { Cancelable = true, RequireManualConfirmation = true };
            var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs)).FirstOrDefault();
            selected?.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(state => state.IsSticky = true);
        }
        await CardPileCmd.Draw(context, _draw, Owner);
    }
    protected override void OnUpgrade()
    {
        _draw = 4;
        if (DynamicVars.TryGetValue("Draw", out var draw)) draw.UpgradeValueBy(1);
    }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class DelayedAnalysis : FactoryCardTemplate
{
    public DelayedAnalysis() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { FactoryCombatState.For(Owner.Creature.CombatState!).DelayedAnalysis = true; return Task.CompletedTask; }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class RapidRecycling : FactoryCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    public RapidRecycling() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "decomposer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var cards = PileType.Hand.GetPile(Owner).Cards.Where(card => !ReferenceEquals(card, this)).ToList();
        foreach (var card in cards)
        {
            await CardPileCmd.Add(card, PileType.Exhaust);
            await FactoryCardActions.AddGeneratedCard<CompoundMaterial>(Owner, PileType.Draw);
            await FactoryCardActions.AddGeneratedCard<MechanicalMaterial>(Owner, PileType.Discard);
        }
        if (cards.Count > 8)
        {
            var missing = RitsuLibFramework.GetMaxHandSize(Owner) - PileType.Hand.GetPile(Owner).Cards.Count;
            if (missing > 0) await CardPileCmd.Draw(context, missing, Owner);
        }
    }
    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class AdvancedDrilling : FactoryCardTemplate
{
    public AdvancedDrilling() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { FactoryCombatState.For(Owner.Creature.CombatState!).AdvancedDrilling = true; return Task.CompletedTask; }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class StrongAttractor : FactoryCardTemplate
{
    public StrongAttractor() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self, true, "producer") { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    { FactoryCombatState.For(Owner.Creature.CombatState!).StrongAttractor = true; return Task.CompletedTask; }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Attractor : FactoryCardTemplate
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3, ValueProp.Move)];
    public Attractor() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await CardPileCmd.Draw(context, 1, Owner);
        var state = FactoryCombatState.For(Owner.Creature.CombatState!);
        state.Attractor = true;
        state.AttractorCard = this;
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}
