using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using KillerFactory.Mechanics;
using STS2RitsuLib;
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
    private sealed class MachineWidget
    {
        public required FactoryMachineState Machine { get; init; }
        public required Control DropRegion { get; init; }
        public required Label Label { get; init; }
    }

    public static FactoryLinePanel? Active { get; private set; }

    private HBoxContainer _slots = null!;
    private Label _status = null!;
    private readonly List<MachineWidget> _widgets = [];
    private int _renderedMachineCount = -1;
    private bool _busy;

    public void Setup(Node parent, Node node)
    {
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = -470f;
        OffsetRight = 470f;
        OffsetTop = -340f;
        OffsetBottom = -205f;
    }

    public override void _Ready()
    {
        Active = this;
        MouseFilter = MouseFilterEnum.Ignore;

        var background = new ColorRect
        {
            Color = new Color("16232be8"),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var layout = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 7);
        layout.AddThemeConstantOverride("separation", 3);
        AddChild(layout);

        var title = new Label
        {
            Text = "杀戮工厂 · 机械产线（最多10台）",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        title.AddThemeColorOverride("font_color", new Color("f5c451"));
        title.AddThemeFontSizeOverride("font_size", 17);
        layout.AddChild(title);

        _slots = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _slots.AddThemeConstantOverride("separation", 5);
        layout.AddChild(_slots);

        _status = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _status.AddThemeFontSizeOverride("font_size", 13);
        layout.AddChild(_status);
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Active, this))
            Active = null;
    }

    public override void _Process(double delta)
    {
        var state = FactoryCombatState.Current;
        Visible = state is not null;
        if (state is null)
            return;

        if (_renderedMachineCount != state.Machines.Count)
            RebuildMachines(state);

        foreach (var widget in _widgets)
        {
            var cardName = widget.Machine.CachedCard?.Title ?? "拖入构件进行装填";
            widget.Label.Text = $"{widget.Machine.Name}\n电量 {widget.Machine.Energy}/{widget.Machine.MaxEnergy}\n{cardName}";
        }

        _status.Text = state.LastAction;
    }

    public bool TryGetDropMachine(Vector2 screenPosition, CardModel card, out FactoryMachineState machine)
    {
        foreach (var widget in _widgets)
        {
            if (widget.DropRegion.GetGlobalRect().HasPoint(screenPosition) && widget.Machine.CanAccept(card))
            {
                machine = widget.Machine;
                return true;
            }
        }

        machine = null!;
        return false;
    }

    public void QueueLoad(FactoryMachineState machine, CardModel card)
    {
        if (_busy)
            return;
        TaskHelper.RunSafely(LoadAsync(machine, card));
    }

    private void RebuildMachines(FactoryCombatState state)
    {
        foreach (var child in _slots.GetChildren())
            child.QueueFree();
        _widgets.Clear();

        foreach (var machine in state.Machines.Take(FactoryCombatState.MaximumMachineSlots))
        {
            var panel = new ColorRect
            {
                Color = new Color("376b78"),
                CustomMinimumSize = new Vector2(180f, 75f),
                MouseFilter = MouseFilterEnum.Stop,
            };
            _slots.AddChild(panel);

            var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 3);
            panel.AddChild(body);

            var label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            body.AddChild(label);

            var buttons = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
            body.AddChild(buttons);
            var start = new Button { Text = "启动", CustomMinimumSize = new Vector2(82f, 23f) };
            var retrieve = new Button { Text = "取回", CustomMinimumSize = new Vector2(82f, 23f) };
            start.Pressed += () => TaskHelper.RunSafely(ActivateAsync(machine));
            retrieve.Pressed += () => TaskHelper.RunSafely(RetrieveAsync(machine));
            buttons.AddChild(start);
            buttons.AddChild(retrieve);

            _widgets.Add(new MachineWidget { Machine = machine, DropRegion = panel, Label = label });
        }

        _renderedMachineCount = state.Machines.Count;
    }

    private async Task LoadAsync(FactoryMachineState machine, CardModel card)
    {
        if (_busy || !machine.CanAccept(card) || card.Pile?.Type != PileType.Hand)
            return;

        _busy = true;
        try
        {
            var previous = machine.CachedCard;
            await CardPileCmd.Add(card, FactoryMachineCachePile.PileType);
            machine.CachedCard = card;
            if (previous is not null && !ReferenceEquals(previous, card))
                await CardPileCmd.Add(previous, PileType.Hand);
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record(
                previous is null ? $"{card.Title} 已装填" : $"{card.Title} 已替换 {previous.Title}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RetrieveAsync(FactoryMachineState machine)
    {
        var card = machine.CachedCard;
        if (_busy || card is null)
            return;

        var hand = PileType.Hand.GetPile(card.Owner);
        if (hand.Cards.Count >= RitsuLibFramework.GetMaxHandSize(card.Owner))
        {
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record("手牌已满，无法取回构件");
            return;
        }

        _busy = true;
        try
        {
            await CardPileCmd.Add(card, PileType.Hand);
            machine.CachedCard = null;
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"已取回 {card.Title}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ActivateAsync(FactoryMachineState machine)
    {
        var card = machine.CachedCard;
        if (_busy || card is null)
            return;

        var cost = card.EnergyCost.GetAmountToSpend();
        if (cost > machine.Energy)
        {
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record("机械电量不足");
            return;
        }

        _busy = true;
        try
        {
            machine.Energy -= cost;
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, skipCardPileVisuals: true);
            if (card.Pile?.Type != PileType.Exhaust)
                await CardPileCmd.Add(card, FactoryMachineCachePile.PileType);
            else
                machine.CachedCard = null;
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"{machine.Name} 启动：{card.Title}");
        }
        finally
        {
            _busy = false;
        }
    }
}
