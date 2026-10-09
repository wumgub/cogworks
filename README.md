# Cogworks

A top-down 2D pixel-art factory game in the spirit of Satisfactory and Minecraft's Create mod: mine, smelt,
craft, research, and drive your machines with Create-style rotational power. Online co-op for up to 4 players.

Built with Unity 6.6 (6000.6.5f1), URP 2D, UI Toolkit, Netcode for GameObjects and Unity Multiplayer Services.

![Overview](Docs/Screenshots/overview.png)

## Playing

Open `Assets/_Game/Scenes/Main.unity` and press Play (or build a player). Press **F1** in game for the full guide.

| Input | Action |
|---|---|
| WASD / scroll | Move / zoom |
| 1–9, 0 | Pick a building (others: click it in the inventory) |
| R / Shift+R | Rotate |
| LMB | Place (drag for lines) · with no tool: open a building, or hold on ore to mine |
| RMB | Remove (refunds the building and its contents) |
| Q | Clear tool, or pipette the hovered building |
| E | Crank a hand crank |
| Tab / T | Inventory and crafting / research |
| F5 · Esc | Save · pause menu |

**Progression:** hand-mine → craft furnaces → smelt plates → drills powered by a hand crank or water wheel →
belts and inserters → science packs and labs → assemblers, splitters, underground belts, steam engines,
fast belts.

## Multiplayer

* **LAN / direct IP** works out of the box: one player clicks *Host LAN Game*, others *Join LAN* with the
  host's IP (port 7777; forward it for internet play).
* **Online by join code** uses Unity Relay and needs the project linked to Unity Cloud once (free):
  *Edit → Project Settings → Services*, sign in, and link or create a project. Then *Host Online Game*
  shows a code friends enter in *Join by Code*.

## Architecture

```
Assets/_Game/
  Sim/    Pure C# simulation, no UnityEngine references. Deterministic: integer math only, fixed 60 ticks/s.
          World/chunks/worldgen, buildings + per-kind logic, belts, kinetic networks, items, recipes,
          research, players, commands, snapshot serializer (saves + late join).
  Net/    Engine-free sessions: LocalSession, HostSession, ClientSession over an abstract transport.
  View/   MonoBehaviours that draw the sim and turn input into commands; HUD and panels (UI Toolkit).
  Game/   Composition: Netcode/Relay glue, avatars, menus, save slot, command-line launcher, demo scenario.
  Editor/ "Cogworks → Build Main Scene" rebuilds the scene and its assets from code.
  Tests/  EditMode tests for the sim and networking.
```

**Every change to the world is a command.** Single-player applies commands locally. In multiplayer the host
assigns each command to a tick and broadcasts every tick's commands; clients run a tick only once its
commands arrive, so all peers compute identical worlds. Late joiners receive a compressed snapshot. Peers
compare world hashes every second, and the host resends the world if a client ever drifts.

**Rotational power:** shafts connect along their axis, gearboxes and machines on all sides. A network runs at
its fastest active generator's RPM and stalls if consumers' stress (impact × RPM) exceeds the generators'
capacity.

## Testing

* EditMode tests (72): Unity Test Runner, or `unity command run_tests --mode EditMode --filter Cogworks`.
* Headless multiplayer soak test (build a Mac player first, into `Builds/Mac`):
  ```
  Cogworks -batchmode -nographics -cw-mode host   -cw-port 7777 -cw-autotest 45 -cw-log host.txt -cw-sandbox
  Cogworks -batchmode -nographics -cw-mode client -cw-ip 127.0.0.1 -cw-port 7777 -cw-autotest 45 -cw-log c1.txt
  ```
  Each process issues random commands of every kind and logs `tick hash` lines; matching hashes prove the
  peers stayed in lockstep.
* Visual check without a screen: `Cogworks -batchmode -cw-mode single -cw-demo -cw-shots <dir>` builds a
  small factory and saves screenshots of the world and each panel; `-cw-menushots <dir>` does the menus.

Performance: about 0.8 ms of sim time per tick with 10,000 items on 2,500 belts; world hash about 0.7 ms.

## Art

All sprites are original 16 px pixel art in a warm industrial palette (brass, copper, wood, iron), generated
by `Tools/art/generate.py` into `Assets/_Game/Art/Resources/Art` (`python3 Tools/art/generate.py`, needs
Pillow and numpy). The PNGs can be hand-edited too; re-running the script overwrites them. Terrain has
per-tile variants and soft borders built at runtime; machines animate while working, at a speed tied to
their network's RPM.

## Not yet built

Fluids/pipes, trains, music, and a dedicated-server mode. The architecture leaves room for all
of them: new mechanics are new building logic plus commands, and the sim already runs headless.
