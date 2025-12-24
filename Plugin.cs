using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AetherialArena.Windows;
using AetherialArena.Core;
using AetherialArena.Services;
using AetherialArena.Models;
using AetherialArena.UI;
using System.Diagnostics;
using Dalamud.Plugin.Internal.Types.Manifest;
using Dalamud.Game.ClientState.Conditions;
using AetherialArena.Audio;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;

namespace AetherialArena
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Aetherial Arena";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ICondition Condition { get; private set; } = null!;
        [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

        public readonly WindowSystem WindowSystem = new("AetherialArena");
        public Configuration Configuration;
        public BattleManager BattleManager;
        public DataManager DataManager;
        public SaveManager SaveManager;
        public PlayerProfile PlayerProfile;
        public EncounterManager EncounterManager;
        public AssetManager AssetManager;
        public AudioManager AudioManager;
        public BattleUIComponent BattleUIComponent;
        public IPluginManifest PluginManifest;

        public HubWindow HubWindow;
        public TitleWindow TitleWindow;
        public AboutWindow AboutWindow;
        public MainWindow MainWindow;
        public ConfigWindow ConfigWindow;
        public DebugWindow DebugWindow;
        public CollectionWindow CollectionWindow;
        public CodexWindow CodexWindow;
        public UpgradeWindow UpgradeWindow;
        public SpecialAbilitySelectionWindow SpecialAbilitySelectionWindow;
        public ArenaSelectionWindow ArenaSelectionWindow;
        public CreditsWindow CreditsWindow;

        private bool searchActionQueued = false;
        private ushort? queuedTerritoryOverride;
        private uint? queuedSubLocationOverride;

        public Plugin()
        {
            this.PluginManifest = PluginInterface.Manifest;
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);
            this.AssetManager = new AssetManager(PluginInterface, TextureProvider);
            this.AudioManager = new AudioManager(this);
            this.DataManager = new DataManager();
            this.SaveManager = new SaveManager();
            this.PlayerProfile = this.SaveManager.LoadProfile();

            this.BattleManager = new BattleManager(this, Framework);
            this.EncounterManager = new EncounterManager(this);
            this.BattleUIComponent = new BattleUIComponent(this, Framework);
            this.HubWindow = new HubWindow(this);
            this.TitleWindow = new TitleWindow(this);
            this.AboutWindow = new AboutWindow(this);
            this.MainWindow = new MainWindow(this, this.BattleUIComponent);
            this.ConfigWindow = new ConfigWindow(this);
            this.DebugWindow = new DebugWindow(this);
            this.CollectionWindow = new CollectionWindow(this);
            this.CodexWindow = new CodexWindow(this);
            this.UpgradeWindow = new UpgradeWindow(this);
            this.SpecialAbilitySelectionWindow = new SpecialAbilitySelectionWindow(this);
            this.ArenaSelectionWindow = new ArenaSelectionWindow(this);
            this.CreditsWindow = new CreditsWindow(this);

            this.WindowSystem.AddWindow(ArenaSelectionWindow);
            this.WindowSystem.AddWindow(HubWindow);
            this.WindowSystem.AddWindow(TitleWindow);
            this.WindowSystem.AddWindow(AboutWindow);
            this.WindowSystem.AddWindow(MainWindow);
            this.WindowSystem.AddWindow(ConfigWindow);
            this.WindowSystem.AddWindow(DebugWindow);
            this.WindowSystem.AddWindow(CollectionWindow);
            this.WindowSystem.AddWindow(CodexWindow);
            this.WindowSystem.AddWindow(UpgradeWindow);
            this.WindowSystem.AddWindow(SpecialAbilitySelectionWindow);
            this.WindowSystem.AddWindow(CreditsWindow);

            CommandManager.AddHandler("/aarena", new CommandInfo(OnCommand) { HelpMessage = "Opens the Aetherial Arena main window." });
            CommandManager.AddHandler("/aadebug", new CommandInfo(OnDebugCommand) {ShowInHelp = false });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

            Framework.Update += OnFrameworkUpdate;
        }

        public void ResetPlayerProfile()
        {
            var profile = this.PlayerProfile;

            profile.CurrentAether = 10;
            profile.MaxAether = 10;
            profile.AttunedSpriteIDs.Clear();
            profile.AttunedSpriteIDs.AddRange(new[] { 1, 2, 3 });
            profile.DefeatCounts.Clear();
            profile.Loadout.Clear();
            profile.Loadout.AddRange(new[] { 1, 2, 3 });
            profile.DefeatedArenaBosses.Clear();
            profile.LastAetherRegenTimestamp = DateTime.UtcNow;
            profile.CapturedSpriteData.Clear();

            this.SaveManager.SaveProfile(profile);
            Log.Info("Player profile has been reset to default.");
        }

        public void UnlockAllSprites()
        {
            var profile = this.PlayerProfile;

            profile.AttunedSpriteIDs.Clear();
            profile.DefeatCounts.Clear();

            profile.AttunedSpriteIDs.AddRange(Enumerable.Range(1, 70));

            int newMaxAether = Math.Min(20, 10 + (profile.AttunedSpriteIDs.Count / 5));
            profile.MaxAether = newMaxAether;
            profile.CurrentAether = newMaxAether;

            this.SaveManager.SaveProfile(profile);
            Log.Info("DEBUG: Unlocked all 70 sprites.");
        }


        public void QueueEncounterSearch(ushort? territoryOverride = null, uint? subLocationOverride = null)
        {
            this.searchActionQueued = true;
            this.queuedTerritoryOverride = territoryOverride;
            this.queuedSubLocationOverride = subLocationOverride;
        }

        public void Dispose()
        {
            this.SaveManager.SaveProfile(this.PlayerProfile);
            this.AssetManager.Dispose();
            this.AudioManager.Dispose();
            Framework.Update -= OnFrameworkUpdate;
            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/aarena");
            CommandManager.RemoveHandler("/aadebug");
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (this.searchActionQueued)
            {
                this.searchActionQueued = false;
                var result = this.EncounterManager.SearchForEncounter(this.queuedTerritoryOverride, this.queuedSubLocationOverride);

                switch (result)
                {
                    case SearchResult.Success:
                        if (this.HubWindow.IsOpen) this.HubWindow.IsOpen = false;
                        break;
                    case SearchResult.NoAether:
                        this.HubWindow.SetStatusMessage("Your aetherial pool is depleted.");
                        break;
                    case SearchResult.InvalidState:
                        this.HubWindow.SetStatusMessage("You cannot search while mounted or in combat.");
                        break;
                    case SearchResult.NoSpritesFound:
                        this.HubWindow.SetStatusMessage("You don't sense any sprites in this area...");
                        break;
                }

                this.queuedTerritoryOverride = null;
                this.queuedSubLocationOverride = null;
            }

            var regenIntervalMinutes = 5;
            var timeSinceLastRegen = DateTime.UtcNow - PlayerProfile.LastAetherRegenTimestamp;

            if (timeSinceLastRegen.TotalMinutes >= regenIntervalMinutes)
            {
                int intervalsPassed = (int)(timeSinceLastRegen.TotalMinutes / regenIntervalMinutes);
                int aetherToRegen = intervalsPassed;

                if (PlayerProfile.CurrentAether < PlayerProfile.MaxAether && aetherToRegen > 0)
                {
                    PlayerProfile.CurrentAether = Math.Min(PlayerProfile.MaxAether, PlayerProfile.CurrentAether + aetherToRegen);
                    SaveManager.SaveProfile(PlayerProfile);
                    Log.Info($"Aether regenerated. Current: {PlayerProfile.CurrentAether}/{PlayerProfile.MaxAether}");
                }

                PlayerProfile.LastAetherRegenTimestamp = PlayerProfile.LastAetherRegenTimestamp.AddMinutes(intervalsPassed * regenIntervalMinutes);
            }
        }

        private void OnCommand(string command, string args) => TitleWindow.Toggle();
        private void OnDebugCommand(string command, string args) => DebugWindow.Toggle();

        private void DrawUI()
        {
            float originalScale = ImGui.GetIO().FontGlobalScale;
            try
            {
                ImGui.GetIO().FontGlobalScale = originalScale * this.Configuration.CustomUiScale;
                this.WindowSystem.Draw();
            }
            finally
            {
                ImGui.GetIO().FontGlobalScale = originalScale;
            }
        }

        private void OnOpenConfigUi() => ConfigWindow.Toggle();
        private void OnOpenMainUi() => TitleWindow.Toggle();
    }
}
