using System.Windows;
using VPet.SmartPet.Core;
using VPet.SmartPet.Behavior;
using VPet.SmartPet.Rendering;
using VPet_Simulator.Windows.Interface;

namespace VPet.SmartPet
{
    /// <summary>
    /// SmartPet — the AI assistant plugin for VPet.
    /// Loaded by VPet's mod system because the class derives from MainPlugin.
    /// All features live in this plugin; the VPet core is never modified.
    /// </summary>
    public class SmartPetPlugin : MainPlugin
    {
        public override string PluginName => "SmartPet";

        // Shared subsystems ---------------------------------------------------
        public static AppUsageTracker UsageTracker { get; private set; } = null!;
        public static VoiceAssistant VoiceAssistant { get; private set; } = null!;
        public static AssistantBrain Brain { get; private set; } = null!;
        public static PetBehaviorEngine BehaviorEngine { get; private set; } = null!;
        public static IPetRenderer Renderer { get; private set; } = null!;

        /// <summary>
        /// Main window, cast from the base plugin reference for convenience.
        /// </summary>
        public static VPet_Simulator.Windows.MainWindow MainWin { get; private set; } = null!;

        public SmartPetPlugin(IMainWindow mainwin) : base(mainwin)
        {
            MainWin = (VPet_Simulator.Windows.MainWindow)mainwin;
        }

        /// <summary>
        /// Called by VPet once the game core and save data are ready.
        /// This is where we wire up every subsystem.
        /// </summary>
        public override void LoadPlugin()
        {
            // Settings live under %APPDATA%\VPet\SmartPet\settings.json
            PluginSettings.Instance.Load();

            UsageTracker = new AppUsageTracker();
            Brain = new AssistantBrain(this);
            BehaviorEngine = new PetBehaviorEngine(this);
            VoiceAssistant = new VoiceAssistant(this);
            Renderer = PetRendererFactory.Create(this);

            // Let the behavior engine add its own recurring actions to the
            // pet's random interaction loop (context-aware comments, etc.)
            BehaviorEngine.AttachToPet();

            // Start everything
            UsageTracker.Start();
            if (PluginSettings.Instance.VoiceEnabled)
                VoiceAssistant.Start();
        }

        /// <summary>
        /// Persist plugin data whenever VPet saves.
        /// </summary>
        public override void Save()
        {
            UsageTracker?.Persist();
            PluginSettings.Instance.Save();
        }

        /// <summary>
        /// Open the English settings window for this plugin.
        /// </summary>
        public override void Setting()
        {
            var win = new SettingsWindow(this) { Owner = MainWin };
            win.ShowDialog();
        }

        /// <summary>
        /// Cleanup when VPet shuts down.
        /// </summary>
        public override void EndGame()
        {
            VoiceAssistant?.Stop();
            UsageTracker?.Stop();
            Save();
        }
    }
}
