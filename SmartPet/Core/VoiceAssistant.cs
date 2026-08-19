using System;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VPet.SmartPet.Core
{
    /// <summary>
    /// Voice interface for the pet.
    ///
    ///   - Listens continuously for a wake word ("hey buddy" by default).
    ///   - After the wake word, captures the next phrase.
    ///   - Phrases go to the brain: local commands cost nothing; only
    ///     unmatched phrases reach the Gemini API.
    ///   - Replies are spoken aloud using offline Windows text-to-speech.
    ///
    /// Everything here is offline except Gemini calls made by the brain,
    /// so keeping common requests as local commands keeps your API bill
    /// close to zero.
    /// </summary>
    public class VoiceAssistant : IDisposable
    {
        private readonly SmartPetPlugin _plugin;
        private SpeechRecognitionEngine? _recognizer;
        private readonly SpeechSynthesizer _synthesizer = new();
        private bool _listening;          // wake word detected, waiting for phrase
        private bool _running;

        public VoiceAssistant(SmartPetPlugin plugin)
        {
            _plugin = plugin;
            _synthesizer.Rate = 1;
        }

        public void Start()
        {
            if (_running) return;
            try
            {
                var culture = new System.Globalization.CultureInfo(PluginSettings.Instance.VoiceCulture);
                _recognizer = new SpeechRecognitionEngine(culture);

                var grammar = new Grammar(new GrammarBuilder(new Choices(
                    // Wake word
                    new Choices(PluginSettings.Instance.WakeWord),
                    // Local commands — recognized even without the wake word,
                    // so the user can bark orders anytime (still offline).
                    new Choices(
                        "come here", "come to me", "follow me", "stop following",
                        "go left", "go right", "go to the top", "go to the bottom", "go to the corner",
                        "go sleep", "take a nap", "wake up", "get up", "stop sleeping",
                        "hide", "show yourself", "come out",
                        "dance", "lie down", "chill",
                        "stats", "status", "app stats", "what app am i using", "current app",
                        "what is your name", "who are you",
                        "good boy", "good girl", "i love you", "pet you", "headpat",
                        "hello", "hi there",
                        "turn off voice", "turn on voice", "stop listening", "start listening"))));
                grammar.Name = "SmartPetCommands";

                _recognizer.LoadGrammar(grammar);
                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SetInputToDefaultAudioDevice();

                // Run recognition on a background thread so the UI stays smooth.
                var thread = new System.Threading.Thread(() =>
                {
                    try { _recognizer.RecognizeAsync(RecognizeMode.Multiple); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SmartPet] Voice recognition failed to start: {ex.Message}");
                    }
                })
                { IsBackground = true };
                thread.Start();

                _running = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] Voice start failed (microphone may be unavailable): {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_running) return;
            try
            {
                _recognizer?.RecognizeAsyncCancel();
                _recognizer?.Dispose();
                _recognizer = null;
            }
            catch { /* best effort */ }
            _running = false;
            _listening = false;
        }

        /// <summary>
        /// Handles every recognized phrase. Wake word arms the listener;
        /// subsequent phrases are processed by the brain.
        /// </summary>
        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            var phrase = e.Result?.Text ?? "";
            if (string.IsNullOrWhiteSpace(phrase))
                return;

            // Arm listening on wake word.
            if (phrase.Contains(PluginSettings.Instance.WakeWord, StringComparison.OrdinalIgnoreCase))
            {
                _listening = true;
                _plugin.MW.Main.Say("I'm listening!", force: true);
                return;
            }

            // After wake word (or immediately for direct commands): route it.
            if (_listening)
            {
                _listening = false;
                _ = HandlePhraseAsync(phrase);
            }
            else if (IsDirectCommand(phrase))
            {
                // Some commands work without a wake word too.
                _ = HandlePhraseAsync(phrase);
            }
        }

        /// <summary>
        /// Commands that don't need the wake word (short imperative forms).
        /// </summary>
        private static bool IsDirectCommand(string phrase)
        {
            var lower = phrase.ToLowerInvariant();
            return lower.StartsWith("come") || lower.StartsWith("go ") ||
                   lower.StartsWith("hide") || lower.StartsWith("dance") ||
                   lower.StartsWith("sleep") || lower.StartsWith("wake") ||
                   lower.StartsWith("stats") || lower.StartsWith("pet you") ||
                   lower.StartsWith("good ") || lower.StartsWith("hi") ||
                   lower.StartsWith("hello");
        }

        private async Task HandlePhraseAsync(string phrase)
        {
            try
            {
                var reply = await SmartPetPlugin.Brain.ProcessRequestAsync(phrase);
                if (!string.IsNullOrEmpty(reply))
                    Speak(reply);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] Phrase handling failed: {ex.Message}");
            }
        }

        /// <summary>Speak a reply using offline Windows TTS.</summary>
        public void Speak(string text)
        {
            if (!_running && _plugin.MW.Dispatcher?.CheckAccess() != true)
                return;
            try
            {
                // Make sure speech happens on the UI dispatcher thread.
                if (_plugin.MW.Dispatcher.CheckAccess())
                    _synthesizer.SpeakAsync(text);
                else
                    _plugin.MW.Dispatcher.InvokeAsync(() => _synthesizer.SpeakAsync(text),
                        DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] TTS failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _synthesizer.Dispose();
        }
    }
}
