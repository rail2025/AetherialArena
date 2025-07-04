using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class CreditsWindow : Window, IDisposable
    {
        private enum CreditsState { Scrolling, Holding, FadingOut }
        private CreditsState currentState;

        private readonly Plugin plugin;
        private float scrollY;
        private float finalLineYPosition = -1; 
        private float holdTimer;
        private float fadeTimer;

        private const float HOLD_DURATION = 2.0f;
        private const float FADE_DURATION = 1.5f;

        private readonly List<(string text, bool isHeader)> credits;

        public CreditsWindow(Plugin plugin) : base("Aetherial Arena Credits###AetherialArenaCreditsWindow", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove)
        {
            this.plugin = plugin;
            this.credits = GetCreditsList();
        }

        public override void OnOpen()
        {
            var viewport = ImGui.GetMainViewport();
            this.Position = viewport.Pos;
            this.Size = viewport.Size;

            currentState = CreditsState.Scrolling;
            scrollY = 0;
            finalLineYPosition = -1;
            holdTimer = HOLD_DURATION;
            fadeTimer = FADE_DURATION;

            plugin.AudioManager.PlayMusic("credits.mp3", true);
        }

        public override void OnClose()
        {
            plugin.BattleManager.EndBattle();
            plugin.TitleWindow.IsOpen = true;
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 1.0f));

            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.BeginChild("CreditsScrollRegion", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoInputs);

            
            RenderCreditsContent();

            ImGui.SetScrollY(scrollY);
            ImGui.EndChild();

            HandleStateMachine();

            if (this.Position.HasValue)
            {
                var skipText = "Skip Credits (Esc)";
                var skipTextSize = ImGui.CalcTextSize(skipText);
                var skipPos = new Vector2(ImGui.GetWindowWidth() - skipTextSize.X - 20, ImGui.GetWindowHeight() - skipTextSize.Y - 20);
                ImGui.GetWindowDrawList().AddText(this.Position.Value + skipPos, ImGui.GetColorU32(ImGuiCol.TextDisabled), skipText);
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                this.IsOpen = false;
            }

            ImGui.PopStyleColor();
        }

        private void HandleStateMachine()
        {
            var io = ImGui.GetIO();
            var scrollSpeed = 0.5f * plugin.Configuration.CustomUiScale;

            switch (currentState)
            {
                case CreditsState.Scrolling:
                    float stopPosition = finalLineYPosition - (ImGui.GetWindowHeight() / 2f);

                    if (finalLineYPosition > 0 && scrollY >= stopPosition)
                    {
                        scrollY = stopPosition;
                        currentState = CreditsState.Holding;
                    }
                    else
                    {
                        scrollY += scrollSpeed;
                    }
                    break;

                case CreditsState.Holding:
                    holdTimer -= io.DeltaTime;
                    if (holdTimer <= 0)
                    {
                        currentState = CreditsState.FadingOut;
                    }
                    break;

                case CreditsState.FadingOut:
                    fadeTimer -= io.DeltaTime;
                    float fadeAlpha = Math.Clamp(1.0f - (fadeTimer / FADE_DURATION), 0, 1);

                    var windowPos = this.Position ?? Vector2.Zero;
                    var windowSize = this.Size ?? Vector2.Zero;
                    ImGui.GetForegroundDrawList().AddRectFilled(windowPos, windowPos + windowSize, ImGui.GetColorU32(new Vector4(0, 0, 0, fadeAlpha)));

                    if (fadeTimer <= 0)
                    {
                        this.IsOpen = false;
                    }
                    break;
            }
        }

        private void RenderCreditsContent()
        {
            
            float initialY = ImGui.GetWindowHeight();
            ImGui.SetCursorPosY(initialY);

            for (int i = 0; i < credits.Count; i++)
            {
                var (text, isHeader) = credits[i];

                
                if (i == credits.Count - 1)
                {
                    finalLineYPosition = ImGui.GetCursorPosY();
                }

                if (string.IsNullOrEmpty(text))
                {
                    ImGui.Spacing();
                    continue;
                }

                if (isHeader)
                {
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 1.0f, 1.0f));
                    ImGui.SetWindowFontScale(1.2f);
                    var textSize = ImGui.CalcTextSize(text);
                    ImGui.SetCursorPosX((ImGui.GetWindowWidth() - textSize.X) * 0.5f);
                    ImGui.Text(text);
                    ImGui.SetWindowFontScale(1.0f);
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                }
                else
                {
                    var parts = text.Split(':', 2);
                    var role = parts[0];
                    var name = parts.Length > 1 ? parts[1].Trim() : "";

                    var roleSize = ImGui.CalcTextSize(role + ":");
                    var windowWidth = ImGui.GetContentRegionAvail().X;

                    ImGui.SetCursorPosX((windowWidth * 0.5f) - roleSize.X);
                    ImGui.Text(role + ":");

                    ImGui.SameLine();
                    ImGui.SetCursorPosX((windowWidth * 0.5f) + 10);
                    ImGui.Text(name);
                }
            }
        }

        private List<(string text, bool isHeader)> GetCreditsList()
        {
            return new List<(string, bool)>
            {
                ("", false), ("", false), ("", false),
                ("AETHERIAL ARENA", true),
                ("", false), ("", false),
                ("A rail Production", true),
                ("", false), ("", false),
                ("Game Director:rail", false),
                ("", false), ("", false),
                ("Production", true),
                ("Executive Producer:rail", false),
                ("Senior Producer:rail", false),
                ("Producer:rail", false),
                ("Associate Producer:rail", false),
                ("Assistant Director:rail", false),
                ("Project Manager:rail", false),
                ("", false),
                ("Design", true),
                ("Lead Designer:rail", false),
                ("Systems Designer:rail", false),
                ("Combat Designer:rail", false),
                ("Economy Designer:rail", false),
                ("UI/UX Lead:rail", false),
                ("UI/UX Designer:rail", false),
                ("Technical Designer:rail", false),
                ("", false),
                ("Writing", true),
                ("Lead Writer:rail", false),
                ("Narrative Designer:rail", false),
                ("Writer & Editor:rail", false),
                ("", false),
                ("Programming & Engineering", true),
                ("Technical Director:rail", false),
                ("Lead Programmer:rail", false),
                ("Senior Gameplay Programmer:rail", false),
                ("Gameplay Programmer:rail", false),
                ("AI Programmer:rail", false),
                ("UI Programmer:rail", false),
                ("Engine Programmer:rail", false),
                ("Tools Programmer:rail", false),
                ("Network Programmer:rail", false),
                ("Junior Programmer:rail", false),
                ("", false),
                ("Quality Assurance", true),
                ("QA Director:rail", false),
                ("QA Manager:rail", false),
                ("QA Lead:rail", false),
                ("Senior QA Tester:rail", false),
                ("Functionality QA Tester:rail", false),
                ("Technical QA Analyst:rail", false),
                ("Compliance Specialist:rail", false),
                ("", false),
                ("Localization", true),
                ("Localization Director:rail", false),
                ("Localization Project Manager:rail", false),
                ("Localization Specialist (NA, EU, JP):rail", false),
                ("Lead Localization Editor:rail", false),
                ("Localization QA Lead:rail", false),
                ("", false),
                ("Marketing & Community", true),
                ("VP of Marketing:rail", false),
                ("Marketing Director:rail", false),
                ("Brand Manager:rail", false),
                ("Public Relations Manager:rail", false),
                ("Social Media Manager:rail", false),
                ("Community Manager:rail", false),
                ("Influencer Relations Coordinator:rail", false),
                ("Market Research Analyst:rail", false),
                ("", false),
                ("Studio Operations", true),
                ("Human Resources Director:rail", false),
                ("Talent Acquisition Specialist:rail", false),
                ("HR Generalist:rail", false),
                ("IT Director:rail", false),
                ("IT Support Technician:rail", false),
                ("General Counsel:rail", false),
                ("Staff Accountant:rail", false),
                ("Payroll Specialist:rail", false),
                ("Accounts Payable/Receivable:rail", false),
                ("Build Manager:rail", false),
                ("Release Manager:rail", false),
                ("Office Administrator:rail", false),
                ("", false),
                ("Executive Leadership", true),
                ("Chief Executive Officer:rail", false),
                ("Chief Operating Officer:rail", false),
                ("Chief Financial Officer:rail", false),
                ("", false), ("", false),
                ("Special Thanks", true),
                ("The Dalamud Team", false),
                ("You, The Player", false),
                ("", false),("", false),
                ("Aetherial Arena", true),
                ("Fin.", false)
            };
        }
    }
}
