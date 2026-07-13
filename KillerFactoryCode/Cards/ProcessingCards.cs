using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Cards;

public abstract class SelectComponentProcedure : FactoryCardTemplate, IFactoryProcedureCard
{
    protected SelectComponentProcedure(int cost, CardRarity rarity) : base(cost, CardType.Skill, rarity, TargetType.Self, true, "process") { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.Procedure];
    protected virtual PileType TargetPile => PileType.Hand;
    protected virtual bool IsLegal(FactoryComponentCard card) => true;
    protected abstract void Apply(FactoryComponentCard card);
    protected virtual string SelectionKey => "KILLER_FACTORY_SELECT_PROCESS_TARGET";
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await ExecuteProcedureAsync(context);
    public virtual async Task<bool> ExecuteProcedureAsync(PlayerChoiceContext context)
    {
        var candidates = TargetPile.GetPile(Owner).Cards.OfType<FactoryComponentCard>().Where(IsLegal).ToList();
        if (candidates.Count == 0) return false;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", SelectionKey), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner, prefs)).OfType<FactoryComponentCard>().FirstOrDefault();
        if (selected is null) return false;
        Apply(selected);
        return true;
    }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class StandardCalibration : SelectComponentProcedure
{
    private int _amount = 5;
    private bool _drawPile;
    protected override PileType TargetPile => _drawPile ? PileType.Draw : PileType.Hand;
    public StandardCalibration() : base(1, CardRarity.Common) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!FactoryProcedureRequirements.CanPay(this)) return;
        if (await ExecuteProcedureAsync(context)) await FactoryProcedureRequirements.PayAsync(this);
    }
    protected override void Apply(FactoryComponentCard card)
    {
        var segment = card.GetNativeEffectSegments().FirstOrDefault(effect => effect.Kind is FactoryEffectKind.Damage or FactoryEffectKind.Block);
        if (segment is null) return;
        card.GetOrCreateCapability<FactoryCardStateCapability>().AddFusion(
            [new FactoryEffectSegment { Kind = segment.Kind, Amount = _amount }]);
    }
    protected override void OnUpgrade() { _amount = 7; _drawPile = true; }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class ExternalSpring : SelectComponentProcedure
{
    public ExternalSpring() : base(1, CardRarity.Uncommon) { }
    protected override bool IsLegal(FactoryComponentCard card) =>
        !card.TryGetCapability<FactoryCardStateCapability>(out var state) || !state.HasExternalSpring;
    protected override void Apply(FactoryComponentCard card) =>
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(state => state.HasExternalSpring = true);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Solidification : SelectComponentProcedure
{
    public Solidification() : base(2, CardRarity.Uncommon) { }
    protected override bool IsLegal(FactoryComponentCard card) => card.Keywords.Contains(FactoryKeywords.DisposableComponent);
    protected override void Apply(FactoryComponentCard card)
    {
        card.RemoveKeyword(FactoryKeywords.DisposableComponent);
        card.RemoveKeyword(CardKeyword.Exhaust);
        card.AddKeyword(FactoryKeywords.PermanentComponent);
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(_ => { });
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class EfficientStructure : SelectComponentProcedure
{
    public EfficientStructure() : base(1, CardRarity.Uncommon) { }
    protected override void Apply(FactoryComponentCard card) =>
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(state =>
        { state.IsEfficient = true; state.EfficientDraw = IsUpgraded; });
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Hardening : SelectComponentProcedure
{
    public Hardening() : base(1, CardRarity.Uncommon) { }
    protected override void Apply(FactoryComponentCard card) =>
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(state => state.IsHard = true);
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class StreamlinedImprovement : SelectComponentProcedure
{
    private int _increase = 2;
    public StreamlinedImprovement() : base(1, CardRarity.Uncommon) { }
    protected override void Apply(FactoryComponentCard card)
    {
        var cost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
        card.EnergyCost.SetCustomBaseCost(cost + _increase);
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(state => state.IsStreamlined = true);
    }
    protected override void OnUpgrade() => _increase = 1;
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class StickyCoating : SelectComponentProcedure
{
    public StickyCoating() : base(1, CardRarity.Uncommon) { }
    protected override void Apply(FactoryComponentCard card) =>
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(state =>
        { state.IsSticky = true; state.StickyFirstCopyFree = IsUpgraded; });
    protected override void OnUpgrade() { }
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class Precisionization : SelectComponentProcedure
{
    public Precisionization() : base(2, CardRarity.Rare) { }
    protected override bool IsLegal(FactoryComponentCard card) =>
        !card.Keywords.Contains(FactoryKeywords.PrecisionComponent) || card.Keywords.Contains(FactoryKeywords.FragileComponent);
    protected override void Apply(FactoryComponentCard card)
    {
        card.RemoveKeyword(FactoryKeywords.FragileComponent);
        card.AddKeyword(FactoryKeywords.PrecisionComponent);
        card.GetOrCreateCapability<FactoryCardStateCapability>().ApplyProcessing(_ => { });
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(KillerFactoryCardPool))]
public sealed class OverallCasting : FactoryCardTemplate, IFactoryProcedureCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.Procedure, CardKeyword.Exhaust];
    public OverallCasting() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self, true, "process") { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await ExecuteProcedureAsync(context);
    public async Task<bool> ExecuteProcedureAsync(PlayerChoiceContext context)
    {
        var components = PileType.Hand.GetPile(Owner).Cards.OfType<FactoryComponentCard>().ToList();
        if (components.Count < 2) return false;
        var bodyPrefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_FUSION_BODY"), 1)
        { Cancelable = true, RequireManualConfirmation = true };
        var body = (await CardSelectCmd.FromSimpleGrid(context, components, Owner, bodyPrefs)).OfType<FactoryComponentCard>().FirstOrDefault();
        if (body is null) return false;
        for (var i = 0; i < 3; i++)
        {
            var materials = PileType.Hand.GetPile(Owner).Cards.OfType<FactoryComponentCard>().Where(card => !ReferenceEquals(card, body)).ToList();
            if (materials.Count == 0) break;
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_FUSION_MATERIAL"), 1)
            { Cancelable = true, RequireManualConfirmation = true };
            var material = (await CardSelectCmd.FromSimpleGrid(context, materials, Owner, prefs)).OfType<FactoryComponentCard>().FirstOrDefault();
            if (material is null) break;
            await FactoryFusionService.FuseAsync(body, material, false);
        }
        return true;
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
