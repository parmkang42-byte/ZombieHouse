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
`C:\Users\<user>\Dev\ZombieHouse` deliberately (it was under `UnityProjects`
until 2026-09-07; that copy is gone). This applies to clones on other
machines too — GitHub Desktop defaults to `Documents\GitHub`, which is usually inside
OneDrive. Change the path.

**No imported art or audio, by design.** Sounds are synthesised in `Audio/ProceduralAudio`
and `Audio/SoundBank`; textures are generated in `Fx/ProtoTextures`; creatures are primitives
animated procedurally by `ZombieVisuals`. Blender is used as a *headless mesh compiler* — the
`.py` in `Tools_Props/` is the source, the `.obj` in `Assets/Resources/Props/` is a committed
build artifact. Preserve this unless the user says they have assets to import.

**A generated body mesh replaces what a part *draws*, never what it *collides* with.**
Same rule as `PropLibrary.Dress`, one level in: `CreatePart` swaps the MeshFilter and leaves
the primitive's own collider alone, so the head is still the sphere carrying the 2.5x critical
and every limb is still its capsule. Nothing in `BodyMesh` ever exceeds radius 0.5 or |y| > 1
either — geometry outside the collider is something the player can see and cannot shoot, which
is why the skull is carved out of a 0.90 ball rather than added to a full-sized one.

`Test Body Meshes` checks all of that, and a hand-built mesh needs **UVs written explicitly** —
Unity's primitives come with them, yours does not, and a mesh without them samples the
generated skin at one texel and looks exactly like the texturing having failed.

**Zombie geometry is not baked into the scenes** (grep `m_Name: Thigh_L` — zero in all eight).
Bodies come from the prefabs at runtime, so a change to `ZombieFactory` or `BodyMesh` needs the
*prefabs* rebuilt, not the scenes: run all eight level builds in a stage and copy back only
`Assets/Prefabs/` and `Assets/Resources/ProtoMeshes/`. That avoids rewriting eight scenes'
worth of YAML for a change that does not touch them.

**The foot is parented to the knee, so it needs an ankle or it points like a ballerina.**
`ZombieVisuals.LevelFoot` gives the foot a world-space orientation instead of an inherited
one. Without it a bent knee drives the toe through the floor, and it was most of the residual
error in the first version of the IK solve.

**Never measure a sole from `Renderer.bounds`.** A bounding box is axis-aligned, so a tilted
foot reports a *taller* box — a 24 cm foot pitched twenty degrees is half again as deep. Feed
that back in as "how far the sole is below the pivot" and the target moves whenever the pose
does, so the solve chases a number it is itself changing. Use half the foot's own scale.

**The two-bone solve is 2D on purpose.** The rig only rotates hips and knees about X, so the
leg lives in the rig's YZ plane and the general 3D solve — pole vector, knee-direction
ambiguity — does not arise. What is left is the law of cosines twice, plus `_shinLean`: the
ankle sits 6 cm forward of the knee as well as below it, so the lower segment is not straight
and ignoring that tilts the shin about eight degrees.

**The project is in LINEAR colour space, and every ambient or fog colour must go through
`LevelLighting.SetAmbient` / `SetFogColor`.** Those colours are authored as sRGB and Unity
linearises them before use; in gamma it did not, so the number in the source *was* the light
contribution. Flipping the switch silently reinterprets all sixteen of them at once — an
ambient of 0.055 stops contributing 0.055 and starts contributing 0.0045, twelve times less.
`AsAuthored` re-encodes so the authored number keeps meaning what it says, and is a no-op
under gamma, which is what makes the switch reversible. `Test Colour Space` caught exactly
this failure on 7 of 8 levels before it shipped.

Ambient lives in the **scene**, so a change to `ConfigureLighting` needs the levels rebuilt —
the same trap as the house lamps. Authored contributions currently run 0.043–0.091, except the
house at 0.251.

**Nothing headless can tell you how the game LOOKS.** Two batch-mode renders of the same scene
came back three times apart from each other, so there is no reliable automated exposure
measurement here; a `MeasureExposure` probe was written, found to be unrepeatable, and deleted
rather than left around to mislead. Known outstanding: **emissives were not re-encoded**, so
every glowing thing (eyes, flames, sight dots — the authored 1.5–2.8 range) is roughly 2.5x
hotter under linear and will bloom harder. That is deliberate — the `.mat` assets already
exist and `CreateMaterial` skips existing ones, so a code-only fix would change half the
picture. Tune it with eyes on the screen.

**A level's own lights cast shadows; anything attached to a creature, pickup or NPC does
not.** `LevelLighting.MakeRoomLight` is the one place that decides, and `Test Shadows`
enforces both halves. Every light in the game used to be created with `LightShadows.None`,
which meant zombies cast nothing — a body under a lamp with no shadow floats in front of the
room rather than standing in it — and, less obviously, that **light passed through walls**:
an unshadowed point light ignores geometry, so the house's forty 16 m lamps were lighting
each other's rooms through solid plaster.

The budget is `pixelLightCount`, which is 4. Past the fourth per-pixel light Unity demotes
the rest to vertex, and a demoted light casts nothing — so a fifth overlapping shadow caster
is a cubemap rendered and thrown away, not a dimmer shadow. `Test Shadows` measures how many
casters reach the average standing spot and holds every level to 4. The house needed its
lamps cut from 16 m to 10 m to get there (7.9 -> 3.4). **Eleven metres was tried first and
measured 4.1**: plan area alone predicts 3.8, and it is wrong because the house has two
storeys and a lamp on the floor above is inside an 11 m sphere as surely as one in the next
room. Measure it; do not derive it.

A shadow-casting light on a *creature* prefab is six shadow map faces per creature and scales
with the horde. That is why the bears' eye-glows and the pickup halos stay `None`, and why
`Test Shadows` walks every prefab looking for one that does not.

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

**Population is headcount against floor area, not headcount.** `Test Crowding` bakes each
level, sums the real area of its NavMesh triangles and divides by the drawn population plus
lurkers plus the boss. The floor is **80 m2 per enemy**, calibrated to the school at 88.5 --
the tightest level here that plays. Current spread: jungle 645, forest 576, town 499,
merryland 401, house 169, pyramid 112, school 88.5, cormorant 93.

Two families, and the check had to learn that the hard way. Its first version compared each
level to the median of all eight and flagged the house, the school and the pyramid, none of
which anyone has complained about: the four outdoor levels run 400-645 and the three indoor
ones 88-169, and a median dominated by open ground is not a standard an interior can meet.

The Cormorant shipped at **39.9** -- the default 46 walkers in a hull a quarter of the
mansion's size, plus nine gulls. Nothing in the project could see it: the spawner counts
bodies, the verify counts reachability, and neither divided one by the other.

**A new level must be added to the cross-level test lists**, or it is simply skipped and its
checks pass by not running. There is now one shared `Levels` table in `ZombieHouseSetup`
that `Test Boss`, `Test Safe Start`, `Test Crowding` and `Test Shadows` all walk — add the
level there, with its boss kind and whether it has a roof — plus the bed table in `Test
Music`, which is separate
because it is keyed on `Sfx` rather than on a scene. `Test Music`'s summary line names a
count ("eight distinct beds") — update it, because a stale count is the tell that a level was
added to the enum and not to the table. `Test Boss` now derives its count from the table
instead, having spent a while saying "six giants" over a table of eight.

This is worth doing as its own step rather than as a footnote. Registering level 8 immediately
failed `Test Safe Start`: **7 of its 10 spawn markers were inside the 13 m safe radius**,
because the player started at row 8 of 21, column 6 of 13, on deck 1 of 4 — the exact centre
of the solid — and a 13 m sphere drawn from the middle of a 28.6 x 46 m box four decks tall
contains most of the box. Nothing about the level looked wrong. It had never been asked.

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
| merryland | 2530 | 1148 |
| cormorant | 1453 | 641 |

The Cormorant's numbers did not move when its player start and two spawn markers were
relocated in build 5, which is the point of tracking them: markers are not geometry.

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

**A level's population comes from its roster, not from `Weight`.** `ZombieArchetype.PickRandom`
draws from `LevelDirector.zombieRoster`, and from the five `GeneralWalkers` when that list is
empty. `Weight` is the mix *within* a pool; it is no longer what keeps a level's own types out
of everyone else's levels.

It used to be. Every level-specific kind carried `Weight = 0` and that was the entire
mechanism — a convention, held only by remembering it. Merryland's four mascots were given
real weights (34/26/30/20) so the park would have a variety, and because the draw ran over
the whole catalogue those were global weights: `Test Zombie Roster` measured **50.3% of every
walker in all seven levels** rolling a mascot's health, speed and scale while wearing that
level's clothes. It shipped that way for a week and no test looked.

So: a level whose population is its own names it (`SetRoster(so, ZombieKind.Deckhand)` at
build time). Anything arriving through the spawner's beast, pack or lurker slots is forced by
name and must **not** also be in the roster, or its statistics turn up inside the other
outfit's body. The roster is a static and outlives a scene load, so `LevelDirector.Awake`
clears it when the list is empty — a level that inherits the last one's roster is the same
bug wearing a different hat.

**Every axis `PadInput` names must exist in `ProjectSettings/InputManager.asset`.**
`Input.GetAxisRaw` throws on an undeclared axis instead of returning zero, and
`InputReader.Move` reads the pad every frame whether one is connected or not — so a renamed
axis is not a broken controller, it is a game that dies on its first frame for everybody.
`Test Gamepad` cross-checks `PadInput.AxisNames` against that file in both directions, so an
axis that is declared and no longer read is caught too. The axes are declared with `dead: 0`
on purpose: Unity's per-axis deadzone is square, and `PadInput` applies a radial one.

**The two triggers get an axis each - 9 and 10 - never the shared 3rd axis.** That axis
carries both, one positive and one negative, and it is wrong twice over. Which trigger takes
the positive sign depends on the driver: reading them off it shipped backwards on a real pad,
so aiming fired the gun and firing aimed it. And one axis cannot carry both at once, so
holding LT and then pulling RT cancels them - which is aiming down the sights and shooting,
the most ordinary thing anyone does with a controller.

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

`BuildLevel1Automated` … `BuildLevel8Automated` rebuild a level's scene.
`VerifyLevel`, `VerifyForest`, `VerifyTown`, `VerifySchool`, `VerifyPyramid`, `VerifyJungle`,
`VerifyMerryland`, `VerifyCormorant`
bake the NavMesh and prove every spawn and the exit are reachable. `VerifyCormorant` also proves the saved scene is crewed by the crew, which no amount of testing the outfits can tell you.

Tests: `TestProps`, `TestBoss`, `TestDread`, `TestMerryland`, `TestReload`, `TestSafeStart`, `TestMusic`, `TestGatling`,
`TestPostFx`, `TestSchool`, `TestPyramid`, `TestBear`, `TestPower`, `TestSurvivors`,
`TestRagdoll`, `TestFlashlight`, `TestRecoil`, `TestReloadSwitch`, `TestZombieRoster`, `TestSailors`, `TestGamepad`, `TestGulls`, `TestPrefabMaterials`, `TestCrowding`, `TestSkin`, `TestShadows`, `TestFootPlacement`, `TestBodyMeshes`, `TestColorSpace`. All print PASS/FAIL.
The weapons test is on the menu as `Test Weapons` but the method is `TestGatling` — `-executeMethod` takes the method name, not the menu path.

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

**Two pieces of arithmetic that must agree, written twice, will disagree.** Merryland's
`MazeCell` helper and `BuildHedgeMaze`'s wall loop both convert a grid cell to a position, and
the helper carried a half-cell offset the loop did not — so every "cell centre" it returned
was the corner where four cells meet, and a marker asking for an open cell landed half inside
a wall. It was on the NavMesh and walled off from everything, which is the hardest shape of
this bug to see. If two expressions must match, they must be identical line for line.

**Positions derived from a wall's own coordinates land inside other things.** Two attempts at
placing spawn markers off Merryland's hedge stubs both failed — beside the stub landed in a
games stall, at its tip landed in a lateral hedge. In a crowded level, place markers against
known-open ground and let the verify prove it, rather than computing them from geometry.

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
jaguars and monkeys all shipped pink this way, and so did both of the Cormorant's sailors:
38 null references across the two, from a rule that was already written down right here.

`Test Prefab Materials` now checks the symptom rather than the rule — every renderer in every
prefab under `Assets/Prefabs` must have a material. Run it after adding any creature. It looks
at the saved asset, not the object in memory, because the creature is *correct* in memory at
the moment it is built; that is the whole trap.

**The same rule applies one level down, to textures.** The five body materials (`skin`,
`shirt`, `trousers`, `gore`, `hair`) wear generated maps from `Fx/ProtoSkin`, written as
`.png` assets under `Assets/Resources/ProtoTextures`. An in-memory `Texture2D` does not
survive being saved into a prefab any more than an in-memory material does, so the maps are
real assets and the normal maps go through the importer — `TextureImporterType.NormalMap`
knows the per-platform encoding, and hand-packing it works right up until someone builds for
a platform where it does not.

**The generated albedo is a multiplier, not a colour, and the correction is applied exactly
once.** It averages white and is divided down by its own peak to fit in bytes;
`ApplySurface` multiplies the material colour back up by that peak, so a textured material
lands on precisely the tone it had when it was flat. That round trip is why texturing every
body in the game needed no palette re-tuning. Apply it twice and everything renders ~28% too
bright; skip it and cloth renders at 62% — both uniform, both with nothing to point at.
`CreateTexturedMaterial` guards on a bound albedo for exactly this reason, and `Test Skin`
measures the decoded mean rather than trusting the arithmetic.

**Keep every noise octave inside the sampling rate.** `ProtoSkin`'s finest octave must stay
at two or more pixels per lattice cell (at 256 px, a base period of 32 over three octaves).
Hair originally used 64, which put its third octave at one cell per pixel — white noise, and
white noise on an enemy seen across a room is shimmer. It also masked its own tiling seam
from `Test Skin`, so the aliasing was hiding the check that would have found it.

Quadrupeds reuse the humanoid bone names on purpose (front legs are "arms") so `ZombieRagdoll`
and `ZombieDismemberment` work unchanged. Even the snake gets four vestigial leg stubs buried
inside its body.
