using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace KillerFactory.Characters;

public sealed class KillerFactoryCardPool : TypeListCardPoolModel
{
    private static readonly Material? PoolFrameTintMaterial =
        MaterialUtils.CreateReplaceHueShaderMaterial(0.42f, 0.65f, 0.72f);

    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => "KillerFactory";
    public override string EnergyColorName => "KillerFactory";

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://KillerFactory/... 里的 KillerFactory 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor => KillerFactoryCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
