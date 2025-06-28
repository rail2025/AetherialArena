using AetherialArena.Models;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherialArena.Core
{
    public class BattleManager
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
        public List<string> CombatLog { get; } = new List<string>();
        public string CurrentBackgroundName { get; private set; } = string.Empty;


        private class ActiveAbilityEffect { public AbilityEffect Effect { get; } public int TurnsRemaining { get; set; } public ActiveAbilityEffect(AbilityEffect effect) { Effect = effect; TurnsRemaining = effect.Duration; } }
        private readonly Dictionary<Sprite, List<ActiveAbilityEffect>> activeEffects = new();
        private readonly Dictionary<Sprite, int> actionGauges = new();

        public BattleManager(Plugin p, IFramework framework)
        {
            this.plugin = p;
            this.framework = framework;
        }

        public int GetActionGauge(Sprite sprite) => actionGauges.GetValueOrDefault(sprite, 0);
        public int GetMaxActionGauge() => ACTION_GAUGE_MAX;

        public void StartBattle(List<int> playerSpriteIDs, int opponentSpriteID)
        {
            CombatLog.Clear();
            PlayerParty.Clear();
            actionGauges.Clear();
            activeEffects.Clear();

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

        public void EndBattle()
        {
            State = BattleState.None;
            PlayerParty.Clear();
            OpponentSprite = null;
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

            if (ActingSprite != null)
            {
                return;
            }

            var allCombatants = PlayerParty.Where(s => s.Health > 0).ToList();
            if (OpponentSprite?.Health > 0)
            {
                allCombatants.Add(OpponentSprite);
            }

            foreach (var combatant in allCombatants)
            {
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

                if (!PlayerParty.Contains(ActingSprite))
                {
                    AITurn();
                }
            }
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
        public void PlayerHeal() => ExecutePlayerAction(p => { if (p.Mana < HEAL_MANA_COST) return; p.Mana -= HEAL_MANA_COST; int h = (int)(p.Attack * 1.5f); p.Health = Math.Min(p.MaxHealth, p.Health + h); AddToLog($"{p.Name} heals for {h} HP."); });
        public void PlayerUseSpecial() => ExecutePlayerAction(p => { if (OpponentSprite == null) return; var a = plugin.DataManager.GetAbility(p.SpecialAbilityID); if (a == null || p.Mana < a.ManaCost) return; p.Mana -= a.ManaCost; AddToLog($"{p.Name} uses {a.Name}!"); ApplyAbility(p, OpponentSprite, a); });

        private void AITurn()
        {
            var p = ActivePlayerSprite;
            if (p == null || ActingSprite == null) return;
            var ai = ActingSprite;

            if (HandlePreTurnChecks(ai))
            {
                actionGauges[ai] = 0;
                ActingSprite = null;
                SetActionDelay();
                return;
            }

            var r = CalculateDamage(ai, p);
            p.Health -= r.Damage;
            LogAttackResult(ai, p, r);
            HandlePostTurnActions(ai);
            actionGauges[ai] = 0;
            ActingSprite = null;
            SetActionDelay();
        }

        private void SetActionDelay() { actionDelay = 1.5; }
        private bool HandlePreTurnChecks(Sprite s) { if (IsStunned(s)) { AddToLog($"{s.Name} is stunned and cannot act!"); TickDownEffects(s); return true; } return false; }
        private void HandlePostTurnActions(Sprite s) { TickDownEffects(s); CheckForWinner(); }
        private void ApplyAbility(Sprite src, Sprite tgt, Ability abi) { foreach (var e in abi.Effects) { var et = abi.Target == TargetType.Self ? src : tgt; if (e.EffectType == EffectType.Stun) AddToLog($"{et.Name} is stunned!"); if (e.Duration > 0) { if (activeEffects.ContainsKey(et)) activeEffects[et].Add(new ActiveAbilityEffect(e)); } else ApplyInstantEffect(e, et); } }
        private void ApplyInstantEffect(AbilityEffect e, Sprite t) { if (e.EffectType == EffectType.Damage) { t.Health -= (int)e.Potency; AddToLog($"{t.Name} takes {e.Potency} damage."); } else if (e.EffectType == EffectType.Heal) { t.Health = Math.Min(t.MaxHealth, t.Health + (int)e.Potency); AddToLog($"{t.Name} heals for {e.Potency} HP."); } }
        private float GetTypeAdvantageMultiplier(SpriteType a, SpriteType d) { if (a == SpriteType.Mechanical || d == SpriteType.Mechanical) return 1.0f; if (a == SpriteType.Figure && d == SpriteType.Beast || a == SpriteType.Beast && d == SpriteType.Creature || a == SpriteType.Creature && d == SpriteType.Figure) return 1.5f; if (a == SpriteType.Beast && d == SpriteType.Figure || a == SpriteType.Creature && d == SpriteType.Beast || a == SpriteType.Figure && d == SpriteType.Creature) return 0.75f; return 1.0f; }
        private float GetElementalMultiplier(Sprite a, Sprite d) { float m = 1.0f; if (!a.AttackType.Any()) return m; var w = new HashSet<string>(d.Weaknesses, StringComparer.OrdinalIgnoreCase); var r = new HashSet<string>(d.Resistances, StringComparer.OrdinalIgnoreCase); foreach (var t in a.AttackType) { if (w.Contains(t)) m *= 1.5f; if (r.Contains(t)) m *= 0.5f; } return m; }
        private DamageResult CalculateDamage(Sprite a, Sprite d) { float am = GetStatMultiplier(a, Stat.Attack); float dm = GetStatMultiplier(d, Stat.Defense); float tm = GetTypeAdvantageMultiplier(a.Type, d.Type); float em = GetElementalMultiplier(a, d); int ba = (int)(a.Attack * am); int ea = (int)(ba * tm * em); int fd = (int)(d.Defense * dm); return new DamageResult { Damage = Math.Max(1, ea - fd), IsSuperEffective = tm > 1.0f || em > 1.0f, IsIneffective = tm < 1.0f || em < 1.0f }; }
        private void LogAttackResult(Sprite a, Sprite d, DamageResult r) { var t = a.AttackType.Any() ? string.Join(" & ", a.AttackType) : "physical"; AddToLog($"{a.Name} attacks with {t} damage."); if (r.IsSuperEffective && !r.IsIneffective) AddToLog("It's super effective!"); if (r.IsIneffective && !r.IsSuperEffective) AddToLog("It's not very effective..."); AddToLog($"{d.Name} takes {r.Damage} damage."); }
        private void AddToLog(string m) { CombatLog.Add(m); if (CombatLog.Count > 50) CombatLog.RemoveAt(0); }
        private void TickDownEffects(Sprite s) { if (!activeEffects.ContainsKey(s)) return; var e = activeEffects[s]; foreach (var ae in e.Where(x => x.Effect.EffectType == EffectType.Heal && x.Effect.Duration > 0)) { s.Health = Math.Min(s.MaxHealth, s.Health + (int)ae.Effect.Potency); } for (int i = e.Count - 1; i >= 0; i--) { e[i].TurnsRemaining--; if (e[i].TurnsRemaining <= 0) e.RemoveAt(i); } }
        private bool IsStunned(Sprite s) => activeEffects.ContainsKey(s) && activeEffects[s].Any(e => e.Effect.EffectType == EffectType.Stun);
        private void CheckForWinner() { if (OpponentSprite != null && OpponentSprite.Health <= 0) { OpponentSprite.Health = 0; State = BattleState.PlayerVictory; AddToLog($"{OpponentSprite.Name} was defeated!"); HandleVictory(); } else if (!PlayerParty.Any(s => s.Health > 0)) { State = BattleState.OpponentVictory; AddToLog("Your party was defeated."); } }
        private void HandleVictory() { if (OpponentSprite == null || plugin.PlayerProfile.AttunedSpriteIDs.Contains(OpponentSprite.ID)) return; int n = OpponentSprite.Rarity switch { RarityTier.Uncommon => 3, RarityTier.Rare => 5, _ => 1, }; plugin.PlayerProfile.DefeatCounts.TryGetValue(OpponentSprite.ID, out var c); c++; if (c >= n) { AddToLog($"You have captured {OpponentSprite.Name}!"); plugin.PlayerProfile.AttunedSpriteIDs.Add(OpponentSprite.ID); plugin.PlayerProfile.DefeatCounts.Remove(OpponentSprite.ID); int ma = Math.Min(20, 10 + (plugin.PlayerProfile.AttunedSpriteIDs.Count / 5)); if (ma > plugin.PlayerProfile.MaxAether) { plugin.PlayerProfile.MaxAether = ma; plugin.PlayerProfile.CurrentAether++; } } else { AddToLog($"Defeat progress: {c}/{n}"); plugin.PlayerProfile.DefeatCounts[OpponentSprite.ID] = c; } plugin.SaveManager.SaveProfile(plugin.PlayerProfile); }
        private float GetStatMultiplier(Sprite s, Stat st) { float m = 1.0f; if (activeEffects.TryGetValue(s, out var e)) { foreach (var ae in e.Where(x => x.Effect.StatAffected == st)) { m *= ae.Effect.Potency; } } return m; }
    }
}
