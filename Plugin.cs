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

namespace AetherialArena
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Aetherial Arena";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ICondition Condition { get; private set; } = null!;
        [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

        public readonly WindowSystem WindowSystem = new("AetherialArena");
        public readonly Configuration Configuration;
        public readonly BattleManager BattleManager;
        public readonly DataManager DataManager;
        public readonly SaveManager SaveManager;
        public readonly EncounterManager EncounterManager;
        public readonly AssetManager AssetManager;
        public readonly PlayerProfile PlayerProfile;
        public readonly AudioManager AudioManager;
        public readonly BattleUIComponent BattleUIComponent;
        public readonly IPluginManifest PluginManifest;

        public readonly HubWindow HubWindow;
        public readonly TitleWindow TitleWindow;
        public readonly AboutWindow AboutWindow;
        public readonly MainWindow MainWindow;
        public readonly ConfigWindow ConfigWindow;
        public readonly DebugWindow DebugWindow;
        public readonly CollectionWindow CollectionWindow;
        public readonly CodexWindow CodexWindow;

        private readonly Stopwatch regenTimer = new();
        private readonly double regenIntervalMinutes = 10;

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

            this.WindowSystem.AddWindow(HubWindow);
            this.WindowSystem.AddWindow(TitleWindow);
            this.WindowSystem.AddWindow(AboutWindow);
            this.WindowSystem.AddWindow(MainWindow);
            this.WindowSystem.AddWindow(ConfigWindow);
            this.WindowSystem.AddWindow(DebugWindow);
            this.WindowSystem.AddWindow(CollectionWindow);
            this.WindowSystem.AddWindow(CodexWindow);

            CommandManager.AddHandler("/aarena", new CommandInfo(OnCommand) { HelpMessage = "Opens the Aetherial Arena main window." });
            CommandManager.AddHandler("/aadebug", new CommandInfo(OnDebugCommand) { HelpMessage = "Opens the Aetherial Arena debug window." });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

            Framework.Update += OnFrameworkUpdate;
            regenTimer.Start();
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

            if (regenTimer.Elapsed.TotalMinutes >= this.regenIntervalMinutes)
            {
                if (PlayerProfile.CurrentAether < PlayerProfile.MaxAether)
                {
                    PlayerProfile.CurrentAether++;
                    SaveManager.SaveProfile(PlayerProfile);
                    Log.Info($"Aether regenerated. Current: {PlayerProfile.CurrentAether}/{PlayerProfile.MaxAether}");
                }
                regenTimer.Restart();
            }
        }

        private void OnCommand(string command, string args) => TitleWindow.Toggle();
        private void OnDebugCommand(string command, string args) => DebugWindow.Toggle();
        private void DrawUI() => WindowSystem.Draw();
        private void OnOpenConfigUi() => ConfigWindow.Toggle();
        private void OnOpenMainUi() => TitleWindow.Toggle();
    }
}
