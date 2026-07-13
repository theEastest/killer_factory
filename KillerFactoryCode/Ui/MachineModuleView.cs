using Godot;
using MegaCrit.Sts2.Core.Models;
using KillerFactory.Mechanics;

namespace KillerFactory.Ui;

/// <summary>机械模块只显示状态并发出操作请求；资源支付和卡牌移动由产线控制器处理。</summary>
public sealed partial class MachineModuleView : PanelContainer
{
    private readonly StyleBoxFlat _style = new();
    private readonly Label _name = new();
    private readonly Label _chargePips = new();
    private readonly Label _chargeText = new();
    private readonly Label _requirement = new();
    private readonly Label _status = new();
    private readonly Button _start = new();
    private readonly Button _retrieve = new();

    public FactoryMachineState Machine { get; }
    public MachineBufferSlotView BufferSlot { get; } = new();
    public event Action<FactoryMachineState>? StartRequested;
    public event Action<FactoryMachineState>? RetrieveRequested;
    public event Action<FactoryMachineState>? LoadRequested;
    public event Action<FactoryMachineState>? Selected;

    public MachineModuleView(FactoryMachineState machine)
    {
        Machine = machine;
        CustomMinimumSize = new Vector2(machine.Kind == FactoryMachineKind.ProcessingTable ? 158f : 142f, 184f);
        MouseFilter = MouseFilterEnum.Stop;

        _style.BgColor = new Color("203943f2");
        _style.BorderColor = new Color("557985");
        _style.SetBorderWidthAll(2);
        _style.SetCornerRadiusAll(9);
        AddThemeStyleboxOverride("panel", _style);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 6);
        AddChild(margin);

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(header);
        var icon = new Label
        {
            Text = machine.Kind == FactoryMachineKind.ProcessingTable ? "台" : "臂",
            CustomMinimumSize = new Vector2(28f, 26f),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.AddThemeColorOverride("font_color", new Color("f5c451"));
        icon.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(icon);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _name.AddThemeFontSizeOverride("font_size", 15);
        header.AddChild(_name);

        var charge = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        charge.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(charge);
        _chargePips.AddThemeColorOverride("font_color", new Color("f5c451"));
        _chargePips.AddThemeFontSizeOverride("font_size", 13);
        charge.AddChild(_chargePips);
        _chargeText.AddThemeFontSizeOverride("font_size", 12);
        charge.AddChild(_chargeText);

        var slotTitle = new Label { Text = machine.SlotName, HorizontalAlignment = HorizontalAlignment.Center };
        slotTitle.AddThemeColorOverride("font_color", new Color("b8d8df"));
        slotTitle.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(slotTitle);
        BufferSlot.Configure(machine.SlotName, machine.Kind == FactoryMachineKind.ProcessingTable ? "工序牌" : "构件牌");
        BufferSlot.Clicked += () => LoadRequested?.Invoke(Machine);
        column.AddChild(BufferSlot);

        _requirement.HorizontalAlignment = HorizontalAlignment.Center;
        _requirement.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _requirement.AddThemeFontSizeOverride("font_size", 11);
        column.AddChild(_requirement);

        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _status.AddThemeFontSizeOverride("font_size", 11);
        column.AddChild(_status);

        var buttons = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        buttons.AddThemeConstantOverride("separation", 4);
        column.AddChild(buttons);
        _start.Text = "启动";
        _retrieve.Text = machine.Kind == FactoryMachineKind.ProcessingTable ? "取回工序" : "取回";
        _start.CustomMinimumSize = new Vector2(58f, 34f);
        _retrieve.CustomMinimumSize = new Vector2(machine.Kind == FactoryMachineKind.ProcessingTable ? 78f : 58f, 34f);
        _start.AddThemeFontSizeOverride("font_size", 13);
        _retrieve.AddThemeFontSizeOverride("font_size", 13);
        _start.Pressed += () => StartRequested?.Invoke(Machine);
        _retrieve.Pressed += () => RetrieveRequested?.Invoke(Machine);
        buttons.AddChild(_start);
        buttons.AddChild(_retrieve);

        GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                Selected?.Invoke(Machine);
        };
    }

    public void Refresh(MachineStartResult startResult, bool executing, bool selected)
    {
        var card = Machine.CachedCard;
        _name.Text = Machine.Name;
        _chargePips.Text = new string('●', Machine.Energy) + new string('○', Math.Max(0, Machine.MaxEnergy - Machine.Energy));
        _chargeText.Text = $" {Machine.Energy}/{Machine.MaxEnergy}";
        _chargeText.AddThemeColorOverride("font_color", Machine.Energy == 0 ? new Color("ff8d83") : Colors.White);
        BufferSlot.Refresh(card);

        _requirement.Text = Machine.Kind == FactoryMachineKind.ProcessingTable
            ? card is null ? "需要：装入工序" : $"需要：{FactoryProcedureRequirements.For(card).DisplayName}"
            : card is null ? "需要：装入构件" : $"启动电量：{Machine.GetRequiredCharge(card)}";
        _requirement.AddThemeColorOverride("font_color", startResult.FailureReason is MachineStartFailureReason.MissingMaterial
            ? new Color("ff786f") : new Color("a9dce5"));

        var transient = Machine.GetVisibleMessage();
        _status.Text = transient ?? (executing ? "正在执行…" : startResult.Message);
        _status.AddThemeColorOverride("font_color", transient is not null || !startResult.Success
            ? new Color("ff9b8f") : new Color("92efb0"));

        // 不可启动时仍允许点击，以便始终播报明确原因；仅执行期间阻止重复启动。
        _start.Disabled = executing;
        _retrieve.Disabled = card is null || executing;
        _start.TooltipText = startResult.Success
            ? $"{Machine.Name}：消耗 {Machine.GetRequiredCharge(card)} 点机械电量并执行 {card?.Title}"
            : $"无法启动：{startResult.Message}";
        _retrieve.TooltipText = card is null
            ? $"{Machine.SlotName}为空"
            : $"免费将 {card.Title} 放回手牌；手牌已满时不可取回";

        _style.BorderColor = executing
            ? new Color("fff0a1")
            : selected
                ? new Color("8bc9ff")
                : startResult.Success ? new Color("e7bd4e") : new Color("557985");
        _style.SetBorderWidthAll(executing || selected ? 4 : 2);
        Modulate = executing ? new Color(1.08f, 1.08f, 0.92f) : Colors.White;
    }

    public void SetDragFeedback(CardModel? draggedCard, Vector2 mousePosition, bool replacementBlocked)
    {
        if (draggedCard is null)
        {
            BufferSlot.SetDropFeedback(BufferDropFeedback.None);
            Modulate = Colors.White;
            return;
        }

        var accepts = Machine.CanAccept(draggedCard) && !replacementBlocked;
        var over = BufferSlot.GetGlobalRect().HasPoint(mousePosition);
        BufferSlot.SetDropFeedback(!accepts
            ? BufferDropFeedback.Reject
            : over && Machine.CachedCard is not null
                ? BufferDropFeedback.Replace
                : BufferDropFeedback.Accept);
        Modulate = accepts ? Colors.White : new Color(0.55f, 0.55f, 0.55f, 0.8f);
    }
}
