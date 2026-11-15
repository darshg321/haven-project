# Functions

What every function does, by file. Update this when a function is added, removed or changes job.

## Game.cs
- `Start` picks the relay address (public relay on https pages, otherwise the page host) and starts preloading sites.
- `OnDestroy` closes the online connection.
- `Preload` downloads the three site bundles from StreamingAssets one by one, tracking progress and errors.
- `StartMatch` waits for the chosen site, builds the map (and the NavMesh for bot games), sets up rounds, spawns you, the bot or the online connection, and the HUD.
- `Fighters` lists everyone in the match.
- `Enemy` returns the bot, or the online player with the lowest id.
- `SpawnFighter` spawns a Pokemon you control (model, CharacterController sized to the model, Motor, Fighter, aim point) on a free spot.
- `SpawnRemote` spawns another online player's Pokemon, which only follows network updates.
- `AttachCamera` makes the camera first person on your Pokemon, hides your own model and adds the hands.
- `OnNewRound` revives and moves the fighters this machine owns to their spawns and clears projectiles.
- `Update` runs one frame: network, rounds (if host), movement locks, your input, the bot and the HUD status.
- `HandleInput` mouse look, WASD/space/shift/ctrl movement, mouse lock, firing and the C/Q/E/X abilities.
- `Solid` a 1x1 texture of one colour for menu backgrounds.
- `MakeStyles` the start screen's panel, label, button, selected and Fight styles.
- `Choice` a row of buttons where one is selected.
- `OnGUI` the start screen: Pokemon, opponent, server, site and the Fight/Join button.

## Map.cs
- `Area` the x/z rectangle of a site inside the full Haven map.
- `Map` (constructor) places the site, brightens its materials, works out the floor bounds and builds the walls.
- `Brighten` swaps in Editor shaders, raises a material's saturation a bit and keeps it matte.
- `BuildWalls` / `Wall` invisible box walls around the site.
- `BuildNavMesh` bakes a NavMesh from the site's colliders for the bot.
- `FindSpawn` a random floor spot with room for a capsule.
- `FindSpawnPair` two reachable spots about 24 m apart for round starts.
- `Connected` whether the NavMesh has a full path between two points.
- `Blocked` whether a capsule standing at a point would touch the map.

## Fighter.cs
- `OnEnable` / `OnDisable` keep the `All` list of fighters.
- `Init` sets id, ownership and stats, puts the model on the fighter layer and resets it.
- `Update` refills ability charges over time.
- `Move` the move in a slot (`Basic` = the left click move).
- `Cooldown01` how much of a slot's cooldown is left (0 = ready).
- `TryFire` fires the basic move if it's off cooldown.
- `TryAbility` uses an ability if it has a charge (or the ult if it's full).
- `Use` fires a move toward the crosshair, starts its recharge lock (Hyper Beam) and tells the network.
- `Launch` plays the attack animation and sound, then spawns Double Team clones or the move's projectiles in a fan.
- `AimPoint` the first thing a ray hits, map or fighter.
- `SegmentHit` whether a moving sphere touches this fighter's capsule hurtbox.
- `ClosestPoints` closest points between two line segments.
- `ReportHit` on the shooter's machine: charges the shooter's ult, raises `LandedHit` and applies damage if this machine owns the target.
- `TakeDamage` loses HP, plays the hit or faint animation.
- `SetHp` sets a remote player's HP from the network.
- `Faint` locks movement and plays the faint animation.
- `Revive` full HP and charges, back to the idle stance.

## Motor.cs
- `Awake` grabs the CharacterController and animator.
- `Teleport` moves the body somewhere and resets its motion.
- `Tick` one frame of movement from an input, plus ceiling bumps and falling off the map.
- `Greninja` fast running, high jump and a double jump.
- `Charizard` slow walking; double tap space to fly, WASD in the air, gravity pulls down, hold space to climb (costs more stamina), ctrl to dive.

## Bot.cs
- `Start` sets up the path and a random aim wobble.
- `Think` decides the bot's movement, aim and whether to fire this frame.
- `NewStrafe` picks a strafe direction for a while.
- `ToStick` turns a world direction into a movement stick.
- `Flat` drops the y of a vector.
- `Walkable` whether the NavMesh continues in a direction.
- `PathDir` direction along the NavMesh path to a goal.
- `WatchShots` plans a sidestep when a shot is about to pass close.

## Projectile.cs
- `OnEnable` / `OnDisable` keep the `All` list.
- `Init` sets owner, move, velocity and whether hits count.
- `Update` moves the shot (homing if the move has it) and checks for fighters and walls.
- `Home` turns the shot toward the nearest enemy (Swift).
- `Finish` impact effect and sound, reports the hit and fades the trail out.

## Rounds.cs
- `Wins` round wins of a fighter.
- `SpawnOf` where a fighter starts this round.
- `ForgetSpawns` picks new spawn spots next round (after someone leaves).
- `HostTick` the round loop: waiting, countdown, fight, round over.
- `End` ends the round with a winner and reason.
- `StartRound` next round, swapping sides.
- `Apply` copies the host's state from JSON.

## Online.cs
- `Online` (constructor) connects to the relay and listens for shots and hits.
- `Dispose` stops listening and closes the connection.
- `Status` connection status text.
- `Send` sends a message tagged with your id and site.
- `OnFired` tells others about your shot.
- `OnLandedHit` tells another player your shot hit them.
- `Update` smooths other players, sends your state and (as host) the rounds.
- `Receive` handles hello, bye, state, fire, hit and rounds messages.
- `Drop` removes a player who left or changed site.

## Relay.cs
- `Relay` (constructor) opens the WebSocket (browser one in WebGL, ClientWebSocket elsewhere).
- `Run` connects and queues incoming messages.
- `SendLoop` sends queued messages on a background thread.
- `Send` queues a message.
- `TryReceive` takes the next received message.
- `Dispose` closes the socket.

## Hud.cs
- `Bind` builds the HUD for your fighter and listens for hits.
- `OnDestroy` stops listening.
- `SetStatus` the status line in the corner.
- `OnLandedHit` hitmarker, sound and a damage number.
- `OnDamaged` red flash when you get hit.
- `Build` creates the canvas: timer, score, HP panels, crosshair, banners, HP number, hint, click to play.
- `BuildAbilities` the four ability slots with icon, key, name, charge pips and ult ring.
- `UpdateAbilities` cooldown sweep, charges and ult fill.
- `ControlsHint` the controls line for your Pokemon.
- `MakeBar` one HP panel (left for you, right for the enemy).
- `NewImage` / `NewText` / `Filled` make UI elements.
- `Place` / `Stretch` position UI elements.
- `Update` refreshes HP, timer, score, hitmarker, flash, stamina and abilities.
- `UpdateBar` HP fill with a slow white chip behind it.
- `UpdateBanner` countdown, FIGHT, round won/lost banners and their sounds.
- `UpdateNumbers` floats damage numbers up and fades them.

## Hands.cs
- `Attach` cuts both hands out of your own Pokemon model and puts them on the camera.
- `OnDestroy` stops listening for shots.
- `OnFired` starts a punch with alternating hands.
- `Cut` bakes the posed model and keeps only the triangles skinned to one side's hand bones, as a mesh in front of the camera.
- `InHand` whether a bone is the hand bone (or a finger under it) on one side.
- `LateUpdate` walk bob and punch motion, hidden when fainted.

## Decoy.cs
- `Spawn` Double Team: copies of the model that run off to the sides and vanish.
- `Update` moves a copy.

## PokemonRoster.cs
- `Spawn` creates a Pokemon model at a height with an animator.
- `Fit` scales the model to a height and stands it on the root.
- `MeshBounds` world bounds of a posed model.

## PokemonAnimator.cs
- `Awake` finds the clips and sets which ones loop.
- `UpdateLocomotion` picks idle, walk, run, jump, fall, land or fly from movement.
- `Play` cross fades to a looping state.
- `PlayAction` plays a one-shot (attack, hit, roar...) and returns its length.
- `HoldAfterAction` stays in a state after the one-shot (fainted).
- `Resolve` the clip for a state, falling back when the model lacks it.
- `IsLooping` whether a clip loops.

## CombatConfig.cs
- `Get` the fighter settings for a species.

## Sfx.cs
- `TooSoon` skips a sound that already played in the last 0.08 s, so bursts don't stack.
- `At` plays a one-shot sound at a point, at half volume overall and quieter for your own throws.
- `Ui` plays a flat UI sound at half volume.

## Editor/SiteBundles.cs
- `BuildPrefabs` cuts the full map into one prefab per site with colliders and converted materials.
- `BuildBundlesWebGL` / `BuildBundles` builds the site asset bundles.
- `Convert` rebuilds a glTF material as opaque URP Lit (cutout, blended grime or glow where needed).
- `Tint` warms up stone, plaster and brick.
- `Compress` re-saves a texture at 512 px so it can be compressed.
- `Has` whether a name contains any of some words.
- `FileName` makes a name safe for a file.

## Editor/WebBuild.cs
- `Build` the itch.io WebGL build.

## Tools/relay_server.py
- `broadcast` sends a message to everyone except the sender.
- `handle` gives a new player an id, relays their messages and says bye when they leave.
- `NoCacheHandler` serves the web build without caching.
- `lan_ip` this machine's LAN address.
- `main` starts the web server and the relay.
