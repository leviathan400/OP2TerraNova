# OP2TerraNova

![Screenshot](https://images.outpostuniverse.org/OP2TerraNova.png)

Unity sandbox for recreating Outpost 2 functionality.


## Current state

- Main menu (Tutorials / Colony Games / About / Help / Quit) — working.
- Mission selection from `.opm` files — working.
- Game scene loads mission, map, sheets, tech tree, and initializes `GameState` — working.
- **Terrain map rendering — working.** Tilesets are extracted from `maps.vol`, decoded from OP2's PBMP chunk format, sliced into 32×32 sprites, and rendered as a tile grid behind the OP2 UI frame.
- **Camera scrolling — working.** WASD / arrow keys pan the camera (hold Shift for 2×). Camera clamps to map bounds. Native pixel zoom (32 screen px per source pixel), anchored to map top-left.
- Stubbed menu buttons: New Campaign, Multiplayer, Load Game, Game Preferences.

**Not yet implemented** (the natural next milestones):
- Unit / structure / vehicle sprites (the sprite atlas is already loaded into `AssetManager`, just needs a renderer).
- Minimap.
- Camera zoom levels.
- Clipping the map render to the inner viewport rectangle so it doesn't draw behind the right-side toolbar.
- Game loop, AI, mission triggers, daylight cycle, morale, research, combat.
- The rest of the game...


## Required OP2 data files

Place these in the `OP2\` folder alongside the application. All files come from a standard Outpost 2 install.

- `op2_art.prt` — sprite/animation metadata
- `OP2_ART.BMP` — 5390 sprite frames (units, structures, animations)
- `sound.vol` — sound effects archive
- `voices.vol` — voiceover archive
- `sheets.vol` — unit/building/weapon/starship/mine/morale balance tables
- `maps.vol` — `.map` files + terrain tile bitmaps (`wellNNNN.bmp`)
- `multitek.txt` — multiplayer tech tree
- `edentek.txt` — Eden campaign tech tree
- `ply_tek.txt` — Plymouth campaign tech tree
- `tutortek.txt` — tutorial tech tree


## Logging

Two log files are written next to the application (or in the project root, in the editor):

- **`startup.log`** — boot sequence, asset load, CWD changes. Captures every `Debug.Log*` call from app start. Source: [Assets/TerraNova/Scripts/StartupLogger.cs](Assets/TerraNova/Scripts/StartupLogger.cs).
- **`game.log`** — written when entering the Game scene. Captures mission details, map info, tileset enumeration, sheet load counts, player setup. Source: [Assets/TerraNova/Scripts/GameLogger.cs](Assets/TerraNova/Scripts/GameLogger.cs).

