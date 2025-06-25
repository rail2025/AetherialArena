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

        public readonly WindowSystem WindowSystem = new("AetherialArena");
        public readonly Configuration Configuration;
        public readonly BattleManager BattleManager;
        public readonly DataManager DataManager;
        public readonly SaveManager SaveManager;
        public readonly EncounterManager EncounterManager;
        public readonly AssetManager AssetManager;
        public readonly PlayerProfile PlayerProfile;
        public readonly BattleUIComponent BattleUIComponent;
        public readonly IPluginManifest PluginManifest;

        public readonly HubWindow HubWindow;
        public readonly TitleWindow TitleWindow;
        public readonly AboutWindow AboutWindow;
        public readonly MainWindow MainWindow;
        public readonly ConfigWindow ConfigWindow;
        public readonly DebugWindow DebugWindow;
        public readonly CollectionWindow CollectionWindow;

        private readonly Stopwatch regenTimer = new();
        private readonly double regenIntervalMinutes = 10;

        public Plugin()
        {
            this.PluginManifest = PluginInterface.Manifest;
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);

            this.AssetManager = new AssetManager();
            this.DataManager = new DataManager();
            this.SaveManager = new SaveManager();
            this.PlayerProfile = this.SaveManager.LoadProfile();
            this.BattleManager = new BattleManager(this);
            this.EncounterManager = new EncounterManager(this);
            this.BattleUIComponent = new BattleUIComponent(this.BattleManager, this.AssetManager);

            this.HubWindow = new HubWindow(this);
            this.TitleWindow = new TitleWindow(this);
            this.AboutWindow = new AboutWindow(this);
            this.MainWindow = new MainWindow(this, this.BattleUIComponent);
            this.ConfigWindow = new ConfigWindow(this);
            this.DebugWindow = new DebugWindow(this);
            this.CollectionWindow = new CollectionWindow(this);

            this.WindowSystem.AddWindow(HubWindow);
            this.WindowSystem.AddWindow(TitleWindow);
            this.WindowSystem.AddWindow(AboutWindow);
            this.WindowSystem.AddWindow(MainWindow);
            this.WindowSystem.AddWindow(ConfigWindow);
            this.WindowSystem.AddWindow(DebugWindow);
            this.WindowSystem.AddWindow(CollectionWindow);

            CommandManager.AddHandler("/aarena", new CommandInfo(OnCommand) { HelpMessage = "Opens the Aetherial Arena main window." });
            CommandManager.AddHandler("/aadebug", new CommandInfo(OnDebugCommand) { HelpMessage = "Opens the Aetherial Arena debug window." });
            CommandManager.AddHandler("/acollection", new CommandInfo(OnCollectionCommand) { HelpMessage = "Opens your sprite collection." });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

            Framework.Update += OnFrameworkUpdate;
            regenTimer.Start();
        }

        public void Dispose()
        {
            this.SaveManager.SaveProfile(this.PlayerProfile);
            this.AssetManager.Dispose();

            Framework.Update -= OnFrameworkUpdate;

            WindowSystem.RemoveAllWindows();

            CommandManager.RemoveHandler("/aarena");
            CommandManager.RemoveHandler("/aadebug");
            CommandManager.RemoveHandler("/acollection");

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (regenTimer.Elapsed.TotalMinutes >= regenIntervalMinutes)
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
        private void OnCollectionCommand(string command, string args) => CollectionWindow.Toggle();
        private void DrawUI() => WindowSystem.Draw();
        private void OnOpenConfigUi() => ConfigWindow.Toggle();
        private void OnOpenMainUi() => TitleWindow.Toggle();
    }
}
