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
        private const int DEVASTATION_STATION_ID = 77;
        private const int EARTH_BOSS_ID = 72;
        private bool isArenaBattle = false;
        public bool ShouldRollCredits { get; private set; } = false;

        private Dictionary<int, List<float>> bossHpSpecialTriggersUsed = new Dictionary<int, List<float>>();
        private List<float> aiHealHpThresholdsUsed = new();
        private int aiTurnCounter = 0;

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
        public int? LastAbilityUsedId { get; private set; }
        public (string iconName, Sprite displayOn)? AnimationTrigger { get; private set; }
        public bool IsHealAction { get; private set; } = false;
        public bool IsSelfBuff { get; private set; } = false;
        public string? UnlockMessage { get; private set; }
        public bool AllSpritesCaptured { get; private set; } = false;
        public List<string> VictoryMessages { get; private set; } = new List<string>();
        public int CurrentOpponentId { get; private set; } = 0;
        public bool IsBossBattle { get; private set; } = false;

        private class ActiveAbilityEffect
        {
            public Sprite Source { get; }
            public AbilityEffect Effect { get; set; }
            public int TurnsRemaining { get; set; }
            public string SourceAbilityName { get; }
            public bool IsTriggered { get; set; } = false;
            public ActiveAbilityEffect(Sprite source, AbilityEffect effect, string sourceAbilityName)
            {
                this.Source = source;
                this.Effect = new AbilityEffect
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

        public void ConsumeAnimationTrigger() => AnimationTrigger = null;

        public void ConsumeScrollLogTrigger() => ShouldScrollLog = false;
        public int GetActionGauge(Sprite sprite) => actionGauges.GetValueOrDefault(sprite, 0);
        public int GetMaxActionGauge() => ACTION_GAUGE_MAX;

        public void StartArenaBattle(int opponentSpriteID)
        {
            this.isArenaBattle = true;
            StartBattle(plugin.PlayerProfile.Loadout, opponentSpriteID, 0);
        }

        private Sprite CreatePlayerSpriteInstance(int spriteId)
        {
            var baseSpriteData = plugin.DataManager.GetSpriteData(spriteId);
            if (baseSpriteData == null) throw new Exception($"Sprite with ID {spriteId} not found.");

            var instance = new Sprite(baseSpriteData);
            instance.SpecialAbilityIDs.Clear();
            instance.SpecialAbilityIDs.Add(baseSpriteData.SpecialAbilityID);

            if (plugin.PlayerProfile.CapturedSpriteData.TryGetValue(spriteId, out var playerData))
            {
                instance.MaxHealth += playerData.AllocatedHP;
                instance.MaxMana += playerData.AllocatedMP;
                instance.Attack += playerData.AllocatedAttack;
                instance.Defense += playerData.AllocatedDefense;
                instance.Speed += playerData.AllocatedSpeed;

                if (playerData.Level >= 5 && playerData.SecondSpecialAbilityID.HasValue)
                {
                    instance.SpecialAbilityIDs.Add(playerData.SecondSpecialAbilityID.Value);
                }
            }

            instance.Health = instance.MaxHealth;
            instance.Mana = instance.MaxMana;

            return instance;
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
            bossHpSpecialTriggersUsed.Clear();
            aiHealHpThresholdsUsed.Clear();
            aiTurnCounter = 0;
            VictoryMessages.Clear();
            ShouldRollCredits = false;
            CurrentOpponentId = opponentSpriteID;

            foreach (var id in playerSpriteIDs)
            {
                var spriteData = plugin.DataManager.GetSpriteData(id);
                if (spriteData != null)
                {
                    var newSprite = CreatePlayerSpriteInstance(id);
                    PlayerParty.Add(newSprite);
                    actionGauges[newSprite] = random.Next(0, 200);
                    activeEffects[newSprite] = new List<ActiveAbilityEffect>();
                }
            }

            var opponentData = plugin.DataManager.GetSpriteData(opponentSpriteID);
            if (opponentData != null)
            {
                IsBossBattle = opponentData.Rarity == RarityTier.Boss;
                OpponentSprite = new Sprite(opponentData);

                if (OpponentSprite.ID == DEVASTATION_STATION_ID)
                {
                    OpponentSprite.SpecialAbilityIDs.Add(101);
                    OpponentSprite.SpecialAbilityIDs.Add(102);
                    OpponentSprite.SpecialAbilityIDs.Add(103);
                }

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
            LastAbilityUsedId = null;
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

        public void PlayerUseSpecial(int abilityId) => ExecutePlayerAction(p =>
        {
            if (OpponentSprite == null) return;
            var ability = plugin.DataManager.GetAbility(abilityId);
            if (ability == null || p.Mana < ability.ManaCost) return;
            p.Mana -= ability.ManaCost;
            this.LastAbilityUsedId = abilityId;
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

            aiTurnCounter++;

            if (HandlePreTurnChecks(aiSprite))
            {
                goto EndAITurn;
            }

            if (aiSprite.ID == DEVASTATION_STATION_ID)
            {
                DevastationStationAILogic(aiSprite, playerSprite);
            }
            else if (aiSprite.Rarity == RarityTier.Boss)
            {
                BossAILogic(aiSprite, playerSprite);
            }
            else
            {
                RegularAILogic(aiSprite, playerSprite);
            }

        EndAITurn:
            HandlePostTurnActions(aiSprite);
            actionGauges[aiSprite] = 0;
            ActingSprite = null;
            SetActionDelay();
        }

        private void RegularAILogic(Sprite aiSprite, Sprite playerSprite)
        {
            if (TryOneTimeHeal(aiSprite)) return;

            var specialAbility = plugin.DataManager.GetAbility(aiSprite.SpecialAbilityID);
            if (specialAbility != null && aiSprite.Mana >= specialAbility.ManaCost)
            {
                if (aiTurnCounter > 1 && aiTurnCounter % random.Next(6, 9) == 0)
                {
                    UseSpecial(aiSprite, playerSprite, specialAbility.ID);
                    return;
                }
            }

            DefaultAttack(aiSprite, playerSprite);
        }

        private void BossAILogic(Sprite aiSprite, Sprite playerSprite)
        {
            if (TryBossTeachingSpecial(aiSprite, playerSprite)) return;

            if (TryOneTimeHeal(aiSprite)) return;

            var specialAbility = plugin.DataManager.GetAbility(aiSprite.SpecialAbilityID);
            if (specialAbility != null && aiSprite.Mana >= specialAbility.ManaCost)
            {
                if (aiTurnCounter > 1 && aiTurnCounter % random.Next(6, 9) == 0)
                {
                    UseSpecial(aiSprite, playerSprite, specialAbility.ID);
                    return;
                }
            }

            DefaultAttack(aiSprite, playerSprite);
        }

        private void DevastationStationAILogic(Sprite aiSprite, Sprite playerSprite)
        {
            float hpPercent = (float)aiSprite.Health / aiSprite.MaxHealth;
            if (!bossHpSpecialTriggersUsed.ContainsKey(aiSprite.ID))
            {
                bossHpSpecialTriggersUsed[aiSprite.ID] = new List<float>();
            }
            var usedThresholds = bossHpSpecialTriggersUsed[aiSprite.ID];

            var script = new Dictionary<float, int>
            {
                { 0.80f, 101 }, { 0.70f, 102 }, { 0.60f, 103 },
                { 0.45f, 101 }, { 0.35f, 102 }, { 0.30f, 103 },
                { 0.10f, 107 }
            };

            foreach (var entry in script.OrderByDescending(kv => kv.Key))
            {
                float threshold = entry.Key;
                int abilityId = entry.Value;

                if (hpPercent <= threshold && !usedThresholds.Contains(threshold))
                {
                    var ability = plugin.DataManager.GetAbility(abilityId);
                    if (ability != null && aiSprite.Mana >= ability.ManaCost)
                    {
                        usedThresholds.Add(threshold);

                        if (abilityId == 107) // Total Annihilation
                        {
                            aiSprite.Mana = 0;
                            AnimationTrigger = ("special_icon.png", aiSprite);
                        }
                        else
                        {
                            aiSprite.Mana -= ability.ManaCost;
                        }
                        UseSpecial(aiSprite, playerSprite, abilityId, false);
                        return;
                    }
                }
            }

            DefaultAttack(aiSprite, playerSprite);
        }

        private bool TryOneTimeHeal(Sprite aiSprite)
        {
            bool canAffordHeal = aiSprite.Mana >= HEAL_MANA_COST;
            if (!canAffordHeal) return false;

            float hpPercent = (float)aiSprite.Health / aiSprite.MaxHealth;
            float[] healThresholds = { 0.5f, 0.25f };

            foreach (var threshold in healThresholds)
            {
                if (hpPercent <= threshold && !aiHealHpThresholdsUsed.Contains(threshold))
                {
                    aiHealHpThresholdsUsed.Add(threshold);
                    aiSprite.Mana -= HEAL_MANA_COST;
                    int healAmount = (int)(aiSprite.Attack * 1.5f);
                    aiSprite.Health = Math.Min(aiSprite.MaxHealth, aiSprite.Health + healAmount);
                    AddToLog($"{aiSprite.Name} heals for {healAmount} HP.", CombatLogColor.Heal);
                    AttackingSprite = aiSprite; TargetSprite = aiSprite; IsHealAction = true;
                    audioManager.PlaySfx("heal.wav");
                    return true;
                }
            }
            return false;
        }

        private bool TryBossTeachingSpecial(Sprite aiSprite, Sprite playerSprite)
        {
            if (!bossHpSpecialTriggersUsed.ContainsKey(aiSprite.ID))
            {
                bossHpSpecialTriggersUsed[aiSprite.ID] = new List<float>();
            }

            if (bossHpSpecialTriggersUsed[aiSprite.ID].Any()) return false;

            if ((float)aiSprite.Health / aiSprite.MaxHealth >= 0.95f)
            {
                var specialAbility = plugin.DataManager.GetAbility(aiSprite.SpecialAbilityID);
                if (specialAbility != null && aiSprite.Mana >= specialAbility.ManaCost)
                {
                    bossHpSpecialTriggersUsed[aiSprite.ID].Add(0.95f);
                    UseSpecial(aiSprite, playerSprite, aiSprite.SpecialAbilityID);
                    return true;
                }
            }

            return false;
        }

        private void UseSpecial(Sprite aiSprite, Sprite playerSprite, int abilityId, bool costMana = true)
        {
            var ability = plugin.DataManager.GetAbility(abilityId);
            if (ability == null) return;

            if (costMana)
            {
                aiSprite.Mana -= ability.ManaCost;
            }

            this.LastAbilityUsedId = abilityId;
            AddToLog($"{aiSprite.Name} uses {ability.Name}!", CombatLogColor.Status);
            switch (abilityId)
            {
                case 101:
                    audioManager.PlaySfx("fire_whip.mp3");
                    break;
                case 102:
                    audioManager.PlaySfx("entomb.mp3");
                    break;
                case 103:
                    audioManager.PlaySfx("gale.mp3");
                    break;
                case 104:
                    audioManager.PlaySfx("glacialedge.mp3");
                    break;
                case 105:
                    audioManager.PlaySfx("wave.mp3");
                    break;
                case 106:
                    audioManager.PlaySfx("thunder.mp3");
                    break;
                case 107:
                    audioManager.PlaySfx("totalannihilation.mp3");
                    break;
            }
            ApplyAbility(aiSprite, playerSprite, ability);
        }

        private void DefaultAttack(Sprite aiSprite, Sprite playerSprite)
        {
            var result = CalculateDamage(aiSprite, playerSprite);
            LogAttackResult(aiSprite, playerSprite, result);
            DealDamage(aiSprite, playerSprite, result.Damage);
        }

        private void SetActionDelay() { actionDelay = 1.5; }
        private bool HandlePreTurnChecks(Sprite s) { if (IsStunned(s)) { AddToLog($"{s.Name} is stunned and cannot act!", CombatLogColor.Status); return true; } return false; }

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

            var damageEffect = ability.Effects.FirstOrDefault(e => e.EffectType == EffectType.Damage);
            if (damageEffect != null)
            {
                damageDealtThisAbility = CalculateDamage(source, primaryTarget).Damage;
            }

            foreach (var effect in ability.Effects)
            {
                var finalTargetType = effect.Target ?? ability.Target;
                var currentTarget = (finalTargetType == TargetType.Self) ? source : primaryTarget;
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
                    default:
                        activeEffects[currentTarget].Add(new ActiveAbilityEffect(source, effect, ability.Name));
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

            if (attacker.ID == EARTH_BOSS_ID)
            {
                var entombCurse = activeEffects.GetValueOrDefault(defender, new List<ActiveAbilityEffect>())
                                             .FirstOrDefault(e => e.SourceAbilityName == "Entomb's Curse" && !e.IsTriggered);
                if (entombCurse != null)
                {
                    entombCurse.IsTriggered = true;
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
                            effectsForSprite.Add(new ActiveAbilityEffect(activeEffect.Source, new AbilityEffect { EffectType = EffectType.ConditionalDamage, Potency = 60, Duration = 2 }, "Entomb's Curse"));
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
                                LastAbilityUsedId = 107; // Trigger the on-hit animation
                                AddToLog($"{activeEffect.Source.Name} unleashes Total Annihilation!", CombatLogColor.Damage);
                                DealDamage(activeEffect.Source, PlayerParty.First(), (int)activeEffect.Effect.Potency);
                                effectsForSprite.RemoveAt(i);
                                effectHandledAndRemoved = true;
                            }
                            else
                            {
                                AddToLog($"{activeEffect.Source.Name}'s attack is charging... {activeEffect.TurnsRemaining - 1} turns remaining.", CombatLogColor.Status);
                            }
                            break;
                        case EffectType.ChanceToStunOverTime:
                            if (random.NextDouble() < activeEffect.Effect.Potency)
                            {
                                AddToLog($"{s.Name}'s aether erupts! They are stunned!", CombatLogColor.Status);
                                effectsForSprite.Add(new ActiveAbilityEffect(activeEffect.Source, new AbilityEffect { EffectType = EffectType.Stun, Duration = 2 }, "Judgment Stun"));
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

                    if (activeEffect.Effect.EffectType == EffectType.Stun && activeEffect.TurnsRemaining == 1)
                    {
                        var entombCurse = effectsForSprite.FirstOrDefault(e => e.SourceAbilityName == "Entomb's Curse");
                        if (entombCurse != null)
                        {
                            if (entombCurse.IsTriggered)
                            {
                                int damage = (int)entombCurse.Effect.Potency;
                                AddToLog($"{s.Name} takes {damage} damage as the tomb shatters!", CombatLogColor.Damage);
                                s.Health -= damage;
                                audioManager.PlaySfx("hit.wav");
                                CheckForWinner();
                            }
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

            if (OpponentSprite.ID == DEVASTATION_STATION_ID)
            {
                ShouldRollCredits = true;
                State = BattleState.PlayerVictory;
                return;
            }




            if (State == BattleState.PlayerVictory && !isArenaBattle)
            {
                GrantExpToParty();
            }

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

        private void GrantExpToParty()
        {
            if (isArenaBattle) return;

            const int expGained = 10;
            string expMessage = $"The party gains {expGained} experience!";
            AddToLog(expMessage);
            VictoryMessages.Add(expMessage);

            foreach (var spriteId in plugin.PlayerProfile.Loadout)
            {
                if (plugin.PlayerProfile.AttunedSpriteIDs.Contains(spriteId))
                {
                    if (!plugin.PlayerProfile.CapturedSpriteData.ContainsKey(spriteId))
                    {
                        plugin.PlayerProfile.CapturedSpriteData[spriteId] = new PlayerSpriteData();
                    }

                    var spriteData = plugin.PlayerProfile.CapturedSpriteData[spriteId];
                    if (spriteData.Level >= 5) continue;

                    spriteData.Experience += expGained;

                    CheckForLevelUp(spriteId, spriteData);
                }
            }
            plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
        }

        private void CheckForLevelUp(int spriteId, PlayerSpriteData spriteData)
        {
            if (spriteData.Level >= 5) return;

            int[] expToLevel = { 0, 40, 60, 80, 100 };
            int requiredExp = expToLevel[spriteData.Level];

            if (spriteData.Experience >= requiredExp)
            {
                spriteData.Level++;
                spriteData.Experience -= requiredExp;
                spriteData.UnspentStatPoints += 10;
                spriteData.UnspentSkillPoints += 1;

                var spriteName = plugin.DataManager.GetSpriteData(spriteId)?.Name ?? "A sprite";
                string levelUpMessage = $"{spriteName} grew to Level {spriteData.Level}!";
                AddToLog(levelUpMessage, CombatLogColor.Heal);
                VictoryMessages.Add(levelUpMessage);

                if (spriteData.Level < 5)
                {
                    CheckForLevelUp(spriteId, spriteData);
                }
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
