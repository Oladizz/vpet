using System.Windows;
using VPet.SmartPet.Core;

namespace VPet.SmartPet
{
    /// <summary>
    /// English-only settings window for the SmartPet plugin.
    /// All labels and help text are in plain English.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SmartPetPlugin _plugin;

        public SettingsWindow(SmartPetPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            LoadFromSettings();
        }

        private void LoadFromSettings()
        {
            var s = PluginSettings.Instance;
            ApiKeyBox.Text = s.GeminiApiKey;
            ModelBox.Text = s.GeminiModel;
            PetNameBox.Text = s.PetName;
            MaxRequestsBox.Text = s.MaxAiRequestsPerDay.ToString();
            VoiceEnabledBox.IsChecked = s.VoiceEnabled;
            WakeWordBox.Text = s.WakeWord;
            CommentIntervalBox.Text = s.UsageCommentIntervalSeconds.ToString();
            MinFocusBox.Text = s.MinimumFocusSeconds.ToString();
            Use3DBox.IsChecked = s.Use3DRenderer;
            ModelPathBox.Text = s.RendererModelPath;

            StatsText.Text = SmartPetPlugin.UsageTracker?.Report.SummaryText()
                ?? "Stats will appear once the tracker starts.";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var s = PluginSettings.Instance;

            if (int.TryParse(MaxRequestsBox.Text, out var maxReq) && maxReq > 0)
                s.MaxAiRequestsPerDay = maxReq;
            if (int.TryParse(CommentIntervalBox.Text, out var interval) && interval >= 30)
                s.UsageCommentIntervalSeconds = interval;
            if (int.TryParse(MinFocusBox.Text, out var minFocus) && minFocus >= 0)
                s.MinimumFocusSeconds = minFocus;

            s.GeminiApiKey = ApiKeyBox.Text.Trim();
            s.GeminiModel = ModelBox.Text.Trim();
            s.PetName = string.IsNullOrWhiteSpace(PetNameBox.Text.Trim()) ? "Buddy" : PetNameBox.Text.Trim();
            s.VoiceEnabled = VoiceEnabledBox.IsChecked == true;
            s.WakeWord = string.IsNullOrWhiteSpace(WakeWordBox.Text.Trim()) ? "hey buddy" : WakeWordBox.Text.Trim();
            s.Use3DRenderer = Use3DBox.IsChecked == true;
            s.RendererModelPath = ModelPathBox.Text.Trim();

            s.Save();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
