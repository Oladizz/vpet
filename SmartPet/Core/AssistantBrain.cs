using System.IO;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VPet.SmartPet.Behavior;
using GenerativeAI.Models;
using GenerativeAI.Types;

namespace VPet.SmartPet.Core
{
    /// <summary>
    /// The pet's brain. Answers voice and text requests with a two-tier router:
    ///
    ///   1. LOCAL COMMANDS (free, offline, instant)
    ///      Commands the pet understands by itself: come here, sleep, wake,
    ///      stats, hide, dance, name, weather-style banter, etc.
    ///      These never touch the AI API — zero cost.
    ///
    ///   2. GEMINI AI (only when no local command matched)
    ///      Real conversations with the Gemini API, rate-limited by a daily
    ///      cap so API cost stays predictable. The pet's context (current app,
    ///      usage stats) is injected into the system prompt automatically.
    /// </summary>
    public class AssistantBrain
    {
        private readonly SmartPetPlugin _plugin;
        private int _requestsToday;
        private DateTime _requestDay = DateTime.UtcNow.Date;

        public AssistantBrain(SmartPetPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>
        /// Process a user request. Returns the pet's reply (English).
        /// Local commands are matched first so common requests cost nothing.
        /// </summary>
        public async Task<string> ProcessRequestAsync(string text)
        {
            text = (text ?? "").Trim();
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var local = LocalCommandRouter.TryRoute(text, _plugin);
            if (local != null)
            {
                // Executed locally; reply is either a direct answer or the
                // command already changed pet state and needs no reply.
                return local.Reply ?? string.Empty;
            }

            return await AskGeminiAsync(text);
        }

        // ---------------- Gemini ----------------------------------------------

        /// <summary>
        /// Ask Gemini. Guarded by a daily request cap; if the cap or the API
        /// key is missing, the pet gives a friendly offline reply instead.
        /// </summary>
        public async Task<string> AskGeminiAsync(string userText)
        {
            var settings = PluginSettings.Instance;
            if (string.IsNullOrEmpty(settings.GeminiApiKey))
            {
                return "I don't have an AI brain yet! Open my settings and add your Gemini API key to chat with me. " +
                       "Meanwhile I can still do local things — try \"come here\", \"sleep\", \"hide\", \"dance\", or \"stats\"!";
            }

            if (_requestDay != DateTime.UtcNow.Date)
            {
                _requestsToday = 0;
                _requestDay = DateTime.UtcNow.Date;
            }
            if (_requestsToday >= settings.MaxAiRequestsPerDay)
            {
                return "I've used up my chat quota for today to keep costs down. " +
                       "You can raise the limit in my settings. Try a local command in the meantime!";
            }

            string systemPrompt = BuildSystemPrompt();

            try
            {
                // Google_GenerativeAI v1.0 API: GenerativeModel(modelName, apiKey)
                // GenerateContentAsync(string) returns the reply text directly.
                var model = new GenerativeModel(settings.GeminiModel, settings.GeminiApiKey);

                var reply = await model.GenerateContentAsync(systemPrompt + "\n\nUser: " + userText);
                _requestsToday++;
                LogAiRequest(userText, reply ?? "");
                reply = (reply ?? "").Trim();
                if (string.IsNullOrEmpty(reply))
                    return "Sorry, I didn't catch that. Could you say it again?";
                return reply;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] Gemini request failed: {ex.Message}");
                return "My AI brain had a hiccup (" + FirstLine(ex.Message) + "). " +
                       "Want to try again, or ask me something I can do myself?";
            }
        }

        /// <summary>
        /// Builds the system prompt from live pet + user context so Gemini
        /// answers feel aware of what the user is doing. Offline-only data —
        /// only sent when an AI request is actually made.
        /// </summary>
        private string BuildSystemPrompt()
        {
            var tracker = SmartPetPlugin.UsageTracker;
            var report = tracker.Report;
            var top = report.MostUsedApp();
            var settings = PluginSettings.Instance;

            string appContext = string.IsNullOrEmpty(tracker.CurrentApp) || tracker.CurrentApp == "Desktop"
                ? "The user is currently not focused on any particular app."
                : $"The user is currently focused on: {tracker.CurrentApp} " +
                  $"(focused for {FormatDuration(tracker.CurrentSessionSeconds)} this session).";

            string statsContext = top.HasValue
                ? $"Most used app overall: {top.Value.Key} " +
                  $"(opened {top.Value.Value.OpenCount} times, " +
                  $"{FormatDuration(top.Value.Value.TotalFocusSeconds)} total focus time)."
                : "No usage statistics collected yet.";

            return
                $"You are {settings.PetName}, a friendly, playful AI desktop pet living on the user's screen. " +
                "You reply in short, casual English (1-3 sentences), like a cute companion, not an assistant. " +
                "Occasionally be cheeky or affectionate. Never use markdown formatting.\n" +
                $"--- Live context ---\n{appContext}\n{statsContext}\n" +
                "--- Commands you should NOT answer with text ---\n" +
                "If the user asks you to move, sleep, hide, dance, or show stats, reply confirming you did it " +
                "(the desktop actions are already handled on this side).";
        }

        private static string FormatDuration(long seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}h {span.Minutes}m"
                : $"{span.Minutes}m {span.Seconds}s";
        }

        private static string FirstLine(string s)
        {
            var idx = s.IndexOf('\n');
            return idx > 0 ? s[..idx] : s;
        }

        /// <summary>Append-only log of AI requests for debugging and cost auditing.</summary>
        private static void LogAiRequest(string request, string reply)
        {
            try
            {
                var entry = new { Time = DateTime.UtcNow, Request = request, Reply = reply };
                var line = JsonSerializer.Serialize(entry) + "\n";
                File.AppendAllText(PluginSettings.AILogPath, line);
            }
            catch
            {
                // Logging is best-effort.
            }
        }
    }

    /// <summary>
    /// Result of a local command match.
    /// </summary>
    public class LocalCommandResult
    {
        /// <summary>What the pet says back (may be empty if action alone suffices).</summary>
        public string? Reply { get; set; }
    }
}
