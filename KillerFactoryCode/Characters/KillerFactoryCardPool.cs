using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace KillerFactory.Characters;

public sealed class AssemblerCardPool : TypeListCardPoolModel
{
    private static readonly Material? PoolFrameTintMaterial =
        MaterialUtils.CreateReplaceHueShaderMaterial(0.42f, 0.65f, 0.72f);

    public override string Title => "Assembler";
    public override string EnergyColorName => "KillerFactory";
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
    public override Color DeckEntryCardColor => AssemblerCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;
    public override bool IsColorless => false;
}
