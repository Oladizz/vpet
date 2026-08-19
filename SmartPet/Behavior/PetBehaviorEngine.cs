using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using VPet.SmartPet.Rendering;
using VPet_Simulator.Core;
using static VPet_Simulator.Core.GraphInfo;
using VPet.SmartPet.Core;

namespace VPet.SmartPet.Behavior
{
    /// <summary>
    /// Edges / walls of the user's screen the pet can crawl along.
    /// </summary>
    public enum PetEdge { Left, Right, Top, Bottom, Corner }

    /// <summary>
    /// The pet's action layer.
    ///
    /// Translates "come here", "dance", "hide" ... into real VPet calls
    /// (Display animations, controller movement) and injects its own
    /// recurring context-aware behaviors into the pet's random interaction
    /// loop (usage comments, idle banter).
    ///
    /// Visuals always go through <see cref="SmartPetRenderer"/> so the 2D
    /// sprite pipeline can later be swapped for a 3D model without
    /// touching this behavior code.
    /// </summary>
    public class PetBehaviorEngine
    {
        private readonly SmartPetPlugin _plugin;
        private readonly DispatcherTimer _usageCommentTimer;
        private System.Timers.Timer? _followTimer;
        private bool _isFollowing;

        public PetBehaviorEngine(SmartPetPlugin plugin)
        {
            _plugin = plugin;
            _usageCommentTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(Math.Max(30, PluginSettings.Instance.UsageCommentIntervalSeconds))
            };
            _usageCommentTimer.Tick += UsageCommentTick;
        }

        /// <summary>
        /// Attach recurring behaviors to the pet's own timers so the pet
        /// naturally comments on usage and idles with personality.
        /// </summary>
        public void AttachToPet()
        {
            var main = _plugin.MW.Main;

            // Context-aware commentary inside the pet's existing
            // random-interaction loop — this costs nothing and piggy-backs
            // on VPet's own timing.
            main.RandomInteractionAction.Add(ContextCommentAction);
            main.RandomInteractionAction.Add(UsageInsightAction);

            // Periodic usage check for longer reports.
            _usageCommentTimer.Start();
        }

        /// <summary>
        /// Occasional short comment about what the user is doing right now.
        /// Returns true when it fires so VPet honors it in the random loop.
        /// </summary>
        private bool ContextCommentAction()
        {
            var app = SmartPetPlugin.UsageTracker.CurrentApp;
            if (string.IsNullOrEmpty(app) || app == "Desktop")
                return false;

            // ~15% chance each cycle so comments stay rare and cute.
            if (Function.Rnd.Next(100) >= 15)
                return false;

            var seconds = SmartPetPlugin.UsageTracker.CurrentSessionSeconds;
            string comment;
            if (seconds > 3600 * 2)
                comment = $"You've been staring at {app} for over 2 hours... don't forget to blink!";
            else if (seconds > 3600)
                comment = $"An hour in {app} already! I'd stretch if I had legs like yours.";
            else if (seconds > 600)
                comment = $"{app} again? You two are inseparable!";
            else
                comment = $"Back to {app}, huh? Okay okay, I'll hang around.";

            _plugin.MW.Main.Say(comment, force: true);
            return true;
        }

        /// <summary>
        /// Deeper insight: mentions the user's favorite app, only when the
        /// pet is idle and the random dice roll lands.
        /// </summary>
        private bool UsageInsightAction()
        {
            if (!_plugin.MW.Main.IsIdel)
                return false;
            if (Function.Rnd.Next(200) >= 3)
                return false;

            var top = SmartPetPlugin.UsageTracker.Report.MostUsedApp();
            if (!top.HasValue)
                return false;

            var stats = top.Value.Value;
            var hours = stats.TotalFocusSeconds / 3600.0;
            if (hours < 2)
                return false;

            _plugin.MW.Main.Say(
                $"Fun fact: {top.Value.Key} is your all-time favorite — " +
                $"{hours:0.#} hours of focus time total! I basically live there too.",
                force: true);
            return true;
        }

        private void UsageCommentTick(object? sender, EventArgs e)
        {
            try
            {
                SmartPetPlugin.UsageTracker.Report.RolloverDayIfNeeded();
            }
            catch { /* never disturb the main loop */ }
        }

        // ---------------- Public actions (called by commands / voice) ---------

        /// <summary>Teleport-crawl the pet to the mouse cursor position.</summary>
        public void MoveToCursor()
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var controller = _plugin.MW.Core.Controller!;
            controller.MoveWindows(
                (cursor.X - SmartPetPlugin.MainWin.ActualWidth / 2) / controller.ZoomRatio,
                (cursor.Y - SmartPetPlugin.MainWin.ActualHeight / 2) / controller.ZoomRatio);
        }

        /// <summary>Move the pet to a screen edge — the "wall crawling" feel.</summary>
        public void MoveToEdge(PetEdge edge)
        {
            var controller = _plugin.MW.Core.Controller!;
            var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            double x = 0, y = 0;
            switch (edge)
            {
                case PetEdge.Left:
                    x = -controller.GetWindowsDistanceLeft() / controller.ZoomRatio;
                    y = screen.Height / 2 - SmartPetPlugin.MainWin.ActualHeight / 2;
                    break;
                case PetEdge.Right:
                    x = screen.Width - controller.GetWindowsDistanceRight() / controller.ZoomRatio - SmartPetPlugin.MainWin.ActualWidth;
                    y = screen.Height / 2 - SmartPetPlugin.MainWin.ActualHeight / 2;
                    break;
                case PetEdge.Top:
                    y = -controller.GetWindowsDistanceUp() / controller.ZoomRatio;
                    x = screen.Width / 2 - SmartPetPlugin.MainWin.ActualWidth / 2;
                    break;
                case PetEdge.Bottom:
                    y = screen.Height - controller.GetWindowsDistanceDown() / controller.ZoomRatio - SmartPetPlugin.MainWin.ActualHeight;
                    x = screen.Width / 2 - SmartPetPlugin.MainWin.ActualWidth / 2;
                    break;
                case PetEdge.Corner:
                    x = -controller.GetWindowsDistanceLeft() / controller.ZoomRatio;
                    y = screen.Height - controller.GetWindowsDistanceDown() / controller.ZoomRatio - SmartPetPlugin.MainWin.ActualHeight;
                    break;
            }
            controller.MoveWindows(x, y);

            // If the pet has a climb animation, play it when going up.
            if (edge == PetEdge.Top)
                PlayIdelAnimation();
        }

        /// <summary>Attach the pet to the mouse with periodic nudges.</summary>
        public void SetFollowing(bool follow)
        {
            _isFollowing = follow;
            _followTimer?.Stop();
            if (follow)
            {
                _followTimer = new System.Timers.Timer(1500);
                _followTimer.Elapsed += (_, _) => MoveToCursor();
                _followTimer.Start();
            }
        }

        /// <summary>Play the pet's sleep animation.</summary>
        public void GoSleep() => _plugin.MW.Main.DisplaySleep(true);

        /// <summary>Wake the pet back to its normal state.</summary>
        public void WakeUp() => _plugin.MW.Main.DisplayToNomal();

        /// <summary>Hide the pet in the side panel (edge hiding mode).</summary>
        public void Hide()
        {
            var controller = _plugin.MW.Core.Controller!;
            // Push the pet off the left edge; VPet automatically plays the
            // side-hide animation when it detects the pet is at the edge.
            controller.MoveWindows(-10000, 0);
        }

        /// <summary>Bring the pet back from the side-hide panel.</summary>
        public void Show()
        {
            var controller = _plugin.MW.Core.Controller!;
            controller.ResetPosition();
        }

        /// <summary>Play a lively idle animation ("dance").</summary>
        public void Dance() => PlayIdelAnimation();

        /// <summary>Relax into the idle "lie down" variant if available.</summary>
        public void LieDown()
        {
            // Try the pet's idle-state-2 animations (usually squatting /
            // relaxed poses), fall back to a regular idle animation.
            var main = _plugin.MW.Main;
            var graph = main.Core.Graph!.FindGraph(null, AnimatType.Single, main.Core.Save!.Mode);
            _ = main.Core.Graph.FindName(GraphType.Idel);
            main.Display(GraphType.Idel, AnimatType.A_Start, LoopBIdel);
        }

        /// <summary>Happy reaction animation + speech.</summary>
        public void ReactHappy()
        {
            _plugin.MW.Main.SayRnd("Heehee~");
            PlayIdelAnimation();
        }

        /// <summary>Headpat reaction.</summary>
        public void ReactHeadpat()
        {
            var main = _plugin.MW.Main;
            if (main.Core.Graph!.FindName(GraphType.Touch_Head) != null)
                main.Display(GraphType.Touch_Head, AnimatType.A_Start, LoopBTouchHead);
            else
                PlayIdelAnimation();
        }

        /// <summary>Play a random idle animation if one exists.</summary>
        private void PlayIdelAnimation()
        {
            var main = _plugin.MW.Main;
            var name = main.Core.Graph!.FindName(GraphType.Idel);
            if (name != null)
                main.Display(name, AnimatType.A_Start, EndAction: (System.Action<string>)(_ => main.Display(name, AnimatType.B_Loop, EndAction: (System.Action<string>)null!)));
        }

        private void LoopBIdel(string _)
        {
            _plugin.MW.Main.Display(GraphType.Idel, AnimatType.B_Loop, EndAction: (System.Action<string>)null!);
        }
        private void LoopBTouchHead(string _)
        {
            _plugin.MW.Main.Display(GraphType.Touch_Head, AnimatType.B_Loop, EndAction: (System.Action<string>)null!);
        }
    }
}
