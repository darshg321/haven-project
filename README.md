# Description
A pokemon 1v1 valorant inspired FPS

# Assets
Haven Map: https://open3dlab.com/project/4a0d5de0-05ac-4db3-a53b-6555879bc29d/

Greninja: https://skfb.ly/pBn7s

Charizard: "Shiny Charizard (Official)" by Mariokart07, CC BY 4.0: https://sketchfab.com/3d-models/shiny-charizard-official-265ec4a94c664e5eb10a238c93c44e55

Sky: Poly Haven, "Kloofendal 48d Partly Cloudy (Pure Sky)", CC0: https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky

Font: Barlow Bold by Jeremy Tribby, SIL OFL 1.1: https://github.com/jpt/barlow (also on https://fonts.google.com/specimen/Barlow)

Ability icons: duiker101, "pokemon-type-svg-icons": https://github.com/duiker101/pokemon-type-svg-icons

Water shuriken texture: "Sharp shuriken" by Darkzaitzev, game-icons.net, CC BY 3.0: https://game-icons.net

Sound effects (all CC0, details in Assets/Art/SOURCES.md):
- Shuriken throw: Kenney, RPG Audio
- Shuriken hit and wall: "40 CC0 water splash / slime SFX" (OpenGameArt)
- Ember cast: Julien Matthey, "JM FX Fireball 01" (OpenGameArt)
- Ember hit and wall: Kenney, Sci-fi Sounds
- Hitmarker, countdown, fight, round win/lose, KO: Kenney, Interface Sounds

Pokemon cries: Pokemon Showdown

Ability circle: Kenney UI Pack (button_round_flat), CC0: https://kenney.nl/assets/ui-pack

# Running it
Open the project in Unity 6.3 and play `Assets/Scenes/LanHaven.unity`. Pick a Pokemon, a site and the bot, then Fight.

For online matches run the relay (`pip install websockets`, then `python3 Tools/relay_server.py`) and pick Online.
It also serves `builds/webgl` on port 8000, so people on the same wifi can open the game there.

WebGL build for itch.io: **HavenDuel > Build WebGL** (or `unity build . --target WebGL --execute-method WebBuild.Build -o builds/webgl`).
