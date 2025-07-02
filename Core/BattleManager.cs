using AetherialArena.Models;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AetherialArena.Audio;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AetherialArena.Core
{
    public unsafe class BattleManager
    {
        public struct DamageResult
        {
            public int Damage;
            public bool IsSuperEffective;
            public bool IsIneffective;
        }

        public enum BattleState { None, InProgress, PlayerVictory, OpponentVictory }

        private readonly Plugin plugin;
        private readonly IFramework framework;
        private readonly AudioManager audioManager;
        private readonly UIState* uiState;
        private readonly Random random = new();
        private double actionDelay = 0;
        private const int HEAL_MANA_COST = 10;
        private const int ACTION_GAUGE_MAX = 1000;

        public List<Sprite> PlayerParty { get; private set; } = new List<Sprite>();
        public Sprite? OpponentSprite { get; private set; }
        public BattleState State { get; private set; } = BattleState.None;
        public Sprite? ActingSprite { get; private set; }
        public bool IsPlayerTurn => ActingSprite != null && PlayerParty.Contains(ActingSprite);
        public Sprite? ActivePlayerSprite => PlayerParty.FirstOrDefault(s => s.Health > 0);
        public List<CombatLogEntry> CombatLog { get; } = new List<CombatLogEntry>();
        public string CurrentBackgroundName { get; private set; } = string.Empty;
        public bool ShouldScrollLog { get; private set; } = false;

        public Sprite? AttackingSprite { get; private set; }
        public Sprite? TargetSprite { get; private set; }
        public List<string> LastAttackTypes { get; private set; } = new();
        public bool IsHealAction { get; private set; } = false;
        public bool IsSelfBuff { get; private set; } = false;
        public string? UnlockMessage { get; private set; } 

        private class ActiveAbilityEffect
        {
            public AbilityEffect Effect { get; }
            public int TurnsRemaining { get; set; }
            public string SourceAbilityName { get; }

            public ActiveAbilityEffect(AbilityEffect effect, string sourceAbilityName)
            {
                Effect = effect;
                TurnsRemaining = effect.Duration;
                SourceAbilityName = sourceAbilityName;
            }
        }
        private readonly Dictionary<Sprite, List<ActiveAbilityEffect>> activeEffects = new();
        private readonly Dictionary<Sprite, int> actionGauges = new();

        public BattleManager(Plugin p, IFramework framework)
        {
            this.plugin = p;
            this.framework = framework;
            this.audioManager = p.AudioManager;
            this.uiState = UIState.Instance();
        }

        public void ConsumeScrollLogTrigger() => ShouldScrollLog = false;
        public int GetActionGauge(Sprite sprite) => actionGauges.GetValueOrDefault(sprite, 0);
        public int GetMaxActionGauge() => ACTION_GAUGE_MAX;

        public void StartBattle(List<int> playerSpriteIDs, int opponentSpriteID, ushort territoryId)
        {
            CombatLog.Clear();
            PlayerParty.Clear();
            actionGauges.Clear();
            activeEffects.Clear();
            ClearLastAction();
            UnlockMessage = null; // Clear message on new battle

            foreach (var id in playerSpriteIDs)
            {
                var spriteData = plugin.DataManager.GetSpriteData(id);
                if (spriteData != null)
                {
                    var newSprite = new Sprite(spriteData);
                    PlayerParty.Add(newSprite);
                    actionGauges[newSprite] = random.Next(0, 200);
                    activeEffects[newSprite] = new List<ActiveAbilityEffect>();
                }
            }

            var opponentData = plugin.DataManager.GetSpriteData(opponentSpriteID);
            if (opponentData != null)
            {
                OpponentSprite = new Sprite(opponentData);
                OpponentSprite.MaxHealth *= 3;
                OpponentSprite.Health = OpponentSprite.MaxHealth;
                actionGauges[OpponentSprite] = random.Next(0, 200);
                activeEffects[OpponentSprite] = new List<ActiveAbilityEffect>();
            }

            int bgIndex = random.Next(1, 8);
            CurrentBackgroundName = $"background{bgIndex}.png";
            plugin.AssetManager.GetIcon(CurrentBackgroundName);

            State = BattleState.InProgress;
            ActingSprite = null;
            AddToLog($"A wild {OpponentSprite?.Name} appears!");
        }

        public void FleeBattle()
        {
            AddToLog("You fled from the battle and lost some aether.");
            plugin.PlayerProfile.CurrentAether = Math.Max(0, plugin.PlayerProfile.CurrentAether - 1);
            plugin.SaveManager.SaveProfile(plugin.PlayerProfile);

            audioManager.StopMusic(0.5f).ContinueWith(t => {
                audioManager.PlayMusic("titlemusic.mp3", true, 1.0f);
            });

            EndBattle();
        }

        public void EndBattle()
        {
            State = BattleState.None;
            PlayerParty.Clear();
            OpponentSprite = null;
            ClearLastAction();
            UnlockMessage = null; // Clear message on battle end
            plugin.MainWindow.IsOpen = false;
            plugin.HubWindow.IsOpen = true;
        }

        private float CalculateFillRate(int speed)
        {
            const float baseRate = 598.16f;
            const float speedMultiplier = 16.36f;
            return baseRate + (speed * speedMultiplier);
        }

        public void Update()
        {
            if (State != BattleState.InProgress) return;
            if (actionDelay > 0)
            {
                actionDelay -= framework.UpdateDelta.TotalSeconds;
                return;
            }
            if (ActingSprite != null) return;
            var allCombatants = new List<Sprite>();
            if (ActivePlayerSprite != null && ActivePlayerSprite.Health > 0) allCombatants.Add(ActivePlayerSprite);
            if (OpponentSprite != null && OpponentSprite.Health > 0) allCombatants.Add(OpponentSprite);

            foreach (var combatant in allCombatants)
            {
                if (IsStunned(combatant)) continue;
                if (!actionGauges.ContainsKey(combatant) || actionGauges[combatant] >= ACTION_GAUGE_MAX) continue;
                float speedStat = combatant.Speed * GetStatMultiplier(combatant, Stat.Speed);
                float fillRate = CalculateFillRate((int)speedStat);
                actionGauges[combatant] = Math.Min(ACTION_GAUGE_MAX, actionGauges[combatant] + (int)(fillRate * framework.UpdateDelta.TotalSeconds));
            }

            var readyCombatant = allCombatants
                .Where(c => actionGauges.GetValueOrDefault(c) >= ACTION_GAUGE_MAX)
                .OrderByDescending(c => actionGauges.GetValueOrDefault(c))
                .FirstOrDefault();

            if (readyCombatant != null)
            {
                ActingSprite = readyCombatant;
                if (!PlayerParty.Contains(ActingSprite)) AITurn();
            }
        }

        public void ClearLastAction()
        {
            AttackingSprite = null;
            TargetSprite = null;
            LastAttackTypes.Clear();
            IsHealAction = false;
            IsSelfBuff = false;
        }

        private void ExecutePlayerAction(Action<Sprite> action)
        {
            if (!IsPlayerTurn || ActingSprite == null) return;
            var p = ActingSprite;
            if (HandlePreTurnChecks(p))
            {
                actionGauges[p] = 0;
                ActingSprite = null;
                SetActionDelay();
                return;
            }
            action(p);
            HandlePostTurnActions(p);
            actionGauges[p] = 0;
            ActingSprite = null;
            SetActionDelay();
        }

        public void PlayerAttack() => ExecutePlayerAction(p => { if (OpponentSprite == null) return; var r = CalculateDamage(p, OpponentSprite); OpponentSprite.Health -= r.Damage; LogAttackResult(p, OpponentSprite, r); });
        public void PlayerHeal() => ExecutePlayerAction(p => { if (p.Mana < HEAL_MANA_COST) return; p.Mana -= HEAL_MANA_COST; int h = (int)(p.Attack * 1.5f); p.Health = Math.Min(p.MaxHealth, p.Health + h); AddToLog($"{p.Name} heals for {h} HP.", CombatLogColor.Heal); AttackingSprite = p; TargetSprite = p; IsHealAction = true; audioManager.PlaySfx("heal.wav"); });
        public void PlayerUseSpecial() => ExecutePlayerAction(p => { if (OpponentSprite == null) return; var a = plugin.DataManager.GetAbility(p.SpecialAbilityID); if (a == null || p.Mana < a.ManaCost) return; p.Mana -= a.ManaCost; AddToLog($"{p.Name} uses {a.Name}!", CombatLogColor.Status); ApplyAbility(p, OpponentSprite, a); });

        private void AITurn()
        {
            var playerSprite = ActivePlayerSprite;
            if (playerSprite == null || ActingSprite == null) return;
            var aiSprite = ActingSprite;

            if (HandlePreTurnChecks(aiSprite))
            {
                actionGauges[aiSprite] = 0;
                ActingSprite = null;
                SetActionDelay();
                return;
            }

            float hpPercent = (float)aiSprite.Health / aiSprite.MaxHealth;
            var specialAbility = plugin.DataManager.GetAbility(aiSprite.SpecialAbilityID);
            bool canAffordHeal = aiSprite.Mana >= HEAL_MANA_COST;
            bool canAffordSpecial = specialAbility != null && aiSprite.Mana >= specialAbility.ManaCost;

            //  Check if the special ability's effect is already active
            bool isSpecialAlreadyActive = false;
            if (canAffordSpecial && specialAbility != null)
            {
                // Determine the target (self for buffs, enemy for debuffs)
                var target = specialAbility.Target == TargetType.Self ? aiSprite : playerSprite;
                isSpecialAlreadyActive = IsEffectActive(target, specialAbility);
            }
            


            // The AI will now only use its special if it's NOT already active.
            if (hpPercent <= 0.25f && canAffordHeal)
            {
                aiSprite.Mana -= HEAL_MANA_COST;
                int healAmount = (int)(aiSprite.Attack * 1.5f);
                aiSprite.Health = Math.Min(aiSprite.MaxHealth, aiSprite.Health + healAmount);
                AddToLog($"{aiSprite.Name} heals for {healAmount} HP.", CombatLogColor.Heal);
                AttackingSprite = aiSprite; TargetSprite = aiSprite; IsHealAction = true;
                audioManager.PlaySfx("heal.wav");
            }
            else if (hpPercent <= 0.30f && canAffordSpecial && specialAbility != null && !isSpecialAlreadyActive)
            {
                aiSprite.Mana -= specialAbility.ManaCost;
                AddToLog($"{aiSprite.Name} uses {specialAbility.Name}!", CombatLogColor.Status);
                ApplyAbility(aiSprite, playerSprite, specialAbility);
            }
            else if (hpPercent <= 0.66f && canAffordHeal)
            {
                aiSprite.Mana -= HEAL_MANA_COST;
                int healAmount = (int)(aiSprite.Attack * 1.5f);
                aiSprite.Health = Math.Min(aiSprite.MaxHealth, aiSprite.Health + healAmount);
                AddToLog($"{aiSprite.Name} heals for {healAmount} HP.", CombatLogColor.Heal);
                AttackingSprite = aiSprite; TargetSprite = aiSprite; IsHealAction = true;
                audioManager.PlaySfx("heal.wav");
            }
            else if (hpPercent <= 0.75f && canAffordSpecial && specialAbility != null && !isSpecialAlreadyActive)
            {
                aiSprite.Mana -= specialAbility.ManaCost;
                AddToLog($"{aiSprite.Name} uses {specialAbility.Name}!", CombatLogColor.Status);
                ApplyAbility(aiSprite, playerSprite, specialAbility);
            }
            else
            {
                // Default action is to attack. This is chosen if the special is redundant.
                var result = CalculateDamage(aiSprite, playerSprite);
                playerSprite.Health -= result.Damage;
                LogAttackResult(aiSprite, playerSprite, result);
            }

            HandlePostTurnActions(aiSprite);
            actionGauges[aiSprite] = 0;
            ActingSprite = null;
            SetActionDelay();
        }

        private bool IsEffectActive(Sprite target, Ability ability)
        {
            if (activeEffects.TryGetValue(target, out var effects))
            {
                // Check if any active effects on the target have the same name as the ability being considered.
                return effects.Any(e => e.SourceAbilityName == ability.Name);
            }
            return false;
        }

        private void SetActionDelay() { actionDelay = 1.5; }
        private bool HandlePreTurnChecks(Sprite s) { if (IsStunned(s)) { AddToLog($"{s.Name} is stunned and cannot act!", CombatLogColor.Status); return true; } return false; }
        private void HandlePostTurnActions(Sprite actingSprite)
        {
            TickDownEffects(actingSprite);
            var otherCombatant = (actingSprite == ActivePlayerSprite) ? OpponentSprite : ActivePlayerSprite;
            if (otherCombatant != null) TickDownStunEffect(otherCombatant);
            CheckForWinner();
        }

        private void ApplyAbility(Sprite src, Sprite tgt, Ability abi)
        {
            AttackingSprite = src;
            IsSelfBuff = abi.Target == TargetType.Self;
            TargetSprite = IsSelfBuff ? src : tgt;
            LastAttackTypes = new List<string> { "special" };

            foreach (var e in abi.Effects)
            {
                var affectedTarget = abi.Target == TargetType.Self ? src : tgt;
                if (e.EffectType == EffectType.Stun)
                {
                    AddToLog($"{affectedTarget.Name} is stunned!", CombatLogColor.Status);
                    if (actionGauges.ContainsKey(affectedTarget)) actionGauges[affectedTarget] = 0;
                }
                if (e.Duration > 0)
                {
                    if (activeEffects.ContainsKey(affectedTarget)) activeEffects[affectedTarget].Add(new ActiveAbilityEffect(e, abi.Name));
                }
                else
                {
                    ApplyInstantEffect(e, affectedTarget);
                }
            }
        }

        private void ApplyInstantEffect(AbilityEffect e, Sprite t) { if (e.EffectType == EffectType.Damage) { t.Health -= (int)e.Potency; AddToLog($"{t.Name} takes {e.Potency} damage.", CombatLogColor.Damage); audioManager.PlaySfx("hit.wav"); } else if (e.EffectType == EffectType.Heal) { t.Health = Math.Min(t.MaxHealth, t.Health + (int)e.Potency); AddToLog($"{t.Name} heals for {e.Potency} HP.", CombatLogColor.Heal); audioManager.PlaySfx("heal.wav"); } }
        private float GetTypeAdvantageMultiplier(SpriteType a, SpriteType d) { if (a == SpriteType.Mechanical || d == SpriteType.Mechanical) return 1.0f; if (a == SpriteType.Figure && d == SpriteType.Beast || a == SpriteType.Beast && d == SpriteType.Creature || a == SpriteType.Creature && d == SpriteType.Figure) return 1.5f; if (a == SpriteType.Beast && d == SpriteType.Figure || a == SpriteType.Creature && d == SpriteType.Beast || a == SpriteType.Figure && d == SpriteType.Creature) return 0.75f; return 1.0f; }
        private float GetElementalMultiplier(Sprite a, Sprite d) { float m = 1.0f; if (!a.AttackType.Any()) return m; var w = new HashSet<string>(d.Weaknesses, StringComparer.OrdinalIgnoreCase); var r = new HashSet<string>(d.Resistances, StringComparer.OrdinalIgnoreCase); foreach (var t in a.AttackType) { if (w.Contains(t)) m *= 1.5f; if (r.Contains(t)) m *= 0.5f; } return m; }
        private DamageResult CalculateDamage(Sprite a, Sprite d) { float am = GetStatMultiplier(a, Stat.Attack); float dm = GetStatMultiplier(d, Stat.Defense); float tm = GetTypeAdvantageMultiplier(a.Type, d.Type); float em = GetElementalMultiplier(a, d); int ba = (int)(a.Attack * am); int ea = (int)(ba * tm * em); int fd = (int)(d.Defense * dm); return new DamageResult { Damage = Math.Max(1, ea - fd), IsSuperEffective = tm > 1.0f || em > 1.0f, IsIneffective = tm < 1.0f || em < 1.0f }; }
        private void LogAttackResult(Sprite a, Sprite d, DamageResult r) { AttackingSprite = a; TargetSprite = d; LastAttackTypes = a.AttackType.Any() ? new List<string>(a.AttackType) : new List<string> { "physical" }; IsHealAction = false; var t = a.AttackType.Any() ? string.Join(" & ", a.AttackType) : "physical"; AddToLog($"{a.Name} attacks with {t} damage."); if (r.IsSuperEffective && !r.IsIneffective) AddToLog("It's super effective!", CombatLogColor.Status); if (r.IsIneffective && !r.IsSuperEffective) AddToLog("It's not very effective...", CombatLogColor.Status); AddToLog($"{d.Name} takes {r.Damage} damage.", CombatLogColor.Damage); audioManager.PlaySfx("hit.wav"); }
        private void AddToLog(string m, CombatLogColor color = CombatLogColor.Normal) { CombatLog.Add(new CombatLogEntry(m, color)); if (CombatLog.Count > 50) CombatLog.RemoveAt(0); ShouldScrollLog = true; }

        private void TickDownEffects(Sprite s)
        {
            if (!activeEffects.ContainsKey(s)) return;
            var effectsForSprite = activeEffects[s];
            foreach (var activeEffect in effectsForSprite.Where(x => x.Effect.EffectType == EffectType.Heal && x.Effect.Duration > 0))
            {
                int healAmount = (int)activeEffect.Effect.Potency;
                s.Health = Math.Min(s.MaxHealth, s.Health + healAmount);
                AddToLog($"{s.Name} is healed for {healAmount} HP by {activeEffect.SourceAbilityName}.", CombatLogColor.Heal);
                audioManager.PlaySfx("heal.wav");
            }
            for (int i = effectsForSprite.Count - 1; i >= 0; i--)
            {
                var effect = effectsForSprite[i];
                if (effect.Effect.EffectType == EffectType.Stun) continue;
                effect.TurnsRemaining--;
                if (effect.TurnsRemaining <= 0)
                {
                    AddToLog($"{s.Name}'s {effect.SourceAbilityName} has worn off.", CombatLogColor.Status);
                    effectsForSprite.RemoveAt(i);
                }
            }
        }

        private void TickDownStunEffect(Sprite s)
        {
            if (!activeEffects.ContainsKey(s)) return;
            var effectsForSprite = activeEffects[s];
            for (int i = effectsForSprite.Count - 1; i >= 0; i--)
            {
                var effect = effectsForSprite[i];
                if (effect.Effect.EffectType != EffectType.Stun) continue;
                effect.TurnsRemaining--;
                if (effect.TurnsRemaining <= 0)
                {
                    AddToLog($"{s.Name}'s {effect.SourceAbilityName} has worn off.", CombatLogColor.Status);
                    effectsForSprite.RemoveAt(i);
                }
            }
        }

        private bool IsStunned(Sprite s) => activeEffects.ContainsKey(s) && activeEffects[s].Any(e => e.Effect.EffectType == EffectType.Stun);
        private void CheckForWinner() { if (State != BattleState.InProgress) return; if (OpponentSprite != null && OpponentSprite.Health <= 0) { OpponentSprite.Health = 0; State = BattleState.PlayerVictory; AddToLog($"{OpponentSprite.Name} was defeated!"); audioManager.PlaySfxAndInterruptMusic("victory.mp3"); HandleVictory(); } else if (!PlayerParty.Any(s => s.Health > 0)) { State = BattleState.OpponentVictory; AddToLog("Your party was defeated."); audioManager.PlaySfxAndInterruptMusic("ko.wav"); } }

        private void HandleVictory()
        {
            if (OpponentSprite == null || uiState == null) return;
            if (plugin.PlayerProfile.AttunedSpriteIDs.Contains(OpponentSprite.ID)) return;

            if (plugin.DataManager.MinionUnlockMap.TryGetValue(OpponentSprite.ID, out var minionData))
            {
                if (!uiState->IsCompanionUnlocked(minionData.Id))
                {
                    // --- MODIFIED: Set the public message property instead of just logging ---
                    UnlockMessage = $"You must unlock this sprite by acquiring the {minionData.Name} minion!";
                    AddToLog(UnlockMessage); // Still log it for posterity
                    return;
                }
            }

            int defeatsNeeded = OpponentSprite.Rarity switch
            {
                RarityTier.Uncommon => 3,
                RarityTier.Rare => 5,
                _ => 1,
            };

            plugin.PlayerProfile.DefeatCounts.TryGetValue(OpponentSprite.ID, out var currentDefeats);
            currentDefeats++;

            if (currentDefeats >= defeatsNeeded)
            {
                AddToLog($"You have captured {OpponentSprite.Name}!");
                audioManager.PlaySfxAndInterruptMusic("capture.mp3");
                plugin.PlayerProfile.AttunedSpriteIDs.Add(OpponentSprite.ID);
                plugin.PlayerProfile.DefeatCounts.Remove(OpponentSprite.ID);

                int newMaxAether = Math.Min(20, 10 + (plugin.PlayerProfile.AttunedSpriteIDs.Count / 5));
                if (newMaxAether > plugin.PlayerProfile.MaxAether)
                {
                    plugin.PlayerProfile.MaxAether = newMaxAether;
                    plugin.PlayerProfile.CurrentAether++;
                }
            }
            else
            {
                AddToLog($"Defeat progress: {currentDefeats}/{defeatsNeeded}");
                plugin.PlayerProfile.DefeatCounts[OpponentSprite.ID] = currentDefeats;
            }

            plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
        }

        private float GetStatMultiplier(Sprite s, Stat st) { float m = 1.0f; if (activeEffects.TryGetValue(s, out var e)) { foreach (var ae in e.Where(x => x.Effect.StatAffected == st)) { m *= ae.Effect.Potency; } } return m; }
    }
}
