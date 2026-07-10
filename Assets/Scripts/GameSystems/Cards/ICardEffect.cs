using System.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using GameSystems;
using GameSystems.Cards;
using UnityEngine.Serialization;


public interface ICardEffect
{
    Task Execute(EffectContext context);
}

// AsyncWaitForCompletion can leave a Task pending if the tween is killed
// (e.g. because the visualizer's GameObject was destroyed mid-animation).
// This wrapper resolves on both normal completion and on kill.
internal static class TweenExtensions
{
    internal static Task AwaitSafe(this Tween tween)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        tween.onComplete += () => tcs.TrySetResult(true);
        tween.onKill += () => tcs.TrySetResult(true);
        return tcs.Task;
    }
}

// LEGACY: default lane combat (clashes, lunges, face hits) is resolved
// centrally by Board.ResolveCombat since combat became simultaneous. Card
// assets still serialize this effect inside DefaultCombatBehaviour, which now
// only marks a minion as a default attacker — the effect itself no longer runs
// during combat. Kept so existing assets deserialize (and for manual wiring).
[System.Serializable]
public class DefaultAttackEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    private ITargetLogic targetLogic = new Default();

    public async Task Execute(EffectContext context)
    {
        if (context.Instance is not MinionInstance minion)
        {
            Debug.LogError(
                $"DefaultAttackEffect expected MinionInstance but got {context.Instance.GetType()}, skipping execution.");
            return;
        }

        // Stunned/sleeping units skip their combat action (triggers still run).
        if (minion.HasStatusEffect(StatusEffectType.Stun) || minion.HasStatusEffect(StatusEffectType.Sleep))
        {
            Debug.Log($"{minion.SourceCard.cardName} is stunned/asleep and skips its attack.");
            return;
        }

        if (context.Instance is FieldableCardInstance fieldableCardInstance)
        {
            if (fieldableCardInstance.Lane == null)
            {
                Debug.LogError("DefaultAttackEffect requires the card to be on a lane, skipping execution.");
                return;
            }

            if (fieldableCardInstance.SourcePortal.GetMinionPosition(fieldableCardInstance) != 0)
            {
                Debug.Log(
                    "DefaultAttackEffect requires the card to be in the front position of the lane, skipping execution.");
                return;
            }
        }

        int amount = minion.CurrentAttack;
        var targets = targetLogic.GetTargets(context);


        if (minion.SourcePortal != null)
        {
            var visualizer = minion.SourcePortal.GetVisualizer(minion);
            if (visualizer != null && targets.Count > 0)
            {
                // Simple DOTween sequence for attacking the first target visually
                var target = targets[0]; //thers only one for default attack
                Vector3 originalPos = visualizer.transform.position;
                Vector3 targetPos = originalPos;

                if (target is MinionInstance targetMinion && targetMinion.SourcePortal != null)
                {
                    var targetVisualizer = targetMinion.SourcePortal.GetVisualizer(targetMinion);
                    if (targetVisualizer != null)
                    {
                        targetPos = targetVisualizer.transform.position;
                    }
                }
                else if (target is Player playerTarget)
                {
                    targetPos = playerTarget.healthText.transform.position; // rough proxy
                }
                await visualizer.transform.DOMove(Vector3.Lerp(originalPos, targetPos, 0.6f), 0.2f)
                    .SetEase(Ease.InCubic).AwaitSafe();
                await target.TakeDamage(new DamageEventData(amount, context.Instance, DamageSourceType.Attack));
                Debug.Log($"[DefaultAttackEffect] {context} → hits {target} for {amount}");
                // Attacker may have died from reflected damage inside TakeDamage;
                // skip the return move if so (visualizer will be destroyed).
                if (minion.IsAlive)
                    await visualizer.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutCubic).AwaitSafe();
            }
        }

        await minion.HandleEvent(new GameEvent(GameEventType.OnAttack, minion, new AttackEventData(targets)));
    }
}

[System.Serializable]
public class DamageEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic amountLogic;

    // What this damage counts as for source-type checks (DamageSourceTypeIs,
    // lifesteal). Set Spell on spell cards, StatusEffect on burn/plague ticks.
    public DamageSourceType sourceType = DamageSourceType.Effect;

    private int damageAmount;


    public DamageEffect()
    {
    }

    public DamageEffect(int amount, ITargetLogic targetLogic)
    {
        this.damageAmount = amount;
        this.targetLogic = targetLogic;
    }

    public async Task Execute(EffectContext context)
    {
        if (targetLogic == null)
        {
            Debug.LogError("No target logic assigned for damage effect, skipping execution.");
            return;
        }

        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            damageAmount = amountLogic.CalculateValue(context);
            await t.TakeDamage(new DamageEventData(damageAmount, context.Instance, sourceType));

            Debug.Log($"[DamageEffect/{sourceType}] {context} → hits {t} for {damageAmount}");
        }
    }
}
[System.Serializable]
public class HealEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic amountLogic;

    private int healAmount;


    public HealEffect()
    {
    }
    public HealEffect(int amount, ITargetLogic targetLogic)
    {
        this.healAmount = amount;
        this.targetLogic = targetLogic;
    }
    
    public async Task Execute(EffectContext context)
    {
        if (targetLogic == null)
        {
            Debug.LogError("No target logic assigned for heal effect, skipping execution.");
            return;
        }

        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            healAmount = amountLogic.CalculateValue(context);
            await t.Heal(new HealEventData(healAmount, context.Instance));
            Debug.Log($"[HealEffect] {context} → heals {t} for {healAmount}");
        }
    }
}

[System.Serializable]
public class DrawCardEffect : ICardEffect
{
    public int count;

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public async Task Execute(EffectContext context)
    {
        if (targetLogic == null)
        {
            Debug.LogError("No target logic assigned for draw effect, skipping execution.");
            return;
        }

        var targets = targetLogic.GetTargets(context);
        bool announced = false;
        foreach (var t in targets)
        {
            if (t is IPlayerTargetable player)
            {
                // Prompt once, before the first draw, only when a player actually draws.
                if (!announced && Announcer.Instance != null)
                {
                    await Announcer.Instance.AnnounceDrawCard();
                    announced = true;
                }
                await player.DrawCard(count);
            }
        }

        Debug.Log($"Drawing {count} cards");
    }
}

[System.Serializable]
public class DiscardCardEffect : ICardEffect
{
    public int count;
    public bool randomDiscard; //If true, discard random cards. If false, discard from the end of the hand.

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public async Task Execute(EffectContext context)
    {
        var targets = targetLogic.GetTargets(context);
        bool announced = false;
        foreach (var t in targets)
        {
            if (t is IPlayerTargetable player)
            {
                // Prompt once, before the first discard, only when a player actually discards.
                if (!announced && Announcer.Instance != null)
                {
                    await Announcer.Instance.AnnounceDiscardCard();
                    announced = true;
                }
                await player.DiscardCard(count);
            }
        }

        Debug.Log($"Discarding {count} cards {(randomDiscard ? "randomly" : "chosen by player")}");
        Debug.LogWarning("RANDOM DISCARD NOT IMPLEMENTED");
    }
}

[System.Serializable]
public class RedirectEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic newTargetLogic;

    public async Task Execute(EffectContext context)
    {
        var gameEvent = context.Event;
        var originalTarget = context.Instance;
        var newTargets = newTargetLogic.GetTargets(context);

        if (newTargets.Count > 0 && gameEvent.GameEventPayload is DamageEventData damageData)
        {
            damageData.IsPrevented = true;

            foreach (var newTarget in newTargets)
            {
                if (newTarget == originalTarget)
                {
                    Debug.Log(
                        $"New target {newTarget} is the same as original target, skipping to avoid infinite loop.");
                    continue;
                }

                if (newTarget is ITargetable targetable)
                {
                    await targetable.TakeDamage(new DamageEventData(damageData.Amount,
                        damageData.Source ?? context.Instance, damageData.SourceType));
                }
            }

            Debug.Log(
                $"Redirecting damage from {originalTarget} to {string.Join(", ", newTargets)}");
            return;
        }

        if (newTargets.Count == 0)
        {
            Debug.Log($"RedirectEffect: no targets resolved for event {gameEvent.Type}.");
            return;
        }

        // For non-damage events, forward the original event type and payload to each new target.
        var fieldInstance = context.Instance as FieldableCardInstance;
        var redirectedEvent = new GameEvent(gameEvent.Type, fieldInstance, gameEvent.GameEventPayload);

        foreach (var newTarget in newTargets)
        {
            if (newTarget == originalTarget)
            {
                Debug.Log(
                    $"New target {newTarget} is the same as original target, skipping to avoid infinite loop.");
                continue;
            }

            if (newTarget is IGameEventReceiver receiver)
            {
                await receiver.HandleEvent(redirectedEvent);
            }
        }

        Debug.Log(
            $"Redirecting {gameEvent.Type} from {originalTarget} to {string.Join(", ", newTargets)}");
    }
}

[System.Serializable]
public class TriggerEventEffect : ICardEffect
{
    public GameEventType eventType;

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetToTriggerEventOn;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToTriggerEventOn.GetTargets(context);

        foreach (var t in targets)
        {
            if (t is IGameEventReceiver receiver)
            {
                await receiver.HandleEvent(new GameEvent(eventType, null));
            }
        }

        Debug.Log($"Triggering event {eventType} on targets: {string.Join(", ", targets)}");
    }
}

[System.Serializable]
public class TriggerAttackEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    private ITargetLogic whoAttacks = new Default();

    [SerializeReference] [SubclassSelector]
    private ITargetLogic attacksWho = new Default();

    public async Task Execute(EffectContext context)
    {
        var attackers = whoAttacks.GetTargets(context);
        var defenders = attacksWho.GetTargets(context);

        foreach (var t in attackers)
        {
            if (t is MinionInstance minionAttacker)
            {
                // Stunned/sleeping units can't be made to attack either.
                if (minionAttacker.HasStatusEffect(StatusEffectType.Stun) ||
                    minionAttacker.HasStatusEffect(StatusEffectType.Sleep))
                {
                    Debug.Log($"{minionAttacker.SourceCard.cardName} is stunned/asleep and skips the triggered attack.");
                    continue;
                }

                var amount = minionAttacker.CurrentAttack;
                foreach (var defender in defenders)
                {
                    //TODO Replace with more sophisticated reusable tween function
                    if (minionAttacker.SourcePortal != null)
                    {
                        var visualizer = minionAttacker.SourcePortal.GetVisualizer(minionAttacker);
                        if (visualizer != null && defenders.Count > 0)
                        {
                            // Simple DOTween sequence for attacking the first target visually
                            var target = defenders[0];
                            Vector3 originalPos = visualizer.transform.position;
                            Vector3 targetPos = originalPos;

                            if (target is MinionInstance targetMinion && targetMinion.SourcePortal != null)
                            {
                                var targetVisualizer = targetMinion.SourcePortal.GetVisualizer(targetMinion);
                                if (targetVisualizer != null)
                                {
                                    targetPos = targetVisualizer.transform.position;
                                }
                            }
                            else if (target is Player playerTarget)
                            {
                                targetPos = playerTarget.healthText.transform.position; // rough proxy
                            }

                            // Punch animation: go to target and back
                            await visualizer.transform.DOMove(Vector3.Lerp(originalPos, targetPos, 0.6f), 0.2f)
                                .SetEase(Ease.InCubic).AwaitSafe();
                            await defender.TakeDamage(new DamageEventData(amount, minionAttacker, DamageSourceType.Attack));
                            if (minionAttacker.IsAlive)
                                await visualizer.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutCubic)
                                    .AwaitSafe();
                        }
                    }

                    Debug.Log(
                        $"[TriggerAttackEffect] {context} → {minionAttacker} hits {defender} for {amount}");
                }
            }
        }
    }
}

[System.Serializable]
public class ModifyStatsEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    ITargetLogic targetToTriggerEffectOn;

    [SerializeReference] [SubclassSelector]
    ICalculateValueLogic hp;

    [SerializeReference] [SubclassSelector]
    ICalculateValueLogic attack;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToTriggerEffectOn.GetTargets(context);
        foreach (var t in targets)
        {
            int hpVal = hp != null ? hp.CalculateValue(context) : 0;
            int attackVal = attack != null ? attack.CalculateValue(context) : 0;
            await t.ModifyStat(MinionStats.Health, hpVal);
            await t.ModifyStat(MinionStats.Attack, attackVal);
            Debug.Log(
                $"Modifying stats of {t} by {hpVal} health and {attackVal} attack. Context: {context}");
        }
    }
}

[System.Serializable]
public class ApplyStatusEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetToPutStatusEffectOn;

    // Instead of complex abstract type matching, just reference the SO directly
    public StatusEffectData statusEffectToApply;
    public int duration = 1;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToPutStatusEffectOn.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is MinionInstance minion)
            {
                // 1. Create the simple runtime wrapper. The acting card is
                // recorded as the source so triggers can tell friend from foe.
                StatusEffectInstance newEffect = new StatusEffectInstance(statusEffectToApply, duration, context.Instance);

                // 2. Add it to the minion. Awaited so OnStatusEffectApplied
                // triggers (e.g. AddModifierEffect for "while infected" buffs)
                // resolve before this effect reports completion.
                await minion.ApplyStatusEffect(newEffect);
                Debug.Log($"Applying status effect {statusEffectToApply.effectName} to {minion}.");
            }
        }

        await Task.CompletedTask;
    }
}

[System.Serializable]
public class RemoveStatusEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetToRemoveStatusEffectOn;

    public StatusEffectData statusEffectToRemove;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToRemoveStatusEffectOn.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is MinionInstance minion)
            {
                await minion.RemoveStatusEffect(statusEffectToRemove);
                Debug.Log($"Removing status effect {statusEffectToRemove.effectName} from {minion}.");
            }
        }

        await Task.CompletedTask;
    }
}

[System.Serializable]
public class CheckCondition : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    IConditionalCheck condition;

    [SerializeReference] [SubclassSelector]
    ICardEffect effectIfTrue;

    [SerializeReference] [SubclassSelector]
    ICardEffect effectIfFalse;


    public Task Execute(EffectContext context)
    {
        // Either branch may be left empty ("do X only if ...").
        if (condition.CheckCondition(context))
        {
            Debug.Log($"Condition {condition} is true, executing effect {effectIfTrue}");
            return effectIfTrue?.Execute(context) ?? Task.CompletedTask;
        }
        else
        {
            Debug.Log($"Condition {condition} is false, executing effect {effectIfFalse}");
            return effectIfFalse?.Execute(context) ?? Task.CompletedTask;
        }
    }
}

[System.Serializable]
public class ReturnToHand : ICardEffect
{
    public Task Execute(EffectContext context)
    {
        if (context.Instance is FieldableCardInstance fieldableCardInstance)
        {
            return fieldableCardInstance.ReturnToHand();
        }
        else
        {
            Debug.LogError($"ReturnToHand effect can only be applied to FieldableCardInstances, but got {context.Instance.GetType()}");
            return Task.CompletedTask;
        }
    }
}

// Adds a sourced, reversible stat modifier to each target. If the triggering
// event is a status-effect application, the modifier is sourced to that status
// instance so RemoveModifierEffect (or the status expiring) can find it; the
// pair implements "while afflicted with X: +N" buffs.
[System.Serializable]
public class AddModifierEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic healthAmount;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic attackAmount;

    public Task Execute(EffectContext context)
    {
        object source = context.Event.GameEventPayload is StatusEffectEventData statusData
            ? statusData.StatusEffect
            : context.Instance;

        foreach (var target in targetLogic.GetTargets(context))
        {
            if (target is not MinionInstance minion) continue;
            int hp = healthAmount?.CalculateValue(context) ?? 0;
            int atk = attackAmount?.CalculateValue(context) ?? 0;
            minion.AddModifier(new StatModifier(source, hp, atk));
            Debug.Log($"Added modifier ({hp} HP / {atk} ATK) to {minion.SourceCard.cardName} from {source}.");
        }

        return Task.CompletedTask;
    }
}

// Removes all modifiers granted by the resolved source (see AddModifierEffect).
[System.Serializable]
public class RemoveModifierEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public Task Execute(EffectContext context)
    {
        object source = context.Event.GameEventPayload is StatusEffectEventData statusData
            ? statusData.StatusEffect
            : context.Instance;

        foreach (var target in targetLogic.GetTargets(context))
        {
            if (target is MinionInstance minion)
            {
                minion.RemoveModifiersFrom(source);
                Debug.Log($"Removed modifiers from {minion.SourceCard.cardName} granted by {source}.");
            }
        }

        return Task.CompletedTask;
    }
}

// Starts a continuous "while this card is on the field" effect. Wire to
// OnGameEvent(OnPlayed) or OnEffectFieldIsActivated. The registry re-resolves
// targets and amounts on every board change, so allies played later are
// buffed too and dynamic amounts (e.g. NumberOfTargets over rats) scale live.
[System.Serializable]
public class RegisterAuraEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic healthAmount;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic attackAmount;

    public Task Execute(EffectContext context)
    {
        if (context.Instance is not FieldableCardInstance source)
        {
            Debug.LogError($"RegisterAuraEffect requires a FieldableCardInstance, got {context.Instance?.GetType().Name}.");
            return Task.CompletedTask;
        }

        if (targetLogic == null)
        {
            Debug.LogError("RegisterAuraEffect has no target logic assigned, skipping.");
            return Task.CompletedTask;
        }

        source.Board.AuraRegistry.Register(new ActiveAura
        {
            Source = source,
            TargetLogic = targetLogic,
            HealthAmount = healthAmount,
            AttackAmount = attackAmount,
        });

        Debug.Log($"Registered aura from {source.SourceCard.cardName}.");
        return Task.CompletedTask;
    }
}

// Ends all auras granted by this card. Wire to OnEffectFieldIsDeActivated for
// rune-bound auras; death and return-to-hand are already covered by the
// Board/Portal safety nets.
[System.Serializable]
public class UnregisterAuraEffect : ICardEffect
{
    public Task Execute(EffectContext context)
    {
        if (context.Instance is FieldableCardInstance source)
        {
            source.Board.AuraRegistry.UnregisterAllFrom(source);
            Debug.Log($"Unregistered auras from {source.SourceCard.cardName}.");
        }

        return Task.CompletedTask;
    }
}

// Cancels the in-flight damage or heal. Only meaningful on interception
// events (OnAboutToTakeDamage / OnAboutToBeHealed) whose payload is mutable.
// Combine with TriggerNTimes for "block the next instance of damage", or with
// CheckCondition + DamageSourceTypeIs for "block all non-attack damage".
[System.Serializable]
public class PreventDamageEffect : ICardEffect
{
    public Task Execute(EffectContext context)
    {
        switch (context.Event.GameEventPayload)
        {
            case DamageEventData damageData:
                damageData.IsPrevented = true;
                Debug.Log($"Prevented {damageData.Amount} damage on {context.Instance}.");
                break;
            case HealEventData healData:
                healData.IsPrevented = true;
                Debug.Log($"Prevented {healData.Amount} healing on {context.Instance}.");
                break;
            default:
                Debug.LogError(
                    $"PreventDamageEffect needs a mutable Damage/HealEventData payload but got {context.Event.GameEventPayload?.GetType().Name ?? "null"} — wire it to an OnAboutTo* trigger.");
                break;
        }

        return Task.CompletedTask;
    }
}

// Brings dead targets back to full health during the death batch. Must run
// from an OnKilled trigger (that's the only window between death and corpse
// removal). Wrap in TriggerNTimes { type = OnKilled, maxTriggers = 1 } for
// "revived one time", and CheckCondition for "only while infected".
[System.Serializable]
public class ReviveEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic = new SelfTarget();

    public Task Execute(EffectContext context)
    {
        foreach (var target in targetLogic.GetTargets(context))
        {
            if (target is not MinionInstance minion || minion.IsAlive) continue;

            minion.RestoreToFullHealth();
            minion.Board.MarkRevived(minion);
            Debug.Log($"Revived {minion.SourceCard.cardName} at full health.");
        }

        return Task.CompletedTask;
    }
}

// Grants temporary damage absorption (consumed before health) to minions or
// the player. "Shield = 1 per ally minion" = amountLogic NumberOfTargets over
// FriendlyMinions, target OwnerHeroTarget.
[System.Serializable]
public class AddShieldEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic amountLogic;

    public Task Execute(EffectContext context)
    {
        int amount = amountLogic?.CalculateValue(context) ?? 0;
        if (amount <= 0) return Task.CompletedTask;

        foreach (var target in targetLogic.GetTargets(context))
        {
            switch (target)
            {
                case MinionInstance minion:
                    minion.Shield += amount;
                    Debug.Log($"Granted {amount} shield to {minion.SourceCard.cardName}.");
                    break;
                case Player player:
                    player.Shield += amount;
                    Debug.Log($"Granted {amount} shield to player {player.playerId}.");
                    break;
            }
        }

        return Task.CompletedTask;
    }
}

// Creates a fresh instance of a card and plays it through the normal placement
// path (portal matching the card's resonance, full visual setup, OnPlayed).
// Covers golem/plant token spawns.
[System.Serializable]
public class SpawnCardEffect : ICardEffect
{
    public CardData cardToSpawn;
    public bool spawnAtFront;     // tokens like the golem enter at the combat position
    public bool spawnForOpponent; // e.g. traps/curses placed on the enemy side

    public async Task Execute(EffectContext context)
    {
        if (cardToSpawn == null)
        {
            Debug.LogError("SpawnCardEffect has no card assigned, skipping.");
            return;
        }

        var owner = spawnForOpponent ? context.Instance.Opponent : context.Instance.Owner;
        var opponent = spawnForOpponent ? context.Instance.Owner : context.Instance.Opponent;
        var board = context.Instance.Board;

        var spawned = CardFactory.CreateInstance(cardToSpawn, owner, opponent, board, board.CurrentRound);

        if (!await board.PlaceCard(spawned))
        {
            Debug.LogWarning($"SpawnCardEffect could not place {cardToSpawn.cardName} (no room or no matching portal).");
            return;
        }

        if (spawnAtFront && spawned is MinionInstance minion)
        {
            minion.SourcePortal.MoveMinion(minion, toFront: true);
        }

        await board.RaiseEvent(new GameEvent(GameEventType.OnPlayed, spawned), spawned);
        Debug.Log($"Spawned {cardToSpawn.cardName} for player {owner.playerId}.");
    }
}

// Moves each targeted minion to the front or back of its own portal stack.
// Attached items move along with their holder.
[System.Serializable]
public class RepositionEffect : ICardEffect
{
    public enum Position { Front, Back }

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public Position position;

    public Task Execute(EffectContext context)
    {
        foreach (var target in targetLogic.GetTargets(context))
        {
            if (target is not MinionInstance minion || minion.SourcePortal == null) continue;

            minion.SourcePortal.MoveMinion(minion, position == Position.Front);
            Debug.Log($"Repositioned {minion.SourceCard.cardName} to the {position} of its lane.");
        }

        return Task.CompletedTask;
    }
}

// Rotates all of one side's card stacks one lane down (wrapping). Targets the
// caster's own side or the opponent's.
[System.Serializable]
public class ShiftLaneEffect : ICardEffect
{
    public bool shiftOpponentSide;

    public Task Execute(EffectContext context)
    {
        var player = shiftOpponentSide ? context.Instance.Opponent : context.Instance.Owner;
        context.Instance.Board.ShiftLanesDown(player.playerSide);
        Debug.Log($"Shifted all lanes down for player {player.playerId}.");
        return Task.CompletedTask;
    }
}

[System.Serializable]
public class CustomLogicEffect : ICardEffect //Escape hatch for  complex logic without the lego bricks
{
    public async Task Execute(EffectContext context)
    {
        Debug.Log("Executing hyper-complex chaos logic");
    }
}

// Sends the opponent's (or the owner's) most-recently-placed card that is still
// on the board to its owner's discard pile. A minion leaves WITHOUT dying — no
// OnKilled, so no deathrattle fires; an item is simply removed. The card's own
// OnEffectFieldIsDeActivated cleanup runs first (via Board.SendToDiscard), so
// buffs it granted to other targets are lifted and nothing lingers on the board.
// Wire it to any trigger (e.g. a spell's OnPlayed or a minion/item battlecry).
[System.Serializable]
public class DiscardLastPlacedEffect : ICardEffect
{
    // Whose last-placed card to discard. Defaults to the opponent's; set false to
    // discard the acting card's own most-recently-placed ally instead.
    public bool targetOpponent = true;

    public async Task Execute(EffectContext context)
    {
        if (context.Instance is not FieldableCardInstance self || self.Board == null)
        {
            Debug.LogError("DiscardLastPlacedEffect must run on a played/fielded card with a board.");
            return;
        }

        Player owner = targetOpponent ? context.Instance.Opponent : context.Instance.Owner;
        if (owner == null)
        {
            Debug.LogError("DiscardLastPlacedEffect could not resolve the target player.");
            return;
        }

        // Newest card of that player still fielded. Reading the live board set
        // (rather than a stored reference) skips any card that already left play,
        // so "last placed" always means "last placed that is still on the board".
        FieldableCardInstance target = null;
        long newest = long.MinValue;
        foreach (var card in self.Board.GetAllCardsOnBoard())
        {
            if (card.Owner != owner || ReferenceEquals(card, self)) continue;
            if (card.PlacementSequence > newest)
            {
                newest = card.PlacementSequence;
                target = card;
            }
        }

        if (target == null)
        {
            Debug.Log($"DiscardLastPlacedEffect: {owner} has no card on the board to discard.");
            return;
        }

        await self.Board.SendToDiscard(target);
        Debug.Log($"DiscardLastPlacedEffect: sent {target} to player {owner.playerId}'s discard pile.");
    }
}