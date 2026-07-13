using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using KillerFactory.Mechanics;

namespace KillerFactory.Ui;

public enum BufferDropFeedback
{
    None,
    Accept,
    Replace,
    Reject,
}

/// <summary>机械缓存的唯一可投放区域，同时显示真实卡牌实例的紧凑缩略信息。</summary>
public sealed partial class MachineBufferSlotView : PanelContainer
{
    private readonly Label _cost = new();
    private readonly Label _title = new();
    private readonly Label _markers = new();
    private readonly Label _hint = new();
    private readonly Button _clickTarget = new();
    private readonly StyleBoxFlat _style = new();
    private string _emptyHint = "点击选择构件";

    public event Action? Clicked;

    public MachineBufferSlotView()
    {
        CustomMinimumSize = new Vector2(112f, 52f);
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "点击并从手牌中选择要装填的牌";

        _style.BgColor = new Color("172c35e8");
        _style.BorderColor = new Color("4d8995");
        _style.SetBorderWidthAll(2);
        _style.SetCornerRadiusAll(7);
        AddThemeStyleboxOverride("panel", _style);

        var root = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        root.AddThemeConstantOverride("margin_left", 5);
        root.AddThemeConstantOverride("margin_right", 5);
        root.AddThemeConstantOverride("margin_top", 3);
        root.AddThemeConstantOverride("margin_bottom", 3);
        AddChild(root);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 5);
        root.AddChild(row);

        _cost.CustomMinimumSize = new Vector2(25f, 25f);
        _cost.HorizontalAlignment = HorizontalAlignment.Center;
        _cost.VerticalAlignment = VerticalAlignment.Center;
        _cost.AddThemeColorOverride("font_color", new Color("ffe17a"));
        _cost.AddThemeFontSizeOverride("font_size", 17);
        row.AddChild(_cost);

        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 0);
        row.AddChild(text);
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _title.AddThemeFontSizeOverride("font_size", 14);
        text.AddChild(_title);
        _markers.AddThemeColorOverride("font_color", new Color("a9dce5"));
        _markers.AddThemeFontSizeOverride("font_size", 11);
        text.AddChild(_markers);

        _hint.Text = "点击选择构件";
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.AddThemeColorOverride("font_color", new Color("8db3bb"));
        _hint.AddThemeFontSizeOverride("font_size", 14);
        root.AddChild(_hint);

        // A real Button is used as a transparent hit target.  This is more reliable than
        // relying on PanelContainer.GuiInput through nested combat UI controls.
        _clickTarget.Flat = true;
        _clickTarget.FocusMode = FocusModeEnum.None;
        _clickTarget.MouseDefaultCursorShape = CursorShape.PointingHand;
        _clickTarget.TooltipText = TooltipText;
        _clickTarget.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _clickTarget.Pressed += () => Clicked?.Invoke();
        AddChild(_clickTarget);
    }

    public void Configure(string slotName, string acceptedCardName)
    {
        _emptyHint = $"点击选择{acceptedCardName}";
        TooltipText = $"{slotName}：点击选择手牌中的{acceptedCardName}";
        _clickTarget.TooltipText = TooltipText;
        if (_hint.Visible) _hint.Text = _emptyHint;
    }

    public void Refresh(CardModel? card)
    {
        var hasCard = card is not null;
        _cost.Visible = hasCard;
        _title.Visible = hasCard;
        _markers.Visible = hasCard;
        _hint.Visible = !hasCard;
        if (card is null)
            return;

        _cost.Text = card.EnergyCost.GetAmountToSpend().ToString();
        _title.Text = card.Title + (card.IsUpgraded ? "+" : string.Empty);
        var markers = new List<string>();
        if (card.Keywords.Contains(FactoryKeywords.PrecisionComponent)) markers.Add("精密");
        if (card.Keywords.Contains(FactoryKeywords.FragileComponent)) markers.Add("脆弱");
        if (card.Keywords.Contains(FactoryKeywords.DisposableComponent)) markers.Add("一次");
        if (card.Keywords.Contains(FactoryKeywords.Procedure)) markers.Add("工序");
        _markers.Text = markers.Count == 0 ? "永久构件" : string.Join(" · ", markers);

        _style.BgColor = card.Type == CardType.Attack
            ? new Color("552f36ee")
            : new Color("254b5aee");
    }

    public void SetDropFeedback(BufferDropFeedback feedback)
    {
        switch (feedback)
        {
            case BufferDropFeedback.Accept:
                _style.BorderColor = new Color("62e7f5");
                _style.SetBorderWidthAll(4);
                _hint.Text = "松开以装填";
                break;
            case BufferDropFeedback.Replace:
                _style.BorderColor = new Color("ffd15c");
                _style.SetBorderWidthAll(4);
                _markers.Text = "松开以替换";
                break;
            case BufferDropFeedback.Reject:
                _style.BorderColor = new Color("7d4b50");
                _style.SetBorderWidthAll(2);
                break;
            default:
                _style.BorderColor = new Color("4d8995");
                _style.SetBorderWidthAll(2);
                _hint.Text = _emptyHint;
                break;
        }
    }
}
