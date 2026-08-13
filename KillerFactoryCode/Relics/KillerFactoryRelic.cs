using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using KillerFactory.Characters;
using KillerFactory.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib;

namespace KillerFactory.Relics;

// RegisterRelic 会把遗物注册进指定遗物池。
// RegisterCharacterStarterRelic 会把它作为 KillerFactoryCharacter 的初始遗物。
[RegisterRelic(typeof(AssemblerRelicPool))]
[RegisterCharacterStarterRelic(typeof(AssemblerCharacter))]
public sealed class AutomaticAssemblyMachine : ModRelicTemplate
{
    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Common;

    // 图片资源统一放在 AssetProfile 里配置。
    // 三个路径可以先指向同一张图。后续有高清图或轮廓图时再拆开。
    public override RelicAssetProfile AssetProfile => new(
        // 小图标（原版 85x85）。
        IconPath: $"{Entry.ResPath}/images/relics/simple_arm.svg",
        // 轮廓图标（原版 85x85）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/simple_arm.svg",
        // 大图标（原版 256x256）。
        BigIconPath: $"{Entry.ResPath}/images/relics/simple_arm.svg");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var hand = MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand.GetPile(player);
        if (hand.Cards.Any(static card => card is BasicAssembly))
            return;

        var assembly = player.Creature.CombatState!.CreateCard<BasicAssembly>(player);
        await MegaCrit.Sts2.Core.Commands.CardPileCmd.AddGeneratedCardToCombat(
            assembly, MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand, player);
    }
}
