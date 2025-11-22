using System;
using System.Numerics;
using AetherialArena.Models;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AetherialArena.Windows
{
    public class UpgradeWindow : Window
    {
        private readonly Plugin plugin;
        private Sprite? currentSprite;
        private PlayerSpriteData? spriteData;

        private int tempAllocatedHp;
        private int tempAllocatedMp;
        private int tempAllocatedAtk;
        private int tempAllocatedDef;
        private int tempAllocatedSpd;
        private int tempUnspentStatPoints;
        private int tempUnspentSkillPoints;

        // Stat caps
        private const int ATTACK_CAP = 20;
        private const int DEFENSE_CAP = 10;
        private const int SPEED_CAP = 20;

        public UpgradeWindow(Plugin plugin) : base("Upgrade Sprite###AetherialArenaUpgradeWindow")
        {
            this.plugin = plugin;
            this.Size = new Vector2(400, 450);
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public void Open(Sprite sprite)
        {
            this.currentSprite = sprite;
            if (!plugin.PlayerProfile.CapturedSpriteData.TryGetValue(sprite.ID, out this.spriteData))
            {
                this.spriteData = new PlayerSpriteData();
                plugin.PlayerProfile.CapturedSpriteData[sprite.ID] = this.spriteData;
            }

            tempAllocatedHp = this.spriteData.AllocatedHP;
            tempAllocatedMp = this.spriteData.AllocatedMP;
            tempAllocatedAtk = this.spriteData.AllocatedAttack;
            tempAllocatedDef = this.spriteData.AllocatedDefense;
            tempAllocatedSpd = this.spriteData.AllocatedSpeed;
            tempUnspentStatPoints = this.spriteData.UnspentStatPoints;
            tempUnspentSkillPoints = this.spriteData.UnspentSkillPoints;

            this.IsOpen = true;
        }

        public override void Draw()
        {
            if (currentSprite == null || spriteData == null)
            {
                this.IsOpen = false;
                return;
            }

            ImGui.Text(currentSprite.Name);
            ImGui.Text($"Level: {spriteData.Level}");
            ImGui.Separator();
            ImGui.Text($"Unspent Stat Points (HP/MP): {tempUnspentStatPoints}");
            ImGui.Text($"Unspent Skill Points (Atk/Def/Spd): {tempUnspentSkillPoints}");
            ImGui.Separator();

            // HP (No cap)
            ImGui.Text($"HP: {currentSprite.MaxHealth + tempAllocatedHp}"); ImGui.SameLine(150);
            if (tempUnspentStatPoints > 0) { if (ImGui.Button($"+##HP")) { tempAllocatedHp++; tempUnspentStatPoints--; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"+##HP"); ImGui.EndDisabled(); }
            ImGui.SameLine();
            if (tempAllocatedHp > spriteData.AllocatedHP) { if (ImGui.Button($"-##HP")) { tempAllocatedHp--; tempUnspentStatPoints++; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"-##HP"); ImGui.EndDisabled(); }

            // MP (No cap)
            ImGui.Text($"MP: {currentSprite.MaxMana + tempAllocatedMp}"); ImGui.SameLine(150);
            if (tempUnspentStatPoints > 0) { if (ImGui.Button($"+##MP")) { tempAllocatedMp++; tempUnspentStatPoints--; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"+##MP"); ImGui.EndDisabled(); }
            ImGui.SameLine();
            if (tempAllocatedMp > spriteData.AllocatedMP) { if (ImGui.Button($"-##MP")) { tempAllocatedMp--; tempUnspentStatPoints++; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"-##MP"); ImGui.EndDisabled(); }

            ImGui.Separator();

            // Attack (Capped)
            int currentAttack = currentSprite.Attack + tempAllocatedAtk;
            ImGui.Text($"Attack: {currentAttack}");
            if (currentAttack >= ATTACK_CAP) { ImGui.SameLine(); ImGui.Text("(MAX)"); }
            ImGui.SameLine(150);
            if (tempUnspentSkillPoints > 0 && currentAttack < ATTACK_CAP) { if (ImGui.Button($"+##ATK")) { tempAllocatedAtk++; tempUnspentSkillPoints--; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"+##ATK"); ImGui.EndDisabled(); }
            ImGui.SameLine();
            if (tempAllocatedAtk > spriteData.AllocatedAttack) { if (ImGui.Button($"-##ATK")) { tempAllocatedAtk--; tempUnspentSkillPoints++; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"-##ATK"); ImGui.EndDisabled(); }

            // Defense (Capped)
            int currentDefense = currentSprite.Defense + tempAllocatedDef;
            ImGui.Text($"Defense: {currentDefense}");
            if (currentDefense >= DEFENSE_CAP) { ImGui.SameLine(); ImGui.Text("(MAX)"); }
            ImGui.SameLine(150);
            if (tempUnspentSkillPoints > 0 && currentDefense < DEFENSE_CAP) { if (ImGui.Button($"+##DEF")) { tempAllocatedDef++; tempUnspentSkillPoints--; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"+##DEF"); ImGui.EndDisabled(); }
            ImGui.SameLine();
            if (tempAllocatedDef > spriteData.AllocatedDefense) { if (ImGui.Button($"-##DEF")) { tempAllocatedDef--; tempUnspentSkillPoints++; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"-##DEF"); ImGui.EndDisabled(); }

            // Speed (Capped)
            int currentSpeed = currentSprite.Speed + tempAllocatedSpd;
            ImGui.Text($"Speed: {currentSpeed}");
            if (currentSpeed >= SPEED_CAP) { ImGui.SameLine(); ImGui.Text("(MAX)"); }
            ImGui.SameLine(150);
            if (tempUnspentSkillPoints > 0 && currentSpeed < SPEED_CAP) { if (ImGui.Button($"+##SPD")) { tempAllocatedSpd++; tempUnspentSkillPoints--; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"+##SPD"); ImGui.EndDisabled(); }
            ImGui.SameLine();
            if (tempAllocatedSpd > spriteData.AllocatedSpeed) { if (ImGui.Button($"-##SPD")) { tempAllocatedSpd--; tempUnspentSkillPoints++; } }
            else { ImGui.BeginDisabled(); ImGui.Button($"-##SPD"); ImGui.EndDisabled(); }

            ImGui.Separator();

            if (spriteData.Level >= 5)
            {
                ImGui.Text("Special Abilities");
                var firstAbility = plugin.DataManager.GetAbility(currentSprite.SpecialAbilityID);
                if (firstAbility != null) ImGui.Text($"1: {firstAbility.Name} (Primary)");

                var secondAbilityName = "Empty";
                if (spriteData.SecondSpecialAbilityID.HasValue)
                {
                    var secondAbility = plugin.DataManager.GetAbility(spriteData.SecondSpecialAbilityID.Value);
                    if (secondAbility != null) secondAbilityName = secondAbility.Name;
                }
                ImGui.Text($"2: {secondAbilityName}");

                if (ImGui.Button("Change Second Special", new Vector2(-1, 0)))
                {
                    plugin.SpecialAbilitySelectionWindow.Open(currentSprite.ID);
                }
            }

            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Save and Return to Codex", new Vector2(-1, 0)))
            {
                // Commit temporary changes to the actual profile data
                spriteData.AllocatedHP = tempAllocatedHp;
                spriteData.AllocatedMP = tempAllocatedMp;
                spriteData.AllocatedAttack = tempAllocatedAtk;
                spriteData.AllocatedDefense = tempAllocatedDef;
                spriteData.AllocatedSpeed = tempAllocatedSpd;
                spriteData.UnspentStatPoints = tempUnspentStatPoints;
                spriteData.UnspentSkillPoints = tempUnspentSkillPoints;

                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                this.IsOpen = false;
                plugin.CodexWindow.IsOpen = true;
            }
        }
    }
}
