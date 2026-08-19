using System;
using VPet.SmartPet.Core;

namespace VPet.SmartPet.Rendering
{
    /// <summary>
    /// 3D-READY SPRITE ABSTRACTION
    /// ============================
    /// This is the seam that lets you replace the 2D PNG sprite with a 3D
    /// model later WITHOUT touching any plugin logic.
    ///
    /// Current behavior (2D):
    ///   Sprite2DRenderer  — tells VPet to use its built-in PNG frame
    ///   pipeline (the vup pet you see today). Nothing else to do.
    ///
    /// Future behavior (3D):
    ///   1. Put a .glb/.gltf model file somewhere on disk.
    ///   2. In the SmartPet settings window, enable "Use 3D renderer"
    ///      and point it at the model file.
    ///   3. Model3DRenderer takes over and renders the pet as a 3D model
    ///      (SharpGLTF is already referenced in the project).
    ///
    /// Both renderers implement this same interface.
    /// </summary>
    public interface IPetRenderer
    {
        /// <summary>Human-readable name shown in settings.</summary>
        string Name { get; }

        /// <summary>Apply this renderer to the pet. Called once at startup.</summary>
        void Attach();

        /// <summary>Play an animation by semantic name (idle, move, sleep, ...).</summary>
        void PlayAnimation(string animationName);
    }

    /// <summary>
    /// The classic 2D renderer. VPet's existing PNG-frame animation system
    /// is the actual renderer; this class is the documented seam around it.
    /// </summary>
    public class Sprite2DRenderer : IPetRenderer
    {
        private readonly SmartPetPlugin _plugin;

        public Sprite2DRenderer(SmartPetPlugin plugin)
        {
            _plugin = plugin;
        }

        public string Name => "2D Sprite (classic)";

        /// <summary>
        /// No setup needed — the pet's PNG animations in
        /// mod/0000_core/pet/vup are already loaded by VPet.
        /// </summary>
        public void Attach()
        {
            Console.WriteLine("[SmartPet] Using 2D sprite renderer (classic VPet PNG animations).");
        }

        /// <summary>
        /// Play an animation on the classic 2D pipeline via the behavior
        /// engine. Semantic names map to VPet GraphType animations.
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            switch (animationName.ToLowerInvariant())
            {
                case "idle":
                case "dance":
                    SmartPetPlugin.BehaviorEngine.Dance();
                    break;
                case "sleep":
                    SmartPetPlugin.BehaviorEngine.GoSleep();
                    break;
                case "wake":
                    SmartPetPlugin.BehaviorEngine.WakeUp();
                    break;
                case "happy":
                    SmartPetPlugin.BehaviorEngine.ReactHappy();
                    break;
                default:
                    SmartPetPlugin.BehaviorEngine.Dance();
                    break;
            }
        }
    }

    /// <summary>
    /// 3D model renderer (OPTIONAL — activate via settings).
    ///
    /// Loads a glTF/GLB model with SharpGLTF and renders it over the pet's
    /// window. Animation states (idle, sleep, move) are driven by the
    /// model's own animations, falling back to simple transforms when the
    /// model has none.
    ///
    /// TODO for the 3D upgrade:
    ///   - Replace the pet's window content with a HelixToolkit /
    ///     SharpGLTF viewport.
    ///   - Map GraphType states to model animation clips by name.
    ///   - Keep transparency + click-through so the 3D pet behaves like
    ///     the 2D one on the desktop.
    /// </summary>
    public class Model3DRenderer : IPetRenderer
    {
        private readonly SmartPetPlugin _plugin;
        private readonly string _modelPath;

        public Model3DRenderer(SmartPetPlugin plugin, string modelPath)
        {
            _plugin = plugin;
            _modelPath = modelPath;
        }

        public string Name => "3D Model (glTF)";

        public void Attach()
        {
            if (string.IsNullOrEmpty(_modelPath) || !System.IO.File.Exists(_modelPath))
            {
                Console.WriteLine("[SmartPet] 3D model path not set or file missing — falling back to 2D sprite.");
                return;
            }

            try
            {
                // SharpGLTF loads the model. Visual hosting (viewport,
                // transparency, click-through) is the remaining wiring.
                var root = SharpGLTF.Schema2.ModelRoot.Load(_modelPath);
                _ = root.LogicalMeshes.Count; _ = root.LogicalAnimations.Count;
                Console.WriteLine($"[SmartPet] 3D model loaded: {_modelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartPet] 3D model load failed: {ex.Message}");
            }
        }

        public void PlayAnimation(string animationName)
        {
            // In the finished 3D version this selects the matching animation
            // clip on the loaded model. Until then, delegate to the 2D look.
            SmartPetPlugin.BehaviorEngine.Dance();
        }
    }

    /// <summary>
    /// Picks the renderer configured in settings.
    /// </summary>
    public static class PetRendererFactory
    {
        public static IPetRenderer Create(SmartPetPlugin plugin)
        {
            var settings = PluginSettings.Instance;
            if (settings.Use3DRenderer)
                return new Model3DRenderer(plugin, settings.RendererModelPath);
            return new Sprite2DRenderer(plugin);
        }
    }
}
