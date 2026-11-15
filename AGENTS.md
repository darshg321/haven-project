# HavenDuel: agent notes

- **Design source of truth:** `GAME.md`. Read it before you design or implement a feature, and update it when the design changes.
- **Unity project:** the repo root, Unity **6000.3.28f1 (6.3 LTS)** with URP. **Target:** WebGL, playable on itch.io.
- **Unity skills:** installed in `.agents/skills/`. Start with `unity-cli` and `unity-pipeline`.

## How to work with Unity here
- **Use the Unity CLI**, not hand-edited scene or asset files and not the MCP server. Always pass `--project-path .` (or set `UNITY_PROJECT_PATH=.` on each call).
- **Open and check the Editor:**
  - `unity open .`
  - `unity status --until-ready --project-path .`
  - `unity command editor set_auto_tick --enable true --project-path .` (keeps the Editor ticking when it isn't focused)
- **Edit loop:**
  1. Edit C# under `Assets/Scripts/`.
  2. `unity command script recompile --project-path .`
  3. Poll `unity command script recompile_status --project-path .`.
  4. Fix any errors it reports.
- **Scene and objects:** `unity command --query <term> --project-path .` finds a command. `unity command script eval '<C#>'` runs C# in the Editor.
- **Verify in Play mode:** follow `.agents/skills/unity-cli/references/playmode-verification-loop.md`, then capture the game view and read the console.
- **Tests:** `unity command test run --mode editor --project-path .`
- **Packages:** use the `unity-package-management` skill. Never hand-edit `Packages/manifest.json`.
- **Safe Mode:** if the Editor won't connect, it may have booted into Safe Mode because of compile errors. Run `unity status` to see them.

## Web build (itch.io)
- **Build:** `unity build . --target WebGL --execute-method WebBuild.BuildForItch -o builds/webgl --no-tail`. This takes about 6 minutes from cold, and the Editor must be closed for this project.
  - `Assets/Editor/WebBuild.cs` applies the itch settings: Gzip compression, Decompression Fallback, data caching. You can also run it from the menu: **HavenDuel → Build WebGL for itch.io**.
  - The game still needs a click-to-play screen, because pointer lock and audio need a user click.
- **Upload:** zip the *contents* of `builds/webgl` so that `index.html` sits at the root of the zip, then upload to itch.io as an HTML project. Never publish without the user's go-ahead.

## Rules
- Don't commit or push unless the user asks.
- Close any Editor you opened when you finish (`unity close .`), unless the user wants it left open.

## Fresh clone setup
- **Install git-lfs and run `git lfs pull`.** Without it, the `.glb` models and the site bundles are only pointer files.
- **Extract the site prefabs** with `tar xzf MapSource/site-data.tar.gz --skip-old-files`, which creates `Assets/Data/Sites` (gitignored). Their meshes need `haven_full.glb`, which isn't in the repo. So the game loads sites from the bundles in `StreamingAssets/sites` in both the Editor and the build, and in the Editor it swaps their WebGL shaders back to the Editor's own copies.
- **Package Manager:** if it timed out on a slow network (for example glTFast), every `.glb` imports as a plain file. Run `UnityEditor.PackageManager.Client.Resolve()`.

## Combat (Assets/Scripts/Combat)
- **Run HavenDuel → Build Combat Assets** after you change `Assets/Editor/CombatSetup.cs`. It rebuilds the particle materials and prefabs in `Assets/Art/Generated`, the Teko TMP font, and `Assets/Data/CombatConfig.asset`.
- **Assets come from the web, never generated.** List each new one in `Assets/Art/SOURCES.md`.
- **Testing without a human:** set `LanGame.debugAutopilot = true` before joining. A Rookie brain then plays your Pokémon against the bot, so you can watch rounds, hits and KOs from the CLI.

## LAN multiplayer test
- Scene `Assets/Scenes/LanTest.unity` (rebuild with **HavenDuel → Build LAN Test Scene**), first in the build list. Code in `Assets/Scripts/Net/`.
- Relay: `python3 Tools/lan_server.py` (WebSocket :7777, also serves `builds/webgl` on :8000). Players on the same wifi open `http://<host LAN ip>:8000`.
- ws:// only works from a page served over http, so this does not work from the https itch.io page.
