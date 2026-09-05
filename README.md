# Zombie House

First-person zombie shooter. Unity 6.5 (6000.5.8f1), Built-in Render Pipeline, C#.

**Project lives at `C:\Users\parmk\UnityProjects\ZombieHouse` — deliberately not in OneDrive.**
Unity rewrites `Library/` constantly; OneDrive sync locks those files mid-import and corrupts
the project. If you want cloud backup, back up `Assets/`, `Packages/` and `ProjectSettings/`
only — everything else regenerates.

---

## The six levels

| | Menu item | Verify |
|---|---|---|
| **Level 1 — the house** | Zombie House → Build Level 1 Scene | Verify Level |
| **Level 2 — the forest** | Zombie House → Build Level 2 Forest | Verify Forest |
| **Level 3 — the town** | Zombie House → Build Level 3 Town | Verify Town |
| **Level 4 — the school** | Zombie House → Build Level 4 School | Verify School |
| **Level 5 — the pyramid** | Zombie House → Build Level 5 Pyramid | Verify Pyramid |
| **Level 6 — the jungle** | Zombie House → Build Level 6 Jungle | Verify Jungle |

All six are built by the same machinery. `ILevelSource` is the seam: a level answers where the
player starts, where the exit is, where pickups go and where zombies can lurk. `LevelDirector`,
`ZombieSpawner` and the NavMesh baker work on either without knowing which they are in.

### Level 2 — the forest

A seeded wood inside a ring of cliffs. You start in a clearing at one edge and have to cross
roughly 120 m of trees to a marked trail head at the other. A full moon low in the sky, thick
fog, and shafts of light between the trunks — enough to see shapes by and not enough to tell
what they are. No interior lamps to lean on, and the torch is now the difference between
crossing this wood and wandering it.

Trees, rocks, logs, bushes and thickets are scattered from a seeded random with minimum
spacing, and the start and exit clearings are kept open. It is a **thick** wood: 1400 tree
attempts at 4.2 m minimum spacing, 66 rocks, 44 logs, 120 bushes, and 26 **thickets** — a
boulder with scrub grown up around it, solid in the middle so it breaks line of sight properly
and soft at the edges so it reads as undergrowth rather than as a wall.

**Every one of those is somewhere to be waiting.** Trees and rocks always were; logs and
bushes now are too, and a thicket contributes two spots on opposite sides so rounding one is
never safe. That takes the wood from 265 lurking positions to **625**. A bush has no collider,
so nothing pathing cares about it — but it hides a body completely, which makes standing in
one the cheapest ambush in the level.

Do not drop `minimumTreeSpacing` below about 3.5: a zombie bear has a 0.85 m agent radius and
needs a 1.7 m gap to path through, and a wood it cannot cross is a wood with no bears in it.
**Verify Forest** is what catches that — it walks the trail end to end and paths every marked
spawn and every survivor back to the player start.

**Night under a full moon.** The moon is the procedural skybox's own disc rather than a sphere
hung in the scene: a sphere that far out would be swallowed by the fog, while the skybox is
drawn behind everything and unfogged. Because the disc follows the directional light, the moon
is genuinely where the moonlight comes from — turn to face it and you are looking at the source
of your own shadow.

It is a **very dark** night. Moonlight sits at **0.42** — pale rather than blue, because a
full moon is much whiter than a blue colour-grade, but dim — with ambient at 0.055/0.065/0.09
and fog at **0.042**, barely lifted off black. Trunks read as silhouettes at the edge of the
torch beam and as nothing at all beyond it. The shafts between the trees are down to 0.85
intensity over 18 m, and the sky is exposed at 0.40 so the moon hangs there without lighting
the wood underneath it.

Those numbers are the entire mood and they live together in `ConfigureForestLighting`: raise
`moon.intensity` and drop `fogDensity` to open the wood back up, or push them the other way to
close it further. The verification refuses anything below **0.35** — at that point nothing
outside the beam exists at all — and anything above 1.0, which stops being night. At 0.42 the
level is deliberately close to the floor of what is playable, which is what makes the torch a
piece of equipment rather than an option.

**The music is different out here.** No music box: a 56-second bed of wind in two bands, a
sub-bass drone of D0 against a copy of itself detuned so the two beat slowly, and three things
that arrive on their own schedule — a bowed harmonic swelling out of nothing, a trunk cracking
somewhere behind you, and twice in the loop a two-note falling call at the edge of hearing that
could be a bird and probably is not. Nothing resolves and nothing lands on a beat, so the loop
never announces itself. Synthesised in `SoundBank.HauntedForest` like everything else;
`GameAudio.musicTrack` is what selects it per level.

**Zombie bears** make up about a third of what is in there: 520 health, a 6.2 m/s charge and
52 damage a hit, but poor hearing and a dreadful turn rate. Keep something solid between you
and one.

**Take the rifle to a bear.** A rifle round does **double** to a bear and nothing else —
200 a body hit, so three drop one, and 500 to the skull, a hair under its 520, so two clean
head shots kill and one very nearly does. The pistol is unchanged at 50, which is eleven body
shots and a bad idea. The multiplier is `RifleDamageMultiplier` on the archetype, so any type
can be made vulnerable to a particular weapon without touching the weapon: `DamageKind` rides
along on the hit, and only the rifle fires `RifleRound`.

**They look the part.** Two rows of generated spikes — twelve teeth, canines longest, upper
row hanging from the snout and lower row standing out of the jaw, all one `FangMesh` at
different scales, hard-edged so the light breaks along the ridges and reads as sharp. The eyes
are blood red and lit from inside, with a small red light living *inside each eyeball* so they
carry in the dark. The light is a child of the eye rather than of the head on purpose:
`ZombieRagdoll` re-parents collider-free **renderers** when a body falls, not lights, so eyes
mounted on the head would hang in mid-air over the corpse — the ghost bug wearing a different
hat. **Zombie House → Test Bear** fails if either the teeth or the eye lights end up wrong.

The bear is a quadruped assembled from the *same named bones* as the humanoid — front legs
are "arms", rear legs are "legs" — so `ZombieRagdoll` and `ZombieDismemberment` work on it
unchanged, and only the gait differs (`ZombieVisuals.quadruped`, diagonal leg pairs).

### Level 3 — the town

One wide dirt street, false-front buildings down both sides, and a gate at the north end
with a church behind it. Where the house is a grid of rooms and the wood is scattered
geometry, this is a **corridor**: you can see the whole length of it, and nothing at all
into the alleys either side. That shape is the level design. Everything worth having is on
the street; everything that wants you is in a gap between two buildings.

Fourteen buildings, each with a false front, a porch on posts and a raised boardwalk — the
boardwalk is cover you can crouch behind and the porch roof is what makes it dark. Between
every pair of them is an alley, unlit and just wide enough to hold something. Barrels,
crates, buckboard wagons and water troughs line the street edges. **114 m** from the south
end to the gate, **80 places to lurk**.

**Windows.** Three to a front at street level, another row above them on the taller
buildings, in frames with mullions — **43 panes** down the street. About a quarter are still
lit, and that is gameplay rather than decoration: a burning window throws a real pool of
light across the boardwalk, so the town is lit in patches you can plan a route around
instead of the flat gloom it had before. The rest are dark, and a black pane at head height
reads as somewhere something could be standing, which is most of what a street like this is
for. Roughly a quarter of the dark ones are **smashed** — jagged shards left in the frame,
half of those boarded over with planks nailed across at an angle.

Only street-level windows carry an actual light. The upper ones glow but light nothing: they
are three storeys above the boardwalk, so a real light there lands on nothing you walk on and
only costs a pixel light. Eighteen window lamps and eight street lamps is the budget.

**Rolling cacti.** Nine balls of cactus blowing down the street on a gusting wind, wandering
across it as they go, tumbling at a rate that matches the ground they cover so they never
look like they are skidding. Off the end, the wind puts them back at the other end, so the
count never changes.

They exist because a level this still reads as a diorama. When the only thing that ever
crosses your view is something trying to kill you, every scrap of movement is a threat and
you learn to stop looking — give the wind something to push and the street stops being a
photograph. The first few times, one coming out of an alley will absolutely make you turn
and put a .50 round through it.

**They have no colliders at all**, and `Verify Town` fails if they ever grow one. A cactus is
the only thing in the level that moves and is not an enemy: a collider on one would carve the
NavMesh where it happened to be standing at bake time, or block a horse, or swallow a round
meant for something behind it — none of it obvious, all of it blamed on something else.

Dust rather than fog out here — thin enough that you can see the far end of the street,
which matters because of what comes down it.

**The dead wear hats.** The town's walkers are the same humanoid as everywhere else in a
sweat-stained hat with a band, and boots with stacked heels and spurs. All of it is
cosmetic and collider-free: a shot to the hat still hits the skull behind it, and
**Test Town** fails if any piece of clothing ever grows a collider.

**Zombie horses** are a fifth of the population. **620 health**, a **9.2 m/s** charge — the
fastest thing in the game by a very long way, and faster than you can sprint — and 58 a hit,
but a 74°/s turn rate, which is dreadful. A horse at full gallop needs most of the street to come back around, so the
answer is always to make it commit to a line and then not be on that line. Rifle rounds do
1.6× to one.

You will see it before you hear it: **bright pink eyes**, lit from inside, the only colour
of that kind anywhere in the game, and **shod hooves** at 0.95 metallic that catch the
torch beam. It is assembled from the same named bones as the walker and the bear — front
legs are "arms", rear legs are "legs" — so the ragdoll and the limb severing work on it
with no changes at all.

**Hostages, not hiding.** The four people here are tied to porch posts: upright, roped at
the chest twice, arms behind their backs, with a lantern set on the ground beside them by
whoever left them there. The prompt reads **CUT THEM LOOSE** rather than HELP THEM UP. The
rule is identical to the other levels — the gate will not open until every one is free —
because being tied to a post is a reason to be found, not a different rule.

### Level 4 — the school

One long double-loaded corridor, six classrooms down each side, a gym at the west end and a
cafeteria at the east. **84 m** from the front hall to the fire door, **111 places to lurk**.
Built from a single ASCII plan like the house, but single storey and much simpler for it.

**Lockers** do the design work. They stand in short banks *inside* the corridor rather than
forming a wall across it, alternating sides, so a hallway you can see the whole length of
still hides a dozen places something can be standing. The middle of the corridor is always
walkable; the sightline down it never is.

**The lighting is the level.** Fluorescents in the ceiling — about **19 dead**, and a third
of the **21** live ones flickering. A dying tube does not pulse like a sine wave: it holds
steady, stutters several times in a fraction of a second, drops out for a beat and comes
back, all at irregular intervals (`FlickeringLight`). No moon, no skybox, almost no ambient:
every scrap of light here comes from a tube or from your torch. A school at night is
frightening because it is a building made to be full and lit.

**Three kinds of dead**, and you can tell them apart at a glance:

| | Health | Chase | Reach | |
|---|---|---|---|---|
| **Teacher** | 210 | 3.2 | 1.3 m | Cardigan, lanyard, staff badge, reading glasses |
| **Kid** | 95 | 5.8 | 1.1 m | Waist height, backpack — and they arrive **three or four at a time** |
| **Janitor** | 620 | 2.6 | **2.6 m** | Dark navy coveralls, tool belt, and a mop (22 damage) |

The **janitor** is the level. He is as tough as a zombie horse, shrugs off **93%** of hits,
and the mop gives him **twice the reach of anything else in the game** — he lands a blow from
outside the distance every previous level has taught you is safe. He is slow, so the counter
is to back away rather than to circle, which is the opposite of what works on a runner.

**The mop swings.** It already rode the forearm, but a two-and-a-half-metre weapon that only
follows the hand reads as a stick taped to a zombie. `MopAnimator` gives it its own motion in
three layers: the **swing**, driven off `ZombieAI.AttackStarted` and the archetype's own windup
so the sweep is in time with the arm the AI is already animating rather than on a timer that
would drift out of sync; the **trail**, where the head lags the handle by a frame or two of
rotation, which is the cheapest possible way to make a swung object look like it has mass;
and the **carry**, a gentle sway between swings so the thing is never perfectly still.

**And it hits softer.** 34 damage down to **22**, with a slower 2.1 s swing. The mop's threat
is supposed to be its *reach* — at 34 a janitor two rooms in took a third of your health for a
mistake you could not see coming, which reads as unfair rather than as dangerous.

That reach is `ZombieArchetype.AttackRange`, a field the archetype did not have until now:
every walker used to attack at the same distance. Types that leave it at zero keep the AI's
own 1.8 m default, so nothing written before the janitor changed — **Test School** checks
exactly that, along with the mop out-reaching a teacher by 2× and carrying no collider. The
mop is scenery; the reach is the archetype. A collider on it would stop rounds meant for the
man holding it.

The **kids** spawn in packs (`ZombieSpawner.ConfigurePacks`): one marker becomes three or
four bodies, so a classroom door opening produces a group rather than a straggler.

Same three conditions on the fire door as everywhere else, with the children hiding under
desks and behind the stage curtain, and the power cell in one of five classrooms.

### Level 5 — the pyramid

**A maze.** 21 × 45 cells of it, **593 walkable squares**, five chambers knocked through it,
and the burial chamber at the far end — **90 m** from the mouth to the sarcophagus even
though the tomb is only 90 m wide, and **234 places to lurk**.

It is carved by recursive backtracking, then **70 extra walls are knocked out** to add loops.
That second step is the important one: a perfect maze is a tree, with exactly one route
between any two points, and it reads as a puzzle to be solved. A maze with loops has
alternative routes and dead ends that reconnect, and reads as a place that was built and then
got lost. You will take a wrong turn in here and come out somewhere you have already been.

The shape is deliberately the opposite of the school's. A school is a corridor with rooms you
can see into; this is corridors you cannot see along, joined by openings barely wider than you
are. You never see a chamber before you are standing in it.

**Torchlight, and only torchlight.** No sky, no moon, ambient at 0.05 — an unlit corridor is
genuinely black. **19 sconces in 593 squares**, of which 13 still burn, every one flickering,
using the same `FlickeringLight` the school's dying fluorescents use. They sit at junctions,
and only a quarter of the junctions get one: the first pass lit every junction and produced
**64** torches, which turned a tomb into a lit corridor system. The dark between them is the
level. It reads completely
differently on a flame: cold stuttering strip lighting says *broken*, warm unsteady firelight
says *old*. Columns, sarcophagi with gilded lids and masks, fallen rubble, hieroglyph bands
at eye height.

**Mummies** are the baseline: 420 health, 0.86 stagger resistance, 2.4 m/s. Slower than a
shambler and much harder to interrupt — what one costs you is ammunition and room to walk
backwards. They are the humanoid walker under about **35 bands of linen**, alternating clean
against filthy tone by tone, with loose ends trailing from the arms and the eye sockets left
dark. That alternation is the whole trick: a single tone at this scale reads as a bodysuit.

**Scarabs climb.** They go slowly up a wall, out onto the ceiling, hang there and cross it
above your head, and let go when they are directly overhead. Slowly is the whole point —
`ZombieWallCrawler`, which the school's toddlers use, scrambles up a wall *fast* to get around
you, and that reads as a chase. A thing creeping across the ceiling over your head is dread,
and dread is what a tomb is for. Every exit from a climb funnels through one `LetGo()` that
puts the beetle back on the NavMesh, so a failed climb means one falling off a wall rather
than one stuck inside it.

**Scarabs** are dog-sized — **1.24 m long, 0.72 m tall**, measured by the test rather than by
eye. 95 health, a **6.6 m/s** charge and a 420°/s turn, so one closes the distance before
you have finished deciding. They arrive in nests of **two to four** and are **30%** of the
level, down from 3-5 and 45%: the tomb was more beetle than pyramid, and the mummies never
got a chance to be the thing you dread.

They bite for **6** on a 1.3 s cycle, down from 14 on 0.75 originally, and **95 health** down
from 130 — two Deagle rounds or a short burst does one, so they die about as fast as they
arrive. They keep the speed and the turn: being swarmed is meant to be the problem, not being
chewed through while you deal with it.

The shell is their design: **a rifle round does 0.55× to a scarab**, less than a pistol
round would. Precision is the wrong tool — the answer is the gatling gun, or letting one
come at you and putting a .50 through it. Built on the same named bones as the bear and the
horse, which is now the third creature that trick has paid for; the middle pair of legs hangs
off the thorax as decoration, outside the bone chain the ragdoll walks.

### Level 6 — the jungle

**A river valley under a closed canopy**, 74 m of radius, a ruined temple gate at the far
end and almost no sky. **124 m** from the start clearing to the gate, **733 places to lurk**,
and the fog is denser than the wood's.

It is the forest's shape — scattered geometry on open ground rather than a grid of rooms —
and deliberately not the forest's problem. The wood is about **the dark**. The valley is
about **the clutter**: buttress roots, fallen trunks, fern beds and toppled columns mean
something is always within eight metres of you and out of sight, and the difference between
the two levels is which of your senses is being taken away.

**The canopy is a ceiling.** A roof of leaves twelve metres up with a handful of gaps in it —
real holes, because it is built from overlapping slabs rather than one plane. There is no
directional light in the scene at all: ambient sits at 0.045, eleven point lights hang under
the gaps, and everything else is your torch. Green-grey, where the wood is cold blue and the
tomb is warm orange, so it reads as somewhere else before you have looked at a single tree.

**The river cuts it in half**, corner to corner, with **three crossings**. You can wade it —
the water has no collider — but you are in the open for nine metres while you do, and the
crossings are exactly where the level puts its best ambush cover: a stump on each bank,
facing the water, and a snake in the reeds at every one. The start and the gate are on
opposite banks, so it gets crossed at least once whichever way you go.

**Snakes** are placed, not spawned. Thirty-odd of them, lying in particular fern beds and
particular reeds, because a snake that wandered in from a spawn marker would just be a very
short jaguar. Nearly deaf and short-sighted at 9 m, so you can walk past one — but 6.8 m/s
over the first two metres and **26 a bite**, and they are the same colours as the floor. The
counter is looking down, which is the one direction the rest of the game trains you out of.

**Jaguars** are the beast, the way bears are the wood's: **8.2 m/s**, the fastest large thing
in the game, and **38 a bite**. Only about as tough as a brute at 430 health, though, and the
**rifle does 1.8×** — it is a burst of speed and one enormous bite, and if you hear it coming
and turn in time it dies before it arrives. The Deagle is not fast enough to matter across the
ground a jaguar covers. A quarter of the population, fewer than the wood's bears, because one
covers ground about twice as fast as a bear and two at once is more than you can turn between.

**Monkeys** arrive as a pack — three to five out of one tree — which is the only way 55
health and 7 damage is a threat at all. What they cost you is ammunition and attention: they
are quick, they climb, and while you are turning to deal with four of them a jaguar is
closing from the ferns.

All three are built on the same named bones as the bear, the horse and the scarab — front
limbs are "arms", rear limbs are "legs" — so `ZombieRagdoll` and `ZombieDismemberment` work
on every one of them unchanged. That is the sixth creature the convention has paid for, and
it is the single most valuable rule in the codebase. The snake's four legs are stubs buried
inside its body: the ragdoll walks the bone chain, so they have to exist, and nobody will
ever see them.

**Its own music** — `MusicJungle`: a bed of cicadas modulated at 14 Hz, a hollow log struck
on an irregular pulse, and bird calls that stop mid-note. The layer that does the work is the
**cut-out**: every twenty seconds or so the insects stop dead for two beats while everything
else keeps going. It costs one multiply, and it means something moved.

**What verification caught.** Two sealings, and the second one is the lesson. The buttress
roots were 2.4 m fins reaching past the minimum tree spacing, so two rooted trees touched and
the valley fragmented into a maze nothing could path across. Then, with that fixed, every
spawn still failed to reach the start and the checks said *the river has sealed the valley* —
which was a lie. The start and the exit were being placed by setting x and z to `radius - 12`
each, and a corner at (-62, 60) is **85 m** from the middle of a **74 m** circular valley:
both ends were outside the wall, standing on the apron of ground beyond it, connected to
nothing. From out there everything inside really is unreachable, and every check downstream
dutifully agreed. A ten-line throwaway that printed an ASCII map of what could be walked to
from the start found it in one run, after two rounds of plausible-sounding guesses had not.

### Level 7 — Merryland

A regional theme park that closed in 1987 and was never demolished. `Build Level 7 Merryland`
/ `Verify Merryland`.

The design problem with an abandoned funfair is that it is very easy to make it merely
ruined, and ruins are not frightening. What unsettles people about a place like this is that
it is **intact**: the paint has gone chalky and the lawns are waist high, but the teacups are
still bolted to their turntable and the castle still says WELCOME over the gate. So the
palette is sun-bleached rather than grimy and nothing is collapsed. All the horror is carried
by the four things still working there.

You come in through the turnstiles at the south end and walk the midway north past the
carousel, the big top, the teacups, the wheel and the funhouse to the castle, which is both
the landmark you navigate by and the way out. The big top and the funhouse can be walked
into, on the same shell technique the town's shops use.

**Mister Squeak** — the park's flagship, and the slowest thing in it. The suit is the whole
design: foam and padding a hand deep, so rifle rounds do 60% and the Desert Eagle is suddenly
the right tool. It shambles. It does not need to hurry.

**Dilly Dog** — all limb. The costume gave him arms a third again too long and the thing
inside has been using them for a while. Reach without much damage, the same lesson the
janitor teaches.

**Missus Squeak** — smaller, quicker, and there is never only one. She arrives in threes,
which is what turns the row of games stalls into a problem.

**The Storybook Princess** — a walkaround performer, not a costume, and that is the
unpleasant part. Everything else in the park is hiding behind moulded foam; she is not, and
the greasepaint has run. She hears further than anything else in the game and was hired for
her voice: kill her first or fight the whole midway at once.

**The Big Cheese** guards the castle gate — a Mister Squeak built at four times the size for
the summer parade, with something living in the chassis. It keeps the suit's rifle
resistance, so the gun that carried you through six levels is the wrong gun here and stays
the wrong gun at the end.

The park's bed is **Waltz For Nobody**: the calliope, still playing to an empty midway. Two
ranks a few cents apart, wow and flutter over the top, and every eighth bar the mechanism
sticks and the melody stops dead for a bar. It is in a major key on purpose — a sad tune in
an abandoned park is just sad, whereas a cheerful one is indifferent, and indifference is
what makes a place feel like it does not need you.

## Getting it running

1. Open Unity Hub → **Add** → select this folder → open with 6000.5.8f1.
2. Menu bar → **Zombie House → Build Level 1 Scene**.
3. Press **Play**.

That menu item builds everything: layers, placeholder materials, the zombie prefab, the
player rig, the managers, and the house — then saves it as `Assets/Scenes/Level1_House.unity`.
Re-run it any time to rebuild the scene from scratch.

### Controls

| Input | Action |
|---|---|
| WASD | Move |
| Mouse wheel | Walk forward / backward (scroll up to advance, down to back off) |
| **Up arrow** | Hold to sprint (loud — zombies hear you from 14 m) |
| Left Ctrl / C | Crouch (quiet, tighter aim) |
| Q | Jump |
| LMB | Fire current gun (Desert Eagle .50 in slot 1) |
| **Space** | Act: lift the power cell, fit it, cut a hostage loose |
| E | Rifle — draws it if holstered, then fires it |
| 1 / 2 / 3 | Select Desert Eagle / rifle / gatling gun directly |
| **MMB (wheel click)** | Cycle pistol → rifle → gatling gun (→ Uzi, while you have one) |
| RMB | Aim down sights (iron sights; crosshair hides) |
| **Right** Ctrl **or** forward thumb button | Swing katana (offhand, left hand) |
| F | Torch on / off — and, on a flat cell, fit a spare |
| **Shift** (or R) | Reload — **throws away whatever is left in the magazine** |
| Esc | Pause |
| Enter | Restart after win/lose |

Aiming is on right mouse. The katana is on **right Ctrl** — the one beside the arrow keys,
away from the movement hand — or the forward ("advance") thumb button, button 5 on an MX
Vertical. Left Ctrl is back to crouching alongside C, and jump stays on **Q**.

**Shift reloads, and a reload costs you the magazine.** Everything still in the gun goes on
the floor with the old magazine, so topping up is nearly free when you are almost empty and
expensive when you are almost full — "reload now or push on with nine rounds" is a real
question. Two exceptions, both deliberate: the belt-fed gatling tops up instead, because a
belt is not a magazine and discarding a hundred rounds for a mistimed keypress would cost a
quarter of its whole supply; and a reload that would leave you with *fewer* rounds than you
started with is simply refused, which only comes up when the reserve is nearly dry.

Shift used to be a second sprint key alongside the up arrow, and cannot be any more — sprint
is read as a held key while reload is read on the press, so sharing Shift would have thrown a
magazine away on the first frame of every sprint. **Sprint is the up arrow now.**

**Space is the action key** — lifting the power cell, fitting it to the motor, cutting a
hostage loose. It used to fire the rifle; the rifle moved to **E**, which the action key
vacated. One key, one job, and no context-sensitive guessing about which you meant. All of
it is one line each in `InputReader` if you want it elsewhere.

### The Desert Eagle vs the rifle

The sidearm is a **Desert Eagle .50**: **125** damage, a **7-round** magazine and a 0.42 s
cycle. One body shot drops a shambler and a head shot (312) kills anything short of a
brute outright — but seven rounds and a wrist-breaking kick mean you cannot hold it down,
and it is heard from **55 m** against the old pistol's 5, so every shot in the town calls
the street.

**One bang, and then almost nothing.** The report used to carry two echo layers — a slapback
and a long tail — and the result was a canyon rather than a gun. Both are gone bar a single
very short, very quiet reflection, and what replaces them is *more bang*: a harder transient,
a heavier body, and a low thump that lands **with** the crack instead of behind it, the whole
thing over in under a third of a second. The rule is worth keeping in mind for any weapon
added later — **reverb makes a sound bigger, a transient makes it louder**, and for something
going off in your own hands it is the transient you want.

**It looks like a threat now.** Four things do that work, in the order they register: it is
**black** — near-black hard chrome instead of the old mid grey, because a pale gun reads as a
prop; **the muzzle is ported**, with cuts you can see across the top of the brake and down
both sides, which is the detail that says the thing is fighting its own recoil; **the slide
overhangs the frame**, so the barrel looks too big for what is holding it; and it is covered
in **hard edges** where the old one was smooth boxes — eight cocking serrations, a notched
scope rail, a squared guard with a hooked front, eight checkered grip panels, and a big
exposed hammer sitting back. Three **tritium dots** finish it: two on the rear sight, one on
the front, faintly glowing. In a dark corridor they are the first thing you see of your own
weapon. The model is the giveaway: a slab slide with a ventilated rib, a squared
trigger guard and a barrel visibly too big for the frame under it. The rifle is **100**, exactly
twice the old pistol, but a 0.85 s bolt cycle, an 8-round magazine and a far tighter cone.
It is the answer to a corridor, not to a crowd — and to a bear, which takes double from a
rifle round, and a horse, which takes 1.6×. Only the drawn weapon's GameObject is active, so a holstered gun costs nothing.

### The torch

`F`. A **52 degree spot, 36 m** of reach at 4.8 intensity, soft shadows, pointed wherever you
look — a proper hand-held searchlight rather than a keyring torch, which is what the wood now
demands. It only drains while it is **on**, and a cell is **300 seconds of light**: five
minutes of beam per cell, which is generous enough that the choice is where to point it rather
than whether to switch it on at all.

Under **18%** the beam browns out and stutters instead of simply dying, so you get enough
warning to decide whether to spend a spare. When the cell goes flat the beam is gone,
`F` presses do nothing, and the HUD says so.

You start with **one spare** and carry **four**. Pressing `F` on a dead cell fits a fresh
one, and that swap costs **1.15 seconds standing in the dark** — which is a long time with
something walking towards you. Spare cells are the copper-topped cylinders: `V` in the floor
plans, **seven** across the two storeys of the house, and **six** scattered through the
forest, which is darker, wider, and has nowhere to shelter while you swap.

The meter sits directly above the health bar, because it is the same kind of thing: a
resource you cannot get back except by finding more. It draws the fitted cell, not the
spares — the spare count beside it is what tells you whether a red bar matters.

Every number is a serialized field on `Flashlight`: `secondsPerCell`, `spareCells`,
`maxSpareCells`, `swapSeconds`, `lowFraction`, and the beam's `range` / `spotAngle` /
`intensity`. **Zombie House → Test Flashlight** runs a cell from full to flat, swaps a
spare in and proves the beam relights, without anyone standing in a dark room for two and
a half minutes.

Note the zombies cannot see the beam — a lit torch does not draw them, and the light is
free in that sense. Making them notice it is the obvious next turn of the screw.

### Pistol vs katana

The katana cuts an arc rather than a line, so one swing can drop three or four walkers packed
into a doorway. The cost is letting them very close indeed: the blade is **0.61 m** and the
swing reaches **1.45 m**, plus a 0.75 m sweep radius, so the edge of the arc is at about
**2.2 m** — down from roughly 3 m. Both the blade and the reach came down together, because a
weapon that cuts things it visibly cannot touch is worse than one with a short arc.

**Noise:** a gunshot now carries only **5 m**, about the same as a blade. Guns no longer pull
the house down on you, which means the katana's old advantage was stealth and is now purely
that it costs no ammunition. If you want gunfire to be the loud option again, `shotNoiseRadius`
is the last argument to each `ConfigureStats` call in the editor setup — it was 45 m for the
pistol and 70 m for the rifle.

**It takes limbs.** Any cut that lands on an arm, a leg or a head severs it at the joint above
the hit — catch a forearm and the arm goes at the elbow, catch an upper arm and it goes at the
shoulder. A head is always fatal. A leg drops them. An arm they survive, minus an arm. The
severed piece keeps its own clothing, gets thrown clear, and stays on the floor.

### The survivors

There are people still alive in here — **three in the house** (`R` in the floor plans, one
upstairs and two down) and **three in the forest**. Walk up to one and press **E**, and they
are out. **The way out will not open until every one of them has been found**, so the level
now asks for two different things: the kill quota tells you to clear the place, and the
survivors tell you to *search* it.

They are built to be findable and impossible to mistake. Each carries a **lantern** — the only
warm light in either level, against cold moonlight and a cold torch — and calls out every eight
seconds or so, positionally, so you can home in on a voice through the trees. The call is a
real voiced fundamental with formants over it, where a zombie is a sawtooth with the life
filtered out of it: human within a fraction of a second, which matters when the alternative is
shooting them.

Except you cannot shoot them. **A survivor has no colliders at all** — bullets, blades and
blasts pass straight through — so there is no way to kill the person you came for, by accident
or otherwise. **Zombie House → Test Survivors** fails if a collider ever appears on one, and
also proves the door stays shut with someone still out there and opens on the last rescue.

Zombies ignore survivors entirely: they are hiding, not fighting, and nothing hunts them while
you are on your way. Making them killable by the horde would turn a search into a timer, which
is a different game.

### The gatling gun — yours from the first frame

**Slot 3 of the loadout, 400 rounds to start, and belt-fed from there.**

It began life as the power-up you found lying about, and moving it into the loadout changed
what it is for. A weapon you might have is a weapon the level cannot be designed around; a
weapon you always have is a **tempo** — the thing you reach for when a room goes wrong, and
the thing whose ammunition count you are quietly managing from the first minute.

What it costs you is ammunition. It reloads from `R` like anything else and takes ordinary
boxes, but at twenty rounds a second it eats them: a box that is ten shots to the Desert Eagle
is six seconds of this. Belt crates are the real resupply, and where they are is a decision
about which way you cross the level. Fire it dry in the first fight and it does not
disappear — but you will be back on the sidearm until you find something to put in it.

| | Damage | Rate | Belt | Total | Heard from |
|---|---|---|---|---|---|
| **Gatling gun** | 34 | **20/sec** | 100 | 400, plus boxes and crates | 45 m |
| Desert Eagle .50 | 125 | 2.4/sec | 7 | resupplied | 55 m |
| **Uzi** (scavenged) | 52 | 10/sec | 32 | 200, then gone | 30 m |

**It is realistic where it counts: the spin-up.** The trigger does not fire it — it *starts*
it, and there is **0.85 s** of barrels winding up before a round comes out. Let go and they
coast down over 1.2 s, so a burst you abandon costs the spin-up all over again, and reloading
stops them dead. Everything else in the game answers instantly; this is the one weapon you
have to commit with, and that second is its whole character. The six barrels visibly turn and
the whine rises with them — without that the delay just reads as a broken gun.

After that it is 1,200 rounds a minute of 34 damage — **680 a second**, against the Desert
Eagle's 300 — with a cone you could drive through. A room-clearing tool at eight metres and
useless at twenty. It is heavy, too: **15% slower** while it is in your hands, though you can
still sprint with it.

The recoil is what stops it being a hose: almost no kick per round, but at twenty a second
the climb is relentless and it walks up a wall inside two seconds, with a fifth of that
staying in your aim. Tap it, or watch your own tracers go over the roof.

### The Uzi — the power-up

**Two per level, 200 rounds, and when the last round is gone so is the gun.**

Walk over one and it is yours — no key press, unlike the power cell, because a power-up
should be instant. It joins the wheel as a fourth slot, draws itself, and starts counting
down. There is no ammunition for it anywhere: what you pick up is all there will ever be, and
when the magazine and the reserve are both empty the weapon leaves your hands and you are
back on the Desert Eagle mid-fight.

That fixed, un-toppable number is the design. A weapon you can resupply is a tool; a weapon
with 200 rounds and no resupply is a **decision** — every burst spends something you cannot
get back.

It also sits in a gap the other two leave open. The Desert Eagle is 125 a shot at 2.4 a
second; the gatling is 680 a second but needs most of a second of spin-up and slows you down
carrying it. The Uzi is **instant** — no wind-up, no weight — at 52 a shot and ten a second,
which is exactly what you want when something comes round a corner at four metres. That is
the one situation the other two are both bad at, and it is most of what a jungle is.

Where they are: `U` in the house and school floor plans, computed open spots in the forest,
the town and the valley. `Verify` fails if a level ever ends up with a number other than two,
or if either one cannot be walked to.

**Test Weapons** runs both through their whole lives, and the point of it is that they must do
*opposite* things at zero rounds. The gatling refuses to fire a tenth of a second after the
trigger goes down, fires once the barrels are up, winds back down when released, empties all
400 — and is **still in the slots**, waiting for a crate. The Uzi empties its 200, removes
itself, and drops you back to the sidearm. The test used to build a gatling gun in the
power-up slot and assert it was discarded, which was true of the rig it had just built and
false of the one the game ships: it passed while the shipped loadout said something else
entirely. It now lays the rig out exactly as `BuildPlayerRig` does.

### Reloading the gatling gun

**It reloads like anything else** — `R`, from reserve, and automatically the moment the belt
runs out — and it takes ordinary ammunition boxes.

The automatic half was broken outright, and the bug is worth writing down because nothing
about it was visible in the code that looked responsible. `TryFire` checked two things in
this order:

```csharp
if (IsRotary && SpinFraction < 1f) return;   // barrels not up to speed
if (AmmoInMagazine <= 0) { ...; TryReload(); return; }
```

And `TickSpin` stops winding the barrels the instant the magazine hits zero — that is what
its `AmmoInMagazine > 0` term is for, so an emptied gun does not sit there howling. The two
are individually reasonable and together they are a **deadlock**: the frame the belt runs out,
the barrels start coasting down, `SpinFraction` drops below 1, and every subsequent `TryFire`
returns at the spin gate — which sits *above* the auto-reload it would have reached. The
gatling gun went silent with three hundred rounds in reserve and never reloaded itself the way
every other weapon in the game does. It did not even dry-fire, so there was not so much as a
click to say what had happened.

The fix is to ask "is it empty" before "are the barrels up", which costs nothing: a weapon
with no rounds in it was never going to fire this frame anyway.

**The test was complicit.** It had been driving the loop like this:

```csharp
if (gatling.AmmoInMagazine == 0) { gatling.TryReload(); ... continue; }
gatling.TickSpin(1f, true);
gatling.TryFire();
```

— reloading by hand the moment the magazine emptied, and winding the barrels a full second
every iteration. It never once took the dry-fire branch, so the deadlock was unreachable from
the test while being unavoidable in play. **A test that does the player's job for them proves
nothing about what happens when the player does not.** It now holds the trigger down and steps
one frame at a time through `TickCooldown`, `TickReload`, `TickSpin`, `TryFire` and nothing
else, and fails if any rounds are left unfired.

That rewrite needed one more change. The gap between shots was an absolute `Time.time` stamp,
and `Time.time` does not advance in edit mode — so a single dry-fire locked the weapon out for
the rest of the test. It is now a countdown ticked by `TickCooldown(deltaTime)`, the same shape
as the reload timer and for the same reason.

It started life strictly belt-fed as well, and a gun that can only be refilled from one scarce
pickup type is a gun you stop carrying: the moment it runs dry it is dead weight for the rest
of the level.

An **ammunition box is worth 120 rounds to it**, against the 24 it gives the Desert Eagle.
That is not a favour, it is a unit conversion: 24 rounds is ten shots to a handgun and a
tenth of a second to this. `Weapon.ammoBoxRounds` holds the override, and every weapon but
this one leaves it at zero and takes the box's own number.

**Belt crates** are still the big top-up — an olive box with a coil of link over the edge,
**150 rounds** each, four to six per level, filling to a ceiling of **800**. A crate you walk
over with a full gun is still there when you come back needing it.

The crate finds the gun by asking which carried weapon has barrels to spin, not by looking in
a particular slot. That is why the gatling could move out of the power-up slot and into the
loadout without a single crate in any level noticing.

The crates are deliberately drab, unlike the weapon itself: you find them because you are
fighting where they are, not because they called to you across a room.

### The post stack

Bloom, tonemapping, colour grading, vignette and grain, in `Fx/PostProcessStack` on the world
camera, running `Resources/Shaders/ZombiePost.shader`.

This was the largest single thing holding the look back — larger than every creature being made
of spheres. Half this game's atmosphere is **emissive**: torches down a tomb corridor, a bear's
eyes in the dark, the horses' pink eyes, the muzzle flash, the school's fluorescents. Without
bloom an emissive surface is not a light, it is a brightly coloured shape. `ProtoMaterials` had
been quietly compensating for years — every emission colour pushed past 2.0 to read at all,
which is fixing a pipeline problem in the material.

**Written by hand rather than using `com.unity.postprocessing`.** The package would work, but it
ships its own shaders and lens-dirt textures, and this project's defining property is that it
has **no imported assets at all** — the audio is synthesised, the textures are generated, the
geometry is primitives. Three shader passes and one component belong here in a way a package
does not. It is also less code than it sounds:

| Pass | Does |
|---|---|
| 0 — bright pass | everything over the threshold, with a soft knee so a surface on the line fades in rather than popping as a torch flickers |
| 1 — separable blur | a 9-tap gaussian collapsed to 5 samples by riding the bilinear filter; run H then V, widening the offset each round |
| 2 — composite | bloom in, ACES tonemap, contrast/saturation/filter, vignette, grain |

Tonemapping *after* the bloom is added is what stops a lit torch blowing out to a flat white
disc, and it is why the emissive values can stay where they are.

**Six grades, one per level**, set in `ApplyPostFx` beside each level's lighting rather than
buried in serialized fields. The colour filter does the heavy lifting and deliberately runs
*with* each level's lighting rather than against it — the wood is already cold blue moonlight,
so its filter leans the same way:

| | Bloom | Threshold | Filter | Vignette |
|---|---|---|---|---|
| House | 0.85 | 1.05 | barely warm | 1.10 |
| Forest | 1.05 | 0.85 | cold blue | 1.35 |
| Town | 0.75 | 1.10 | warm, dusty | 0.95 |
| School | 0.70 | 1.15 | faintly green | 1.15 |
| Tomb | **1.20** | **0.80** | openly warm | 1.45 |
| Jungle | 0.95 | 0.90 | green-grey | **1.50** |

**On the world camera, not the weapon camera.** The rig has two — world, then a weapon camera at
depth+1 that clears depth only and draws the view model over the top. That means the gun in your
hands is not graded, which is a common choice and, more to the point, the arrangement that
behaves predictably: chaining an image effect onto the second camera of a depth-only pair
depends on the colour buffer surviving between them, which is exactly the sort of thing that
looks right on one machine and renders black on another. Almost everything worth blooming is on
the world camera anyway.

**`Test PostFx` measures the bloom rather than the wiring.** A shader with a typo in a property
name compiles, binds nothing, and renders a plausible slightly-wrong frame — so checking that a
component exists proves nothing. The test pushes a frame with one 8.0 highlight through the real
render path and reads the pixels back:

```
Bloom measured across the falloff: 0.054 at 6 px, 0.044 at 14, 0.025 at 26, 0.010 in the corner.
```

That shape is the assertion. The first version of this check asserted an absolute brightness
instead and failed a perfectly good bloom at 0.047, because the ACES curve crushes the dark end
and a scene sitting at 0.02 tonemaps to about 0.010 — **absolute brightness after a tonemap is
not a thing worth having an opinion about.** A localised falloff is: it is precisely what
distinguishes a bloom from adding a constant to the whole frame.

The pixel half needs a graphics device, so run it as `-batchmode` *without* `-nographics`; under
`-nographics` it skips that half and says so rather than passing quietly.

**Not included: ambient occlusion.** It wants either a deferred path or a depth-normals prepass,
and it pays off on complex geometry under soft indirect light — which is the opposite of a game
made of flat-coloured boxes lit by point lights. Bloom was the missing piece here; AO would be
work for very little.

### Footsteps, and why there is a budget

Every walking thing emits a footstep on each sign change of its stride phase. That was fine
with one kind of zombie and became **static** with six.

The stride phase is a *visual* tuning knob — `strideCyclesPerMetre`, how fast the legs
should look like they are cycling — and small fast creatures need it high. A scarab at 1.3
cycles per metre doing 6.6 m/s crosses the sign of its stride **seventeen times a second**. A
snake at 1.5 and 6.8 m/s manages twenty. Multiply that by a pack of four, add a horde
converging because you sprinted past them, and the mix is nothing but taps. Sprinting is what
surfaced it: moving fast makes noise, noise wakes things, and woken things move at chase speed
in your direction.

Three gates fix it, and all three are needed because each catches something the others do not:

| Gate | Stops |
|---|---|
| `minimumInterval` (0.26 s) | one fast-legged creature machine-gunning on its own |
| `audibleRange` (19 m) | the far half of the level being audible at all |
| `levelFootstepsPerSecond` (14) | thirty things that each pass the first two adding up anyway |

The budget is `static` on purpose: it is a property of the mix, not of any one creature, and
the whole point is that no single zombie can know how many others are stepping this second.

A footstep is a **proximity cue** — it means *something is near you*. At thirty metres it is
not a cue, it is texture, and the level already has texture. Cutting the range to 19 m made
the ones you do hear mean something again.

And the snake has no feet. `ZombieAudio.SilenceFootsteps()` turns them off outright; a soft
tap following a snake around came free with reusing the quadruped rig, and it was the most
obviously wrong sound in the valley.

### Recoil

Every bullet weapon carries a `RecoilProfile`, and three things in it are what make it feel
like a gun rather than a screen shake.

**It climbs.** The first shot from rest is the smallest; each shot fired before the muzzle
has settled kicks harder than the last, up to a cap. Firing in pairs is controllable,
holding the trigger is not.

**It walks.** The horizontal component is a drifting bias rather than fresh noise every
shot, so a burst wanders off in one direction — which can be learned and corrected, where
pure randomness can only be endured.

**Some of it does not come back.** A share of every kick is added to your actual aim rather
than to a decaying offset, so sustained fire genuinely walks the muzzle up and you have to
pull down. That share — `uncorrectedShare` — is the single most important number here; at
zero the gun is a laser with an animation on it.

| | Kick | Climb/shot | Cap | Uncorrected | Braced |
|---|---|---|---|---|---|
| **Desert Eagle .50** | 5.2° | +62% | 3.2× | **42%** | 0.72× |
| **Rifle** | 2.6° | +20% | 1.8× | 20% | 0.55× |

Aiming down sights braces the weapon and cuts the kick; a holstered weapon settles, so
switching away mid-burst and back does not resume a climb. **Test Recoil** checks the lot,
including that a 10° kick at a 0.30 share moves the aim by exactly 3°. The Deagle's numbers
are deliberately brutal: a 5.2° first shot climbing to 16.6° under sustained fire, with 42%
of every kick staying in your aim. Two shots is a pair; four is a prayer.

### The power cell and the motor

Every level has a **winch motor** beside its way out, and every motor is dead. Somewhere out
there is a **power cell** — a crate-sized battery with a hazard stripe and a dim charge lamp.

**It moves.** Each level defines a set of places the cell could be, and one is drawn every
time the scene loads: **7** in the house, **11** in the forest, **9** in the town. The draw
uses `UnityEngine.Random` rather than the level's own seeded generator, on purpose — a given
seed still builds exactly the same forest, so routes stay learnable and bugs stay
reproducible, but the one thing that must not be learnable is not. You cannot walk to where
it was last time.

The nearest 40% of the candidates to your start are never drawn, so it is always a walk.
Every candidate is somewhere off the route: a corner room on either storey of the house, the
outer ring of the wood well away from the trail, and the far end of an alley in the town —
never on the street itself, so walking the length of the town is not enough to find it.

**Finding it is meant to be work.** Its lamp used to throw light 6.5 m and gave the crate
away from across a room; it now reaches **2.4 m** — enough to confirm what your torch is
already pointed at, and no help at all from the doorway. What replaces it is sound: the cell
**hums**, quietly, positionally, audible within about **9 m**. That rewards walking into the
right room without announcing anything from the end of a corridor. `humRadius` on
`PowerCell` turns it down to nothing if you want the search pure.

Press **Space** to shoulder it. While you are carrying it you move at **72%** speed and
**cannot sprint at all**, which is the entire cost of the errand: the cell buys you the door
and spends your legs to do it. Space again puts it down if you would rather fight first and
come back for it. Carry it to the motor, press Space, and the starter catches — a whine
climbing, a clatter as it engages, and then a hum you can hear from across the level.

**Verification walks every candidate, not the one that happened to be drawn.** Any of them
can be the live one, so a single unreachable spot is a run in which the level cannot be
finished — and that would surface as an unrepeatable bug rather than as a broken level. The
check also enforces the distance: a cell within 20 m of its own motor fails as *"a button,
not an errand"*. It earned its keep immediately, catching two forest positions that had
landed near the trail head.

| | Where the cell can be | Where the motor is | Carry |
|---|---|---|---|
| **House** | 7 corner rooms across both storeys (`W`) | Beside the extraction pad (`G`) | 40–101 m |
| **Forest** | 11 points around the outer ring | At the trail head | 50–110 m |
| **Town** | 9 alley ends, never the street | Bolted to the gate | 44–104 m |

So the way out now waits on three separate things, and they ask for three different kinds of
work: the kill quota says clear the level, the survivors say search it, and the motor says
carry something heavy back across it. **Test Power** proves the door stays shut with the
quota met and the motor dead, opens the moment it runs, and — over 200 draws — that the
cell genuinely moves and never lands on the doorstep.

### The objective loop

Start in the foyer, get out through the door in the south wall — diagonally opposite, across
both storeys. The door stays **shut until two thirds of the house is dead, all three survivors
are out, and the motor is running**; the HUD names whichever of the three is still
outstanding, and counts down how many more are needed.

The remaining third is forgiven on purpose. It used to be 90%, and that last tenth was the
worst part of every run: the level was effectively over, and you were walking two storeys
looking for one shambler in a wardrobe. Two thirds still means fighting through the place
rather than sprinting past it — you cannot reach that number by avoiding everything — but it
ends the level while there is still a level to end. The share is `requiredKillFraction` on
`GameManager`, and `ExitZone.requiresKillQuota` turns the gate off entirely.

Clear the house, find everyone, then reach the extraction pad in the master bedroom
(south-east corner). The pad stays red and inert until both conditions are met, then turns
green. Die and you restart.

---

## How the level is built

The house is **not** hand-placed geometry. It is generated from an ASCII floor plan on the
`House` object's `HouseGenerator` component:

```
##############################
#........#..........#........#
#........#....Z.....#........#
#........D..........#....A...#
...
```

| Char | Meaning |
|---|---|
| `#` | Wall |
| `.` | Floor |
| `D` | Doorway (floor, no wall) |
| `P` | Player start |
| `Z` | Zombie spawn point |
| `A` | Ammo box |
| `M` | Medkit |
| `E` | Extraction point |
| `V` | Torch battery (one spare cell) |
| `R` | Survivor waiting to be found |
| `W` | Possible power cell position (one is drawn per run) |
| `U` | Gatling gun power-up (two per level) |
| `N` | Belt crate (150 rounds for the gatling gun) |
| `G` | Door motor |
| `T` | Table |
| `S` | Sofa |
| `B` | Bed |
| `C` | Cabinet / crate |
| `F` | Bookshelf |

Furniture cells are still walkable floor — the prop stands on them. Props are colliders on
the Default layer, so they are baked into the NavMesh as obstacles: zombies path around the
sofa rather than through it, and you get something to break line of sight behind.

The house is **two storeys plus a roof**, joined by a staircase. Each storey is its own plan
in the `floors` array, and every storey must have the same dimensions because they stack.

Stairs are a run of arrow characters pointing uphill — `>>>` climbs east, `^^^` climbs north.
The storey above **automatically loses its floor over that run**, so the stairwell is open and
you can walk up through it; there is no second thing to keep in sync when you move a staircase.
A flight is built as 18 solid steps rising exactly one storey, with risers low enough that both
the player's step offset and the NavMesh agent's climb height handle them without special cases.

The opening is fenced with a banister on every edge that borders standing floor — except the
one the staircase arrives at, which is left open or the railing would seal the stairs off from
the storey they lead to. That gap is derived from the flight, so moving a staircase moves it too.

Edit the strings in the inspector, then right-click the component → **Generate House**
(or menu → **Zombie House → Regenerate House Geometry**). Every row must be the same length;
the generator logs an error and refuses to build if they are not.

After changing the plan, run **Zombie House → Verify Level**. It bakes the NavMesh in edit
mode and checks that every spawn point stands on navigable floor, that each one can path to
the player start, and that the exit is reachable — a single stray `#` can seal a room off,
and this catches it in a second instead of ten minutes into a playtest.

The current plan is 30 × 22 cells at 2 m = **60 m × 44 m**: foyer, living room, dining room,
a spine corridor, kitchen, utility room, study, two bedrooms and a master suite.

The NavMesh is baked **at runtime** from the generated colliders (`RuntimeNavMeshBaker`,
using `UnityEngine.AI.NavMeshBuilder`). That is why the project needs no `com.unity.ai.navigation`
package and why there is no bake button to forget after you edit the floor plan.

---

## Code map

```
Assets/Scripts/
  Core/       IDamageable, DamageInfo, GameManager (run state, win/lose), Noise (AI hearing)
  Player/     InputReader (works on either input backend), PlayerController, MouseLook, PlayerHealth
  Combat/     Weapon (hitscan, spread, recoil, ammo), MeleeWeapon (the machete arc),
              Hitbox (headshot multipliers), WeaponViewModel (sway/bob/recoil/reload),
              WeaponFx (muzzle, casings), Shell, ImpactFx
  Enemies/    ZombieAI (state machine), ZombieHealth, ZombieSpawner (waves), ZombieFactory,
              ZombieRig (bone references), ZombieVisuals (procedural animation),
              ZombieAppearance (per-walker variation), ZombieRagdoll (death physics), ZombieAudio
  Audio/      ProceduralAudio (the synth), SoundBank (the recipes), GameAudio (pool + playback),
              PlayerAudio, WeaponAudio, StingerAudio
  Fx/         ProtoTextures (generated sprites), ParticleFactory, ImpactSystem, DecalPool
  Level/      HouseGenerator, LevelDirector (wires markers to gameplay), RuntimeNavMeshBaker,
              ExitZone, ProtoMaterials
  Items/      Pickup (ammo / health)
  UI/         HudController (uGUI canvas, built in code)
Assets/Editor/
  ZombieHouseSetup.cs   the "Build Level 1 Scene" and "Verify Level" menus
```

### Things worth knowing

- **Damage flows through `IDamageable`.** A bullet hits a collider, finds the nearest
  `IDamageable` upward, and a `Hitbox` in between multiplies it — head = 2.5× and flags a crit.
  Add new enemies by implementing the interface; nothing in `Weapon` needs to change.
- **Zombies hear, not just see.** `Noise.Emit(position, radius)` is the channel. A gunshot
  carries 45 m, sprinting 14 m, walking 6 m, crouch-walking 1.5 m. Sight is a 110° cone with a
  line-of-sight raycast plus a short all-round awareness radius. They remember your last known
  position for 7 seconds after losing you.
- **Input works on both backends.** `InputReader` compiles against the new Input System or the
  legacy Input Manager, so switching Active Input Handling will not break the build.
- **No art dependencies.** Everything is primitives and code-generated materials living in
  `Assets/Resources/ProtoMaterials`. Edit those `.mat` files to restyle the whole house at once.

---

## Roadmap

### Stage 1 — playable blockout ✅ (this commit)
Movement, gunplay, zombie AI, generated house, pickups, objective loop, HUD, win/lose.
The whole loop is playable and tunable; nothing is final-looking.

### Stage 2 — make it look and sound like a game ✅
- **Audio, synthesised in C#** — no .wav files anywhere. `ProceduralAudio` is the DSP,
  `SoundBank` is the recipes: gunshots, impacts on stone vs flesh, footsteps, zombie
  groans/alerts/snarls/deaths, player grunts, a heartbeat that quickens as you bleed out,
  pickup blips, win/lose stingers, and a looping ambient drone.
- **Procedural zombie animation** — the zombie is now rigged (hips, shoulders, legs) and
  `ZombieVisuals` drives a distance-based walk cycle, a lurching roll, a wind-up-and-strike
  attack and hit jolts. Feet do not skate because the cycle advances with distance, not time.
- **Weapon feel** — `WeaponViewModel` adds sway that lags the mouse, a walk bob, a recoil
  kick that springs back, and a reload dip. `WeaponFx` adds muzzle smoke, sparks and
  physical ejected casings that tink when they land.
- **Impacts** — `ImpactSystem` turns each hit into particles, a decal and a positional
  sound. Bullet holes on walls, blood pools on the floor under a hit body, pooled so a
  long firefight allocates nothing.
- **HUD rebuilt in uGUI** — animated health bar, spread-driven crosshair, hit markers,
  directional damage indicators, objective panel, low-health throb. Built in code, no prefab.
- **Interior pass** — tables, sofas, beds, cabinets and bookshelves placed from the floor
  plan, blocking the NavMesh so they work as cover.

Still open (needs authored assets rather than code): a rigged humanoid model with real
animation clips, recorded audio if you ever want it, and lightmapping.

### Stage 2.5 — walkers, corpses and the machete ✅
- **The zombie is a humanoid**, not a capsule: pelvis, spine, neck, skull with jaw and
  sunken sockets, arms with elbows, legs with knees, in a torn shirt and trousers with
  exposed wounds. Hit multipliers by body part — head 2.5×, torso 1.0×, legs 0.7×, arms 0.6×.
- **No two walkers are alike.** `ZombieAppearance` varies height, build, skin tone,
  clothing colour, head tilt and turn, shoulder droop, walking pace, and which leg it
  limps on. The limp is real: the bad leg takes a shorter stride, keeps a stiffer knee,
  and the body dips as it takes weight.
- **They fall over and stay dead.** `ZombieRagdoll` builds a jointed physics ragdoll at
  the moment of death, shoves the part that was actually hit, and lets the body collapse.
  Once it stops moving the corpse freezes in place permanently — it costs nothing from
  then on, and the bodies you leave behind are a map of where you have already been.
  Corpses still stop bullets, but sit on their own layer so you never trip over them.
- **The machete** (right mouse). An offhand arc rather than a raycast, so one swing can
  cut down two or three walkers in a doorway. Quiet — 4 m of noise against the pistol's
  45 m — which makes it the tool for clearing a room without calling the house in.

To change how a zombie dies, see `ZombieRagdoll`; setting `ZombieHealth`'s
`removeCorpseAfterSeconds` above zero makes bodies disappear again if they ever pile up.

### Stage 2.6 — the katana, dismemberment, and the ghost fix ✅
- **The machete became a katana**: longer reach (3.4 m), a wider arc, four targets per
  swing, and `ZombieDismemberment` — limbs and heads come off at the joint.
- **Much more blood**: heavier, longer-lived droplets that arc down and land, plus
  scattered spatter around each hit so blood reads as splashed rather than stamped.
  The decal pool tripled and marks last five minutes.
- **Fixed: corpses left standing as ghosts.** Only parts with colliders become ragdoll
  bodies, so the shirt, trousers, hair, hands and feet stayed pinned to the animation
  pivots and hung in mid-air while the skeleton fell. `ZombieRagdoll` now re-parents every
  collider-free piece onto its nearest bone before physics starts.

  The ragdoll test missed this because it measured rigidbodies — the very things that were
  behaving. It now measures **renderers**, which is what the player actually sees.

### Stage 2.7 — one-piece blade, a real pistol, grenades ✅
- **The katana blade is a single generated mesh** (`BladeMesh`), a wedge cross-section swept
  along a curved centreline: 149 vertices, 294 triangles, **one connected surface**, no
  segment joins. Winding is verified by measuring the enclosed volume and flipping if it
  comes out negative, so the surface cannot end up inside out. Cached as
  `Assets/Meshes/KatanaBlade.asset` because a runtime-only mesh would not survive a scene save.
- **The pistol is built from its actual parts**: a slide that cycles on every shot and
  **locks back when the magazine runs dry**, a frame, a raked grip, a trigger inside its
  guard, a magazine floorplate, and front and rear sights far enough apart to line up.
  15-round magazine, tighter base spread, and the crosshair now hides while aiming so the
  iron sights are what you aim with.
- **Grenades** — see above.

### Stage 2.8 — launcher, voices and music ✅
- **The grenade became an underbarrel launcher** on the pistol, fired with Space: flat and
  fast, impact-fused, three rounds, an 11 m blast, and a camera shake to sell it. It arms
  1.6 m out of the tube so a point-blank shot bounces rather than killing you instantly.
- **The zombie voice was rebuilt** as a slow low human grumble with a wet crackle running
  through it — a gated-noise "fry" layer under a 52–70 Hz sawtooth and a sub-octave chest
  rumble, all heavily low-passed so it sits in the chest rather than the throat.
- **They bite.** `ZombieBite` — jaw snap, bone, wet tearing and a guttural surge — plays
  only when an attack actually connects, so the sound always means you have been hurt
  rather than becoming background noise.
- **Haunted mansion music**: a 48-second loop of a broken music box in D minor over an
  organ drone, with an unresolved tritone swelling underneath. The notes are unevenly
  spaced, sometimes struck twice, and slightly detuned against themselves — a mechanism
  that has not been wound in decades. Runs on its own audio channel; `GameAudio.musicVolume`
  balances it, or `SetMusicVolume(0)` turns it off.

### Stage 3 — a house with storeys, and a horde worth fearing ✅
- **Two storeys, a staircase and a roof.** `HouseGenerator` takes an array of floor plans
  instead of one, stacks them, cuts the stairwell openings automatically, and closes the
  building in with a roof and parapet. Verification confirms the stairs work: every
  upper-floor spawn can path to the ground-floor player start.
- **Four kinds of walker**, rolled per zombie from a weighted catalogue:

  | | Health | Chase speed | Notes |
  |---|---|---|---|
  | **Shambler** (58%) | 175 | 3.0 | The baseline |
  | **Runner** (22%) | 110 | 5.4 | Fast, frantic, light. Deal with it now |
  | **Brute** (13%) | 480 | 2.3 | Barely notices gunfire. Do not get cornered |
  | **Stalker** (7%) | 145 | 4.1 | 30 m sight, 22 s memory. Finds you again |
  | **Toddler** (9%) | 80 | 5.0 | ~1.3 m child, and comes at you across the walls |
  | **Mutant** (one per level) | 950 | 1.6 | Nearly deaf. Walk around it, or commit |

  Everything went up by roughly half in the difficulty pass, and stagger resistance with it:
  a shambler is now two Desert Eagle body shots rather than one, and hits interrupt these
  things less often than they used to.

  Speed, toughness, senses, build, tint and *gait* all travel together, so you can tell
  what is coming down the corridor by how it moves.
- **They hunt as a pack.** Spotting you triggers a cry (`ZombieComms`) that pulls every
  walker within 26 m onto your position — being seen once by one of them is far worse than
  it sounds. Getting shot broadcasts a smaller call, so opening fire gives away your room.
- **They aim where you are going**, not where you are, and each one approaches at its own
  angle so a pack arrives spread out and surrounds you instead of queueing up single file.
- **Breaking line of sight is no longer enough.** They search several places around your
  last known position before giving up, so standing still in the next room does not work.
- **Harder to put down**: more health across the board, and a stagger-resistance roll means
  hits often fail to interrupt them. A brute shrugs off 88% of them and keeps walking.

### Stage 3.1 — polish pass ✅
- **Banister around the stairwell**, with the landing left open (see above).
- **Brighter interiors** now the roof shuts the moonlight out: point lights up from 1.35 to
  2.6 intensity and 12 to 16 m range, spaced every 5 cells instead of 6, and scene ambient
  lifted from 0.14 to 0.24.
- **Zombie voices dropped and quietened**: fundamentals from 52–70 Hz down to 38–50 Hz, the
  mouth filter from 480 Hz to 360 Hz, and output level roughly a third lower. The chest layer
  moved from an octave below to a fifth below — an octave under a 38 Hz fundamental falls out
  of range on most speakers entirely.
- **Player footsteps are sand now** — a soft broadband swell with a fine grain under it and no
  impact, thump or ring, since sand absorbs all three. Zombies kept the wooden footstep, so
  the two are easy to tell apart in the dark.

### Stage 3.2 — rifle, toddlers, chandeliers ✅
- **Fixed: severed limbs left parts floating.** The rigidbody was going on the bone that
  carried the collider, so physics moved the bone and left the sleeve and hand hanging where
  the arm used to be. It now goes on the limb's pivot, and Unity treats the child colliders
  as one compound body so the whole limb travels together. **Zombie House → Test Dismemberment**
  cuts an arm off, drops it, and fails if any visible part stays up or the piece comes apart.
- **Fixed: the NavMesh bake was silently drifting upward.** `BuildNavMeshData` takes bounds
  *relative* to its position argument, but was being handed world-centred bounds — offsetting
  the volume by its own centre. At the old ceiling height the lower edge landed at y = −0.1
  and scraped the ground floor in by 10 cm; raising the ceiling pushed it to +1.7 and the
  entire ground floor lost its NavMesh. Caught by verification, not by playing.
- **Rifle**, on middle mouse — see above. **Katana range** trimmed 3.4 m → 2.7 m.
- **Toddlers that climb the walls** (`ZombieWallCrawler`): small, fast, and they leave the
  NavMesh entirely to scale a wall, cross it above your eyeline and drop on you. While
  climbing they steer by raycast and orient their up-vector to the surface normal. Every exit
  path funnels through one Drop() that puts them back on the NavMesh, with a hard timeout, so
  a failed climb just means falling off the wall rather than getting stuck.
- **Ceilings raised** 3 m → 4.2 m with **chandeliers** hanging in every lit room — chain, ring,
  arms and candles, all collider-free so they cannot disturb navigation below. The stair run
  lengthened to five cells to keep the climb gentle at the new storey height.
- **Gruesome decapitation**: a ragged neck stump with exposed spine, a head thrown 2.4× harder,
  and an arterial fountain that keeps pumping for ~2.6 s in weakening pulses as the body drops.
- **Zombie voices** dropped again to 32–56 Hz with per-clip variation in crackle density,
  chest weight and breath, across eight variants. **Footsteps** quieter still, with a faint
  high tick under the sand for the marble beneath.

### Stage 3.3 — the torch ✅
- **Flashlight with battery drain** on `F`, spare cells as pickups (`V`), a dying-cell
  flicker, a 1.15 s swap you spend in the dark, and a meter above the health bar. See
  *The torch* above. **Test Flashlight** covers the whole cycle headless.

### Stage 3.4 — a moonlit wood and a worse bear ✅
- **Level 2 is a night under a full moon**: skybox moon disc, moonlight more than doubled,
  ambient up, fog halved and tinted so the trees silhouette instead of vanishing.
- **The rifle does double to bears** and nothing different to anything else, via
  `DamageKind` on the hit and `RifleDamageMultiplier` on the archetype.
- **Bears have teeth and red eyes** — generated fangs and eyes lit from inside, each with
  its light inside the eyeball so it falls with the head.

### Stage 3.5 — survivors, a darker wood, and a gun that stays working ✅
- **Survivors in both levels**, three each, rescued with `E`. The exit now waits on them
  as well as on the kill quota. They carry lanterns, call out positionally, and have no
  colliders so they cannot be shot.
- **The forest is properly dark again** — moonlight 0.62, fog 0.030 — with its own music:
  wind, a beating sub-drone, and three things that happen on their own schedule.
- **Fixed: reloading and immediately switching weapons bricked the gun.** The reload was a
  coroutine; holstering deactivates the GameObject, Unity kills the routine mid-flight, and
  `IsReloading` stayed true for ever — and both firing and reloading check it first. It is
  a plain timer now, so a holstered gun just pauses and picks the reload up when redrawn.
  **Test Reload Switch** reproduces the exact sequence.
- **The katana is shorter**: 0.61 m blade, 1.45 m swing, about 2.2 m of effective reach.

### Stage 3.6 — a thicker, blacker wood ✅
- **Darker again**: moonlight 0.42, ambient 0.055/0.065/0.09, fog 0.042, sky exposure 0.40.
  Close to the floor of what the verification will accept, on purpose.
- **A stronger torch to carry into it**: 52° / 36 m / 4.8 intensity, and 300 seconds a cell
  instead of 150.
- **Far more cover**: denser trees, more rocks and logs, twice the bushes, and a new
  thicket — boulder plus scrub — that gives two hiding places on opposite sides. Lurking
  positions went from 265 to **625**, and logs and bushes now count as cover where before
  they were only scenery.

### Stage 4 — the town ✅
- **Level 3, an Old West town**: one street, fourteen false-front buildings, porches,
  boardwalks, alleys, wagons, troughs and oil lamps. 114 m end to end, 83 lurking spots.
- **Walkers in cowboy hats and boots**, spurs included, all of it cosmetic.
- **Zombie horses** — pink eyes lit from inside, shod metal hooves, 7.4 m/s and a
  dreadful turning circle. Same bone names as the bear, so ragdoll and dismemberment
  needed no changes.
- **Hostages** roped to porch posts, cut loose with `E`. The gate waits for all four.
- **The Desert Eagle .50** replaces the pistol: 125 damage, 7 rounds, heard from 40 m.
- **Real recoil** on every bullet weapon — climbing, walking, and partly uncorrected.

### Stage 4.3 — glass and tumbleweed ✅
- **43 windows** down the street, framed and mullioned, a quarter of them lit and throwing
  real light onto the boardwalk; the rest dark, and a quarter of those smashed or boarded.
- **Nine rolling cacti** on a gusting wind, tumbling in step with the ground they cover
  and blown back to the far end when they run out of street. No colliders, enforced.

### Stage 4.1 — power, sprint, and a harder night ✅
- **The power cell and the door motor**, in all three levels. Find it, shoulder it at 72%
  speed with no sprinting, carry it back, fit it. Third condition on the way out.
- **Sprint on the up arrow** (Shift still works), and **Space is now the action key** —
  the rifle moved to E.
- **Everything is tougher**: walker health up by roughly half across the board, the mutant
  to 950, and the horse to 620 health at 9.2 m/s — faster than you can run.
- **The Desert Eagle kicks harder and sounds like it**: 5.2° first shot, 42% uncorrected,
  its own .50 report, heard from 55 m.

### Stage 4.2 — the cell moves ✅
- **The power cell is drawn at random every run** from a per-level candidate set (7 / 11 /
  9), using the unseeded generator so level geometry stays reproducible and the cell does
  not. The nearest 40% to your start are never used.
- **Harder to find**: its lamp went from 6.5 m to 2.4 m, replaced by a quiet 9 m hum, so
  the search rewards entering the right room rather than glancing down a corridor.
- **Verification walks every candidate**, and caught two forest positions sitting within
  20 m of the motor.

### Stage 5 — Level 4, the elementary school ✅

Built. What follows was the plan; it survived contact largely intact, with the layout
rebuilt once after verification found three separate ways the rooms were sealed off.



The next level, designed but not built. Written down here first because it needs three new
enemy types and a new generator, and it is cheaper to argue with a plan than with code.

**The place.** A single-storey elementary school: one long double-loaded corridor — classrooms
down both sides — opening into a gym at one end and a cafeteria at the other, with a library,
a staff room and a boiler room off it. Built from stacked ASCII floor plans exactly like the
house, which already handles rooms, doorways, props and markers; the school is a new plan set
and a new prop table rather than new machinery. Lighting is the opposite of every level so
far: **fluorescents**, half of them dead, the live ones flickering and buzzing. A school at
night is frightening because it is a place that is supposed to be full.

Props worth having, in rough order of value: **lockers** lining the corridor (cover, and a
lurking spot behind every bank of them), **desks** in rows, **cafeteria tables** folded and
stacked on their sides, **gym equipment** — a climbing frame, a bench, balls — and a **stage**
at the end of the gym. Corridor lockers do most of the work: they turn a straight corridor
into a series of blind alcoves.

**Three new kinds of dead.**

| | Health | Chase | Reach | Notes |
|---|---|---|---|---|
| **Teacher** | 210 | 3.2 | 1.3 m | The baseline here. Cardigan, lanyard, reading glasses |
| **Kid** | 95 | 5.8 | 1.1 m | Waist height, fast, and they come in threes |
| **Janitor** | 620 | 2.6 | **2.6 m** | Dark coveralls. Swings a mop. Very hard to put down |

The **janitor** is the level's real problem and the reason it is worth building. He is as
tough as a zombie horse, he does not stagger, and the mop gives him **twice the reach of
anything else in the game** — he can hit you from outside the distance every other encounter
has taught you is safe. The counter is that he is slow: the mop is a reason to back away
rather than to circle, which is the opposite of what works on a runner.

Mechanically the mop is `ZombieArchetype.AttackRange` — a field the archetype does not have
yet, because until now every walker attacked at the same distance. Adding it is a two-line
change to `ZombieAI`, and it immediately makes reach a property a type can own.

The **kids** are the existing wall-crawling toddler grown up: school-age rather than infant,
faster, and spawned in small groups so a classroom door opening produces three of them at
once rather than one. The **teachers** are the baseline walker in a cardigan, and exist so
the school is mostly populated by something ordinary — a level of nothing but janitors would
be a boss rush.

**Objective.** The same three conditions as everywhere else: a kill quota, everyone rescued,
and the power cell fitted to the motor on the fire door. The survivors here are **children
hiding** — under desks, in the equipment cupboard, behind the stage curtain — which is the
huddled pose the house and forest already use rather than the town's bound one. The power
cell lives in the **boiler room**, and its candidate positions are the classrooms: the search
is room by room down the corridor, which is exactly the shape of the level.

**What has to be written.**

1. `SchoolGenerator` — ASCII plans, corridor and rooms, fluorescent lighting, prop table.
2. `ZombieArchetype.AttackRange` plus the two-line `ZombieAI` change that reads it.
3. Three archetype entries, and a `ZombieOutfit.Teacher` / `.Kid` / `.Janitor` so the same
   humanoid factory dresses all three — the town's cowboy hat proved that pattern works.
4. A mop: a prop in the janitor's hands, built like the katana blade but crude.
5. `Build Level 4 School` / `Verify School`, and a `Test School` covering the janitor's
   reach, the kids' group spawn, and that the mop carries no collider.

**The one open question** was how hard the janitor should be to *avoid* rather than to kill.
He is a quarter of the population as built, which is a guess: a corridor is exactly where
routing around something is hardest, and three in one hallway may be a dead end rather than a
challenge. `ConfigureBeasts(janitorPrefab, 0.25f, ...)` in `BuildSchoolManagers` is the one
number to turn after playing it.

**What verification caught, in order.** The floor plan's own validator rejected a row that was
43 characters against 42. Then three separate sealings, each invisible by eye: one door per
wing left 14 of 17 spawns walled off; a bank of lockers stood in front of a classroom door;
and the gym's stage sat on the gym's only doorway. The plan is now generated by a script that
flood-fills it before it is ever written into the generator, treats every solid prop as solid,
and carves a clear channel through each door — so a room that cannot be entered fails at the
plan stage rather than after a two-minute bake.

### Stage 6 — the Uzi ✅
- **A power-up weapon**: two per level in all four levels, 200 rounds, full auto, and it
  removes itself the instant the last round is fired.
- `Weapon.discardWhenEmpty` plus a `Depleted` event; `WeaponSwitcher` grows and shrinks a
  slot around it. Slot **3** on the number keys.
- Its own report — a dry 9 mm snap with a bolt clatter, three variants.

### Stage 7 — Level 5, the pyramid ✅
- **A tomb**: entrance passage, spine, four chambers and a burial chamber, lit only by
  flickering torches. Plan generated and flood-filled by `Tools_PyramidPlan.py` before it
  ever reached Unity — it passed verification first try, which the school did not.
- **Mummies**: 35 bands of two-tone linen over the standard walker, slow and unstaggerable.
- **Scarabs**: dog-sized, fast, armoured on top — rifle rounds do 0.55× — and they come in
  nests of three to five.
- **The mop swings** (`MopAnimator`) and hits for 22 instead of 34.
- **The Uzi does 39** instead of 26.

### Stage 7.1 — tomb balance ✅
- **Scarabs bite for 9 instead of 14**, on a slower cycle, and there are fewer of them:
  30% of the level in nests of 2-4, down from 45% in nests of 3-5.
- **The Uzi does 52**, up from 39.
- **The wheel jumps to the Uzi**, and cycles normally once you are holding it.

### Stage 8 — the gatling gun ✅
- **The Uzi is gone**, replaced by a hand-held gatling gun: six spinning barrels, 1,200
  rounds a minute, 400 rounds and no resupply anywhere.
- **Spin-up is the point** — 0.85 s before it fires, 1.2 s to wind down, and reloading stops
  the barrels. `Weapon.SpinFraction` owns the rule; `BarrelSpinner` only shows it.
- **The wheel is a plain three-way cycle** again: pistol, rifle, gatling.
- **Scarabs down to 6 damage and 95 health.**

### Stage 9 — the maze, the ceiling and the belts ✅
- **The pyramid is a real maze**: recursive backtracker, 593 walkable squares, 70 loops
  knocked through it, five chambers, 90 m to the exit and 234 lurking spots.
- **Its own music** — `MusicTomb`: a drone beating at a fifth of a hertz, a Phrygian-dominant
  motif struck on metal under a long delay, and a breath in the dark every dozen seconds.
- **Scarabs crawl the walls and ceilings** and drop on you from above (`ScarabCeilingCrawler`).
- **Belt crates** reload the gatling gun — 150 rounds, four to six a level.

### Stage 10 — the loadout, the gait and the jungle ✅
- **The gatling gun is loadout**, slot 3 from the first frame, and it is **no longer
  discarded when empty** — a gun you lose permanently in the first room makes every belt
  crate in the level worthless.
- **The Uzi is back** as the scavenged weapon: two a level, 200 rounds, gone when spent.
  It fills the gap the other two leave — instant, at four metres, with no wind-up and no
  weight penalty.
- **Belt crates key on the weapon being rotary**, not on which slot it is in
  (`WeaponSwitcher.FeedBeltFed`), which is why the gatling could move and no crate noticed.
- **Scarab legs move on walls and ceilings** (`ScarabGait`): an alternating tripod driven by
  measured displacement rather than agent velocity, because the agent is switched *off*
  during a climb and `ZombieVisuals` takes its stride from it. The legs used to freeze solid
  the moment one started up a wall.
- **Level 6, the jungle**: river valley, closed canopy, three crossings, snakes, jaguars and
  monkeys, and `MusicJungle`.
- **`ILurkerSource`**: an optional extra a level may implement for creatures placed at exact
  positions rather than rolled against spawn markers. Kept off `ILevelSource` on purpose —
  five levels do not have these and should not have to answer a question about them.
- **`Test Gatling` became `Test Weapons`** and now lays its rig out exactly as
  `BuildPlayerRig` does. It had been asserting the gatling was discarded against a rig it
  built itself, so it passed while the shipped loadout said the opposite.

### Stage 11 — the launcher goes, the props arrive ✅
- **The grenade launcher is removed entirely** — the weapon, the crates, the `X` marker, the
  HUD counter, the `Right Shift` binding, four synthesised sounds and `ExplosionFx`. Nineteen
  files touched. Levels have 4–5 fewer pickups each as a result; say the word and those slots
  become ammunition boxes instead.
- **Removing Sfx enum members shifts every later index**, and `musicTrack` is stored in a
  scene *as an index* — so an unrebuilt scene would silently play the wrong bed. The music
  checks in the verifies exist for exactly this and confirmed all six after the rebuild.
- **Blender is wired in as a headless mesh compiler.** `Tools_Props/*.py` generate props,
  `blender --background --python` runs them, they export unit-sized OBJ into
  `Resources/Props`. Boulder, ruined column, gate post so far.

### Stage 3 — systems depth
- Multiple weapons + switching (shotgun for corridors, rifle for the long spine).
- Zombies that notice the beam — the torch is currently free to leave on.
- Doors you can shut and barricade; zombies break through over time.
- Stamina tied to sprint.
- Damage reactions: limb-specific damage, crawlers.

### Stage 4 — content
- Second floor and basement (the generator needs a per-floor layout array + stairwell cells).
- Scripted set pieces: the power cut, the horde through the kitchen window.
- A second and third level reusing the generator with new floor plans.

### Stage 5 — ship it
- Main menu, options (sensitivity, volume, difficulty), save/continue.
- Difficulty tuning pass, performance profiling, standalone Windows build.

---

## Tuning knobs

Almost everything is a serialized field, so you can tune in Play mode:

| Feel | Where |
|---|---|
| Too slow / floaty | `PlayerController` → speeds, gravity, jumpHeight |
| Gun too weak / strong | `Weapon` → damage, fireInterval, magazineSize |
| Aim too loose | `Weapon` → baseSpread, movingSpreadBonus |
| Zombies too easy | `ZombieHealth` → maxHealth; `ZombieAI` → chaseSpeed, attackDamage |
| Too many / too few | `ZombieSpawner` → totalZombies, maxAliveAtOnce, spawnInterval |
| Too dark / bright | `HouseGenerator` → interiorLightIntensity; scene `Moonlight` intensity |
| Too flat / washed out | `PostProcessStack` → bloomIntensity, threshold, contrast, vignetteIntensity |
| One thing glares | that material's emission in `CreatePlaceholderMaterials`, not the bloom — turning the stack down dims every level |
| Audio too loud / quiet | `GameAudio` → masterVolume, ambienceVolume |
| A sound is wrong | `SoundBank` — every number in a recipe is audible; change and press Play |
| Zombies move oddly | `ZombieVisuals` → strideCyclesPerMetre, legSwingDegrees, lurchRollDegrees |
| Gun feels floaty / stiff | `WeaponViewModel` → kickBack, kickPitchDegrees, kickRecoverySpeed, swayAmount |
| Too much blood | `ImpactSystem` → bloodMistCount, bloodPoolSize; `DecalPool` → capacity, lifetime |
| Katana too strong / weak | `MeleeWeapon` → damage, cooldown, range, arcHalfAngle |
| No dismemberment wanted | `MeleeWeapon` → seversLimbs off; `ZombieDismemberment` → legLossIsFatal |
| Not enough blood | `ImpactSystem` → bloodMistCount, spatterPerHit, bloodPoolSize |
| Corpses pile up | `ZombieHealth` → removeCorpseAfterSeconds (0 = permanent) |
| Ragdolls look wrong | `ZombieRagdoll` → joint limits, masses, impulseScale; re-run **Test Ragdoll** |
| Walkers too samey | `ZombieAppearance` → heightScale, widthScale, colour spreads, limpSeverity |
| Zombie mix wrong | `ZombieArchetype.Catalogue` → Weight per kind, or any of its stats |
| Horde too coordinated | `ZombieAI` → hordeCallRadius, flankDistance, leadTime, searchPoints |
| Too many / too few | `ZombieSpawner` → totalZombies (30), maxAliveAtOnce (12) |
| Interior too dark / bright | `HouseGenerator` → interiorLightIntensity, lightSpacingCells; scene ambient |
| Zombies too quiet / loud | `ZombieAudio` → voiceVolume; `SoundBank.ZombieGrumble` → fundamental, Normalize peak |
| Footsteps wrong surface | `SoundBank.FootstepSand` (player) and `Footstep` (zombies) are separate |
| Railing in the way | `HouseGenerator` → buildStairRailings, railingHeight |
| Add a storey | `HouseGenerator.floors` → another plan of the same size, plus a stair run below it |
| Ceiling height | `HouseGenerator` → wallHeight (stairs and storey spacing follow automatically) |
| Rifle balance | `BuildRifle` in the editor setup → the ConfigureStats call |
| Scope magnification | `BuildRifle` → the aim FOV argument (18); lower is more zoom |
| Reticle look | `HudController.BuildScope` → the post / cross / aimPoint colours and bars |
| Weapon visible while scoped | Remove `ScopedWeaponHider` — but see its notes on FOV magnification |
| Toddlers climb too often | `ZombieWallCrawler` → climbAttemptInterval, minimumPlayerDistance |
| Chandeliers in the way | `HouseGenerator` → buildChandeliers, chandelierDrop |
