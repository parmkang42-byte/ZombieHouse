# Working on Zombie House

Operating notes for Claude. `README.md` covers what the game *is* and why it is designed the
way it is — read it for intent. This file is the shorter, duller document: the things that
will silently break if you do not know them.

Unity **6000.5.8f1**, Built-in Render Pipeline, C#. Every line that runs in the game is C#;
the Python under `Tools_*` is offline build tooling that never ships.

---

## Rules that are load-bearing

**Never move this project into OneDrive.** Unity rewrites `Library/` constantly, OneDrive
locks those files mid-write, and the project comes back corrupted. It lives at
`C:\Users\<user>\UnityProjects\ZombieHouse` deliberately. This applies to clones on other
machines too — GitHub Desktop defaults to `Documents\GitHub`, which is usually inside
OneDrive. Change the path.

**No imported art or audio, by design.** Sounds are synthesised in `Audio/ProceduralAudio`
and `Audio/SoundBank`; textures are generated in `Fx/ProtoTextures`; creatures are primitives
animated procedurally by `ZombieVisuals`. Blender is used as a *headless mesh compiler* — the
`.py` in `Tools_Props/` is the source, the `.obj` in `Assets/Resources/Props/` is a committed
build artifact. Preserve this unless the user says they have assets to import.

**The NavMesh baker bakes Default-layer colliders only.** Everything about props and doors
depends on this. Doors sit on their own `Door` layer precisely so a closed door does not bake
as a wall and seal a room.

**Changing what a prop *draws* must never change what it *collides* with.**
`PropLibrary.Dress` swaps the mesh and must not touch the transform (the transform carries the
collider). `PropLibrary.Overlay` adds a collider-free child and hides the boxes it replaces.
Use Overlay when the prop is several boxes, or when the mesh is bigger than the box carrying
collision. A missing prop mesh is a deliberate no-op that leaves the boxes visible.

**Only ever append to the `Sfx` enum — and to `ZombieKind`.** `LevelDirector.bossKind` and
`ZombieProfile.kind` are stored in scenes and prefabs as `enumValueIndex` too, so inserting a
creature renumbers every kind after it and silently changes which boss a level spawns.

**A new level must be added to the cross-level test lists**, or it is simply skipped and its
checks pass by not running. There are three: the level table in `Test Boss`, the one in
`Test Safe Start`, and the bed table in `Test Music`. `Test Music`'s summary line names a
count ("seven distinct beds") — update it, because a stale count is the tell that a level was
added to the enum and not to the table.

**Only ever append to the `Sfx` enum.** `GameAudio.musicTrack` is stored in a scene as an
`enumValueIndex`, so removing or inserting a member silently changes which music bed an
unrebuilt level plays. Rebuild all six levels after any `Sfx` edit.

**Every prop mesh is normalised to a 1×1×1 box at the origin**, because generators size props
through `localScale` on a unit cube. `Test Props` enforces it, and so does `verify_export` in
`_propkit.py`.

---

## The NavMesh counts are the canary

Diff these after *any* change to props, doors, or level geometry. They must not move unless
you intended to move them.

| level | vertices | triangles |
|---|---|---|
| house | 1924 | 866 |
| forest | 13381 | 5969 |
| town | 4405 | 1925 |
| school | 1487 | 683 |
| pyramid | 2265 | 1009 |
| jungle | 12838 | 5732 |
| merryland | 1846 | 824 |

A prop change that moves these has changed where things can walk, which is a bug even when
the level still verifies.

The town's numbers jumped on 2026-09-04 when its buildings stopped being solid blocks and
became enterable shells. That was the intended change, and it is the only time so far these
have been allowed to move.

**Bosses must fit the room they fight in.** A humanoid is about 1.75 m of height per unit of
`Scale`, so the house (4.2 m ceiling) caps its giant near 2.2x and the school (3.4 m) near
1.75x. `Test Boss` measures this on a real built body rather than computing it. Boss size is
authored, not rolled — `ZombieAppearance` skips the 0.92-1.07 crowd-variety spread for any
archetype with `Weight == 0`, because a boss that is a different height every run puts its
head through the ceiling on some runs and not others.

---

## Running Unity headlessly

**Only one Unity instance may hold the project lock.** If the user has the Editor open, a
batch run aborts with "another Unity instance is running". Never kill their Editor. Instead
copy `Assets`, `Packages` and `ProjectSettings` into a temp directory and run there.

```bash
# Stage a verify copy (fresh directory each time — see the warning below)
robocopy "<project>\Assets"          "<stage>\Assets"          /E /NFL /NDL /NJH /NJS /NP
robocopy "<project>\Packages"        "<stage>\Packages"        /E /NFL /NDL /NJH /NJS /NP
robocopy "<project>\ProjectSettings" "<stage>\ProjectSettings" /E /NFL /NDL /NJH /NJS /NP
```

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" \
  -projectPath "<stage>" -batchmode -nographics -quit \
  -executeMethod ZombieHouse.EditorTools.ZombieHouseSetup.VerifyLevel \
  -logFile "<stage>/verify.log"
```

**Copying results back from a stage means `ProjectSettings/` too, not just `Assets/`.**
`AddSceneToBuildSettings` writes `ProjectSettings/EditorBuildSettings.asset`, so a level built
in a stage is registered *in the stage*. Copy back only `Assets/` and the new scene exists,
verifies, and is absent from the build — it plays from the editor and is missing from a player
build, which nothing in the test suite looks at. Level 7 shipped this way for one commit.
Check with `grep -oE "Assets/Scenes/[A-Za-z0-9_]+\.unity" ProjectSettings/EditorBuildSettings.asset`.

**Never `rm -rf Assets` inside an existing stage and re-copy.** It corrupts that copy's asset
database and it comes back with ~69 phantom `UnityEngine.UI does not exist` errors in
`HudController` that no code change fixes. Stage a whole new directory, or mirror over the
existing `Assets` without deleting first. Reusing a stale `Library/` causes the same thing.

Pixel and GPU tests need `-batchmode` **without** `-nographics`.

### Methods worth knowing

`BuildLevel1Automated` … `BuildLevel7Automated` rebuild a level's scene.
`VerifyLevel`, `VerifyForest`, `VerifyTown`, `VerifySchool`, `VerifyPyramid`, `VerifyJungle`,
`VerifyMerryland`
bake the NavMesh and prove every spawn and the exit are reachable.

Tests: `TestProps`, `TestBoss`, `TestDread`, `TestMerryland`, `TestReload`, `TestSafeStart`, `TestMusic`, `TestGatling`,
`TestPostFx`, `TestSchool`, `TestPyramid`, `TestBear`, `TestPower`, `TestSurvivors`,
`TestRagdoll`, `TestFlashlight`, `TestRecoil`, `TestReloadSwitch`. All print PASS/FAIL.

---

## Testing discipline

This project has been burned repeatedly by tests that pass while the thing is broken. The
house style is therefore:

**Measure the invariant, not the wiring.** Asserting a component exists proves nothing — a
shader with a typo'd property name compiles, binds nothing, and renders a plausible frame.
`Test PostFx` pushes a real frame through and reads pixels back.

**Judge shape, not absolutes.** The first bloom test asserted `> 0.05` and failed a working
bloom at 0.047, because ACES crushes darks. Check falloff with distance instead.

**A test that does the player's job proves nothing.** `Test Gatling` used to reload by hand
the moment the magazine emptied, so it never took the dry-fire branch that was deadlocked in
real play. Drive the real entry point one frame at a time and assert on the outcome.

**Assert both ends of a range.** `Test Boss` checked only floors (health ≥ 1500, stagger ≥
0.85), which is how six *unwinnable* bosses passed a green suite. Enemies fail by being too
much as easily as too little.

**Prove a new test can fail** by breaking the thing it guards and watching it go red.

**A box on a grid must never be wider than the grid spacing.** This has now sealed three
separate things: the big top (panels of `radius*0.58` on a `chord` spacing, so removing one
for a doorway left an opening of nothing), the hedge maze (`cell + thickness` blocks on a
`cell` grid, so every corridor was squeezed out and the whole thing baked solid), and both
looked completely correct from above.

**Partition an open level with a perimeter ring, not with walls to the fence.** Merryland's
hedges stop ~27 m short of the boundary, so a continuous loop runs round the park and every
enclosure opens onto it — sealed pockets become impossible by construction rather than by
inspection. Three rounds of patching individual pockets preceded that, each fix revealing the
next; the pattern is why real parks and zoos have a loop path with the attractions inside it.

**Write the map, don't guess.** A ten-line throwaway that printed an ASCII map of reachable
cells found a level-connectivity bug that two rounds of plausible fixes had missed.

### Edit-mode gotchas

- `Awake` and `Start` do not run in edit mode. Give components a public `Initialise()` /
  `Configure*()` the test can call, and subscribe events from both.
- `Time.time` does not advance in a batch run. Use countdowns ticked by `Tick(deltaTime)`,
  never absolute timestamps.
- `Physics.SyncTransforms()` before reading `collider.bounds` — bounds come from the physics
  scene, and nothing steps physics in edit mode, so you will otherwise measure the old pose.
- `AmmoInMagazine` is an auto-property and is not serialised. Editor checks must use
  `MagazineSize + ReserveAmmo`, not `TotalAmmo`.
- Qualify `UnityEngine.Random` in any file that has `using System;`.

---

## Git conventions

Remote is a **private** GitHub repo. Branch `main`.

**Level rebuilds go in their own commit.** `BuildLevelNAutomated` rewrites roughly 100,000 of
a scene's 175,000 lines, so mixing a rebuild into a code commit buries the code change under
megabytes of regenerated YAML.

**Scenes are committed despite being generated**, because `PowerCellPlacement.Draw` uses
unseeded `UnityEngine.Random` — a rebuild is not reproducible, so the committed scene is the
canonical one.

**`Assets/Resources/Props/*.obj` is exempted from Git LFS** in `.gitattributes`. The generic
Unity template sends `*.obj` to LFS, which is right for imported models and wrong for these —
they are generated text and the point is that they diff.

**`.meta` files must travel with their assets.** Unity mints a GUID per asset into its
`.meta`, and scenes reference scripts by that GUID. Copying scenes back from a verify stage
while leaving a new script's `.meta` behind means this project mints a *different* GUID and
every scene referencing it shows "Missing (Mono Script)". Check with:

```bash
comm -23 <(grep -ohE "guid: [a-f0-9]{32}" Assets/Scenes/*.unity | cut -d' ' -f2 | sort -u) \
         <(grep -rhoE "guid: [a-f0-9]{32}" --include=*.meta Assets/ | cut -d' ' -f2 | sort -u)
```

Anything but Unity's two built-in sentinels (`0000…e000…`, `0000…f000…`) is dangling.

---

## The prop pipeline

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" \
  --background --python Tools_Props/furnishings.py
```

Blender is **not on PATH** — call it by full path, and do not trust `Get-Command blender` to
tell you whether it exists.

- **Blender Z → Unity Y, Blender Y → Unity Z.** Measured, not assumed. `finish(..., lay_along=)`
  handles it; `"y"` for upright things, `"z"` for things that lie down.
- **Bevel runs after decimation, never before.** The decimator collapses lowest-error geometry
  first and a bevel is exactly that, so bevelling first spends the triangles and leaves the
  edges sharp.
- **`furniture.py` and `furnishings.py` pass `bevelled=False`** because `block()` already
  bevels every box. Running both tripled the staircase to 3,122 triangles against a 1,500
  budget.
- **Decimation counts polygons; the exporter triangulates**, so Unity sees roughly double.

---

## A new creature

Add its materials to `CreatePlaceholderMaterials` in `ZombieHouseSetup`, **not just to
`ProtoMaterials`**. `ProtoMaterials.Get` silently falls back to an in-memory material when the
`.mat` asset is missing, and an in-memory material does not survive being saved into a prefab
— the prefab ships with a dangling reference and renders **magenta**. The jungle's snakes,
jaguars and monkeys all shipped pink this way.

Quadrupeds reuse the humanoid bone names on purpose (front legs are "arms") so `ZombieRagdoll`
and `ZombieDismemberment` work unchanged. Even the snake gets four vestigial leg stubs buried
inside its body.
