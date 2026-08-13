using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using KillerFactory.Cards;

namespace KillerFactory.Mechanics;

public enum AssemblerModuleKind
{
    Damage,
    Block,
    Draw,
    EdgeBlock,
    BoostNext,
    RepeatPrevious,
    ConvertNextDamageToAll,
    StructureDurability,
    SelfRepair,
    ScrapBlock,
    FlankBranch,
    BiologicalBlock,
    EndBlockDraw,
    RollerFilter,
}

public sealed class AssemblerModuleSpec
{
    public AssemblerModuleKind Kind { get; set; }
    public int Amount { get; set; }
    public int BonusAmount { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public CardType? SourceType { get; set; }
}

public interface IAssemblerPart
{
    AssemblerModuleSpec CreateModule();
}

public sealed class AssemblerProductState
{
    public List<AssemblerModuleSpec> Modules { get; set; } = [];
    public int CurrentDurability { get; set; }
    public int MaxDurability { get; set; }
    public int ScrapProduced { get; set; }
    public bool SkipFirstDurabilityLoss { get; set; }
    public bool RepeatOutputsOnFirstUse { get; set; }
    public bool HasBeenUsed { get; set; }
    public int PatchedAfterUse { get; set; }
    public int BaseDurability { get; set; }
    public int DurabilityAdjustment { get; set; }
}

[RegisterModelCapability]
public sealed class AssemblerProductCapability
    : StatefulModelCapability<CardModel, AssemblerProductState>,
      ICardPropertyContributor,
      ICardDescriptionContributor
{
    public IReadOnlyList<AssemblerModuleSpec> Modules => State.Modules;
    public int CurrentDurability => State.CurrentDurability;
    public int MaxDurability => State.MaxDurability;
    public bool IsBroken => State.CurrentDurability <= 0;
    public int PatchedAfterUse => State.PatchedAfterUse;
    public IReadOnlyList<AssemblerPartCard> OriginalParts => _originalParts;
    public IReadOnlyList<AssemblerPartCard?> ModuleParts => _moduleParts;
    private readonly List<AssemblerPartCard> _originalParts = [];
    private readonly List<AssemblerPartCard?> _moduleParts = [];

    public void Configure(IEnumerable<AssemblerModuleSpec> modules, int maxDurability, int scrapProduced,
        IEnumerable<AssemblerPartCard>? originalParts = null, bool repeatOutputsOnFirstUse = false)
    {
        var copied = modules.Select(CopyModule).ToList();
        SetState(new AssemblerProductState
        {
            Modules = copied,
            CurrentDurability = Math.Max(1, maxDurability),
            MaxDurability = Math.Max(1, maxDurability),
            BaseDurability = Math.Max(1, maxDurability),
            ScrapProduced = scrapProduced,
            RepeatOutputsOnFirstUse = repeatOutputsOnFirstUse,
        });
        _originalParts.Clear();
        _moduleParts.Clear();
        if (originalParts is not null)
        {
            var parts = originalParts.ToList();
            _originalParts.AddRange(parts);
            _moduleParts.AddRange(parts);
        }
        while (_moduleParts.Count < copied.Count) _moduleParts.Add(null);
        RecalculateStructuralEffects();
    }

    public void SpendDurability()
    {
        var state = CopyState();
        state.CurrentDurability = Math.Max(0, state.CurrentDurability - 1);
        SetState(state);
    }

    public bool TrySkipDurabilityLoss()
    {
        if (!State.SkipFirstDurabilityLoss || State.HasBeenUsed) return false;
        var state = CopyState();
        state.HasBeenUsed = true;
        SetState(state);
        return true;
    }

    public bool ShouldRepeatOutputs() => State.RepeatOutputsOnFirstUse && !State.HasBeenUsed;

    public void MarkUsed()
    {
        var state = CopyState();
        state.HasBeenUsed = true;
        SetState(state);
    }

    public void SetPatchedAfterUse(int mode)
    {
        var state = CopyState();
        state.PatchedAfterUse = mode;
        SetState(state);
    }

    public void AddModule(AssemblerModuleSpec module, int durabilityDelta = 0, AssemblerPartCard? sourcePart = null)
    {
        var state = CopyState();
        state.Modules.Add(CopyModule(module));
        state.DurabilityAdjustment += durabilityDelta;
        SetState(state);
        _moduleParts.Add(sourcePart);
        if (sourcePart is not null && !_originalParts.Contains(sourcePart)) _originalParts.Add(sourcePart);
        RecalculateStructuralEffects();
    }

    public void InsertModule(AssemblerModuleSpec module, int index, int durabilityDelta = 0, AssemblerPartCard? sourcePart = null)
    {
        var state = CopyState();
        index = Math.Clamp(index, 0, state.Modules.Count);
        state.Modules.Insert(index, CopyModule(module));
        state.DurabilityAdjustment += durabilityDelta;
        SetState(state);
        _moduleParts.Insert(Math.Min(index, _moduleParts.Count), sourcePart);
        if (sourcePart is not null && !_originalParts.Contains(sourcePart)) _originalParts.Add(sourcePart);
        RecalculateStructuralEffects();
    }

    public void SwapModules(int first, int second)
    {
        if (first < 0 || second < 0 || first >= State.Modules.Count || second >= State.Modules.Count) return;
        var state = CopyState();
        (state.Modules[first], state.Modules[second]) = (state.Modules[second], state.Modules[first]);
        SetState(state);
        if (first < _moduleParts.Count && second < _moduleParts.Count)
            (_moduleParts[first], _moduleParts[second]) = (_moduleParts[second], _moduleParts[first]);
        RecalculateStructuralEffects();
    }

    public AssemblerModuleSpec? RemoveModuleAt(int index, int durabilityDelta = 0)
    {
        if (index < 0 || index >= State.Modules.Count) return null;
        var state = CopyState();
        var removed = state.Modules[index];
        state.Modules.RemoveAt(index);
        state.DurabilityAdjustment += durabilityDelta;
        if (index < _moduleParts.Count)
        {
            var source = _moduleParts[index];
            _moduleParts.RemoveAt(index);
            if (source is not null) _originalParts.Remove(source);
        }
        SetState(state);
        RecalculateStructuralEffects();
        return removed;
    }

    public AssemblerPartCard? RemoveOriginalPartAt(int index)
    {
        if (index < 0 || index >= _originalParts.Count) return null;
        var part = _originalParts[index];
        _originalParts.RemoveAt(index);
        return part;
    }

    public AssemblerPartCard? GetModulePart(int index) =>
        index >= 0 && index < _moduleParts.Count ? _moduleParts[index] : null;

    private void RecalculateStructuralEffects()
    {
        var state = CopyState();
        var structuralBonus = state.Modules.Count > 0 && state.Modules[0].Kind == AssemblerModuleKind.StructureDurability
            ? state.Modules[0].Amount : 0;
        var newMax = Math.Max(1, state.BaseDurability + state.DurabilityAdjustment + structuralBonus);
        if (newMax > state.MaxDurability)
            state.CurrentDurability = Math.Min(newMax, state.CurrentDurability + newMax - state.MaxDurability);
        else
            state.CurrentDurability = Math.Min(state.CurrentDurability, newMax);
        state.MaxDurability = newMax;
        state.SkipFirstDurabilityLoss = state.Modules.Count > 0 &&
            state.Modules[^1].Kind == AssemblerModuleKind.StructureDurability;
        SetState(state);
    }

    public void Repair(int amount)
    {
        var state = CopyState();
        state.CurrentDurability = Math.Min(state.MaxDurability, state.CurrentDurability + Math.Max(0, amount));
        SetState(state);
    }

    public TargetType? GetTargetType(CardModel card) =>
        State.Modules.Any(static module => module.Kind == AssemblerModuleKind.Damage)
            ? TargetType.AnyEnemy
            : TargetType.Self;

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        var summary = string.Join(" → ", State.Modules.Select(DescribeModule));
        var text = new LocString("cards", IsBroken
            ? "KILLER_FACTORY_PRODUCT_STATE_BROKEN"
            : "KILLER_FACTORY_PRODUCT_STATE_READY");
        text.Add("Current", State.CurrentDurability);
        text.Add("Max", State.MaxDurability);
        text.Add("Modules", summary);
        text.Add("Scrap", State.ScrapProduced);
        yield return new CardDescriptionFragment(text);
    }

    private AssemblerProductState CopyState() => new()
    {
        Modules = State.Modules.Select(CopyModule).ToList(),
        CurrentDurability = State.CurrentDurability,
        MaxDurability = State.MaxDurability,
        ScrapProduced = State.ScrapProduced,
        SkipFirstDurabilityLoss = State.SkipFirstDurabilityLoss,
        RepeatOutputsOnFirstUse = State.RepeatOutputsOnFirstUse,
        HasBeenUsed = State.HasBeenUsed,
        PatchedAfterUse = State.PatchedAfterUse,
        BaseDurability = State.BaseDurability,
        DurabilityAdjustment = State.DurabilityAdjustment,
    };

    private static AssemblerModuleSpec CopyModule(AssemblerModuleSpec module) => new()
    {
        Kind = module.Kind,
        Amount = module.Amount,
        BonusAmount = module.BonusAmount,
        SourceTitle = module.SourceTitle,
        SourceType = module.SourceType,
    };

    private static string DescribeModule(AssemblerModuleSpec module) => module.Kind switch
    {
        AssemblerModuleKind.Damage => $"{module.SourceTitle}：伤害{module.Amount}",
        AssemblerModuleKind.Block => $"{module.SourceTitle}：格挡{module.Amount}",
        AssemblerModuleKind.Draw => $"{module.SourceTitle}：抽{module.Amount}",
        AssemblerModuleKind.EdgeBlock => $"{module.SourceTitle}：格挡{module.Amount}，首尾+{module.BonusAmount}",
        AssemblerModuleKind.BoostNext => $"{module.SourceTitle}：增压{module.Amount}%",
        AssemblerModuleKind.RepeatPrevious => $"{module.SourceTitle}：重复{module.Amount}%",
        _ => module.SourceTitle,
    };
}

public static class AssemblerService
{
    public sealed record AdjacentParts(AssemblerPartCard Left, AssemblerPartCard Right, int LeftIndex);
    public sealed record ConsecutiveParts(IReadOnlyList<AssemblerPartCard> Cards, int LeftIndex);

    public static bool IsScrap(CardModel card) => card is MechanicalScrap or BiologicalScrap;

    public static bool HasConsecutiveParts(Player owner, int count)
    {
        if (count < 1) return false;
        var cards = PileType.Hand.GetPile(owner).Cards;
        for (var i = 0; i + count <= cards.Count; i++)
            if (cards.Skip(i).Take(count).All(c => c is AssemblerPartCard)) return true;
        return false;
    }

    public static bool HasAdjacentAssemblyPair(Player owner)
    {
        if (HasConsecutiveParts(owner, 2)) return true;
        var state = AssemblerCombatState.For(owner.Creature.CombatState!);
        if (state.BypassedScrap is null) return false;
        var cards = PileType.Hand.GetPile(owner).Cards;
        for (var i = 0; i + 2 < cards.Count; i++)
            if (cards[i] is AssemblerPartCard && ReferenceEquals(cards[i + 1], state.BypassedScrap) &&
                cards[i + 2] is AssemblerPartCard) return true;
        return false;
    }

    public static AssemblerModuleSpec CreateModuleFromPart(AssemblerPartCard part)
    {
        var module = part.CreateModule();
        module.SourceType = part.Type;
        return module;
    }

    public static async Task PutHandCardOnDrawBottomAsync(PlayerChoiceContext context, Player owner)
    {
        var cards = PileType.Hand.GetPile(owner).Cards.ToList();
        if (cards.Count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_MOVE_CARD"), 1)
        { Cancelable = false, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, cards, owner, prefs)).FirstOrDefault();
        if (selected is null) return;
        await CardPileCmd.Add(selected, PileType.Draw);
        var draw = PileType.Draw.GetPile(owner);
        draw.RemoveInternal(selected, true);
        draw.AddInternal(selected, draw.Cards.Count, false);
    }

    public static HandPositionSnapshot GetPositionSnapshot(CardModel card)
    {
        var state = AssemblerCombatState.For(card.Owner.Creature.CombatState!);
        if (state.PositionSnapshots.TryGetValue(card, out var snapshot)) return snapshot;
        var cards = PileType.Hand.GetPile(card.Owner).Cards.ToList();
        var index = cards.IndexOf(card);
        return new HandPositionSnapshot(index, cards.Count,
            index > 0 ? cards[index - 1] : null,
            index >= 0 && index + 1 < cards.Count ? cards[index + 1] : null);
    }

    public static int CountAdjacentAssemblyScrap(CardModel card)
    {
        var snapshot = GetPositionSnapshot(card);
        return (snapshot.Left is not null && IsScrap(snapshot.Left) ? 1 : 0) +
               (snapshot.Right is not null && IsScrap(snapshot.Right) ? 1 : 0);
    }

    public static int ApplyPendingAttackBonus(CardModel card, int amount)
    {
        var state = AssemblerCombatState.For(card.Owner.Creature.CombatState!);
        var result = (int)Math.Floor(amount * state.PendingAssemblerAttackMultiplier);
        state.PendingAssemblerAttackMultiplier = 1m;
        state.LastAssemblerOutputKind = AssemblerModuleKind.Damage;
        state.LastAssemblerOutputAmount = result;
        return result;
    }

    public static PartPlayModifier TakePartModifier(CardModel card)
    {
        var state = AssemblerCombatState.For(card.Owner.Creature.CombatState!);
        if (!state.PartModifiers.Remove(card, out var modifier)) modifier = new PartPlayModifier();
        modifier.DamageBonus += state.PendingAssemblerAttackMultiplier - 1m;
        state.PendingAssemblerAttackMultiplier = 1m;
        return modifier;
    }

    public static async Task ExecutePartAttackAsync(AssemblerPartCard card, int baseAmount,
        PlayerChoiceContext context, CardPlay play)
    {
        var modifier = TakePartModifier(card);
        var amount = (int)Math.Floor(baseAmount * (1m + modifier.DamageBonus) * modifier.AreaMultiplier);
        if (modifier.MakeDamageAll)
            await DamageCmd.Attack(amount).FromCard(card, play).TargetingAllOpponents(card.Owner.Creature.CombatState!).Execute(context);
        else if (play.Target is not null && !play.Target.IsDead)
            await DamageCmd.Attack(amount).FromCard(card, play).Targeting(play.Target).Execute(context);
        if (modifier.RepeatPercent > 0)
        {
            var repeat = (int)Math.Floor(baseAmount * modifier.RepeatPercent / 100m);
            if (modifier.MakeDamageAll)
                await DamageCmd.Attack(repeat).FromCard(card, play).TargetingAllOpponents(card.Owner.Creature.CombatState!).Execute(context);
            else if (play.Target is not null && !play.Target.IsDead)
                await DamageCmd.Attack(repeat).FromCard(card, play).Targeting(play.Target).Execute(context);
        }
        var state = AssemblerCombatState.For(card.Owner.Creature.CombatState!);
        state.LastAssemblerOutputKind = AssemblerModuleKind.Damage;
        state.LastAssemblerOutputAmount = amount;
    }

    public static async Task ExecutePartBlockAsync(AssemblerPartCard card, int baseAmount,
        PlayerChoiceContext context, CardPlay play)
    {
        var modifier = TakePartModifier(card);
        await CreatureCmd.GainBlock(card.Owner.Creature, new BlockVar(baseAmount, ValueProp.Move), play);
        if (modifier.RepeatPercent > 0)
            await CreatureCmd.GainBlock(card.Owner.Creature,
                new BlockVar((int)Math.Floor(baseAmount * modifier.RepeatPercent / 100m), ValueProp.Move), play);
        RecordBlock(card, baseAmount);
    }

    public static bool TryMarkRightPart(CardModel source, Func<AssemblerPartCard, bool> predicate,
        Action<PartPlayModifier> apply)
    {
        var snapshot = GetPositionSnapshot(source);
        if (snapshot.Right is not AssemblerPartCard part || !predicate(part))
            return false;
        var state = AssemblerCombatState.For(source.Owner.Creature.CombatState!);
        if (!state.PartModifiers.TryGetValue(part, out var modifier))
            state.PartModifiers[part] = modifier = new PartPlayModifier();
        apply(modifier);
        return true;
    }

    public static async Task<List<CardModel>> SelectUpToAsync(PlayerChoiceContext context, Player owner,
        IReadOnlyList<CardModel> candidates, int min, int max, string selectionKey)
    {
        var selected = new List<CardModel>();
        var remaining = candidates.Distinct().ToList();
        while (selected.Count < max && remaining.Count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("card_selection", selectionKey), 1)
            {
                Cancelable = selected.Count >= min,
                RequireManualConfirmation = true,
            };
            var next = (await CardSelectCmd.FromSimpleGrid(context, remaining, owner, prefs)).FirstOrDefault();
            if (next is null) break;
            selected.Add(next);
            remaining.Remove(next);
        }
        return selected.Count >= min ? selected : [];
    }

    public static async Task<CardModel?> ProcessScrapAsync(CardModel scrap, PileType destination = PileType.Hand,
        int? destinationIndex = null)
    {
        if (!IsScrap(scrap)) return null;
        var owner = scrap.Owner;
        var hand = PileType.Hand.GetPile(owner);
        var originalIndex = hand.Cards.ToList().IndexOf(scrap);
        await CardPileCmd.Add(scrap, PileType.Exhaust);
        CardModel result = scrap is BiologicalScrap
            ? owner.Creature.CombatState!.CreateCard<SecondaryEnergyTank>(owner)
            : owner.Creature.CombatState!.CreateCard<SecondaryConveyor>(owner);
        await CardPileCmd.AddGeneratedCardToCombat(result, destination, owner);
        if (destination == PileType.Hand)
            MoveWithinHand(result, destinationIndex ?? Math.Max(0, originalIndex));
        var state = AssemblerCombatState.For(owner.Creature.CombatState!);
        if (scrap is BiologicalScrap) state.ProcessedBiologicalThisTurn = true;
        else state.ProcessedMechanicalThisTurn = true;
        if (state.ClassificationProtocol > 0)
        {
            if (scrap is BiologicalScrap && !state.ClassificationBiologicalUsedThisTurn)
            {
                state.ClassificationBiologicalUsedThisTurn = true;
                state.ClassificationEnhancedCards.Add(result);
            }
            else if (scrap is MechanicalScrap && !state.ClassificationMechanicalUsedThisTurn)
            {
                state.ClassificationMechanicalUsedThisTurn = true;
                state.ClassificationEnhancedCards.Add(result);
            }
        }
        if (state.HeterogeneousGrowth > 0 && !state.HeterogeneousGrowthUsedThisTurn &&
            state.ProcessedMechanicalThisTurn && state.ProcessedBiologicalThisTurn)
        {
            state.HeterogeneousGrowthUsedThisTurn = true;
            state.Productivity += state.HeterogeneousGrowth;
            if (state.HeterogeneousGrowthDraw > 0)
                await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), state.HeterogeneousGrowthDraw, owner);
        }
        return result;
    }

    public static void RecordBlock(CardModel card, int amount)
    {
        var state = AssemblerCombatState.For(card.Owner.Creature.CombatState!);
        state.LastAssemblerOutputKind = AssemblerModuleKind.Block;
        state.LastAssemblerOutputAmount = amount;
    }

    public static bool IsAtHandEdge(CardModel card)
    {
        var snapshot = GetPositionSnapshot(card);
        return snapshot.IsLeftEdge || snapshot.IsRightEdge;
    }

    public static async Task<AdjacentParts?> SelectAdjacentPartsAsync(
        PlayerChoiceContext context,
        Player owner)
    {
        var hand = PileType.Hand.GetPile(owner);
        var ordered = hand.Cards.ToList();
        var assemblyState = AssemblerCombatState.For(owner.Creature.CombatState!);
        var firstCandidates = ordered
            .OfType<AssemblerPartCard>()
            .Where(card =>
            {
                var index = ordered.IndexOf(card);
                return index > 0 && ordered[index - 1] is AssemblerPartCard ||
                       index + 1 < ordered.Count && ordered[index + 1] is AssemblerPartCard ||
                       assemblyState.BypassedScrap is not null &&
                       ((index > 1 && ReferenceEquals(ordered[index - 1], assemblyState.BypassedScrap) && ordered[index - 2] is AssemblerPartCard) ||
                        (index + 2 < ordered.Count && ReferenceEquals(ordered[index + 1], assemblyState.BypassedScrap) && ordered[index + 2] is AssemblerPartCard));
            })
            .Cast<CardModel>()
            .ToList();
        if (firstCandidates.Count == 0)
            return null;

        var prefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_ADJACENT_PARTS"), 2)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, firstCandidates, owner, prefs))
            .OfType<AssemblerPartCard>()
            .Distinct()
            .ToList();
        if (selected.Count != 2)
            return null;

        ordered = hand.Cards.ToList();
        var indexed = selected
            .Select(card => (Card: card, Index: ordered.IndexOf(card)))
            .OrderBy(entry => entry.Index)
            .ToList();
        if (indexed[0].Index < 0) return null;
        if (indexed[1].Index != indexed[0].Index + 1)
        {
            var state = AssemblerCombatState.For(owner.Creature.CombatState!);
            if (indexed[1].Index != indexed[0].Index + 2 || state.BypassedScrap is null ||
                !ReferenceEquals(ordered[indexed[0].Index + 1], state.BypassedScrap)) return null;
            var bypassed = state.BypassedScrap;
            MoveWithinHand(bypassed, state.BypassMoveRight ? int.MaxValue : 0);
            var drawForFeeder = state.BypassAlwaysDraw ||
                PileType.Hand.GetPile(owner).Cards.OfType<Feeder>().Any(f => CountAdjacentAssemblyScrap(f) > 0);
            state.BypassedScrap = null;
            state.BypassAlwaysDraw = false;
            state.BypassMoveRight = false;
            if (drawForFeeder) await CardPileCmd.Draw(context, 1, owner);
            ordered = hand.Cards.ToList();
            indexed = selected.Select(card => (Card: card, Index: ordered.IndexOf(card))).OrderBy(entry => entry.Index).ToList();
            if (indexed[1].Index != indexed[0].Index + 1) return null;
        }

        return new AdjacentParts(indexed[0].Card, indexed[1].Card, indexed[0].Index);
    }

    public static async Task<ConsecutiveParts?> SelectConsecutivePartsAsync(
        PlayerChoiceContext context, Player owner, int count)
    {
        var hand = PileType.Hand.GetPile(owner);
        var ordered = hand.Cards.ToList();
        var candidates = ordered.OfType<AssemblerPartCard>().Cast<CardModel>().ToList();
        if (count < 1 || candidates.Count < count) return null;
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "KILLER_FACTORY_SELECT_ADJACENT_PARTS"), count)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, owner, prefs))
            .OfType<AssemblerPartCard>().Distinct().ToList();
        if (selected.Count != count) return null;
        ordered = hand.Cards.ToList();
        var indexed = selected.Select(c => (Card: c, Index: ordered.IndexOf(c))).OrderBy(x => x.Index).ToList();
        if (indexed[0].Index < 0 || indexed.Select((x, i) => x.Index == indexed[0].Index + i).Any(ok => !ok)) return null;
        return new ConsecutiveParts(indexed.Select(x => x.Card).ToList(), indexed[0].Index);
    }

    public static async Task<ConsecutiveParts?> SelectConsecutivePartsAsync(
        PlayerChoiceContext context, Player owner, int min, int max)
    {
        var hand = PileType.Hand.GetPile(owner);
        var ordered = hand.Cards.ToList();
        var candidates = ordered.OfType<AssemblerPartCard>().Cast<CardModel>().ToList();
        var selected = (await SelectUpToAsync(context, owner, candidates, min, max,
            "KILLER_FACTORY_SELECT_ADJACENT_PARTS")).OfType<AssemblerPartCard>().ToList();
        if (selected.Count < min) return null;
        ordered = hand.Cards.ToList();
        var indexed = selected.Select(c => (Card: c, Index: ordered.IndexOf(c))).OrderBy(x => x.Index).ToList();
        if (indexed[0].Index < 0 || indexed.Select((x, i) => x.Index == indexed[0].Index + i).Any(ok => !ok)) return null;
        return new ConsecutiveParts(indexed.Select(x => x.Card).ToList(), indexed[0].Index);
    }

    public static async Task<AssembledProduct?> AssembleAsync(
        Player owner,
        AdjacentParts parts,
        int energyCost = 1,
        int maxDurability = 2,
        int scrapCount = 1)
    {
        return await AssembleAsync(owner,
            new ConsecutiveParts([parts.Left, parts.Right], parts.LeftIndex),
            energyCost, maxDurability, scrapCount);
    }

    public static async Task<AssembledProduct?> AssembleAsync(Player owner, ConsecutiveParts parts,
        int energyCost, int maxDurability, int scrapCount, bool repeatOutputsOnFirstUse = false)
    {
        var hand = PileType.Hand.GetPile(owner);
        var ordered = hand.Cards.ToList();
        if (parts.Cards.Count == 0 || parts.Cards.Select((c, i) => ordered.IndexOf(c) == parts.LeftIndex + i).Any(ok => !ok))
            return null;
        var combatState = owner.Creature.CombatState!;
        var factoryState = AssemblerCombatState.For(combatState);
        foreach (var part in parts.Cards) factoryState.PartModifiers.Remove(part);
        if (factoryState.FullAutoProduction > 0 && !factoryState.AssemblyUsedThisTurn)
        {
            scrapCount = Math.Max(0, scrapCount - 1);
            factoryState.AssemblyUsedThisTurn = true;
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, owner);
        }
        var product = combatState.CreateCard<AssembledProduct>(owner);
        product.EnergyCost.SetCustomBaseCost(energyCost);
        product.GetOrCreateCapability<AssemblerProductCapability>().Configure(
            parts.Cards.Select(CreateModuleFromPart), maxDurability, scrapCount, parts.Cards, repeatOutputsOnFirstUse);
        var productCapability = product.GetOrCreateCapability<AssemblerProductCapability>();
        if (factoryState.PendingBlueprintModule is not null)
        {
            if (factoryState.PendingBlueprintAtFront)
                productCapability.InsertModule(factoryState.PendingBlueprintModule, 0, -1);
            else
                productCapability.AddModule(factoryState.PendingBlueprintModule, -1);
            factoryState.PendingBlueprintModule = null;
            factoryState.PendingBlueprintAtFront = false;
        }
        if (factoryState.BlueprintLibrary > 0 && !factoryState.BlueprintLibraryUsedThisTurn && factoryState.LastExhaustedPart is not null)
        {
            factoryState.BlueprintLibraryUsedThisTurn = true;
            productCapability.AddModule(CreateModuleFromPart(factoryState.LastExhaustedPart), -1);
        }
        await CardPileCmd.RemoveFromCombat(parts.Cards.Cast<CardModel>().ToList(), false);
        await CardPileCmd.AddGeneratedCardToCombat(product, PileType.Hand, owner);
        MoveWithinHand(product, parts.LeftIndex);
        factoryState.AssembledThisTurn = true;
        factoryState.LatestProduct = product;
        if (factoryState.PendingBufferDraw)
        {
            factoryState.PendingBufferDraw = false;
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, owner);
        }
        if (factoryState.BufferedCard is not null)
        {
            var buffered = factoryState.BufferedCard;
            factoryState.BufferedCard = null;
            await CardPileCmd.AddGeneratedCardToCombat(buffered, PileType.Hand, owner);
            MoveWithinHand(buffered, int.MaxValue);
            if (factoryState.BufferedCardCostReduction) AssemblerAbilityRuntime.ReduceCostUntilPlayed(buffered, 1);
            factoryState.BufferedCardCostReduction = false;
        }
        foreach (var tagged in parts.Cards.Where(factoryState.BatchTags.ContainsKey).ToList())
        {
            var position = productCapability.ModuleParts.ToList().IndexOf(tagged);
            var upgraded = factoryState.BatchTags[tagged];
            if (position == 0) await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, owner);
            else if (position == productCapability.Modules.Count - 1) AssemblerAbilityRuntime.ReduceCostUntilPlayed(product, 1);
            else if (upgraded) await PlayerCmd.GainEnergy(1, owner);
            factoryState.BatchTags.Remove(tagged);
        }
        var firstType = productCapability.Modules.FirstOrDefault()?.SourceType;
        var lastType = productCapability.Modules.LastOrDefault()?.SourceType;
        if (factoryState.InterlockUsedThisTurn < factoryState.InterlockStation &&
            firstType is CardType.Attack or CardType.Skill && lastType is CardType.Attack or CardType.Skill &&
            firstType != lastType)
        {
            factoryState.InterlockUsedThisTurn++;
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, owner);
            if (productCapability.Modules.Count > 2)
                await CreatureCmd.GainBlock(owner.Creature, new BlockVar(4, ValueProp.Move), null);
        }
        if (factoryState.ZeroWasteFactory > 0 && !factoryState.ZeroWasteUsedThisTurn)
        {
            factoryState.ZeroWasteUsedThisTurn = true;
            var current = hand.Cards.ToList();
            var productIndex = current.IndexOf(product);
            var leftScrap = productIndex > 0 ? current[productIndex - 1] : null;
            var rightScrap = productIndex + 1 < current.Count ? current[productIndex + 1] : null;
            if (leftScrap is not null && (factoryState.ZeroWasteProcessesAnyScrap || leftScrap is MechanicalScrap) && IsScrap(leftScrap))
                await ProcessScrapAsync(leftScrap);
            if (rightScrap is not null && (factoryState.ZeroWasteProcessesAnyScrap || rightScrap is BiologicalScrap) && IsScrap(rightScrap))
                await ProcessScrapAsync(rightScrap);
        }
        for (var i = 0; i < scrapCount; i++)
        {
            var scrap = combatState.CreateCard<MechanicalScrap>(owner);
            await CardPileCmd.AddGeneratedCardToCombat(scrap, PileType.Discard, owner);
        }
        return product;
    }

    public static async Task<AssembledProduct?> SelectProductToRepairAsync(
        PlayerChoiceContext context,
        Player owner,
        int amount)
    {
        var products = PileType.Hand.GetPile(owner).Cards.OfType<AssembledProduct>().Cast<CardModel>().ToList();
        if (products.Count == 0)
            return null;

        var prefs = new CardSelectorPrefs(
            new LocString("card_selection", "KILLER_FACTORY_SELECT_PRODUCT_REPAIR"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var product = (await CardSelectCmd.FromSimpleGrid(context, products, owner, prefs))
            .OfType<AssembledProduct>()
            .FirstOrDefault();
        if (product is not null) await RepairProductAsync(product, amount);
        return product;
    }

    public static async Task RepairProductAsync(AssembledProduct product, int amount)
    {
        product.GetOrCreateCapability<AssemblerProductCapability>().Repair(amount);
        var state = AssemblerCombatState.For(product.Owner.Creature.CombatState!);
        if (state.PreventiveRepairDraw && !state.RepairDrawUsedThisTurn)
        {
            state.RepairDrawUsedThisTurn = true;
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, product.Owner);
        }
    }

    public static void MoveWithinHand(CardModel card, int destinationIndex)
    {
        var hand = PileType.Hand.GetPile(card.Owner);
        if (!hand.Cards.Contains(card))
            return;
        destinationIndex = Math.Clamp(destinationIndex, 0, Math.Max(0, hand.Cards.Count - 1));
        hand.RemoveInternal(card, true);
        hand.AddInternal(card, destinationIndex, false);
    }
}
