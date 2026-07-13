using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

[RegisterCard(typeof(KillerFactoryCardPool))]
[RegisterCharacterStarterCard(typeof(KillerFactoryCharacter), 1)]
public sealed class ManualAssembly : FactoryCardTemplate
{
    public sealed record FusionTargets(FactoryComponentCard Body, FactoryComponentCard Material);

    public override IEnumerable<CardKeyword> CanonicalKeywords => [FactoryKeywords.Procedure];

    public ManualAssembly() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true, "process")
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var targets = await SelectTargetsAsync(choiceContext, Owner);
        if (targets is not null)
            await FactoryFusionService.FuseAsync(targets.Body, targets.Material, IsUpgraded);
    }

    public async Task<FusionTargets?> SelectTargetsAsync(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Players.Player owner,
        bool reportCancellation = true)
    {
        var state = FactoryCombatState.For(owner.Creature.CombatState!);
        var components = PileType.Hand.GetPile(owner).Cards
            .OfType<FactoryComponentCard>()
            .ToList();

        if (components.Count < 2)
        {
            state.Record("手工装配失败：需要两张可融锻构件");
            return null;
        }

        var bodyPrefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_FUSION_BODY"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var body = (await CardSelectCmd.FromSimpleGrid(choiceContext, components, owner, bodyPrefs))
            .OfType<FactoryComponentCard>()
            .FirstOrDefault();
        if (body is null)
        {
            if (reportCancellation) state.Record("取消手工装配");
            return null;
        }

        var materialPrefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_FUSION_MATERIAL"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var material = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                components.Where(card => !ReferenceEquals(card, body)).ToList(),
                owner,
                materialPrefs))
            .OfType<FactoryComponentCard>()
            .FirstOrDefault();
        if (material is null)
        {
            if (reportCancellation) state.Record("取消手工装配");
            return null;
        }

        return new FusionTargets(body, material);
    }

    protected override void OnUpgrade()
    {
        // 升级改变装配流程：先分别执行两张构件的标准升级，费用仍为 1。
    }
}
