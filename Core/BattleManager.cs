using AetherialArena.Models;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int DEVASTATION_STATION_ID = 77; // Set this to the correct ID for Devastation Station
        private const int EARTH_BOSS_ID = 72;  // Set this to the correct ID for Earth Slam
        private bool isArenaBattle = false;

        private List<float> aiSpecialAbilityHpThresholdsUsed = new();

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
        public bool AllSpritesCaptured { get; private set; } = false;

        private class ActiveAbilityEffect
        {
            public AbilityEffect Effect { get; set; } // Changed to allow modification
            public int TurnsRemaining { get; set; }
            public string SourceAbilityName { get; }
            public bool IsTriggered { get; set; } = false;
            public ActiveAbilityEffect(AbilityEffect effect, string sourceAbilityName)
            {
                Effect = new AbilityEffect
                {
                    EffectType = effect.EffectType,
                    StatAffected = effect.StatAffected,
                    Potency = effect.Potency,
                    Duration = effect.Duration,
                    InitialDelay = effect.InitialDelay,
                    Stacks = effect.Stacks
                };
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

        public void StartArenaBattle(int opponentSpriteID)
        {
            this.isArenaBattle = true;
            StartBattle(plugin.PlayerProfile.Loadout, opponentSpriteID, 0);
        }

        public void StartBattle(List<int> playerSpriteIDs, int opponentSpriteID, ushort territoryId)
        {
            CombatLog.Clear();
            PlayerParty.Clear();
            actionGauges.Clear();
            activeEffects.Clear();
            ClearLastAction();
            UnlockMessage = null;
            AllSpritesCaptured = false;
            aiSpecialAbilityHpThresholdsUsed.Clear();

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
                if (opponentData.Rarity != RarityTier.Boss)
                {
                    OpponentSprite.MaxHealth *= 3;
                    OpponentSprite.Health = OpponentSprite.MaxHealth;
                }
                actionGauges[OpponentSprite] = new Random().Next(0, 200);
                activeEffects[OpponentSprite] = new List<ActiveAbilityEffect>();
            }

            if (this.isArenaBattle)
            {
                CurrentBackgroundName = "arenabackground.png";
            }
            else
            {
                CurrentBackgroundName = plugin.DataManager.GetBackgroundForTerritory(territoryId);
            }
            plugin.AssetManager.GetIcon(CurrentBackgroundName);

            State = BattleState.InProgress;
            ActingSprite = null;
            AddToLog($"A wild {OpponentSprite?.Name} appears!");
            this.isArenaBattle = false;
        }

        public void FleeBattle()
        {
            AddToLog("You fled from the battle and lost some aether.");
            plugin.PlayerProfile.CurrentAether = Math.Max(0, plugin.PlayerProfile.CurrentAether - 1);
            plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
            EndBattle();
        }

        public void EndBattle()
        {
            State = BattleState.None;
            PlayerParty.Clear();
            OpponentSprite = null;
            ClearLastAction();
            UnlockMessage = null;
            AllSpritesCaptured = false;
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

        public void PlayerAttack() => ExecutePlayerAction(p =>
        {
            if (OpponentSprite == null) return;
            var result = CalculateDamage(p, OpponentSprite);
            LogAttackResult(p, OpponentSprite, result);
            DealDamage(p, OpponentSprite, result.Damage);
        });

        public void PlayerHeal() => ExecutePlayerAction(p =>
        {
            if (p.Mana < HEAL_MANA_COST) return;
            p.Mana -= HEAL_MANA_COST;
            int h = (int)(p.Attack * 1.5f);
            p.Health = Math.Min(p.MaxHealth, p.Health + h);
            AddToLog($"{p.Name} heals for {h} HP.", CombatLogColor.Heal);
            AttackingSprite = p; TargetSprite = p; IsHealAction = true;
            audioManager.PlaySfx("heal.wav");
        });

        public void PlayerUseSpecial() => ExecutePlayerAction(p =>
        {
            if (OpponentSprite == null) return;
            var ability = plugin.DataManager.GetAbility(p.SpecialAbilityID);
            if (ability == null || p.Mana < ability.ManaCost) return;
            p.Mana -= ability.ManaCost;
            AddToLog($"{p.Name} uses {ability.Name}!", CombatLogColor.Status);
            ApplyAbility(p, OpponentSprite, ability);
        });

        public void PlayerSwap(int reserveSpriteId) => ExecutePlayerAction(p =>
        {
            var currentSprite = p;
            var reserveSprite = PlayerParty.FirstOrDefault(s => s.ID == reserveSpriteId);

            if (reserveSprite == null || reserveSprite.Health <= 0)
            {
                AddToLog("Cannot swap to that sprite.", CombatLogColor.Status);
                return;
            }

            int currentIndex = PlayerParty.IndexOf(currentSprite);
            int reserveIndex = PlayerParty.IndexOf(reserveSprite);

            if (currentIndex != -1 && reserveIndex != -1)
            {
                var temp = PlayerParty[0];
                PlayerParty[0] = PlayerParty[reserveIndex];
                PlayerParty[reserveIndex] = temp;

                AddToLog($"{currentSprite.Name} swaps out for {reserveSprite.Name}!", CombatLogColor.Status);
            }
        });

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

            // Special AI Logic for Devastation Station
            if (aiSprite.ID == DEVASTATION_STATION_ID && specialAbility != null)
            {
                bool canAffordSpecialForBoss = aiSprite.Mana >= specialAbility.ManaCost;
                if (hpPercent <= 0.15f && canAffordSpecialForBoss && !activeEffects[aiSprite].Any(e => e.Effect.EffectType == EffectType.Casting))
                {
                    aiSprite.Mana -= specialAbility.ManaCost;
                    AddToLog($"{aiSprite.Name} begins casting Total Annihilation!", CombatLogColor.Status);
                    activeEffects[aiSprite].Add(new ActiveAbilityEffect(specialAbility.Effects.First(), specialAbility.Name));
                    goto EndAITurn;
                }
            }

            // Standard AI Heal Logic
            bool canAffordHeal = aiSprite.Mana >= HEAL_MANA_COST;
            float[] healThresholds = { 0.25f, 0.5f };
            foreach (var threshold in healThresholds)
            {
                if (hpPercent <= threshold && canAffordHeal)
                {
                    aiSprite.Mana -= HEAL_MANA_COST;
                    int healAmount = (int)(aiSprite.Attack * 1.5f);
                    aiSprite.Health = Math.Min(aiSprite.MaxHealth, aiSprite.Health + healAmount);
                    AddToLog($"{aiSprite.Name} heals for {healAmount} HP.", CombatLogColor.Heal);
                    AttackingSprite = aiSprite; TargetSprite = aiSprite; IsHealAction = true;
                    audioManager.PlaySfx("heal.wav");
                    goto EndAITurn;
                }
            }

            // Standard AI Special Ability Logic
            bool canAffordSpecial = specialAbility != null && aiSprite.Mana >= specialAbility.ManaCost;
            if (canAffordSpecial && specialAbility != null)
            {
                float[] specialThresholds = { 0.90f, 0.66f, 0.40f };
                foreach (var threshold in specialThresholds)
                {
                    if (hpPercent <= threshold && !aiSpecialAbilityHpThresholdsUsed.Contains(threshold))
                    {
                        aiSpecialAbilityHpThresholdsUsed.Add(threshold);
                        aiSprite.Mana -= specialAbility.ManaCost;
                        AddToLog($"{aiSprite.Name} uses {specialAbility.Name}!", CombatLogColor.Status);
                        ApplyAbility(aiSprite, playerSprite, specialAbility);
                        goto EndAITurn;
                    }
                }
            }

            // Default action: a standard attack
            var result = CalculateDamage(aiSprite, playerSprite);
            LogAttackResult(aiSprite, playerSprite, result);
            DealDamage(aiSprite, playerSprite, result.Damage);

        EndAITurn:
            HandlePostTurnActions(aiSprite);
            actionGauges[aiSprite] = 0;
            ActingSprite = null;
            SetActionDelay();
        }

        private void SetActionDelay() { actionDelay = 1.5; }
        private bool HandlePreTurnChecks(Sprite s) { if (IsStunned(s)) { AddToLog($"{s.Name} is stunned and cannot act!", CombatLogColor.Status); return true; } return false; }

        // This is the simplified, correct version.
        private void HandlePostTurnActions(Sprite actingSprite)
        {
            TickDownEffects(actingSprite);
            CheckForWinner();
        }

        private void ApplyAbility(Sprite source, Sprite primaryTarget, Ability ability)
        {
            AttackingSprite = source;
            IsSelfBuff = ability.Target == TargetType.Self;
            LastAttackTypes = new List<string> { "special" };
            int damageDealtThisAbility = 0;

            // Pre-calculate damage if a LifeSteal effect exists, as it depends on the damage dealt.
            var damageEffect = ability.Effects.FirstOrDefault(e => e.EffectType == EffectType.Damage);
            if (damageEffect != null)
            {
                damageDealtThisAbility = CalculateDamage(source, primaryTarget).Damage;
            }

            foreach (var effect in ability.Effects)
            {
                // CORRECTED LOGIC: Check for the effect's own target, but fall back to the ability's target if it's not specified.
                var finalTargetType = effect.Target ?? ability.Target;
                var currentTarget = (finalTargetType == TargetType.Self) ? source : primaryTarget;

                // Set properties for the UI to display the action correctly
                IsSelfBuff = currentTarget == source;
                TargetSprite = currentTarget;

                switch (effect.EffectType)
                {
                    case EffectType.Damage:
                        var damageResult = CalculateDamage(source, currentTarget);
                        LogAttackResult(source, currentTarget, damageResult);
                        DealDamage(source, currentTarget, damageResult.Damage, false);
                        break;

                    case EffectType.LifeSteal:
                        if (damageDealtThisAbility > 0)
                        {
                            int healAmount = (int)(damageDealtThisAbility * effect.Potency);
                            source.Health = Math.Min(source.MaxHealth, source.Health + healAmount);
                            AddToLog($"{source.Name} absorbs {healAmount} HP!", CombatLogColor.Heal);
                        }
                        break;

                    case EffectType.Heal:
                    case EffectType.ManaDrain:
                        ApplyInstantEffect(effect, currentTarget, source);
                        break;

                    default: // All other effects are duration-based (Stun, Reflect, DoT, Buffs, Debuffs, etc.)
                        activeEffects[currentTarget].Add(new ActiveAbilityEffect(effect, ability.Name));
                        break;
                }
            }
            audioManager.PlaySfx("heal.wav");
        }

        private void ApplyInstantEffect(AbilityEffect e, Sprite t, Sprite s)
        {
            if (e.EffectType == EffectType.Heal)
            {
                t.Health = Math.Min(t.MaxHealth, t.Health + (int)e.Potency);
                AddToLog($"{t.Name} heals for {e.Potency} HP.", CombatLogColor.Heal);
            }
            if (e.EffectType == EffectType.ManaDrain)
            {
                int manaDrained = (int)e.Potency;
                t.Mana = Math.Max(0, t.Mana - manaDrained);
                AddToLog($"{s.Name} drains {manaDrained} mana from {t.Name}!", CombatLogColor.Status);
            }
        }

        private void DealDamage(Sprite attacker, Sprite defender, int damageAmount, bool playSound = true)
        {
            if (State != BattleState.InProgress) return;

            // Entomb Trigger Logic 
            // If the attacker is the Earth Boss, check if the defender has the Entomb curse.
            if (attacker.ID == EARTH_BOSS_ID)
            {
                // Find an untriggered curse on the defender.
                var entombCurse = activeEffects.GetValueOrDefault(defender, new List<ActiveAbilityEffect>())
                                             .FirstOrDefault(e => e.SourceAbilityName == "Entomb's Curse" && !e.IsTriggered);
                if (entombCurse != null)
                {
                    entombCurse.IsTriggered = true; // Mark the curse as "primed"
                    AddToLog($"{defender.Name}'s earthen prison becomes unstable!", CombatLogColor.Status);
                }
            }
            

            var reflectEffect = activeEffects[defender].FirstOrDefault(e => e.Effect.EffectType == EffectType.Reflect);

            if (reflectEffect != null)
            {
                int reflectedDamage = (int)(damageAmount * reflectEffect.Effect.Potency);
                if (reflectedDamage > 0)
                {
                    attacker.Health -= reflectedDamage;
                    AddToLog($"{defender.Name}'s shield reflects {reflectedDamage} damage!", CombatLogColor.Damage);
                    if (playSound) audioManager.PlaySfx("hit.wav");
                    CheckForWinner();
                    if (State != BattleState.InProgress) return;
                }
            }

            defender.Health -= damageAmount;
            AddToLog($"{defender.Name} takes {damageAmount} damage.", CombatLogColor.Damage);
            if (playSound) audioManager.PlaySfx("hit.wav");

            CheckForWinner();
        }

        private float GetTypeAdvantageMultiplier(SpriteType a, SpriteType d) { if (a == SpriteType.Mechanical || d == SpriteType.Mechanical) return 1.0f; if (a == SpriteType.Figure && d == SpriteType.Beast || a == SpriteType.Beast && d == SpriteType.Creature || a == SpriteType.Creature && d == SpriteType.Figure) return 1.5f; if (a == SpriteType.Beast && d == SpriteType.Figure || a == SpriteType.Creature && d == SpriteType.Beast || a == SpriteType.Figure && d == SpriteType.Creature) return 0.75f; return 1.0f; }
        private float GetElementalMultiplier(Sprite a, Sprite d) { float m = 1.0f; if (!a.AttackType.Any()) return m; var w = new HashSet<string>(d.Weaknesses, StringComparer.OrdinalIgnoreCase); var r = new HashSet<string>(d.Resistances, StringComparer.OrdinalIgnoreCase); foreach (var t in a.AttackType) { if (w.Contains(t)) m *= 1.5f; if (r.Contains(t)) m *= 0.5f; } return m; }
        private DamageResult CalculateDamage(Sprite a, Sprite d) { float am = GetStatMultiplier(a, Stat.Attack); float dm = GetStatMultiplier(d, Stat.Defense); float tm = GetTypeAdvantageMultiplier(a.Type, d.Type); float em = GetElementalMultiplier(a, d); int ba = (int)(a.Attack * am); int ea = (int)(ba * tm * em); int fd = (int)(d.Defense * dm); return new DamageResult { Damage = Math.Max(1, ea - fd), IsSuperEffective = tm > 1.0f || em > 1.0f, IsIneffective = tm < 1.0f || em < 1.0f }; }

        private void LogAttackResult(Sprite a, Sprite d, DamageResult r)
        {
            AttackingSprite = a; TargetSprite = d;
            LastAttackTypes = a.AttackType.Any() ? new List<string>(a.AttackType) : new List<string> { "physical" };
            IsHealAction = false;
            var t = a.AttackType.Any() ? string.Join(" & ", a.AttackType) : "physical";
            AddToLog($"{a.Name} attacks with {t} damage.");
            if (r.IsSuperEffective && !r.IsIneffective) AddToLog("It's super effective!", CombatLogColor.Status);
            if (r.IsIneffective && !r.IsSuperEffective) AddToLog("It's not very effective...", CombatLogColor.Status);
        }
        private void AddToLog(string m, CombatLogColor color = CombatLogColor.Normal) { CombatLog.Add(new CombatLogEntry(m, color)); if (CombatLog.Count > 50) CombatLog.RemoveAt(0); ShouldScrollLog = true; }


        private void TickDownEffects(Sprite actingSprite)
        {
            var allCombatants = new List<Sprite>(PlayerParty);
            if (OpponentSprite != null) allCombatants.Add(OpponentSprite);

            foreach (var s in allCombatants)
            {
                if (!activeEffects.ContainsKey(s)) continue;
                var effectsForSprite = activeEffects[s];

                for (int i = effectsForSprite.Count - 1; i >= 0; i--)
                {
                    var activeEffect = effectsForSprite[i];

                    if (activeEffect.Effect.EffectType == EffectType.Stun && s == actingSprite) continue;

                    if (activeEffect.Effect.InitialDelay > 0)
                    {
                        activeEffect.Effect.InitialDelay--;
                        continue;
                    }

                    bool effectHandledAndRemoved = false;
                    switch (activeEffect.Effect.EffectType)
                    {
                        case EffectType.DelayedStun:
                            AddToLog($"{s.Name} is entombed and stunned!", CombatLogColor.Status);
                            activeEffect.Effect = new AbilityEffect { EffectType = EffectType.Stun, Duration = 2 };
                            activeEffect.TurnsRemaining = 2;
                            effectsForSprite.Add(new ActiveAbilityEffect(new AbilityEffect { EffectType = EffectType.ConditionalDamage, Potency = 60, Duration = 2 }, "Entomb's Curse"));
                            break;

                        case EffectType.DelayedDamage:
                            if (s == ActivePlayerSprite)
                            {
                                if (OpponentSprite != null)
                                {
                                    AddToLog($"{s.Name} is struck by a massive gale!", CombatLogColor.Damage);
                                    DealDamage(OpponentSprite, s, (int)activeEffect.Effect.Potency);
                                }
                            }
                            else
                            {
                                AddToLog($"{s.Name} avoided the gale by being in reserve!", CombatLogColor.Status);
                            }
                            effectsForSprite.RemoveAt(i);
                            effectHandledAndRemoved = true;
                            break;

                        case EffectType.Casting:
                            if (activeEffect.TurnsRemaining == 1)
                            {
                                AddToLog($"{s.Name} unleashes Total Annihilation!", CombatLogColor.Damage);
                                DealDamage(s, PlayerParty.First(), (int)activeEffect.Effect.Potency);
                                effectsForSprite.RemoveAt(i);
                                effectHandledAndRemoved = true;
                            }
                            else
                            {
                                AddToLog($"{s.Name}'s attack is charging... {activeEffect.TurnsRemaining - 1} turns remaining.", CombatLogColor.Status);
                            }
                            break;

                        case EffectType.ChanceToStunOverTime:
                            if (random.NextDouble() < activeEffect.Effect.Potency)
                            {
                                AddToLog($"{s.Name}'s aether erupts! They are stunned!", CombatLogColor.Status);
                                effectsForSprite.Add(new ActiveAbilityEffect(new AbilityEffect { EffectType = EffectType.Stun, Duration = 2 }, "Judgment Stun"));
                            }
                            break;

                        case EffectType.DamageOverTime:
                            int dotDamage = (int)activeEffect.Effect.Potency;
                            s.Health -= dotDamage;
                            AddToLog($"{s.Name} takes {dotDamage} damage from {activeEffect.SourceAbilityName}.", CombatLogColor.Damage);
                            audioManager.PlaySfx("hit.wav");
                            break;

                        case EffectType.Heal:
                            if (activeEffect.Effect.Duration > 0)
                            {
                                int healAmount = (int)activeEffect.Effect.Potency;
                                s.Health = Math.Min(s.MaxHealth, s.Health + healAmount);
                                AddToLog($"{s.Name} is healed for {healAmount} HP by {activeEffect.SourceAbilityName}.", CombatLogColor.Heal);
                                audioManager.PlaySfx("heal.wav");
                            }
                            break;
                    }

                    if (effectHandledAndRemoved) continue;

                    // Entomb Expiration Logic
                    if (activeEffect.Effect.EffectType == EffectType.Stun && activeEffect.TurnsRemaining == 1)
                    {
                        var entombCurse = effectsForSprite.FirstOrDefault(e => e.SourceAbilityName == "Entomb's Curse");
                        if (entombCurse != null)
                        {
                            // Only deal damage if the curse was triggered by a hit.
                            if (entombCurse.IsTriggered)
                            {
                                int damage = (int)entombCurse.Effect.Potency;
                                AddToLog($"{s.Name} takes {damage} damage as the tomb shatters!", CombatLogColor.Damage);
                                s.Health -= damage;
                                audioManager.PlaySfx("hit.wav");
                                CheckForWinner();
                            }
                            // Always remove the curse when the stun wears off.
                            effectsForSprite.Remove(entombCurse);
                        }
                    }
                    

                    activeEffect.TurnsRemaining--;
                    if (activeEffect.TurnsRemaining <= 0)
                    {
                        if (activeEffect.Effect.EffectType != EffectType.Casting) AddToLog($"{s.Name}'s {activeEffect.SourceAbilityName} has worn off.", CombatLogColor.Status);
                        effectsForSprite.RemoveAt(i);
                    }
                }
            }
        }

        

        private bool IsStunned(Sprite s) => activeEffects.ContainsKey(s) && activeEffects[s].Any(e => e.Effect.EffectType == EffectType.Stun);

        private void CheckForWinner()
        {
            if (State != BattleState.InProgress) return;
            if (OpponentSprite != null && OpponentSprite.Health <= 0)
            {
                OpponentSprite.Health = 0;
                State = BattleState.PlayerVictory;
                AddToLog($"{OpponentSprite.Name} was defeated!");
                HandleVictory();
            }
            else if (!PlayerParty.Any(s => s.Health > 0))
            {
                State = BattleState.OpponentVictory;
                AddToLog("Your party was defeated.");
                audioManager.PlaySfxAndInterruptMusic("ko.wav", null);
            }
        }

        private void HandleVictory()
        {
            if (OpponentSprite == null || uiState == null) return;

            if (OpponentSprite.Rarity == RarityTier.Boss)
            {
                if (!plugin.PlayerProfile.DefeatedArenaBosses.Contains(OpponentSprite.ID))
                {
                    plugin.PlayerProfile.DefeatedArenaBosses.Add(OpponentSprite.ID);
                    plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                }
                audioManager.PlaySfxAndInterruptMusic("victory.mp3", null);
                return;
            }

            if (plugin.PlayerProfile.AttunedSpriteIDs.Contains(OpponentSprite.ID))
            {
                audioManager.PlaySfxAndInterruptMusic("victory.mp3", null);
                return;
            }

            if (plugin.DataManager.MinionUnlockMap.TryGetValue(OpponentSprite.ID, out var minionData))
            {
                if (!uiState->IsCompanionUnlocked(minionData.Id))
                {
                    UnlockMessage = $"You must unlock this sprite by acquiring the {minionData.Name} minion!";
                    AddToLog(UnlockMessage);
                    audioManager.PlaySfxAndInterruptMusic("victory.mp3", null);
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
                plugin.PlayerProfile.AttunedSpriteIDs.Add(OpponentSprite.ID);
                plugin.PlayerProfile.DefeatCounts.Remove(OpponentSprite.ID);

                int newMaxAether = Math.Min(20, 10 + (plugin.PlayerProfile.AttunedSpriteIDs.Count / 5));
                if (newMaxAether > plugin.PlayerProfile.MaxAether)
                {
                    plugin.PlayerProfile.MaxAether = newMaxAether;
                    plugin.PlayerProfile.CurrentAether++;
                }

                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);

                if (plugin.PlayerProfile.AttunedSpriteIDs.Count >= 70)
                {
                    AllSpritesCaptured = true;
                    audioManager.PlayMusic("allcapmusic.mp3", true);
                }
                else
                {
                    audioManager.PlaySfxAndInterruptMusic("capture.mp3", null);
                }
            }
            else
            {
                AddToLog($"Defeat progress: {currentDefeats}/{defeatsNeeded}");
                plugin.PlayerProfile.DefeatCounts[OpponentSprite.ID] = currentDefeats;
                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                audioManager.PlaySfxAndInterruptMusic("victory.mp3", null);
            }
        }

        private float GetStatMultiplier(Sprite s, Stat st)
        {
            float m = 1.0f;
            if (activeEffects.TryGetValue(s, out var e))
            {
                foreach (var ae in e.Where(x => x.Effect.StatAffected == st))
                {
                    m *= ae.Effect.Potency;
                }
            }
            return m;
        }

        public void ForceWinAndCapture()
        {
            if (State != BattleState.InProgress || OpponentSprite == null) return;

            if (OpponentSprite.Rarity != RarityTier.Boss && !plugin.PlayerProfile.AttunedSpriteIDs.Contains(OpponentSprite.ID))
            {
                plugin.PlayerProfile.AttunedSpriteIDs.Add(OpponentSprite.ID);
                plugin.PlayerProfile.DefeatCounts.Remove(OpponentSprite.ID);
                AddToLog($"DEBUG: Force-captured {OpponentSprite.Name}!");
                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
            }

            OpponentSprite.Health = 0;
            State = BattleState.PlayerVictory;
            HandleVictory();
        }

        public void Debug_HealPlayerToFull()
        {
            if (State != BattleState.InProgress || ActivePlayerSprite == null) return;
            ActivePlayerSprite.Health = ActivePlayerSprite.MaxHealth;
            AddToLog($"DEBUG: Healed {ActivePlayerSprite.Name} to full health.", CombatLogColor.Heal);
        }

        public void Debug_DealDamageToOpponent(int damageAmount)
        {
            if (State != BattleState.InProgress || OpponentSprite == null) return;
            OpponentSprite.Health -= damageAmount;
            AddToLog($"DEBUG: Dealt {damageAmount} damage to {OpponentSprite.Name}.", CombatLogColor.Damage);
            CheckForWinner();
        }
    }
}
