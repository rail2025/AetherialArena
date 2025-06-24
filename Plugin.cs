using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AetherialArena.Windows;
using AetherialArena.Core;
using AetherialArena.Services;
using AetherialArena.Models;

namespace AetherialArena
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Aetherial Arena";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!; // Added TextureProvider

        public readonly WindowSystem WindowSystem = new("AetherialArena");
        public readonly Configuration Configuration;
        public readonly BattleManager BattleManager;
        public readonly DataManager DataManager;
        public readonly SaveManager SaveManager;
        public readonly EncounterManager EncounterManager;
        public readonly AssetManager AssetManager; // Added AssetManager
        public readonly PlayerProfile PlayerProfile;

        public readonly MainWindow MainWindow;
        public readonly ConfigWindow ConfigWindow;
        public readonly DebugWindow DebugWindow;

        public Plugin()
        {
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);

            this.AssetManager = new AssetManager(); // Initialize AssetManager
            this.DataManager = new DataManager();
            this.SaveManager = new SaveManager();
            this.PlayerProfile = this.SaveManager.LoadProfile();
            this.BattleManager = new BattleManager();
            this.EncounterManager = new EncounterManager(this);

            this.MainWindow = new MainWindow(this);
            this.ConfigWindow = new ConfigWindow(this);
            this.DebugWindow = new DebugWindow(this);

            this.WindowSystem.AddWindow(MainWindow);
            this.WindowSystem.AddWindow(ConfigWindow);
            this.WindowSystem.AddWindow(DebugWindow);

            CommandManager.AddHandler("/aarena", new CommandInfo(OnCommand) { HelpMessage = "Opens the Aetherial Arena main window." });
            CommandManager.AddHandler("/aadebug", new CommandInfo(OnDebugCommand) { HelpMessage = "Opens the Aetherial Arena debug window." });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;
        }

        public void Dispose()
        {
            this.SaveManager.SaveProfile(this.PlayerProfile);
            this.AssetManager.Dispose(); // Dispose of assets
            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/aarena");
            CommandManager.RemoveHandler("/aadebug");
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        }

        private void OnCommand(string command, string args) => MainWindow.Toggle();
        private void OnDebugCommand(string command, string args) => DebugWindow.Toggle(); // Toggle debug window
        private void DrawUI() => WindowSystem.Draw();
        private void OnOpenConfigUi() => ConfigWindow.Toggle();
        private void OnOpenMainUi() => MainWindow.Toggle();
    }
}
