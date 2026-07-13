using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using KillerFactory.Mechanics;
using KillerFactory.Cards;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Models.Capabilities;

namespace KillerFactory.Ui;

[RegisterNodeAttachment(
    typeof(NCombatUi),
    "factory_line_panel",
    NodeName = "KillerFactoryLinePanel",
    DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName)]
public sealed partial class FactoryLinePanel : Control, INodeAttachmentSetup
{
    public static FactoryLinePanel? Active { get; private set; }

    private readonly List<MachineModuleView> _modules = [];
    private HBoxContainer _machineRow = null!;
    private Label _capacity = null!;
    private Label _lastAction = null!;
    private PanelContainer _detailPopup = null!;
    private Label _detailText = null!;
    private FactoryMachineState? _selectedMachine;
    private FactoryMachineState? _executingMachine;
    private int _renderedMachineCount = -1;
    private bool _busy;
    private bool _draggingWindow;
    private Vector2 _lastWindowDragPosition;

    public void Setup(Node parent, Node node)
    {
        // 70% 响应式宽度；停靠在手牌上方并保留约 20px 间距。
        AnchorLeft = 0.15f;
        AnchorRight = 0.85f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetTop = -470f;
        OffsetBottom = -230f;
    }

    public override void _Ready()
    {
        Active = this;
        MouseFilter = MouseFilterEnum.Ignore;

        var dock = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var dockStyle = new StyleBoxFlat
        {
            BgColor = new Color("14252edb"),
            BorderColor = new Color("496d78"),
        };
        dockStyle.SetBorderWidthAll(2);
        dockStyle.SetCornerRadiusAll(12);
        dock.AddThemeStyleboxOverride("panel", dockStyle);
        dock.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dock);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 9);
        margin.AddThemeConstantOverride("margin_right", 9);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        dock.AddChild(margin);

        var layout = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 4);
        margin.AddChild(layout);

        var header = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0f, 30f),
            MouseDefaultCursorShape = CursorShape.Move,
            TooltipText = "按住并拖动以移动机械产线",
        };
        header.GuiInput += OnHeaderGuiInput;
        layout.AddChild(header);
        var title = new Label
        {
            Text = "机械产线　⋮⋮ 拖动",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        title.AddThemeColorOverride("font_color", new Color("f5c451"));
        title.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(title);
        _lastAction = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _lastAction.AddThemeColorOverride("font_color", new Color("b8d8df"));
        _lastAction.AddThemeFontSizeOverride("font_size", 13);
        header.AddChild(_lastAction);
        _capacity = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _capacity.AddThemeFontSizeOverride("font_size", 14);
        header.AddChild(_capacity);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        layout.AddChild(scroll);
        _machineRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _machineRow.Alignment = BoxContainer.AlignmentMode.Center;
        _machineRow.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_machineRow);

        BuildDetailPopup();
    }

    private void OnHeaderGuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _draggingWindow = button.Pressed;
            _lastWindowDragPosition = GetViewport().GetMousePosition();
            AcceptEvent();
            return;
        }

        if (!_draggingWindow || input is not InputEventMouseMotion)
            return;

        var mouse = GetViewport().GetMousePosition();
        MoveWindowWithinViewport(mouse - _lastWindowDragPosition);
        _lastWindowDragPosition = mouse;
        AcceptEvent();
    }

    public override void _Input(InputEvent input)
    {
        if (!_draggingWindow)
            return;

        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _draggingWindow = false;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (input is not InputEventMouseMotion)
            return;

        var mouse = GetViewport().GetMousePosition();
        MoveWindowWithinViewport(mouse - _lastWindowDragPosition);
        _lastWindowDragPosition = mouse;
        GetViewport().SetInputAsHandled();
    }

    private void MoveWindowWithinViewport(Vector2 delta)
    {
        var rect = GetGlobalRect();
        var viewportSize = GetViewportRect().Size;
        var desired = rect.Position + delta;
        desired.X = Mathf.Clamp(desired.X, 0f, Mathf.Max(0f, viewportSize.X - rect.Size.X));
        desired.Y = Mathf.Clamp(desired.Y, 0f, Mathf.Max(0f, viewportSize.Y - rect.Size.Y));
        Position += desired - rect.Position;
    }

    private void BuildDetailPopup()
    {
        _detailPopup = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(310f, 122f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _detailPopup.AnchorLeft = 0.5f;
        _detailPopup.AnchorRight = 0.5f;
        _detailPopup.AnchorTop = 0f;
        _detailPopup.AnchorBottom = 0f;
        _detailPopup.OffsetLeft = -155f;
        _detailPopup.OffsetRight = 155f;
        _detailPopup.OffsetTop = -132f;
        _detailPopup.OffsetBottom = -10f;
        var style = new StyleBoxFlat { BgColor = new Color("132830f4"), BorderColor = new Color("79aeb8") };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(9);
        _detailPopup.AddThemeStyleboxOverride("panel", style);
        AddChild(_detailPopup);

        var column = new VBoxContainer();
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 8);
        _detailPopup.AddChild(column);
        _detailText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = SizeFlags.ExpandFill };
        _detailText.AddThemeFontSizeOverride("font_size", 14);
        column.AddChild(_detailText);
        var close = new Button { Text = "关闭", CustomMinimumSize = new Vector2(82f, 32f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        close.Pressed += () => SelectMachine(null);
        column.AddChild(close);
    }

    public override void _ExitTree()
    {
        _draggingWindow = false;
        if (ReferenceEquals(Active, this)) Active = null;
    }

    public override void _Process(double delta)
    {
        var state = FactoryCombatState.Current;
        Visible = state is not null;
        if (state is null) return;

        if (_renderedMachineCount != state.Machines.Count)
            RebuildMachines(state);

        _capacity.Text = $"{state.Machines.Count}/{FactoryCombatState.MaximumMachineSlots}";
        _lastAction.Text = state.LastAction;
        foreach (var module in _modules)
        {
            var executing = ReferenceEquals(_executingMachine, module.Machine);
            module.Refresh(ValidateStart(module.Machine, executing), executing, ReferenceEquals(_selectedMachine, module.Machine));
        }
        RefreshDetail();
    }

    private void RebuildMachines(FactoryCombatState state)
    {
        foreach (var child in _machineRow.GetChildren()) child.QueueFree();
        _modules.Clear();

        foreach (var machine in state.Machines.Take(FactoryCombatState.MaximumMachineSlots))
        {
            var module = new MachineModuleView(machine);
            module.LoadRequested += requested => TaskHelper.RunSafely(SelectCardToLoadAsync(requested));
            module.StartRequested += requested => TaskHelper.RunSafely(ActivateFromPlayerAsync(requested));
            module.RetrieveRequested += requested => TaskHelper.RunSafely(RetrieveAsync(requested));
            module.Selected += selected => SelectMachine(ReferenceEquals(_selectedMachine, selected) ? null : selected);
            _machineRow.AddChild(module);
            _modules.Add(module);
        }

        var remaining = FactoryCombatState.MaximumMachineSlots - state.Machines.Count;
        if (remaining > 0)
        {
            var empty = new PanelContainer { CustomMinimumSize = new Vector2(96f, 150f), MouseFilter = MouseFilterEnum.Ignore };
            var style = new StyleBoxFlat { BgColor = new Color("1d303766"), BorderColor = new Color("6b858c88") };
            style.SetBorderWidthAll(2);
            style.BorderWidthLeft = style.BorderWidthRight = 2;
            style.SetCornerRadiusAll(9);
            empty.AddThemeStyleboxOverride("panel", style);
            var hint = new Label
            {
                Text = $"＋\n{remaining} 空槽",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            hint.AddThemeColorOverride("font_color", new Color("7f9ba2"));
            hint.AddThemeFontSizeOverride("font_size", 16);
            empty.AddChild(hint);
            _machineRow.AddChild(empty);
        }
        _renderedMachineCount = state.Machines.Count;
    }

    public void QueueActivate(FactoryMachineState machine)
    {
        if (!_busy) TaskHelper.RunSafely(ActivateAsync(machine));
    }

    private async Task ActivateFromPlayerAsync(FactoryMachineState machine)
    {
        if (machine.Kind != FactoryMachineKind.ProcessingTable)
        {
            await ActivateAsync(machine);
            return;
        }

        var state = FactoryCombatState.Current;
        var localPlayerId = LocalContext.NetId;
        var owner = localPlayerId.HasValue
            ? state?.CombatState.Players.FirstOrDefault(player => player.NetId == localPlayerId.Value)
            : null;
        owner ??= state?.CombatState.Players.FirstOrDefault();
        if (_busy || state is null || owner is null || !localPlayerId.HasValue)
            return;

        // Processing-table activation originates from a UI button rather than a running
        // GameAction.  It therefore needs the same synchronized choice context as loading.
        var choiceContext = new HookPlayerChoiceContext(
            owner, localPlayerId.Value, GameActionType.CombatPlayPhaseOnly);
        var activationTask = ActivateAsync(machine, choiceContext);
        await choiceContext.AssignTaskAndWaitForPauseOrCompletion(activationTask);
        await choiceContext.WaitForCompletion();
    }

    public bool IsExecutingCard(CardModel card) =>
        ReferenceEquals(_executingMachine?.CachedCard, card);

    // Programmatic loading is still used by automation cards; player interaction uses
    // SelectCardToLoadAsync exclusively.
    public void QueueLoad(FactoryMachineState machine, CardModel card)
    {
        if (!_busy) TaskHelper.RunSafely(LoadAsync(machine, card));
    }

    private async Task SelectCardToLoadAsync(FactoryMachineState machine)
    {
        var state = FactoryCombatState.Current;
        var localPlayerId = LocalContext.NetId;
        var owner = localPlayerId.HasValue
            ? state?.CombatState.Players.FirstOrDefault(player => player.NetId == localPlayerId.Value)
            : null;
        owner ??= state?.CombatState.Players.FirstOrDefault();
        if (_busy || state is null || owner is null || !localPlayerId.HasValue ||
            machine.Kind == FactoryMachineKind.Power)
            return;

        // This click originates outside a running GameAction.  HookPlayerChoiceContext
        // creates and queues a synchronized action when the selection UI is requested.
        var choiceContext = new HookPlayerChoiceContext(
            owner, localPlayerId.Value, GameActionType.CombatPlayPhaseOnly);
        var selectionTask = SelectCardToLoadCoreAsync(machine, state, owner, choiceContext);
        await choiceContext.AssignTaskAndWaitForPauseOrCompletion(selectionTask);
        await choiceContext.WaitForCompletion();
    }

    private async Task SelectCardToLoadCoreAsync(
        FactoryMachineState machine,
        FactoryCombatState state,
        Player owner,
        PlayerChoiceContext choiceContext)
    {
        var candidates = PileType.Hand.GetPile(owner).Cards
            .Where(machine.CanAccept)
            .ToList();
        if (candidates.Count == 0)
        {
            var accepted = machine.Kind switch
            {
                FactoryMachineKind.ProcessingTable => "工序牌",
                FactoryMachineKind.Decomposer => "原料或废料",
                FactoryMachineKind.Storage => "卡牌",
                _ => "构件牌",
            };
            machine.ShowMessage($"手牌中没有可装入的{accepted}");
            state.Record($"{machine.Name}：手牌中没有可装入的{accepted}");
            return;
        }

        CardModel? selected;
        _busy = true;
        try
        {
            var prefs = new CardSelectorPrefs(
                new LocString("card_selection", "KILLER_FACTORY_SELECT_MACHINE_LOAD"), 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };
            selected = (await CardSelectCmd.FromSimpleGrid(
                    choiceContext, candidates, owner, prefs))
                .FirstOrDefault();
        }
        finally { _busy = false; }

        if (selected is not null)
            await LoadAsync(machine, selected);
    }

    public async Task<int> ActivateAllAsync(PlayerChoiceContext? choiceContext = null)
    {
        var state = FactoryCombatState.Current;
        if (state is null) return 0;
        var count = 0;
        foreach (var machine in state.Machines.ToList())
        {
            if (!ValidateStart(machine, false).Success) continue;
            await ActivateAsync(machine, choiceContext);
            count++;
        }
        return count;
    }

    private void SelectMachine(FactoryMachineState? machine)
    {
        _selectedMachine = machine;
        _detailPopup.Visible = machine is not null;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        var machine = _selectedMachine;
        if (machine is null || !_detailPopup.Visible) return;
        var card = machine.CachedCard;
        var cost = machine.GetRequiredCharge(card);
        var target = machine.Kind == FactoryMachineKind.ProcessingTable
            ? "从当前手牌选择合法构件"
            : "按缓存构件的目标规则执行";
        _detailText.Text = $"{machine.Name}\n当前电量：{machine.Energy}/{machine.MaxEnergy}　{machine.SlotName}：{card?.Title ?? "无"}\n启动消耗：{cost} 点机械电量　目标：{target}\n自动运行：基础设备不支持";
    }

    private async Task LoadAsync(FactoryMachineState machine, CardModel card)
    {
        if (_busy || !machine.CanAccept(card) || card.Pile?.Type != PileType.Hand) return;
        var shouldAutoStart = false;
        _busy = true;
        try
        {
            var previous = machine.CachedCard;
            await CardPileCmd.Add(card, FactoryMachineCachePile.PileType);
            machine.CachedCard = card;
            if (previous is not null && !ReferenceEquals(previous, card)) await CardPileCmd.Add(previous, PileType.Hand);
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record(previous is null
                ? $"{card.Title} 已装入{machine.SlotName}"
                : $"{card.Title} 已替换 {previous.Title}");
            var state = FactoryCombatState.For(card.Owner.Creature.CombatState!);
            shouldAutoStart = state.AutoStartProtocol && machine.Kind == FactoryMachineKind.MechanicalArm;
            if (state.AutoLoadDraw && !state.AutoLoadDrawUsed)
            {
                state.AutoLoadDrawUsed = true;
                await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, card.Owner);
            }
        }
        finally { _busy = false; }
        if (shouldAutoStart) await ActivateAsync(machine);
    }

    private async Task RetrieveAsync(FactoryMachineState machine)
    {
        var card = machine.CachedCard;
        if (_busy || card is null) return;
        if (PileType.Hand.GetPile(card.Owner).Cards.Count >= RitsuLibFramework.GetMaxHandSize(card.Owner))
        {
            var state = FactoryCombatState.For(card.Owner.Creature.CombatState!);
            state.ReportFailure(machine, MachineStartResult.Fail(MachineStartFailureReason.HandFull, "手牌已满，无法取回缓存牌"));
            return;
        }

        _busy = true;
        try
        {
            await CardPileCmd.Add(card, PileType.Hand);
            machine.CachedCard = null;
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"已取回 {card.Title}");
        }
        finally { _busy = false; }
    }

    private async Task ActivateAsync(FactoryMachineState machine, PlayerChoiceContext? choiceContext = null)
    {
        var card = machine.CachedCard;
        var validation = ValidateStart(machine, ReferenceEquals(_executingMachine, machine));
        if (!validation.Success)
        {
            if (FactoryCombatState.Current is { } failedState)
                failedState.ReportFailure(machine, validation);
            return;
        }

        if (card is null) return;
        if (machine.Kind == FactoryMachineKind.Decomposer)
        {
            _busy = true;
            _executingMachine = machine;
            try
            {
                await PayChargeAsync(machine, card, 1);
                await CardPileCmd.Add(card, PileType.Exhaust);
                machine.CachedCard = null;
                FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"{machine.Name} 分解 {card.Title}");
            }
            finally { _executingMachine = null; _busy = false; }
            return;
        }
        if (machine.Kind == FactoryMachineKind.ProcessingTable)
        {
            await ActivateProcessingTableAsync(machine, card, choiceContext);
            return;
        }

        var cost = machine.GetRequiredCharge(card);

        _busy = true;
        _executingMachine = machine;
        try
        {
            await PayChargeAsync(machine, card, cost);
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, skipCardPileVisuals: true);
            if (CombatManager.Instance.IsOverOrEnding) machine.CachedCard = null;
            else if (await FactoryAbilityRuntime.ResolveExternalSpringAfterMachinePlayAsync(card)) machine.CachedCard = null;
            else if (card.Pile?.Type != PileType.Exhaust) await CardPileCmd.Add(card, FactoryMachineCachePile.PileType);
            else machine.CachedCard = null;
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"{machine.Name} 启动：{card.Title}");
            var state = FactoryCombatState.For(card.Owner.Creature.CombatState!);
            if (state.AutoStartRefund && !state.AutoStartRefundUsed)
            {
                state.AutoStartRefundUsed = true;
                machine.Energy = Math.Min(machine.MaxEnergy, machine.Energy + 1);
            }
            if (state.SelfStartingBusThreshold > 0 && ++state.MachineCardsPlayed >= state.SelfStartingBusThreshold)
            {
                state.MachineCardsPlayed = 0;
                state.RechargeMachines();
                await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, card.Owner);
            }
        }
        finally
        {
            _executingMachine = null;
            _busy = false;
        }
    }

    private MachineStartResult ValidateStart(FactoryMachineState machine, bool executing)
    {
        if (_busy && !executing)
            return MachineStartResult.Fail(MachineStartFailureReason.ActionQueueBusy, "动作队列或选择界面正忙");
        if (executing)
            return MachineStartResult.Fail(MachineStartFailureReason.MachineBusy, "机械正在运行");
        if (machine.Kind == FactoryMachineKind.Power)
            return MachineStartResult.Fail(MachineStartFailureReason.MachineDisabled, "动力机械无需手动装填启动");
        if (machine.Kind == FactoryMachineKind.Storage)
            return MachineStartResult.Fail(MachineStartFailureReason.MachineDisabled, "仓储室使用装入和取回，不需要启动");

        var card = machine.CachedCard;
        if (card is null)
            return MachineStartResult.Fail(
                machine.Kind == FactoryMachineKind.ProcessingTable
                    ? MachineStartFailureReason.NoCachedOperation
                    : MachineStartFailureReason.NoLoadedCard,
                machine.Kind == FactoryMachineKind.ProcessingTable ? "未装入工序" : "未装入构件");
        if (!machine.CanAccept(card))
            return MachineStartResult.Fail(MachineStartFailureReason.InvalidCachedCard,
                machine.Kind == FactoryMachineKind.ProcessingTable ? "缓存牌不是合法工序" : "缓存牌不是合法构件");

        var required = machine.GetRequiredCharge(card);
        var available = GetAvailableCharge(machine, card);
        if (available < required)
            return MachineStartResult.Fail(MachineStartFailureReason.InsufficientCharge,
                $"需要 {required} 点电量，当前可用 {available} 点", required, available);

        if (machine.Kind == FactoryMachineKind.ProcessingTable)
        {
            if (card is not ManualAssembly && card is not IFactoryProcedureCard)
                return MachineStartResult.Fail(MachineStartFailureReason.InvalidCachedCard, "该工序尚未实现加工台执行规则");
            var legalCount = PileType.Hand.GetPile(card.Owner).Cards.OfType<FactoryComponentCard>().Count();
            if (card is ManualAssembly && legalCount < 2)
                return MachineStartResult.Fail(MachineStartFailureReason.NoLegalCardTarget, "手牌中需要两张可融锻构件");
            if (card is IFactoryProcedureCard procedure && !procedure.HasLegalTargets(fromProcessingTable: true))
                return MachineStartResult.Fail(MachineStartFailureReason.NoLegalCardTarget, "手牌中没有可加工构件");
            var requirement = FactoryProcedureRequirements.For(card);
            if (!FactoryProcedureRequirements.CanPay(card))
                return MachineStartResult.Fail(MachineStartFailureReason.MissingMaterial,
                    $"缺少 {requirement.DisplayName}", missingResourceName: requirement.DisplayName);
        }

        return MachineStartResult.Ok(machine.Kind == FactoryMachineKind.ProcessingTable ? "可选择加工目标" : "可启动");
    }

    private async Task ActivateProcessingTableAsync(
        FactoryMachineState machine,
        CardModel card,
        PlayerChoiceContext? choiceContext)
    {
        var processingChoiceContext = choiceContext ?? new ThrowingPlayerChoiceContext();
        if (card is IFactoryProcedureCard procedure)
        {
            _busy = true;
            _executingMachine = machine;
            try
            {
                var resourcesCommitted = false;
                async Task<bool> CommitResourcesAsync()
                {
                    if (!ReferenceEquals(machine.CachedCard, card))
                    {
                        FactoryCombatState.For(card.Owner.Creature.CombatState!).ReportFailure(machine,
                            MachineStartResult.Fail(MachineStartFailureReason.CardNoLongerAvailable, "工序牌已不在加工台中"));
                        return false;
                    }
                    if (GetAvailableCharge(machine, card) < 1)
                    {
                        FactoryCombatState.For(card.Owner.Creature.CombatState!).ReportFailure(machine,
                            MachineStartResult.Fail(MachineStartFailureReason.InsufficientCharge, "确认目标后电量已不足", 1,
                                GetAvailableCharge(machine, card)));
                        return false;
                    }
                    if (!FactoryProcedureRequirements.CanPay(card))
                    {
                        var requirement = FactoryProcedureRequirements.For(card);
                        FactoryCombatState.For(card.Owner.Creature.CombatState!).ReportFailure(machine,
                            MachineStartResult.Fail(MachineStartFailureReason.MissingMaterial,
                                $"确认目标后缺少 {requirement.DisplayName}", missingResourceName: requirement.DisplayName));
                        return false;
                    }

                    await FactoryProcedureRequirements.PayAsync(card);
                    await PayChargeAsync(machine, card, 1);
                    resourcesCommitted = true;
                    return true;
                }

                var applied = await procedure.ExecuteProcedureAsync(
                    processingChoiceContext,
                    fromProcessingTable: true,
                    commitResources: CommitResourcesAsync);
                if (!applied)
                {
                    if (!resourcesCommitted)
                        FactoryCombatState.For(card.Owner.Creature.CombatState!).Record("已取消加工；未消耗机械电量或材料");
                    return;
                }
                await ResolveRecursiveProcessingAsync(machine, card.Owner);
                FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"{machine.Name} 执行 {card.Title}；工序继续保留");
            }
            finally { _executingMachine = null; _busy = false; }
            return;
        }
        if (card is not ManualAssembly assembly) return;
        _busy = true;
        _executingMachine = machine;
        try
        {
            var targets = await assembly.SelectTargetsAsync(
                processingChoiceContext, card.Owner, reportCancellation: false);
            if (targets is null)
            {
                FactoryCombatState.For(card.Owner.Creature.CombatState!).Record("已取消加工；未消耗电量或材料");
                return;
            }

            // 选择完成后重新验证真实实例。验证失败时，不移动卡牌也不支付电量。
            var hand = PileType.Hand.GetPile(card.Owner);
            var stillValid = ReferenceEquals(machine.CachedCard, card)
                && GetAvailableCharge(machine, card) >= 1
                && hand.Cards.Contains(targets.Body)
                && hand.Cards.Contains(targets.Material)
                && !ReferenceEquals(targets.Body, targets.Material);
            if (!stillValid)
            {
                FactoryCombatState.For(card.Owner.Creature.CombatState!).ReportFailure(machine,
                    MachineStartResult.Fail(MachineStartFailureReason.TargetNoLongerValid, "确认前目标或资源已失效"));
                return;
            }

            await PayChargeAsync(machine, card, 1);
            var fused = await FactoryFusionService.FuseAsync(targets.Body, targets.Material, assembly.IsUpgraded);
            if (fused)
                await ResolveRecursiveProcessingAsync(machine, card.Owner);
            FactoryCombatState.For(card.Owner.Creature.CombatState!).Record($"{machine.Name} 执行 {card.Title}；工序继续保留");
        }
        finally
        {
            _executingMachine = null;
            _busy = false;
        }
    }

    private static int GetAvailableCharge(FactoryMachineState machine, CardModel card) =>
        machine.Energy + PileType.Hand.GetPile(card.Owner).Cards.OfType<PortableBattery>().Sum(battery => battery.Charge);

    private static async Task PayChargeAsync(FactoryMachineState machine, CardModel source, int amount)
    {
        var remaining = amount;
        foreach (var battery in PileType.Hand.GetPile(source.Owner).Cards.OfType<PortableBattery>().ToList())
        {
            remaining -= battery.ConsumeCharge(remaining);
            if (battery.Charge == 0)
                await CardCmd.TransformTo<KillerFactoryScrap>(battery, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
            if (remaining == 0) break;
        }
        machine.Energy = Math.Max(0, machine.Energy - remaining);
    }

    private static async Task ResolveRecursiveProcessingAsync(FactoryMachineState table, MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        var state = FactoryCombatState.For(owner.Creature.CombatState!);
        if (!state.RecursiveProcessing || state.RecursiveProcessingThreshold <= 0) return;
        foreach (var card in PileType.Hand.GetPile(owner).Cards.OfType<FactoryComponentCard>()
                     .Where(card => card.Keywords.Contains(FactoryKeywords.PrecisionComponent)))
        {
            if (!card.TryGetCapability<FactoryCardStateCapability>(out var capability) ||
                capability.ProcessCount < state.RecursiveProcessingThreshold) continue;
            capability.ResetProcessCount();
            await FactoryCardActions.AddGeneratedCardToHand<UniversalMaterial>(owner);
            table.Energy = Math.Min(table.MaxEnergy, table.Energy + 1);
        }
    }
}
