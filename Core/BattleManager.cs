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
                actionGauges[OpponentSprite] = random.Next(0, 200);
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
            AttackingSprite = p;
            TargetSprite = p;
            IsHealAction = true;
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

        // --- NEW METHOD for Sprite Swapping ---
        public void PlayerSwap(int reserveSpriteId) => ExecutePlayerAction(p =>
        {
            var currentSprite = p;
            var reserveSprite = PlayerParty.FirstOrDefault(s => s.ID == reserveSpriteId);

            if (reserveSprite == null || reserveSprite.Health <= 0)
            {
                AddToLog("Cannot swap to that sprite.", CombatLogColor.Status);
                // Note: This still consumes the turn as a penalty for a failed action.
                return;
            }

            int currentIndex = PlayerParty.IndexOf(currentSprite);
            int reserveIndex = PlayerParty.IndexOf(reserveSprite);

            if (currentIndex != -1 && reserveIndex != -1)
            {
                // Simple swap in the list. The ActivePlayerSprite property will find the new first healthy one.
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
            bool canAffordHeal = aiSprite.Mana >= HEAL_MANA_COST;
            var specialAbility = plugin.DataManager.GetAbility(aiSprite.SpecialAbilityID);
            bool canAffordSpecial = specialAbility != null && aiSprite.Mana >= specialAbility.ManaCost;

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

            float[] specialThresholds = { 0.90f, 0.66f, 0.40f };
            if (canAffordSpecial && specialAbility != null)
            {
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
        private void HandlePostTurnActions(Sprite actingSprite)
        {
            TickDownEffects(actingSprite);
            var otherCombatant = (actingSprite == ActivePlayerSprite) ? OpponentSprite : ActivePlayerSprite;
            if (otherCombatant != null) TickDownStunEffect(otherCombatant);
            CheckForWinner();
        }

        private void ApplyAbility(Sprite source, Sprite primaryTarget, Ability ability)
        {
            AttackingSprite = source;
            IsSelfBuff = ability.Target == TargetType.Self;
            TargetSprite = IsSelfBuff ? source : primaryTarget;
            LastAttackTypes = new List<string> { "special" };

            var mainTarget = ability.Target == TargetType.Self ? source : primaryTarget;
            int damageDealtThisAbility = 0;

            var damageEffect = ability.Effects.FirstOrDefault(e => e.EffectType == EffectType.Damage);
            if (damageEffect != null)
            {
                var damageResult = CalculateDamage(source, mainTarget);
                damageDealtThisAbility = damageResult.Damage;
            }

            foreach (var effect in ability.Effects)
            {
                var currentTarget = ability.Target == TargetType.Self ? source : primaryTarget;

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
                    case EffectType.StatBuff:
                    case EffectType.StatDebuff:
                    case EffectType.Stun:
                    case EffectType.Reflect:
                    case EffectType.DamageOverTime:
                        if (effect.Duration > 0)
                        {
                            activeEffects[currentTarget].Add(new ActiveAbilityEffect(effect, ability.Name));
                        }
                        else
                        {
                            ApplyInstantEffect(effect, currentTarget);
                        }
                        break;

                    case EffectType.ManaDrain:
                        int manaDrained = (int)effect.Potency;
                        currentTarget.Mana = Math.Max(0, currentTarget.Mana - manaDrained);
                        AddToLog($"{source.Name} drains {manaDrained} mana from {currentTarget.Name}!", CombatLogColor.Status);
                        break;
                }
            }
            audioManager.PlaySfx("heal.wav");
        }

        private void ApplyInstantEffect(AbilityEffect e, Sprite t)
        {
            if (e.EffectType == EffectType.Heal)
            {
                t.Health = Math.Min(t.MaxHealth, t.Health + (int)e.Potency);
                AddToLog($"{t.Name} heals for {e.Potency} HP.", CombatLogColor.Heal);
            }
        }

        private void DealDamage(Sprite attacker, Sprite defender, int damageAmount, bool playSound = true)
        {
            if (State != BattleState.InProgress) return;

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
            AttackingSprite = a;
            TargetSprite = d;
            LastAttackTypes = a.AttackType.Any() ? new List<string>(a.AttackType) : new List<string> { "physical" };
            IsHealAction = false;
            var t = a.AttackType.Any() ? string.Join(" & ", a.AttackType) : "physical";
            AddToLog($"{a.Name} attacks with {t} damage.");
            if (r.IsSuperEffective && !r.IsIneffective) AddToLog("It's super effective!", CombatLogColor.Status);
            if (r.IsIneffective && !r.IsSuperEffective) AddToLog("It's not very effective...", CombatLogColor.Status);
        }
        private void AddToLog(string m, CombatLogColor color = CombatLogColor.Normal) { CombatLog.Add(new CombatLogEntry(m, color)); if (CombatLog.Count > 50) CombatLog.RemoveAt(0); ShouldScrollLog = true; }

        private void TickDownEffects(Sprite s)
        {
            if (!activeEffects.ContainsKey(s)) return;
            var effectsForSprite = activeEffects[s];

            foreach (var activeEffect in effectsForSprite.Where(x => x.Effect.EffectType == EffectType.DamageOverTime))
            {
                int dotDamage = (int)activeEffect.Effect.Potency;
                s.Health -= dotDamage;
                AddToLog($"{s.Name} takes {dotDamage} damage from {activeEffect.SourceAbilityName}.", CombatLogColor.Damage);
                audioManager.PlaySfx("hit.wav");
            }
            CheckForWinner();
            if (State != BattleState.InProgress) return;


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

        private float GetStatMultiplier(Sprite s, Stat st) { float m = 1.0f; if (activeEffects.TryGetValue(s, out var e)) { foreach (var ae in e.Where(x => x.Effect.StatAffected == st)) { m *= ae.Effect.Potency; } } return m; }

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
    }
}
