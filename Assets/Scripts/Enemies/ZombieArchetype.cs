using UnityEngine;

namespace ZombieHouse.Enemies
{
    public enum ZombieKind
    {
        Shambler, Runner, Brute, Stalker, Toddler, Mutant, Bear, Horse,
        Teacher, Kid, Janitor,
        Mummy, Scarab,
        Snake, Jaguar, Monkey,

        // The bosses. One per level, each a giant of that level's own creature —
        // so the thing guarding the way out is the thing you have been fighting all
        // along, which is a better ending than a monster from nowhere.
        BossZombie, BossBear, BossJanitor, BossScarab, BossJaguar, BossHorse,

        // Merryland, the theme park. The costumes are the point: a mascot suit is a
        // person-shaped thing that is deliberately NOT person-shaped, and every one of
        // these was built to be reassuring.
        MascotMouse, MascotDog, MascotBowMouse, StorybookPrincess, BossMascot,

        // The Cormorant. Not costumes this time but a crew, which is worse: the mascots
        // were people hiding inside something cheerful, and these are just men who were
        // working when it happened and have not stopped.
        Deckhand, Officer, Gull, BossBosun
    }

    /// <summary>
    /// One kind of walker. Speed, toughness, senses and gait all travel together, so a
    /// type is recognisable at a distance: you should be able to tell what is coming down
    /// the corridor by how it moves, and decide whether to shoot it or run.
    /// </summary>
    [System.Serializable]
    public class ZombieArchetype
    {
        public ZombieKind Kind;
        public string Name;
        public float Weight;

        [Header("Toughness")]
        public float Health;
        [Tooltip("Chance a hit fails to interrupt it. A brute barely notices being shot.")]
        [Range(0f, 1f)] public float StaggerResistance;

        [Header("Movement")]
        public float WanderSpeed;
        public float InvestigateSpeed;
        public float ChaseSpeed;
        public float TurnSpeed;

        [Header("Attack")]
        public float AttackDamage;

        [Tooltip("How far it can hit you from. Every walker used to share one number; the "
                 + "janitor's mop is the reason this is per-type. Zero keeps the AI's own "
                 + "default, which is what the quadrupeds and the original walkers use.")]
        public float AttackRange;
        public float AttackCooldown;
        public float AttackWindup;

        [Header("Senses")]
        public float SightRange;
        public float FieldOfView;
        public float MemorySeconds;

        [Tooltip("Scales how far it hears. 1 is normal; near zero is all but deaf.")]
        public float HearingMultiplier = 1f;

        [Tooltip("Scales damage from rifle rounds only. Above 1 makes the rifle the "
                 + "answer to this thing and the pistol a waste of ammunition.")]
        public float RifleDamageMultiplier = 1f;

        [Header("Look")]
        public float Scale;
        public Color SkinTint;
        public float StrideCyclesPerMetre;
        public float LurchDegrees;

        [Header("Special")]
        [Tooltip("Can leave the floor and scale walls to reach you.")]
        public bool ClimbsWalls;

        /// <summary>
        /// The catalogue. Weights are relative, so a shambler is the common case and a
        /// brute is the one that makes you back out of the room.
        /// </summary>
        public static readonly ZombieArchetype[] Catalogue =
        {
            new ZombieArchetype
            {
                Kind = ZombieKind.Shambler, Name = "Shambler", Weight = 58f,
                Health = 175f, StaggerResistance = 0.42f,
                WanderSpeed = 0.85f, InvestigateSpeed = 1.9f, ChaseSpeed = 3.0f, TurnSpeed = 200f,
                AttackDamage = 15f, AttackCooldown = 1.3f, AttackWindup = 0.45f,
                SightRange = 19f, FieldOfView = 115f, MemorySeconds = 9f,
                HearingMultiplier = 1f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.62f, LurchDegrees = 5f
            },
            new ZombieArchetype
            {
                // The mutant. Weight 0 keeps it out of the random draw — the spawner
                // places exactly one per level by name, because "the mutant" only works
                // as a landmark if there is one of it.
                //
                // Enormously tough and very slow, and it barely registers gunfire, so it
                // cannot be baited away with noise the way everything else can. You either
                // walk around it or commit to killing it.
                Kind = ZombieKind.Mutant, Name = "Mutant", Weight = 0f,
                Health = 950f, StaggerResistance = 0.96f,
                WanderSpeed = 0.4f, InvestigateSpeed = 0.9f, ChaseSpeed = 1.6f, TurnSpeed = 70f,
                AttackDamage = 45f, AttackCooldown = 2.4f, AttackWindup = 0.8f,
                SightRange = 14f, FieldOfView = 95f, MemorySeconds = 25f,
                HearingMultiplier = 0.12f,
                Scale = 1.5f, SkinTint = new Color(0.78f, 0.86f, 0.74f),
                StrideCyclesPerMetre = 0.34f, LurchDegrees = 11f
            },
            new ZombieArchetype
            {
                // Fast and frantic, but light: the one you deal with immediately.
                Kind = ZombieKind.Runner, Name = "Runner", Weight = 22f,
                Health = 110f, StaggerResistance = 0.2f,
                WanderSpeed = 1.5f, InvestigateSpeed = 3.4f, ChaseSpeed = 5.4f, TurnSpeed = 330f,
                AttackDamage = 11f, AttackCooldown = 0.85f, AttackWindup = 0.28f,
                SightRange = 24f, FieldOfView = 125f, MemorySeconds = 13f,
                Scale = 0.95f, SkinTint = new Color(0.94f, 0.9f, 0.86f),
                StrideCyclesPerMetre = 0.5f, LurchDegrees = 2.5f
            },
            new ZombieArchetype
            {
                // Slow, enormous, and barely notices gunfire. Do not let it corner you.
                Kind = ZombieKind.Brute, Name = "Brute", Weight = 13f,
                Health = 480f, StaggerResistance = 0.9f,
                WanderSpeed = 0.6f, InvestigateSpeed = 1.4f, ChaseSpeed = 2.3f, TurnSpeed = 120f,
                AttackDamage = 30f, AttackCooldown = 1.9f, AttackWindup = 0.65f,
                SightRange = 16f, FieldOfView = 100f, MemorySeconds = 11f,
                Scale = 1.2f, SkinTint = new Color(0.85f, 0.88f, 0.82f),
                StrideCyclesPerMetre = 0.42f, LurchDegrees = 8.5f
            },
            new ZombieArchetype
            {
                // Small, fast, and it does not stay on the floor. Comes at you across the
                // walls, which puts it outside the arc you are watching.
                Kind = ZombieKind.Toddler, Name = "Toddler", Weight = 9f,
                Health = 80f, StaggerResistance = 0.26f,
                WanderSpeed = 1.3f, InvestigateSpeed = 3.2f, ChaseSpeed = 5.0f, TurnSpeed = 380f,
                AttackDamage = 9f, AttackCooldown = 0.7f, AttackWindup = 0.24f,
                SightRange = 21f, FieldOfView = 130f, MemorySeconds = 14f,
                // 0.72 of the adult body puts them around 1.3 m — school-age, not infant.
                Scale = 0.72f, SkinTint = new Color(0.98f, 0.94f, 0.9f),
                StrideCyclesPerMetre = 0.82f, LurchDegrees = 4f,
                ClimbsWalls = true
            },
            new ZombieArchetype
            {
                // The forest's own. Weight 0: the forest spawner places bears explicitly,
                // and they have no business turning up in a bedroom.
                //
                // It closes far faster than anything in the house and hits harder than the
                // mutant, but its hearing is poor and its turn rate is dreadful — the way
                // to survive one is to keep something solid between you and it.
                Kind = ZombieKind.Bear, Name = "Zombie Bear", Weight = 0f,
                Health = 520f, StaggerResistance = 0.82f,
                // A rifle round goes through a bear in a way a pistol round does not:
                // 200 a body hit, so three drop one, and 500 to the skull — a hair under
                // its 520, so two clean head shots kill and one very nearly does.
                RifleDamageMultiplier = 2f,
                WanderSpeed = 1.1f, InvestigateSpeed = 3f, ChaseSpeed = 6.2f, TurnSpeed = 95f,
                AttackDamage = 52f, AttackCooldown = 1.6f, AttackWindup = 0.5f,
                SightRange = 26f, FieldOfView = 105f, MemorySeconds = 18f,
                HearingMultiplier = 0.4f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.3f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // The town's own, and the fastest thing in the game by a long way. It is
                // survivable only because it cannot turn: a horse at full gallop needs the
                // length of the street to come around, so the answer is always to put
                // something solid between you and it and let it commit to the wrong line.
                Kind = ZombieKind.Horse, Name = "Zombie Horse", Weight = 0f,
                Health = 620f, StaggerResistance = 0.82f,
                WanderSpeed = 1.5f, InvestigateSpeed = 4.2f, ChaseSpeed = 9.2f, TurnSpeed = 74f,
                AttackDamage = 58f, AttackCooldown = 1.5f, AttackWindup = 0.42f,
                SightRange = 30f, FieldOfView = 125f, MemorySeconds = 15f,
                HearingMultiplier = 0.85f,
                RifleDamageMultiplier = 1.6f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.26f, LurchDegrees = 3f
            },
            new ZombieArchetype
            {
                // The school's baseline. Ordinary in every way, which is the point: a
                // level of nothing but janitors would be a boss rush.
                Kind = ZombieKind.Teacher, Name = "Teacher", Weight = 0f,
                Health = 210f, StaggerResistance = 0.4f,
                WanderSpeed = 0.9f, InvestigateSpeed = 2.1f, ChaseSpeed = 3.2f, TurnSpeed = 210f,
                AttackDamage = 18f, AttackCooldown = 1.25f, AttackWindup = 0.42f,
                AttackRange = 1.3f,
                SightRange = 20f, FieldOfView = 118f, MemorySeconds = 11f,
                HearingMultiplier = 1f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.6f, LurchDegrees = 5f
            },
            new ZombieArchetype
            {
                // Waist height and quick. Spawned in small groups, so a classroom door
                // opening produces three of them rather than one.
                Kind = ZombieKind.Kid, Name = "Kid", Weight = 0f,
                Health = 95f, StaggerResistance = 0.18f,
                WanderSpeed = 1.4f, InvestigateSpeed = 3.6f, ChaseSpeed = 5.8f, TurnSpeed = 330f,
                AttackDamage = 11f, AttackCooldown = 0.8f, AttackWindup = 0.26f,
                AttackRange = 1.1f,
                SightRange = 17f, FieldOfView = 125f, MemorySeconds = 8f,
                HearingMultiplier = 1.1f,
                Scale = 0.74f, SkinTint = new Color(0.95f, 1f, 0.92f),
                StrideCyclesPerMetre = 0.95f, LurchDegrees = 7f
            },
            new ZombieArchetype
            {
                // The mop is the whole design. Twice the reach of anything else in the
                // game means he hits you from outside the distance every other encounter
                // has taught you is safe — and because he is slow, the answer is to back
                // off rather than to circle, which is the opposite of what works on a
                // runner. Barely staggers, and takes a magazine to put down.
                Kind = ZombieKind.Janitor, Name = "Janitor", Weight = 0f,
                Health = 620f, StaggerResistance = 0.93f,
                WanderSpeed = 0.7f, InvestigateSpeed = 1.8f, ChaseSpeed = 2.6f, TurnSpeed = 120f,
                // 22 rather than 34, and a slower swing: the mop's threat is supposed to
                // be its *reach*, not the size of the hit. At 34 a janitor two rooms into
                // the level took a third of your health for a mistake you could not see
                // coming, which reads as unfair rather than as dangerous.
                AttackDamage = 22f, AttackCooldown = 2.1f, AttackWindup = 0.62f,
                AttackRange = 2.6f,
                SightRange = 22f, FieldOfView = 110f, MemorySeconds = 20f,
                HearingMultiplier = 0.9f,
                RifleDamageMultiplier = 1.4f,
                Scale = 1.08f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.5f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // The pyramid's baseline. Slow even by the standards of a shambler, but
                // it does not stagger and it hits like something that has had four
                // thousand years to think about it. Fire is not implemented, so what it
                // costs you is ammunition and room to walk backwards.
                Kind = ZombieKind.Mummy, Name = "Mummy", Weight = 0f,
                Health = 420f, StaggerResistance = 0.86f,
                WanderSpeed = 0.6f, InvestigateSpeed = 1.5f, ChaseSpeed = 2.4f, TurnSpeed = 130f,
                AttackDamage = 30f, AttackCooldown = 1.8f, AttackWindup = 0.6f,
                AttackRange = 1.6f,
                SightRange = 18f, FieldOfView = 105f, MemorySeconds = 24f,
                HearingMultiplier = 0.75f,
                Scale = 1.04f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.48f, LurchDegrees = 9f
            },
            new ZombieArchetype
            {
                // Dog-sized, and it behaves like a dog: it closes fast, turns on the spot
                // and is only dangerous because there are several. Low health, but the
                // shell means a rifle round does *less* than a pistol round would — the
                // way to kill one is a hail of small bullets, not precision.
                Kind = ZombieKind.Scarab, Name = "Scarab", Weight = 0f,
                // 95 health rather than 130, so two Deagle rounds or a short burst does
                // one — they should die about as fast as they arrive.
                Health = 95f, StaggerResistance = 0.2f,
                WanderSpeed = 1.6f, InvestigateSpeed = 4.4f, ChaseSpeed = 6.6f, TurnSpeed = 420f,
                // 6 a bite on a 1.3 s cycle, down from 14 on 0.75 originally. They keep
                // the speed and the turn — being swarmed is meant to be the problem, not
                // being chewed through while you deal with it.
                AttackDamage = 6f, AttackCooldown = 1.3f, AttackWindup = 0.26f,
                AttackRange = 1.2f,
                SightRange = 15f, FieldOfView = 150f, MemorySeconds = 10f,
                HearingMultiplier = 1.3f,
                RifleDamageMultiplier = 0.55f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 1.3f, LurchDegrees = 3f
            },
            new ZombieArchetype
            {
                // The snake. Weight 0: the jungle places these itself, in the fern beds
                // and the reeds, because a snake that wandered in from a spawn marker
                // would just be a very short jaguar.
                //
                // Nearly deaf and short-sighted, so you can walk past one — but it is the
                // fastest thing in the game over the first two metres and it hits hard
                // enough that being surprised by one is a real cost. Low to the ground and
                // the same colours as the floor: the counter is looking down, which is the
                // one direction the rest of the game trains you out of.
                Kind = ZombieKind.Snake, Name = "Snake", Weight = 0f,
                Health = 70f, StaggerResistance = 0.15f,
                WanderSpeed = 0.5f, InvestigateSpeed = 1.4f, ChaseSpeed = 6.8f, TurnSpeed = 480f,
                AttackDamage = 26f, AttackCooldown = 1.6f, AttackWindup = 0.22f,
                AttackRange = 1.9f,
                SightRange = 9f, FieldOfView = 160f, MemorySeconds = 6f,
                HearingMultiplier = 0.35f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 1.5f, LurchDegrees = 7f
            },
            new ZombieArchetype
            {
                // The jaguar. Weight 0: the jungle spawner places them as its beast, the
                // way the wood places bears.
                //
                // The fastest large thing in the game and the hardest hitter that is not
                // the mutant, but only about as tough as a brute — it is a burst of speed
                // and one enormous bite, and if you hear it coming and turn in time it
                // dies before it arrives. The rifle is the answer at 1.8x; the Deagle is
                // not fast enough to matter across the ground a jaguar covers.
                Kind = ZombieKind.Jaguar, Name = "Jaguar", Weight = 0f,
                Health = 430f, StaggerResistance = 0.55f,
                WanderSpeed = 1.2f, InvestigateSpeed = 3.6f, ChaseSpeed = 8.2f, TurnSpeed = 340f,
                AttackDamage = 38f, AttackCooldown = 1.7f, AttackWindup = 0.4f,
                AttackRange = 2.4f,
                SightRange = 26f, FieldOfView = 120f, MemorySeconds = 18f,
                HearingMultiplier = 1.4f,
                RifleDamageMultiplier = 1.8f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.75f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // The monkeys. Weight 0: they arrive as a pack, three to five at a time
                // out of one tree, which is the only way they are a threat at all.
                //
                // Individually the weakest thing in the game — one Deagle round, or a
                // second of the gatling gun across the whole group. What they cost you is
                // ammunition and attention: they are quick, they come from above and
                // behind, and while you are turning to deal with four of them a jaguar is
                // closing from the ferns.
                Kind = ZombieKind.Monkey, Name = "Monkey", Weight = 0f,
                Health = 55f, StaggerResistance = 0.1f,
                WanderSpeed = 1.8f, InvestigateSpeed = 4.2f, ChaseSpeed = 6.2f, TurnSpeed = 500f,
                AttackDamage = 7f, AttackCooldown = 0.8f, AttackWindup = 0.2f,
                AttackRange = 1.5f,
                SightRange = 22f, FieldOfView = 150f, MemorySeconds = 12f,
                HearingMultiplier = 1.5f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 1.1f, LurchDegrees = 6f,
                ClimbsWalls = true
            },
            new ZombieArchetype
            {
                // The house. A shambler grown to two and a half metres — the first thing
                // the game taught you to kill, returned at a size where everything it
                // taught you is wrong.
                //
                // Every boss follows the same shape: enormous health, near-total stagger
                // resistance so it cannot be stunlocked, slow enough to outrun in a
                // straight line, and a hit that takes most of a health bar. The fight is
                // about the room, not about aim — you are meant to be moving.
                Kind = ZombieKind.BossZombie, Name = "The Landlord", Weight = 0f,
                Health = 4100f, StaggerResistance = 0.86f,
                WanderSpeed = 0.6f, InvestigateSpeed = 1.6f, ChaseSpeed = 3.4f, TurnSpeed = 95f,
                AttackDamage = 34f, AttackCooldown = 2.2f, AttackWindup = 0.75f,
                AttackRange = 3.2f,
                SightRange = 30f, FieldOfView = 130f, MemorySeconds = 40f,
                HearingMultiplier = 1.2f,
                Scale = 2.20f, SkinTint = new Color(0.72f, 0.78f, 0.70f),
                StrideCyclesPerMetre = 0.30f, LurchDegrees = 9f
            },
            new ZombieArchetype
            {
                // The wood. Rifle-vulnerable like every bear, which keeps the level's own
                // lesson intact at the size where it matters most.
                Kind = ZombieKind.BossBear, Name = "The Warden", Weight = 0f,
                Health = 4400f, StaggerResistance = 0.84f,
                WanderSpeed = 0.9f, InvestigateSpeed = 2.4f, ChaseSpeed = 5.2f, TurnSpeed = 130f,
                AttackDamage = 36f, AttackCooldown = 2.2f, AttackWindup = 0.7f,
                AttackRange = 3.6f,
                SightRange = 32f, FieldOfView = 120f, MemorySeconds = 40f,
                HearingMultiplier = 1.4f,
                RifleDamageMultiplier = 1.7f,
                Scale = 3.4f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.42f, LurchDegrees = 5f
            },
            new ZombieArchetype
            {
                // The school. The janitor's whole design is that he reaches you from
                // outside the distance everything else has taught you is safe; at this size
                // the mop covers most of a classroom.
                Kind = ZombieKind.BossJanitor, Name = "The Caretaker", Weight = 0f,
                Health = 4200f, StaggerResistance = 0.87f,
                WanderSpeed = 0.7f, InvestigateSpeed = 1.9f, ChaseSpeed = 3.9f, TurnSpeed = 110f,
                AttackDamage = 32f, AttackCooldown = 2.0f, AttackWindup = 0.65f,
                AttackRange = 6.1f,
                SightRange = 30f, FieldOfView = 125f, MemorySeconds = 40f,
                HearingMultiplier = 1.2f,
                Scale = 1.72f, SkinTint = new Color(0.62f, 0.66f, 0.62f),
                StrideCyclesPerMetre = 0.34f, LurchDegrees = 7f
            },
            new ZombieArchetype
            {
                // The town. Not asked for, but the street needed *something* and a giant
                // zombie horse is the only honest answer — the horses are what the level is
                // remembered for. Pink eyes and shod hooves at three metres.
                //
                // Fastest of the six in a straight line and the worst at corners, which is
                // the fight: a street is long and open, so you want to be somewhere it has
                // to turn.
                Kind = ZombieKind.BossHorse, Name = "The Marshal's Horse", Weight = 0f,
                Health = 4000f, StaggerResistance = 0.82f,
                WanderSpeed = 1.2f, InvestigateSpeed = 3.4f, ChaseSpeed = 6.0f, TurnSpeed = 85f,
                AttackDamage = 34f, AttackCooldown = 2.2f, AttackWindup = 0.6f,
                AttackRange = 3.6f,
                SightRange = 34f, FieldOfView = 115f, MemorySeconds = 40f,
                HearingMultiplier = 1.3f,
                Scale = 3.0f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.5f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // The tomb. Keeps the shell — 0.55x from rifle rounds — so the answer at
                // the end of the level is the same as the answer all the way through it,
                // only now you need all of it.
                Kind = ZombieKind.BossScarab, Name = "The Queen", Weight = 0f,
                Health = 3700f, StaggerResistance = 0.80f,
                WanderSpeed = 1.1f, InvestigateSpeed = 3.0f, ChaseSpeed = 5.4f, TurnSpeed = 240f,
                AttackDamage = 28f, AttackCooldown = 1.8f, AttackWindup = 0.5f,
                AttackRange = 3.4f,
                SightRange = 26f, FieldOfView = 150f, MemorySeconds = 35f,
                HearingMultiplier = 1.5f,
                RifleDamageMultiplier = 0.55f,
                Scale = 5.2f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.9f, LurchDegrees = 3f
            },
            new ZombieArchetype
            {
                // The valley. The fastest boss by a distance, and the one you cannot simply
                // walk away from — the fight is about finding something to put between you.
                Kind = ZombieKind.BossJaguar, Name = "The Green Mother", Weight = 0f,
                Health = 3800f, StaggerResistance = 0.78f,
                // 5.9, not 6.2, and the ceiling is the enrage rather than the base. LevelBoss
                // multiplies chase speed by 1.12 below half health, and 6.2 x 1.12 is 6.94
                // against a 6.8 m/s sprint — she became uncatchable-from at exactly the point
                // the player most needs to break away. 5.9 x 1.12 = 6.6, which still closes
                // faster than anything else in the game and can still be walked away from.
                WanderSpeed = 1.4f, InvestigateSpeed = 3.8f, ChaseSpeed = 5.9f, TurnSpeed = 260f,
                AttackDamage = 33f, AttackCooldown = 2.1f, AttackWindup = 0.55f,
                AttackRange = 3.8f,
                SightRange = 34f, FieldOfView = 125f, MemorySeconds = 45f,
                HearingMultiplier = 1.5f,
                RifleDamageMultiplier = 1.6f,
                Scale = 3.6f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.55f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // MISTER SQUEAK -- the park's flagship, and the slowest thing in it.
                //
                // The suit is the whole design. Foam and padding a hand deep soak rifle
                // rounds that would drop a walker, so it is the one enemy in the game the
                // rifle is *bad* against and the shotgun-adjacent Desert Eagle is good
                // against. It shambles. It does not need to hurry, and something that does
                // not hurry while you back away from it is worse than something that runs.
                Kind = ZombieKind.MascotMouse, Name = "Mister Squeak", Weight = 34f,
                Health = 460f, StaggerResistance = 0.88f,
                WanderSpeed = 0.5f, InvestigateSpeed = 1.3f, ChaseSpeed = 2.1f, TurnSpeed = 70f,
                AttackDamage = 26f, AttackCooldown = 2.0f, AttackWindup = 0.7f,
                AttackRange = 2.4f,
                SightRange = 20f, FieldOfView = 95f, MemorySeconds = 26f,
                HearingMultiplier = 0.8f,
                RifleDamageMultiplier = 0.6f,
                Scale = 1.24f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.34f, LurchDegrees = 11f
            },
            new ZombieArchetype
            {
                // DILLY DOG -- all limb. The costume gave him arms a third again too long
                // and the thing inside has been using them for a while now.
                //
                // Reach without much damage, the same lesson the janitor teaches: what makes
                // it frightening is being hit from further away than you judged, not the
                // size of the hit.
                Kind = ZombieKind.MascotDog, Name = "Dilly Dog", Weight = 26f,
                Health = 300f, StaggerResistance = 0.7f,
                WanderSpeed = 0.8f, InvestigateSpeed = 2.2f, ChaseSpeed = 3.6f, TurnSpeed = 130f,
                AttackDamage = 21f, AttackCooldown = 1.7f, AttackWindup = 0.5f,
                AttackRange = 3.4f,
                SightRange = 26f, FieldOfView = 120f, MemorySeconds = 24f,
                HearingMultiplier = 1.1f,
                RifleDamageMultiplier = 0.85f,
                Scale = 1.34f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.46f, LurchDegrees = 9f
            },
            new ZombieArchetype
            {
                // MISSUS SQUEAK -- smaller, quicker, and there is never only one.
                //
                // The counterpart costume, and the one that turns a corridor of stalls into
                // a problem. Low health and low stagger resistance: they die easily and
                // they arrive in threes, which is a completely different kind of pressure
                // from her opposite number.
                Kind = ZombieKind.MascotBowMouse, Name = "Missus Squeak", Weight = 30f,
                Health = 180f, StaggerResistance = 0.34f,
                WanderSpeed = 0.9f, InvestigateSpeed = 2.6f, ChaseSpeed = 4.3f, TurnSpeed = 165f,
                AttackDamage = 15f, AttackCooldown = 1.3f, AttackWindup = 0.34f,
                AttackRange = 2.1f,
                SightRange = 23f, FieldOfView = 125f, MemorySeconds = 22f,
                HearingMultiplier = 1.3f,
                Scale = 1.06f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.58f, LurchDegrees = 7f
            },
            new ZombieArchetype
            {
                // THE STORYBOOK PRINCESS -- a walkaround performer, not a costume. There is
                // a face under the paint and that is the unpleasant part.
                //
                // Hears further than anything else in the park and remembers longest. The
                // gown drags, so you hear her before you see her, and she was hired for her
                // voice: the loudest caller in the game, which makes her the one you must
                // kill first or fight the whole midway at once.
                Kind = ZombieKind.StorybookPrincess, Name = "The Storybook Princess", Weight = 20f,
                Health = 210f, StaggerResistance = 0.3f,
                WanderSpeed = 0.7f, InvestigateSpeed = 2.4f, ChaseSpeed = 4.6f, TurnSpeed = 150f,
                AttackDamage = 19f, AttackCooldown = 1.5f, AttackWindup = 0.4f,
                AttackRange = 2.2f,
                SightRange = 30f, FieldOfView = 135f, MemorySeconds = 42f,
                HearingMultiplier = 1.9f,
                Scale = 1.1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.44f, LurchDegrees = 5f
            },
            new ZombieArchetype
            {
                // THE BIG CHEESE -- the parade float that was never packed away.
                //
                // A Mister Squeak built at four times the size for the summer parade, with
                // something living in the chassis. Outdoors, so nothing caps its height the
                // way the house and school cap theirs.
                //
                // Keeps the suit's rifle resistance, so the level's own lesson holds at the
                // size where it matters most: the gun that has carried you through six
                // levels is the wrong gun here, and it stays the wrong gun at the end.
                Kind = ZombieKind.BossMascot, Name = "The Big Cheese", Weight = 0f,
                Health = 4300f, StaggerResistance = 0.85f,
                WanderSpeed = 0.6f, InvestigateSpeed = 1.7f, ChaseSpeed = 3.7f, TurnSpeed = 85f,
                AttackDamage = 35f, AttackCooldown = 2.2f, AttackWindup = 0.8f,
                AttackRange = 4.2f,
                SightRange = 32f, FieldOfView = 120f, MemorySeconds = 45f,
                HearingMultiplier = 1.1f,
                RifleDamageMultiplier = 0.5f,
                Scale = 3.2f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.26f, LurchDegrees = 13f
            },
            new ZombieArchetype
            {
                // THE DECKHAND -- the ship's ordinary walker, and the reason the corridors
                // below are worse than the corridors anywhere else.
                //
                // Oilskins over a life vest is a lot of bulk, so he is slower and tougher
                // than a shambler; but a deck is two metres wide and there is nowhere to
                // back away to, which turns "slow and tough" from a manageable problem into
                // the wrong problem entirely. Hears well: a steel hull carries a footstep
                // the length of the ship.
                Kind = ZombieKind.Deckhand, Name = "Deckhand", Weight = 62f,
                Health = 230f, StaggerResistance = 0.5f,
                WanderSpeed = 0.8f, InvestigateSpeed = 2.0f, ChaseSpeed = 3.3f, TurnSpeed = 190f,
                AttackDamage = 17f, AttackCooldown = 1.25f, AttackWindup = 0.44f,
                SightRange = 17f, FieldOfView = 110f, MemorySeconds = 14f,
                HearingMultiplier = 1.35f,
                Scale = 1.02f, SkinTint = new Color(0.92f, 0.97f, 0.95f),
                StrideCyclesPerMetre = 0.58f, LurchDegrees = 6f
            },
            new ZombieArchetype
            {
                // THE OFFICER -- fewer, faster, and he came down from the bridge.
                //
                // The counterpart, the way Dilly Dog is Mister Squeak's: no more dangerous
                // in a straight fight, but quick enough that a companionway does not buy you
                // the time you thought it did. Sees furthest of anything on the ship, which
                // is what makes the open weather deck the dangerous part of the level rather
                // than the safe part.
                //
                // The braid and the white cap are the whole point of him: in a level lit by
                // a few working lamps, one silhouette paler than the others tells you which
                // one is about to close the distance.
                Kind = ZombieKind.Officer, Name = "Officer", Weight = 38f,
                Health = 195f, StaggerResistance = 0.36f,
                WanderSpeed = 1.1f, InvestigateSpeed = 2.9f, ChaseSpeed = 4.7f, TurnSpeed = 280f,
                AttackDamage = 14f, AttackCooldown = 1.0f, AttackWindup = 0.32f,
                SightRange = 27f, FieldOfView = 125f, MemorySeconds = 18f,
                HearingMultiplier = 1.15f,
                Scale = 1.06f, SkinTint = new Color(0.90f, 0.95f, 0.94f),
                StrideCyclesPerMetre = 0.66f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // THE BOSUN -- the man who ran the deck, and still does.
                //
                // He fights on the weather deck and he has to: the deckhead below is 2.6 m,
                // which caps an indoor boss at about 1.4x, and 1.4x is not a boss, it is a
                // large man. Above deck there is nothing overhead at all, so he is 2.6x --
                // about 4.6 m -- and the only enemy in this level you have to look up at.
                // Test Boss measures that on a real built body rather than trusting it.
                //
                // Slower than the officer he was promoted past, and much harder to stagger:
                // the whole fight is that the weather deck is open and there is nowhere on
                // it he cannot follow you. The rails are the only cover and he goes through
                // them.
                //
                // Reach of 3.6 m is the marlinspike. Same lesson as the janitor's mop and
                // Dilly Dog's arms -- being hit from further away than you judged is worse
                // than being hit harder.
                Kind = ZombieKind.BossBosun, Name = "The Bosun", Weight = 0f,
                Health = 4150f, StaggerResistance = 0.87f,
                WanderSpeed = 0.7f, InvestigateSpeed = 2.1f, ChaseSpeed = 4.4f, TurnSpeed = 105f,
                AttackDamage = 33f, AttackCooldown = 1.9f, AttackWindup = 0.62f,
                AttackRange = 3.6f,
                SightRange = 30f, FieldOfView = 120f, MemorySeconds = 40f,
                HearingMultiplier = 1.2f,
                Scale = 2.6f, SkinTint = new Color(0.88f, 0.94f, 0.92f),
                StrideCyclesPerMetre = 0.30f, LurchDegrees = 8f
            },
            new ZombieArchetype
            {
                // THE GULL -- weight 0, because it is placed on a rail rather than drawn.
                //
                // Almost nothing on its own: forty health, and it hits for eight. What it
                // costs you is attention. Every other enemy in this game arrives along the
                // floor, so the player learns to read one plane and stops looking anywhere
                // else; a gull comes down from outside that plane, screams first so it is
                // never a cheap shot, and is gone again before you have finished turning.
                //
                // The rifle multiplier is up because the answer to a small fast thing at
                // range is the accurate gun, and a shotgun-adjacent Desert Eagle should feel
                // wrong here in the same way it feels right against the mascots.
                //
                // Speed and senses are mostly unread -- GullFlight owns the movement and
                // there is no NavMeshAgent to give them to -- but SightRange is not: the
                // flight uses it as the range it notices you from.
                Kind = ZombieKind.Gull, Name = "Gull", Weight = 0f,
                Health = 40f, StaggerResistance = 0.1f,
                WanderSpeed = 0f, InvestigateSpeed = 0f, ChaseSpeed = 0f, TurnSpeed = 600f,
                AttackDamage = 8f, AttackCooldown = 5.5f, AttackWindup = 0.65f,
                AttackRange = 2f,
                SightRange = 20f, FieldOfView = 300f, MemorySeconds = 6f,
                HearingMultiplier = 0.4f,
                RifleDamageMultiplier = 1.4f,
                Scale = 1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0f, LurchDegrees = 0f
            },
            new ZombieArchetype
            {
                // Sees furthest and remembers longest. It is the one that finds you again
                // after you thought you had lost it.
                Kind = ZombieKind.Stalker, Name = "Stalker", Weight = 7f,
                Health = 145f, StaggerResistance = 0.36f,
                WanderSpeed = 1.1f, InvestigateSpeed = 2.8f, ChaseSpeed = 4.1f, TurnSpeed = 280f,
                AttackDamage = 18f, AttackCooldown = 1.05f, AttackWindup = 0.34f,
                SightRange = 30f, FieldOfView = 140f, MemorySeconds = 22f,
                Scale = 0.92f, SkinTint = new Color(0.82f, 0.86f, 0.8f),
                StrideCyclesPerMetre = 0.58f, LurchDegrees = 3.5f
            }
        };

        /// <summary>
        /// A private copy of this archetype.
        ///
        /// Every entry in <see cref="Catalogue"/> is a single shared object read by every
        /// zombie of that kind, so anything wanting to change one at runtime — a boss
        /// enraging at half health, say — must clone it first. Mutating the catalogue entry
        /// instead applies the change to every zombie of that kind for the rest of the
        /// session, and compounds each time it happens: the second boss starts where the
        /// first one finished. Nothing errors, and the difficulty simply drifts.
        /// </summary>
        public ZombieArchetype Clone()
        {
            return new ZombieArchetype
            {
                Kind = Kind, Name = Name, Weight = Weight,
                Health = Health, StaggerResistance = StaggerResistance,
                WanderSpeed = WanderSpeed, InvestigateSpeed = InvestigateSpeed,
                ChaseSpeed = ChaseSpeed, TurnSpeed = TurnSpeed,
                AttackDamage = AttackDamage, AttackRange = AttackRange,
                AttackCooldown = AttackCooldown, AttackWindup = AttackWindup,
                SightRange = SightRange, FieldOfView = FieldOfView,
                MemorySeconds = MemorySeconds, HearingMultiplier = HearingMultiplier,
                RifleDamageMultiplier = RifleDamageMultiplier,
                Scale = Scale, SkinTint = SkinTint,
                StrideCyclesPerMetre = StrideCyclesPerMetre, LurchDegrees = LurchDegrees
            };
        }

        /// <summary>
        /// The walkers that belong to no level in particular, and the pool a level draws
        /// from when it has not asked for its own.
        ///
        /// Everything else in the catalogue is somebody's: the school's teachers and
        /// children, the tomb's mummies, the park's mascots, the ship's crew, and the
        /// bosses. Those are placed by name.
        /// </summary>
        public static readonly ZombieKind[] GeneralWalkers =
        {
            ZombieKind.Shambler, ZombieKind.Runner, ZombieKind.Brute,
            ZombieKind.Toddler, ZombieKind.Stalker
        };

        private static ZombieKind[] _roster;

        /// <summary>
        /// What the current level's ordinary population is drawn from.
        ///
        /// This exists because the previous mechanism was Weight = 0 and nothing else. Every
        /// level-specific type carried a zero weight to stay out of the draw, which worked
        /// perfectly and silently until Merryland's four mascots were given real weights so
        /// the park would have a mix. They were not park weights. PickRandom draws over the
        /// whole catalogue, so they were global weights, and they totalled 110 against the
        /// 109 of the five general walkers: half of every walker in the mansion, the forest,
        /// the town, the school, the tomb and the jungle was rolling a mascot's health,
        /// speed and scale while wearing that level's clothes. Nothing about the body said
        /// so and no test looked.
        ///
        /// A pool you have to remember not to join is the wrong shape. This one you have to
        /// be invited to.
        /// </summary>
        public static void SetRoster(params ZombieKind[] kinds)
        {
            _roster = kinds != null && kinds.Length > 0 ? kinds : null;
        }

        /// <summary>Back to the general walkers. Statics outlive a scene load; this matters.</summary>
        public static void ClearRoster() => _roster = null;

        /// <summary>What PickRandom is currently drawing from. For tests and for the log.</summary>
        public static ZombieKind[] ActiveRoster => _roster ?? GeneralWalkers;

        /// <summary>The catalogue entry for one kind, or the shambler if there is none.</summary>
        public static ZombieArchetype Of(ZombieKind kind)
        {
            foreach (ZombieArchetype archetype in Catalogue)
                if (archetype.Kind == kind) return archetype;

            return Catalogue[0];
        }

        /// <summary>
        /// One walker from the active pool, weighted.
        ///
        /// Weight is now the mix within a pool rather than membership of it, so a pool whose
        /// members all carry zero — which is every pool made of level-specific types, since
        /// those were all authored at zero — falls back to an even draw rather than always
        /// returning the first one.
        /// </summary>
        public static ZombieArchetype PickRandom()
        {
            ZombieKind[] pool = ActiveRoster;

            float total = 0f;
            foreach (ZombieKind kind in pool) total += Of(kind).Weight;

            if (total <= 0f) return Of(pool[Random.Range(0, pool.Length)]);

            float roll = Random.Range(0f, total);
            foreach (ZombieKind kind in pool)
            {
                ZombieArchetype archetype = Of(kind);
                roll -= archetype.Weight;
                if (roll <= 0f) return archetype;
            }

            return Of(pool[pool.Length - 1]);
        }
    }
}
