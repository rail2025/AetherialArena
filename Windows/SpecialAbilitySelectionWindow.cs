using System.Linq;
using System.Numerics;
using AetherialArena.Models;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AetherialArena.Windows
{
    public class SpecialAbilitySelectionWindow : Window
    {
        private readonly Plugin plugin;
        private int currentSpriteId;

        public SpecialAbilitySelectionWindow(Plugin plugin) : base("Select Special Ability")
        {
            this.plugin = plugin;
            this.Size = new Vector2(500, 400);
            this.Flags = ImGuiWindowFlags.NoScrollbar;
        }

        public void Open(int spriteId)
        {
            this.currentSpriteId = spriteId;
            this.IsOpen = true;
        }

        public override void Draw()
        {
            var capturedSprites = plugin.PlayerProfile.AttunedSpriteIDs
                .Select(id => plugin.DataManager.GetSpriteData(id))
                .Where(s => s != null);

            var availableAbilities = capturedSprites
                .Select(s => plugin.DataManager.GetAbility(s!.SpecialAbilityID))
                .Where(a => a != null)
                .DistinctBy(a => a!.ID)
                .ToList();

            if (ImGui.BeginTable("AbilitySelectionTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Ability Name", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var ability in availableAbilities)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    if (ImGui.Selectable(ability!.Name, false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        var spriteData = plugin.PlayerProfile.CapturedSpriteData[currentSpriteId];
                        spriteData.SecondSpecialAbilityID = ability.ID;
                        plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                        this.IsOpen = false;
                    }

                    ImGui.TableSetColumnIndex(1);
                    string correctedDescription = ability.Description.Replace("%", "%%");
                    ImGui.TextWrapped(correctedDescription);
                }
                ImGui.EndTable();
            }

            ImGui.Separator();
            if (ImGui.Selectable("None (Clear Slot)##remove_special"))
            {
                var spriteData = plugin.PlayerProfile.CapturedSpriteData[currentSpriteId];
                spriteData.SecondSpecialAbilityID = null;
                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                this.IsOpen = false;
            }

            ImGui.Separator();
            if (ImGui.Button("Cancel", new Vector2(-1, 0)))
            {
                this.IsOpen = false;
            }
        }
    }
}
