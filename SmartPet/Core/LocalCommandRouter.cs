using System;
using System.Text.RegularExpressions;
using VPet.SmartPet.Behavior;

namespace VPet.SmartPet.Core
{
    /// <summary>
    /// Offline command router — the cost-saver.
    ///
    /// Matches spoken/written phrases to actions the pet performs itself,
    /// with zero AI API calls. Add new patterns here whenever you want a
    /// new free command.
    ///
    /// Supported command families (English, case-insensitive):
    ///   Movement : "come here", "come to me", "follow me", "go to the edge",
    ///              "go left", "go right", "go to the top", "go to the bottom",
    ///              "come to the corner"
    ///   State    : "go sleep", "sleep", "take a nap", "wake up", "get up",
    ///              "stop sleeping"
    ///   Pose     : "hide", "go hide", "show yourself", "come out",
    ///              "dance", "do a dance", "lie down", "chill"
    ///   Info     : "stats", "status", "how am i doing", "what am i doing",
    ///              "usage stats", "app stats", "tell me my stats",
    ///              "what app am i using", "what app is this"
    ///   Identity : "what is your name", "who are you", "your name is *",
    ///              "i name you *"
    ///   Social   : "good boy", "good girl", "i love you", "pet you",
    ///              "headpat", "pat pat", "hello", "hi there"
    ///   Control  : "turn off voice", "turn on voice", "stop listening",
    ///              "start listening"
    /// </summary>
    public static class LocalCommandRouter
    {
        /// <summary>
        /// Try to handle [text] as a local command. Returns a result when
        /// handled, null when nothing matched (caller should ask the AI).
        /// </summary>
        public static LocalCommandResult? TryRoute(string text, SmartPetPlugin plugin)
        {
            var lower = text.ToLowerInvariant();
            var t = LowerNoPunct(lower);

            // ---------- Movement ----------
            if (Match(t, @"come (here|to me|over|here please)"))
            {
                SmartPetPlugin.BehaviorEngine.MoveToCursor();
                return new LocalCommandResult { Reply = "On my way!" };
            }
            if (Match(t, @"(go |move )?(to the )?(left side|left)"))
            {
                SmartPetPlugin.BehaviorEngine.MoveToEdge(PetEdge.Left);
                return new LocalCommandResult { Reply = "Heading left!" };
            }
            if (Match(t, @"(go |move )?(to the )?(right side|right)"))
            {
                SmartPetPlugin.BehaviorEngine.MoveToEdge(PetEdge.Right);
                return new LocalCommandResult { Reply = "Heading right!" };
            }
            if (Match(t, @"(go |move )?(to the )?(top|ceiling)"))
            {
                SmartPetPlugin.BehaviorEngine.MoveToEdge(PetEdge.Top);
                return new LocalCommandResult { Reply = "Climbing up!" };
            }
            if (Match(t, @"(go |move )?(to the )?(bottom|floor)"))
            {
                SmartPetPlugin.BehaviorEngine.MoveToEdge(PetEdge.Bottom);
                return new LocalCommandResult { Reply = "Dropping down!" };
            }
            if (Match(t, @"(go |move )?(to a )?corner"))
            {
                SmartPetPlugin.BehaviorEngine.MoveToEdge(PetEdge.Corner);
                return new LocalCommandResult { Reply = "Corner spot it is!" };
            }
            if (Match(t, @"follow me"))
            {
                SmartPetPlugin.BehaviorEngine.SetFollowing(true);
                return new LocalCommandResult { Reply = "I'll stick with you!" };
            }
            if (Match(t, @"stop following"))
            {
                SmartPetPlugin.BehaviorEngine.SetFollowing(false);
                return new LocalCommandResult { Reply = "Okay, I'll wander free again." };
            }

            // ---------- State ----------
            if (Match(t, @"(go )?(to )?(sleep|bed|nap)|take a nap|nap time|night night"))
            {
                SmartPetPlugin.BehaviorEngine.GoSleep();
                return new LocalCommandResult { Reply = "Zzz... goodnight!" };
            }
            if (Match(t, @"wake (up)?|get up|stop sleeping|rise and shine"))
            {
                SmartPetPlugin.BehaviorEngine.WakeUp();
                return new LocalCommandResult { Reply = "Rise and shine!" };
            }

            // ---------- Pose ----------
            if (Match(t, @"hide|go into hiding|go hide"))
            {
                SmartPetPlugin.BehaviorEngine.Hide();
                return new LocalCommandResult { Reply = "*hides*" };
            }
            if (Match(t, @"show yourself|come out|stop hiding"))
            {
                SmartPetPlugin.BehaviorEngine.Show();
                return new LocalCommandResult { Reply = "Boo! I'm back!" };
            }
            if (Match(t, @"dance|do a dance|dance party"))
            {
                SmartPetPlugin.BehaviorEngine.Dance();
                return new LocalCommandResult { Reply = "Dance dance!" };
            }
            if (Match(t, @"lie down|lay down|chill|relax"))
            {
                SmartPetPlugin.BehaviorEngine.LieDown();
                return new LocalCommandResult { Reply = "Ahhh, comfy!" };
            }

            // ---------- Info ----------
            if (Match(t, @"stats|status|how am i doing|usage stats|app stats|tell me my stats|daily report"))
            {
                var summary = SmartPetPlugin.UsageTracker.Report.SummaryText();
                return new LocalCommandResult { Reply = "Here's your usage summary: " + summary };
            }
            if (Match(t, @"(what|which) app (am i (using|on)|is (this|open))|current app|what am i using"))
            {
                var app = SmartPetPlugin.UsageTracker.CurrentApp;
                return new LocalCommandResult
                {
                    Reply = app == "Desktop"
                        ? "Nothing in particular — you're just hanging out on the desktop with me!"
                        : $"You're focused on {app} right now."
                };
            }

            // ---------- Identity ----------
            if (Match(t, @"(what(is|'s) your name|who are you)"))
            {
                return new LocalCommandResult
                {
                    Reply = $"I'm {PluginSettings.Instance.PetName}, your AI desktop pet!"
                };
            }
            var nameMatch = Regex.Match(t, @"(?:your name is|i name you|i call you|you are now called)\s+(.+)");
            if (nameMatch.Success)
            {
                var newName = ToTitleCase(nameMatch.Groups[1].Value.Trim());
                if (!string.IsNullOrEmpty(newName))
                {
                    PluginSettings.Instance.PetName = newName;
                    PluginSettings.Instance.Save();
                    return new LocalCommandResult { Reply = $"I love it! From now on, I'm {newName}!" };
                }
            }

            // ---------- Social ----------
            if (Match(t, @"good (boy|girl)|good pet|well done"))
            {
                SmartPetPlugin.BehaviorEngine.ReactHappy();
                return new LocalCommandResult { Reply = "Hehe, thank you!" };
            }
            if (Match(t, @"i love you|love you"))
            {
                SmartPetPlugin.BehaviorEngine.ReactHappy();
                return new LocalCommandResult { Reply = "I love you too!" };
            }
            if (Match(t, @"(head)?pat|pet you|pet pat|stroke"))
            {
                SmartPetPlugin.BehaviorEngine.ReactHeadpat();
                return new LocalCommandResult { Reply = "Purrrr~ more please!" };
            }
            if (Match(t, @"^(hi|hello|hey|greetings|howdy)( there)?( buddy)?( pet)?"))
            {
                return new LocalCommandResult { Reply = $"Hey! I'm {PluginSettings.Instance.PetName}! Need anything?" };
            }

            // ---------- Control ----------
            if (Match(t, @"turn off voice|mute|stop listening|go deaf"))
            {
                PluginSettings.Instance.VoiceEnabled = false;
                SmartPetPlugin.VoiceAssistant.Stop();
                PluginSettings.Instance.Save();
                return new LocalCommandResult { Reply = "Voice off — I'll rest my ears." };
            }
            if (Match(t, @"turn on voice|unmute|start listening"))
            {
                PluginSettings.Instance.VoiceEnabled = true;
                SmartPetPlugin.VoiceAssistant.Start();
                PluginSettings.Instance.Save();
                return new LocalCommandResult { Reply = "Voice on — I'm all ears!" };
            }

            return null;
        }

        // ---------------- Helpers ----------------

        private static bool Match(string normalizedText, string pattern) =>
            Regex.IsMatch(normalizedText, @"\b" + pattern + @"\b");

        private static string LowerNoPunct(string s) =>
            Regex.Replace(s.ToLowerInvariant(), @"[^\w\s]", " ").Replace("  ", " ").Trim();

        private static string ToTitleCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..];
            return string.Join(' ', words);
        }
    }


}
