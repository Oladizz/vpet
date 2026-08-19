# SmartPet — AI Assistant Plugin for VPet

SmartPet is a **code plugin** for [VPet](https://github.com/LorisYounger/VPet) (the open-source Windows desktop pet). It turns the pet into an **interactive AI assistant** that knows about your computer: which apps you use, how often you open them, and how long you spend on them. The 2D sprite can be swapped for a 3D model later without touching any plugin logic.

Everything — code comments, settings, pet dialogue, and this documentation — is in **English**.

---

## What it does

### 1. Context-aware app usage tracking
The pet quietly watches which window is in the foreground (using the Windows `GetForegroundWindow` API, the same way the taskbar knows what you're doing). It keeps a running score of every app you use:

| Tracked data | What the pet does with it |
|---|---|
| How many times you opened each app | Quotes it back: "You opened VS Code 30 times today!" |
| Total focus time per app | Tells you your daily favorite and warns about marathon sessions |
| Current session length on the active app | "You've been on YouTube for 40 minutes..." |
| Daily rollover | Stats reset each day, history kept |

The data is saved in `%APPDATA%\VPet\SmartPet\usage.json`.

### 2. The pet lives on your desktop like a real pet
The **Pet Behavior Engine** makes the pet act on the desktop through VPet's own controller:

- **Come here** — jumps to your cursor position.
- **Wall crawling / edges** — climbs to the top edge, sits on the left/right edge, or lies on the bottom (taskbar side), just like the classic VPet wall behavior, but triggered by you or by the pet's own mood.
- **Lie down / sleep / wake up** — full sleep cycle on command.
- **Dance** — random idle animations for fun.
- **Hide / show** — hides behind the screen edge when you're busy.
- **Mood reactions** — responds happily when you chat with it.
- **Context comments** — on its own initiative (every ~5 minutes by default) the pet comments on your usage: "You've been in the same app for an hour, take a break!" — completely driven by the real usage data.

### 3. Voice commands (fully offline, zero API cost)
Built on the Windows `System.Speech` engine (no cloud needed). Say the wake word — default **"hey buddy"** (configurable) — then a command, or speak a command directly:

| Command examples | What happens | API cost |
|---|---|---|
| "come here" | Pet moves to your cursor | $0 |
| "go to sleep" / "wake up" | Sleep cycle | $0 |
| "dance" / "do a trick" | Random idle animation | $0 |
| "show my stats" / "how's my day" | Usage summary speech | $0 |
| "hide" / "come back" | Edge hiding | $0 |
| "what's my favorite app" | Best-app trivia | $0 |
| "what's my name" / "good boy" | Identity and social reactions | $0 |
| Anything else ("explain recursion") | Sent to Gemini AI | Capped |

Built-in **text-to-speech** makes the pet talk back. Speech recognition and TTS run entirely on your machine.

### 4. Gemini AI chat with smart cost saving
Questions that are not one of the built-in commands are forwarded to the **Gemini API** (Google AI). The plugin sends the pet's name, mood, and **your live app-usage context** in every prompt, so answers are personalized:

> "You've been in VS Code for two hours with 120 opens this week — sounds like a coding day! Here's how recursion works..."

Cost protection is built in:

- **Local command router first** — dozens of commands never touch the API.
- **Daily request cap** — default 100 AI requests per day (configurable in settings); after that, voice chat politely declines and suggests trying again tomorrow.
- **Every AI request is logged** to `%APPDATA%\VPet\SmartPet\ai_requests.json` with timestamp, prompt and reply, so you can audit exactly where your credits go.
- You choose the model (default `gemini-2.5-flash` — cheap and fast). Set your free Gemini API key once in the settings window.

---

## Install

1. Download or build **`SmartPet.dll`** (see Build below).
2. Open your VPet install folder and drop the DLL into:
   `VPet\mod\1000_smartpet\SmartPet.dll`
   (create the `1000_smartpet` folder if it doesn't exist; the number prefix makes the mod load first).
3. Start VPet. On the welcome screen VPet asks whether to trust unsigned mods — accept it.
4. Right-click the pet → the new **SmartPet Settings** entry appears. Paste your Gemini API key there (get one free at [Google AI Studio](https://aistudio.google.com/app/apikey)).
5. Talk to your pet!

## Settings

| Setting | Default | Meaning |
|---|---|---|
| Gemini API key | (empty) | Your free Gemini key; required only for AI chat |
| Gemini model | `gemini-2.5-flash` | Any Gemini model name |
| Pet name | Buddy | What the pet calls itself |
| Wake word | `hey buddy` | Starts a voice command |
| Voice enabled | on | Master switch for the microphone |
| Daily AI cap | 100 | Max AI requests per day |
| Context comment interval | 300 s | How often the pet volunteers a usage comment |
| Minimum focus seconds | 60 | Ignore focus shorter than this |
| Use 3D renderer | off | Switch to a 3D model (see below) |

## Swapping the 2D sprite for a 3D model later

The rendering is behind an interface (`IPetRenderer`) — this is the single seam for the 3D upgrade:

1. In SmartPet settings, enable **Use 3D renderer** and point it at a `.glb`/`.gltf` file.
2. The plugin's `Model3DRenderer` loads the model with SharpGLTF (already referenced), picks animation clips by semantic name (idle, sleep, move), and falls back to the 2D look if the model can't be loaded.
3. To go further (real viewport, transparency, click-through), extend `Model3DRenderer` — the pet's brain, voice, and tracking code never needs to change.

## Edit the code

The plugin is a standalone C# project (`SmartPet/SmartPet.csproj`). Nothing in VPet core is modified. To change behavior, edit only files inside `SmartPet/`, build, and replace the DLL in the mod folder.

| Folder | Purpose |
|---|---|
| `SmartPet/Core/` | `AppUsageTracker` (foreground tracking), `LocalCommandRouter` (offline commands), `AssistantBrain` (two-tier local + Gemini), `VoiceAssistant` (wake word, STT, TTS), `PluginSettings` |
| `SmartPet/Behavior/` | `PetBehaviorEngine` — movement, sleep, dance, hide, mood |
| `SmartPet/Rendering/` | `IPetRenderer`, `Sprite2DRenderer`, `Model3DRenderer`, factory |
| `SmartPet/SmartPetPlugin.cs` | Plugin entry point (inherits VPet's `MainPlugin`) |
| `SmartPet/SettingsWindow.xaml(.cs)` | English settings UI |

## Build

On Windows with .NET 8 SDK:

```powershell
dotnet build SmartPet/SmartPet.csproj -c Release -p:Platform=x64
copy SmartPet\bin\x64\Release\net8.0-windows\SmartPet.dll VPet-Simulator.Windows\bin\x64\Release\net8.0-windows\mod\1000_smartpet\
```

Or use the included GitHub Actions workflow (`.github/workflows/build.yml`) — it builds on a cloud Windows runner and produces a ready-to-run `VPet-SmartPet-win-x64.zip` containing VPet plus the plugin pre-installed.

## Notes

- Windows 10/11, .NET 8 runtime (VPet already installs it), and English (US) speech recognition are required for voice commands.
- All usage data stays on your machine; only Gemini chat prompts leave it, and only up to your daily cap.
- The offline command patterns are regex-based (`SmartPet/Core/LocalCommandRouter.cs`) — add your own phrases there with zero API cost.
