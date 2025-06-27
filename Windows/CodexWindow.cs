using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Windowing;
using AetherialArena.Models;

namespace AetherialArena.Windows
{
    public class CodexWindow : Window
    {
        private readonly Plugin plugin;
        private int selectedSpriteId = -1;
        private const string PlaceholderIconPath = "AetherialArena.assets.icon.placeholder_icon.png";

        public CodexWindow(Plugin plugin) : base("Codex", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(375, 330),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            this.plugin = plugin;
        }

        public override void Draw()
        {
            if (selectedSpriteId == -1)
            {
                DrawSpriteTable();
            }
            else
            {
                DrawDetailView();
            }
        }

        private void DrawSpriteTable()
        {
            if (ImGui.BeginTable("codexTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("ID");
                ImGui.TableSetupColumn("Icon");
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Rarity");
                ImGui.TableSetupColumn("Status");
                ImGui.TableHeadersRow();

                var placeholderIcon = plugin.AssetManager.GetEmbed(PlaceholderIconPath);

                foreach (var sprite in plugin.DataManager.AllSprites)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (ImGui.Selectable($"##{sprite.ID}", false, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, 40)))
                    {
                        selectedSpriteId = sprite.ID;
                    }
                    ImGui.SameLine();

                    ImGui.Text(sprite.ID.ToString());
                    ImGui.TableNextColumn();

                    // Render the loaded placeholder icon
                    if (placeholderIcon != null)
                    {
                        ImGui.Image(placeholderIcon.ImGuiHandle, new Vector2(40, 40));
                    }
                    else
                    {
                        ImGui.Dummy(new Vector2(40, 40)); // Fallback
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(sprite.Name);
                    ImGui.TableNextColumn();
                    ImGui.Text(sprite.Rarity.ToString());
                    ImGui.TableNextColumn();

                    var status = "Not Encountered";
                    if (plugin.PlayerProfile.CapturedSprites.Contains(sprite.ID))
                    {
                        status = "Captured";
                    }
                    else if (plugin.PlayerProfile.EncounteredSprites.Contains(sprite.ID))
                    {
                        status = "Encountered";
                    }
                    ImGui.Text(status);
                }
                ImGui.EndTable();
            }
        }

        private void DrawDetailView()
        {
            var spriteData = plugin.DataManager.GetSpriteData(selectedSpriteId);

            if (ImGui.Button("< Back to List"))
            {
                selectedSpriteId = -1;
                return;
            }

            ImGui.Separator();

            if (spriteData == null)
            {
                ImGui.Text("Detailed sprite data not found.");
                return;
            }

            ImGui.BeginChild("DetailViewChild");

            ImGui.Text($"Name: {spriteData.Name} (ID: {spriteData.ID})");

            // Render the loaded placeholder icon in the detail view
            var placeholderIcon = plugin.AssetManager.GetEmbed(PlaceholderIconPath);
            if (placeholderIcon != null)
            {
                ImGui.Image(placeholderIcon.ImGuiHandle, new Vector2(64, 64));
            }
            else
            {
                ImGui.Dummy(new Vector2(64, 64)); // Fallback
            }

            ImGui.Text($"Type: {spriteData.Type}");
            ImGui.Text($"Sub-type: {spriteData.SubType}");
            ImGui.Text($"Attack Type: {spriteData.AttackType}");

            ImGui.Separator();
            ImGui.Text("Stats:");
            ImGui.Indent();
            ImGui.Text($"HP: {spriteData.Stats.HP}");
            ImGui.Text($"Attack: {spriteData.Stats.Attack}");
            ImGui.Text($"Defense: {spriteData.Stats.Defense}");
            ImGui.Text($"Speed: {spriteData.Stats.Speed}");
            ImGui.Unindent();

            ImGui.Separator();
            ImGui.Text("Combat Properties:");
            ImGui.Indent();
            ImGui.Text($"Weaknesses: {string.Join(", ", spriteData.Weaknesses)}");
            ImGui.Text($"Resistances: {string.Join(", ", spriteData.Resistances)}");
            ImGui.Text($"Special Move: {spriteData.SpecialAttackName}");
            ImGui.Unindent();

            ImGui.EndChild();
        }
    }
}
