# Handoff: haven_project LAN multiplayer, maps, itch

Latest user request (verbatim): "make it so that in the game start screen you can select your pokemon and you can select wihch site the map will be (have site c like the low poly one, site b with mid up to the end of the window, and site a"

Earlier pending: "upload to itch and run on two separate tabs" (NOT done). I told user: say "go" and I close Unity, build, upload to Pokeduel draft, test 2 tabs, reopen Unity (build needs Editor closed ~6+ min; user was viewing scene).

Rules: say "you", no name; no emoji; firefox-harness FH_PORT=9232 (headless profile copy logged in to itch as fizzyf15h); kill only PIDs I started, never pgrep -f strings in my own command; don't commit; unity CLI `--project-path .` in ~/projects/haven_project; keep replies short.

## Done (uncommitted)
- Greninja glb fix; LAN relay Tools/lan_server.py; Assets/Scripts/Net/{LanSocket,LanGame}.cs; jslib; Funnel wss://flyingfish-pc.tail988c5f.ts.net:8443 running (off: `tailscale funnel --https=8443 off` + kill lan_server PID).
- Maps: Assets/Models/Maps/havenmap_sitec.glb (low-poly Site C, floorPrefix "sols"), Assets/Models/Maps/haven_full.glb (from ~/Downloads/haven-open3dlab/Haven.blend via scratchpad/export.py: playable area, dropped plants/wires/etc, decimate 0.3 >3000 tris, 512px jpg; 1.3M tris, 122 MB; floors "Floor_*").
- Assets/Editor/LanSetup.cs: Build(mapPath, scenePath, floorPrefix); menu Build LAN Test Scene (LanTest.unity, Site C) and BuildHaven (LanHaven.unity, "Floor"); NoCollide list (Canopy, Leaves, Foliage, Banner, Outline...). Build list first scene = last built (LanHaven).
- LanGame: playerHeight 1.8, capsule radius from PokemonModel.MeshBounds clamp .3-.6, FindSpawn on floorPrefix meshes, BuildBounds edge walls from floor bounds, fallY respawn; mouse+keyboard only; Greninja agile (sprint 8.5, double jump, wall kick via nested WallProbe), Charizard flight (Space in air, Space climb, Ctrl dive, fatigue 8 s), Msg has f flag.

## In progress: site select
Unity-space bounds in haven_full (x,z):
- ABomb x -75..-48 z -112..-76; ALong x -104..-45 z -85..-53; Garden x -43..-4 z -72..-31
- CBomb x 9..37 z -109..-78; Courtyard x 4..5 z -72..-59
- Mid/Window/BSite name bounds are huge (whole map) -> need B site + mid window floor names; check Floor_* names containing B / Mid / Window individually.
Plan: one scene with both maps (Site C low poly object + full Haven), start menu picks Pokemon + Site (A, B, C). Site A/B = full map with invisible walls box around region; Site C = low-poly map. Add site to Msg (m) and ignore players on other sites. Then build WebGL & upload when user says go.
- 22:50 user: preload all 3 site bundles at start, instantiate one per round; make map look like Valorant (switching to Linear color space + URP volume ACES/bloom, warm sun, gradient ambient, procedural sky). Code: LanGame Preload coroutine, LanSetup lighting.
## 22:58 state
- Done: Preload of 3 bundles in LanGame.Start; Linear color space; LanSetup.Look() (sky, trilight, fog, ACES volume Assets/Data/HavenLook*.asset). Scene LanHaven rebuilt with LanSetup.Build(null,"Assets/Scenes/LanHaven.unity","Floor").
- Found washed look = glTF emissive (emissiveFactor white * emissiveTexture) + metallic 1. Fix in progress: SiteBundles.CompressedMaterial -> emissive black (except Light/Lamp/Emiss names), metallicFactor 0, roughnessFactor 1, metallicRoughnessTexture null; brighter ambient in Look(). Then: SiteBundles.BuildPrefabs(); SiteBundles.BuildBundles(); rebuild scene; screenshot Site A at (-58,3,-89).
- Still pending: itch build/upload/2-tab test when user says go.
## 23:10 latest user request (verbatim): "see how it doesnt look like the valorant map [screenshot vs real Valorant A site] fix it."
- Real look: tan plaster walls, light gray stone floors, pale hazy sky, soft shadows, glowing green crates.
- Done: SiteBundles.CompressedMaterial tints by family (Tile/Stone/Slate/Floor x1.55 warm, Plaster/Paint/Wall tan .98/.84/.66, Brick, Wood), emissive x2 only when emissiveTexture != baseColorTexture; Look(): sun 1.8, shadowStrength .55, pale sky (thickness 1.5, tint .62/.72/.82, exposure 1.35), brighter trilight, fog 35-180, saturation 6, postExposure .25. Bundles+scene rebuilt; screenshot Temp/lookC.png (Site C) to review next. Rebuild cmd: unity command script eval --timeout 2400 --code 'SiteBundles.BuildPrefabs(); SiteBundles.BuildBundles(); LanSetup.Build(null, "Assets/Scenes/LanHaven.unity", "Floor"); return "ok";' --project-path .
- 23:20 user: fix translucency, vibrant colors, shader issues, crates not filled (green glow panels), Site A includes def spawn + part of B. Plan: URP/Lit opaque materials in SiteBundles, emissive for glow panels, center-in-region cut, tighter AreaA.
- 23:30 done: URP Lit opaque rebuild in SiteBundles.CompressedMaterial (cutout for foliage, emission for RebelMetalCrates/Glow/Light/Screen), center-in-area cut margin 3, AreaA -105..-44 x -116..-50, saturation 18 contrast 10. Bundles A 27MB B 36MB C 31MB. Next: review Temp/lookA5.png (crate at -73,4,-101), report; user said 'fix everything make no mistakes'.
- 23:50: re-exported glb (drop only sky/vista/blockers/gravel/wires/cable; decimate only trees/foliage/rocks 0.35). Crates fixed (Meteor_0_BoxA/B were dropped before). Tint by texture name; Slate x1.7 but A floor (Tile_M1_BrickPatternSlate) still dark. Walk test: A,C 0 overlaps; B 4 of 8 runs overlaps=1 -> investigate. Bundles A 36 B 46 C 42 MB. Still pending: dark slate floors, B overlap, itch build+upload+2-tab test.

## Step A: Greninja vs Charizard fight (new branch)
- **Asked for:** step A of the plan, with Greninja and Charizard only. Use web assets and never generate any (licensing is handled later).
- **Done:**
  - Code in `Assets/Scripts/Combat/`:
    - `PokemonMotor`: movement, moved out of `LanGame`.
    - `Fighter`: HP, hurtbox and the basic move.
    - `Projectile`.
    - `RoundManager`: countdown, 120 s, KO or HP%, result screen, side swap.
    - `BotBrain`: the Rookie bot, using a runtime NavMesh.
    - `Hud`: uGUI and TMP.
    - `Sfx`.
  - `CombatConfig` holds the tunables.
  - The start screen has an Opponent choice: Bot Greninja, Bot Charizard, or Online.
- **Verified in Play mode:**
  - Bot against autopilot on Sites C and A: rounds, KOs, score and spawn swaps all work.
  - Online through `lan_server.py` with a Python stand-in client: hits, KO, the next round starting at full HP, and a host handover.
  - Fixed the desktop/Editor `LanSocket` send backlog, which had caused lag of more than 20 s.
- **Not done:**
  - A WebGL build and a test with two real browser tabs.
  - Balance: Greninja beats Charizard in Rookie-against-Rookie fights. Revisit it with humans, and again when PP lands in step B.
  - Next is step B (PP economy).
