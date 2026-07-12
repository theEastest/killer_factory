using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace KillerFactory.Ui;

[RegisterNodeAttachment(
    typeof(NCombatUi),
    "factory_line_panel",
    NodeName = "KillerFactoryLinePanel",
    DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName)]
public sealed partial class FactoryLinePanel : Control, INodeAttachmentSetup
{
    private Label _arm = null!;
    private Label _storage = null!;
    private Label _status = null!;

    public void Setup(Node parent, Node node)
    {
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = -455f;
        OffsetRight = 455f;
        OffsetTop = -350f;
        OffsetBottom = -238f;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var background = new ColorRect { Color = new Color("16232bdf") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 8);
        layout.AddThemeConstantOverride("separation", 4);
        AddChild(layout);

        var title = new Label
        {
            Text = "杀戮工厂 · 机械产线（初步实现）",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeColorOverride("font_color", new Color("f5c451"));
        title.AddThemeFontSizeOverride("font_size", 18);
        layout.AddChild(title);

        var slots = new HBoxContainer();
        slots.AddThemeConstantOverride("separation", 6);
        layout.AddChild(slots);

        _arm = AddSlot(slots, "机械臂", new Color("376b78"));
        AddSlot(slots, "加工台\n未安装", new Color("55515f"));
        _storage = AddSlot(slots, "仓储室", new Color("6b5b35"));
        AddSlot(slots, "分解室\n待机", new Color("664044"));
        _status = AddSlot(slots, "传送带", new Color("385844"));
    }

    public override void _Process(double delta)
    {
        var state = FactoryCombatState.Current;
        Visible = state is not null;
        if (state is null)
            return;

        _arm.Text = state.SimpleArmInstalled ? "机械臂\n已架设" : "机械臂\n等待架设";
        _storage.Text = $"仓储室\n机械 {state.MechanicalMaterial} / 化合 {state.CompoundMaterial}";
        _status.Text = $"传送带\n{state.LastAction}";
    }

    private static Label AddSlot(Container parent, string text, Color color)
    {
        var block = new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(174f, 58f),
        };
        parent.AddChild(block);

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 14);
        block.AddChild(label);
        return label;
    }
}
