using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ManualSorting : FactoryCardTemplate
{
    private int _look = 5;
    private int _take = 2;
    public ManualSorting() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var draw = PileType.Draw.GetPile(Owner);
        var candidates = draw.Cards.TakeLast(_look).ToList();
        if (candidates.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_SORTING"), Math.Min(_take, candidates.Count))
        { Cancelable = false, RequireManualConfirmation = true };
        foreach (var selected in await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs))
            await CardPileCmd.Add(selected, PileType.Hand);
    }
    protected override void OnUpgrade() { _look = 7; _take = 3; }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class DirectedRetrieval : FactoryComponentCard
{
    private bool _anyCard;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.DisposableComponent, CardKeyword.Exhaust];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() => [];
    public DirectedRetrieval() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var candidates = PileType.Draw.GetPile(Owner).Cards
            .Where(card => _anyCard || card is FactoryComponentCard).ToList();
        if (candidates.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_RETRIEVAL"), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs)).FirstOrDefault();
        if (selected is not null) await CardPileCmd.Add(selected, PileType.Hand);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => _anyCard = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class FloodRetrieval : FactoryCardTemplate
{
    public FloodRetrieval() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var initial = PileType.Draw.GetPile(Owner).Cards.Count;
        for (var i = 0; i < initial; i++)
        {
            await CardPileCmd.Draw(context, 1, Owner);
            var hand = PileType.Hand.GetPile(Owner);
            var rightmost = hand.Cards.LastOrDefault(card => !ReferenceEquals(card, this));
            if (rightmost is not null) await CardPileCmd.Add(rightmost, PileType.Discard);
        }
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class PrecisionBlank : FactoryCardTemplate
{
    private bool _upgraded;
    public PrecisionBlank() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true, "producer") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var attack = Owner.Creature.CombatState!.CreateCard<PrecisionCuttingCore>(Owner);
        var block = Owner.Creature.CombatState!.CreateCard<PrecisionProtectionCore>(Owner);
        if (_upgraded) { CardCmd.Upgrade(attack); CardCmd.Upgrade(block); }
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_PRECISION_CORE"), 1)
        { Cancelable = false, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, [attack, block], Owner, prefs)).FirstOrDefault();
        if (selected is not null) await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner);
    }
    protected override void OnUpgrade() => _upgraded = true;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class VirusNeedleCluster : FactoryComponentCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.VirusComponent, FactoryKeywords.PermanentComponent];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move)];
    public override IEnumerable<FactoryEffectSegment> GetNativeEffectSegments() =>
        [new() { Kind = FactoryEffectKind.Damage, Amount = (int)DynamicVars.Damage.BaseValue }];
    public VirusNeedleCluster() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true, "component_attack") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target).Execute(context);
        var other = PileType.Hand.GetPile(Owner).Cards.Where(card => !ReferenceEquals(card, this)).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
        if (other is not null) await CardCmd.TransformTo<VirusNeedleCluster>(other, CardPreviewStyle.None);
        await FactoryFusionService.ResolveFusedEffects(this, context, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}
