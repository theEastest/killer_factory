using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using KillerFactory.Characters;
using KillerFactory.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace KillerFactory.Cards;

public abstract class AssemblerPileCard : AssemblerCardTemplate
{
    protected AssemblerPileCard(int cost, CardType type, CardRarity rarity)
        : base(cost, type, rarity, TargetType.Self, true, "process") { }
    protected async Task<CardModel?> Select(PlayerChoiceContext context, IEnumerable<CardModel> cards, string key)
    { var list=cards.ToList();if(list.Count==0)return null;var prefs=new CardSelectorPrefs(new LocString("card_selection",key),1){Cancelable=true,RequireManualConfirmation=true};return(await CardSelectCmd.FromSimpleGrid(context,list,Owner,prefs)).FirstOrDefault(); }
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class TopBinInspection : AssemblerPileCard
{
    protected override bool IsPlayable => base.IsPlayable && PileType.Draw.GetPile(Owner).Cards.Count > 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public TopBinInspection():base(0,CardType.Skill,CardRarity.Common){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var draw=PileType.Draw.GetPile(Owner).Cards.Take(IsUpgraded?5:3).OfType<AssemblerPartCard>().Cast<CardModel>().ToList();var card=await Select(context,draw,"KILLER_FACTORY_SELECT_PART");if(card is null)return;await CardPileCmd.Add(card,PileType.Hand);var edge=0;if(IsUpgraded){var hand=PileType.Hand.GetPile(Owner).Cards.ToList();var anchor=await Select(context,new[]{hand.First(),hand.Last()},"KILLER_FACTORY_SELECT_MOVE_DESTINATION");edge=ReferenceEquals(anchor,hand.Last())?int.MaxValue:0;}AssemblerService.MoveWithinHand(card,edge);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BottomBinHoist : AssemblerPileCard
{
    protected override bool IsPlayable => base.IsPlayable &&
        (PileType.Draw.GetPile(Owner).Cards.Count > 0 || PileType.Discard.GetPile(Owner).Cards.Count > 0);
    public BottomBinHoist():base(1,CardType.Skill,CardRarity.Common){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {await CardPileCmd.ShuffleIfNecessary(context,Owner);var card=PileType.Draw.GetPile(Owner).Cards.LastOrDefault();if(card is null)return;await CardPileCmd.Add(card,PileType.Hand);AssemblerService.MoveWithinHand(card,int.MaxValue);if(card is AssemblerPartCard)AssemblerAbilityRuntime.ReduceCostUntilPlayed(card,1);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class TopDeckPreassembly : AssemblerPileCard
{
    protected override bool IsPlayable
    {
        get
        {
            var top=PileType.Draw.GetPile(Owner).Cards.Take(IsUpgraded?3:2).ToList();
            if(!IsUpgraded)return top.Count>=2&&top[0] is AssemblerPartCard&&top[1] is AssemblerPartCard;
            return top.Zip(top.Skip(1)).Any(p=>p.First is AssemblerPartCard&&p.Second is AssemblerPartCard);
        }
    }
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public TopDeckPreassembly():base(1,CardType.Skill,CardRarity.Uncommon){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var top=PileType.Draw.GetPile(Owner).Cards.Take(IsUpgraded?3:2).ToList();List<AssemblerPartCard> parts;if(!IsUpgraded){parts=top.Take(2).OfType<AssemblerPartCard>().ToList();}else{var starts=Enumerable.Range(0,Math.Max(0,top.Count-1)).Where(i=>top[i] is AssemblerPartCard&&top[i+1] is AssemblerPartCard).ToList();if(starts.Count==0)return;var chosen=await Select(context,starts.Select(i=>top[i]),"KILLER_FACTORY_SELECT_PART");var start=starts.First(i=>ReferenceEquals(top[i],chosen));parts=[(AssemblerPartCard)top[start],(AssemblerPartCard)top[start+1]];}if(parts.Count!=2)return;foreach(var p in parts)await CardPileCmd.Add(p,PileType.Hand);AssemblerService.MoveWithinHand(parts[0],0);AssemblerService.MoveWithinHand(parts[1],1);var data=new AssemblerService.ConsecutiveParts(parts,0);var product=await AssemblerService.AssembleAsync(Owner,data,1,2,1);if(product is not null)AssemblerService.MoveWithinHand(product,0);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class FirstInFirstOut : AssemblerPileCard
{
    public FirstInFirstOut():base(2,CardType.Power,CardRarity.Rare){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play){AssemblerCombatState.For(Owner.Creature.CombatState!).FirstInFirstOut++;return Task.CompletedTask;}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReworkPickup : AssemblerPileCard
{
    protected override bool IsPlayable=>base.IsPlayable&&PileType.Discard.GetPile(Owner).Cards.OfType<AssemblerPartCard>().Any();
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public ReworkPickup():base(1,CardType.Skill,CardRarity.Common){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var card=PileType.Discard.GetPile(Owner).Cards.Reverse().OfType<AssemblerPartCard>().FirstOrDefault();if(card is null)return;await CardPileCmd.Add(card,PileType.Hand);AssemblerService.MoveWithinHand(card,card.Type==CardType.Attack?0:int.MaxValue);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReworkInspection : AssemblerPileCard
{
    protected override bool IsPlayable=>base.IsPlayable&&PileType.Discard.GetPile(Owner).Cards.Reverse()
        .Take(IsUpgraded?5:3).OfType<AssemblerPartCard>().Any();
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public ReworkInspection():base(1,CardType.Skill,CardRarity.Uncommon){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var window=PileType.Discard.GetPile(Owner).Cards.Reverse().Take(IsUpgraded?5:3).ToList();var card=await Select(context,window.OfType<AssemblerPartCard>(),"KILLER_FACTORY_SELECT_PART");if(card is null)return;var top=window.FirstOrDefault();await CardPileCmd.Add(card,PileType.Hand);if(ReferenceEquals(card,top))AssemblerAbilityRuntime.MakeFreeUntilPlayed(card);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReverseBinExchange : AssemblerPileCard
{
    protected override bool IsPlayable=>base.IsPlayable&&PileType.Draw.GetPile(Owner).Cards.Count>0&&PileType.Discard.GetPile(Owner).Cards.Count>0;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>IsUpgraded?[]:[CardKeyword.Exhaust];
    public ReverseBinExchange():base(0,CardType.Skill,CardRarity.Uncommon){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var draw=PileType.Draw.GetPile(Owner).Cards.FirstOrDefault();var discard=PileType.Discard.GetPile(Owner).Cards.LastOrDefault();if(draw is null||discard is null)return;await CardPileCmd.Add(draw,PileType.Discard);await CardPileCmd.Add(discard,PileType.Draw);var before=PileType.Hand.GetPile(Owner).Cards.ToHashSet();await CardPileCmd.Draw(context,1,Owner);var drawn=PileType.Hand.GetPile(Owner).Cards.FirstOrDefault(c=>!before.Contains(c));if(drawn is AssemblerPartCard){var hand=PileType.Hand.GetPile(Owner).Cards.ToList();var anchor=await Select(context,new[]{hand.First(),hand.Last()},"KILLER_FACTORY_SELECT_MOVE_DESTINATION");AssemblerService.MoveWithinHand(drawn,ReferenceEquals(anchor,hand.Last())?int.MaxValue:0);}}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ReworkBus : AssemblerPileCard
{
    public ReworkBus():base(2,CardType.Power,CardRarity.Rare){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play){AssemblerCombatState.For(Owner.Creature.CombatState!).ReworkBus++;return Task.CompletedTask;}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class ArchiveSample : AssemblerPileCard
{
    protected override bool IsPlayable=>base.IsPlayable&&PileType.Hand.GetPile(Owner).Cards is var cards&&cards.Count>0&&
        (IsUpgraded?cards.OfType<AssemblerPartCard>().Any():cards[0] is AssemblerPartCard||cards[^1] is AssemblerPartCard);
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public ArchiveSample():base(0,CardType.Skill,CardRarity.Common){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var hand=PileType.Hand.GetPile(Owner).Cards.ToList();var candidates=(IsUpgraded?hand:new[]{hand.FirstOrDefault(),hand.LastOrDefault()}.Where(c=>c is not null).Cast<CardModel>()).OfType<AssemblerPartCard>().Cast<CardModel>().ToList();var card=await Select(context,candidates,"KILLER_FACTORY_SELECT_PART");if(card is null)return;await CardPileCmd.Add(card,PileType.Exhaust);await CardPileCmd.Draw(context,2,Owner);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BlueprintInvocation : AssemblerPileCard
{
    protected override bool IsPlayable=>base.IsPlayable&&PileType.Exhaust.GetPile(Owner).Cards.OfType<AssemblerPartCard>().Any();
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public BlueprintInvocation():base(1,CardType.Skill,CardRarity.Uncommon){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var part=await Select(context,PileType.Exhaust.GetPile(Owner).Cards.OfType<AssemblerPartCard>(),"KILLER_FACTORY_SELECT_PART") as AssemblerPartCard;if(part is null)return;var state=AssemblerCombatState.For(Owner.Creature.CombatState!);state.PendingBlueprintModule=AssemblerService.CreateModuleFromPart(part);state.PendingBlueprintAtFront=false;if(IsUpgraded){var hand=PileType.Hand.GetPile(Owner).Cards.ToList();if(hand.Count>0){var edge=await Select(context,new[]{hand.First(),hand.Last()},"KILLER_FACTORY_SELECT_MOVE_DESTINATION");state.PendingBlueprintAtFront=ReferenceEquals(edge,hand.First());}}}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class LegacyReplica : AssemblerPileCard
{
    protected override bool IsPlayable=>base.IsPlayable&&PileType.Exhaust.GetPile(Owner).Cards.OfType<AssemblerPartCard>().Any();
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    public LegacyReplica():base(1,CardType.Skill,CardRarity.Uncommon){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {var part=await Select(context,PileType.Exhaust.GetPile(Owner).Cards.OfType<AssemblerPartCard>(),"KILLER_FACTORY_SELECT_PART") as AssemblerPartCard;if(part is null)return;var replica=(AssemblerPartCard)part.CreateClone();await CardPileCmd.AddGeneratedCardToCombat(replica,PileType.Hand,Owner);var destination=int.MaxValue;if(IsUpgraded){var hand=PileType.Hand.GetPile(Owner).Cards.ToList();var anchor=await Select(context,new[]{hand.First(),hand.Last()},"KILLER_FACTORY_SELECT_MOVE_DESTINATION");destination=ReferenceEquals(anchor,hand.First())?0:int.MaxValue;}AssemblerService.MoveWithinHand(replica,destination);AssemblerAbilityRuntime.MakeFreeUntilPlayed(replica);AssemblerCombatState.For(Owner.Creature.CombatState!).LegacyReplicas.Add(replica);}
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(AssemblerCardPool))]
public sealed class BlueprintLibrary : AssemblerPileCard
{
    public BlueprintLibrary():base(2,CardType.Power,CardRarity.Rare){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play){AssemblerCombatState.For(Owner.Creature.CombatState!).BlueprintLibrary++;return Task.CompletedTask;}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
