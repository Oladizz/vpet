using System;
using System.IO;
using System.Text.Json;

namespace VPet.SmartPet.Core
{
    /// <summary>
    /// Plugin-wide settings. Stored as JSON in %APPDATA%\VPet\SmartPet\.
    /// All UI text and defaults are in English.
    /// </summary>
    public class PluginSettings
    {
        public static readonly PluginSettings Instance = new();

        // ---- AI (Gemini) -----------------------------------------------------

        /// <summary>Gemini API key. Empty means AI chat is disabled.</summary>
        public string GeminiApiKey { get; set; } = string.Empty;

        /// <summary>Gemini model name. Flash models keep cost very low.</summary>
        public string GeminiModel { get; set; } = "gemini-2.5-flash";

        /// <summary>Name the pet uses for itself in AI conversations.</summary>
        public string PetName { get; set; } = "Buddy";

        // ---- Voice -----------------------------------------------------------

        public bool VoiceEnabled { get; set; } = true;
        public string VoiceCulture { get; set; } = "en-US";
        public string WakeWord { get; set; } = "hey buddy";
        public double VoiceSensitivity { get; set; } = 0.5;

        // ---- Behavior --------------------------------------------------------

        /// <summary>How often (seconds) the pet checks app usage to comment.</summary>
        public int UsageCommentIntervalSeconds { get; set; } = 300;

        /// <summary>Maximum daily AI chat requests — safety cap to protect your wallet.</summary>
        public int MaxAiRequestsPerDay { get; set; } = 100;

        /// <summary>Seconds of app focus before the pet starts caring about that session.</summary>
        public int MinimumFocusSeconds { get; set; } = 60;

        // ---- 3D renderer -----------------------------------------------------

        /// <summary>
        /// false = use the classic 2D PNG sprite (Sprite2DRenderer).
        /// true  = use the 3D model pipeline (IModel3DRenderer) when a
        ///         glTF model file exists at RendererModelPath.
        /// </summary>
        public bool Use3DRenderer { get; set; } = false;

        /// <summary>Path to the pet's .glb/.gltf model (only used when Use3DRenderer is true).</summary>
        public string RendererModelPath { get; set; } = string.Empty;

        // ---- Paths -----------------------------------------------------------

        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VPet", "SmartPet");

        public static string SettingsPath => Path.Combine(DataDir, "settings.json");
        public static string UsagePath => Path.Combine(DataDir, "usage.json");
        public static string AILogPath => Path.Combine(DataDir, "ai_requests.json");

        // ---- (De)serialization -------------------------------------------------

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var loaded = JsonSerializer.Deserialize<PluginSettings>(File.ReadAllText(SettingsPath));
                    if (loaded != null)
                        CopyFrom(loaded);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] Failed to load settings, using defaults: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] Failed to save settings: {ex.Message}");
            }
        }

        private void CopyFrom(PluginSettings other)
        {
            GeminiApiKey = other.GeminiApiKey;
            GeminiModel = other.GeminiModel;
            PetName = other.PetName;
            VoiceEnabled = other.VoiceEnabled;
            VoiceCulture = other.VoiceCulture;
            WakeWord = other.WakeWord;
            VoiceSensitivity = other.VoiceSensitivity;
            UsageCommentIntervalSeconds = other.UsageCommentIntervalSeconds;
            MaxAiRequestsPerDay = other.MaxAiRequestsPerDay;
            MinimumFocusSeconds = other.MinimumFocusSeconds;
            Use3DRenderer = other.Use3DRenderer;
            RendererModelPath = other.RendererModelPath;
        }
    }
}
