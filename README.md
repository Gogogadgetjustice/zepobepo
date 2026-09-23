# Big Walk Doom — porting notes

This scaffold reuses the real Doom engine from Hawkbat's `ow-mod-doom` almost
verbatim, and replaces only the parts that were specific to Outer Wilds/OWML.

## What's unchanged from the original mod

- `UnityDoom/ManagedDoom/**` (everything except `Unity/`) — the actual id
  Tech 1 engine: WAD parsing, renderer, game logic, monsters, menus. Pure C#,
  no Unity or OWML dependency, copied byte-for-byte.
- `UnityDoom/ManagedDoom/Unity/*.cs` — the Unity-facing layer (renders to a
  `Texture2D` on a `Quad`, reads keyboard/mouse, plays audio via MeltySynth).
  These only call plain Unity APIs (`GameObject`, `Texture2D`, `Material`,
  `Input`), not OWML APIs, so they need **no logic changes** — just recompiling
  against Big Walk's IL2CPP interop assemblies instead of OW's Mono ones.
- `doom1.wad` / `TimGM6mb.sf2` / licenses — shareware IWAD and soundfont,
  freely redistributable, unchanged.

## What's new

Outer Wilds's `Doom.cs` and `DoomShipLogMode.cs` existed to hook into OWML's
`ModBehaviour` lifecycle and a specific ship-log-menu plugin API
(`ICustomShipLogModesAPI`). Big Walk has neither of those, so those two files
are replaced by:

- **`Plugin.cs`** — the BepInEx 6 IL2CPP entry point. Registers the two new
  component types with IL2CPP and hooks scene loads.
- **`DoomCheatTrigger.cs`** — sits near the couch/screen, checks proximity to
  the player, and buffers raw keystrokes the same way vanilla Doom itself
  detects `iddqd`/`idclip`/etc. When it sees `iddqd` while the player is in
  range, it spawns/toggles the overlay. This deliberately does **not** hook
  Big Walk's chat or UI systems — it reads hardware keyboard input directly,
  which is simpler and more true to how the cheat actually works in Doom.
- **`DoomOverlay.cs`** — thin wrapper around `UnityDoom` (unchanged from the
  original), handling open/close instead of ship-log enter/exit.

## Screen location — filled in

`DoomCheatTrigger.SpawnAtScreen()` now tries `GameObject.Find("SecondGoodbye_Curtain_front")`
first (assuming that's the actual screen surface, not `_back`), and falls back
to a plane fitted through the four logged world-space corners if that name
doesn't resolve at runtime. If the fallback quad ends up facing the wrong way
(you'll see Doom rendering backwards, or the trigger volume floating off in
the wrong direction), flip the sign on `normal` or `down` in that method — one
of those two orientation flips is basically guaranteed on the first try with
hand-fitted corner data.

`Couch x2` is worth keeping in mind for later: that's consistent with local
co-op (two players, two couches, one screen). For the local-client milestone
this doesn't matter — the trigger just checks distance to whatever
`GameObject.FindWithTag("Player")` returns. It becomes relevant once you're
thinking about whether *either* player typing the code should open Doom, or
only whoever's actually looking at the screen.

## Two things still open

1. **How to get the local player's `Transform`.** Still stubbed as
   `GameObject.FindWithTag("Player")` in `DoomCheatTrigger.cs`. `DebugTools`
   (F8, nearby-object scan) run while standing on the couch should show you
   the real player object's name, which tells you whether that tag exists or
   whether you need a singleton reference instead.
2. **Legacy Input Manager vs. the new Input System package.** Still open —
   once you've got the interact-button method name from `DebugTools`' F9
   scan, check whether its declaring assembly references
   `UnityEngine.InputSystem` or plain `UnityEngine.Input`. Whichever it is,
   that's almost certainly what the rest of the game uses too, so it settles
   this for `DoomCheatTrigger`'s keystroke buffer as well.

## Using `DebugTools.cs` for the credits-trigger reverse engineering

This is a separate, self-contained file (own `[BepInPlugin]`) — drop it into
its own tiny plugin project, or into this one, doesn't matter. Once you're
next to the couch in-game:

- **F8** (nearby scan) tells you the real player object name/tag, and
  confirms whether `Couch` and `SecondGoodbye_Curtain_front` are still active
  and named the same at that point in the game (sometimes objects get
  renamed/re-parented by cutscene logic).
- **F9** (toggle interact-call logging), then press whatever button starts
  the credits. This patches every method with "interact" in its name across
  every loaded assembly and logs each call with its declaring type — so
  whatever fires when you hit the peck-interact button will show up by name
  in the log immediately. Toggle F9 again afterward, since leaving it on
  logs *every* interact call in the game, not just this one.
- **F7 + F10**: look straight at the screen, hit F7 to raycast it, then F10
  to dump every component (and public field) on whatever F7 hit. If the
  credits are driven by, say, a `VideoPlayer` or a custom `CreditsSequence`
  component sitting right on that collider, this is the fastest way to see
  its field names (target texture, camera reference, whatever) without
  dnSpy at all.

## Build setup

1. Install the BepInEx 6 IL2CPP pack into your Big Walk install (already
   covered by the mods on Thunderstore), launch once so it generates
   `BepInEx/interop/*.dll`.
2. Point the `HintPath`s in `BigWalkDoom.csproj` at that `interop` folder.
3. Build, drop `BigWalkDoom.dll` (plus `doom1.wad`/`TimGM6mb.sf2` next to it)
   into `BepInEx/plugins/`.

## Multiplayer note

Big Walk is multiplayer. Nothing here syncs Doom over the network — it opens
locally for whoever types the cheat, like a private easter egg, not a shared
screen. If you want other players to see the Doom footage on the actual couch
screen, that's a much bigger job (streaming the render texture over the
network) — worth confirming that's even in scope before you go further.
