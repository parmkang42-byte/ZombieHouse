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
        BossZombie, BossBear, BossJanitor, BossScarab, BossJaguar, BossHorse
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
                Health = 1400f, StaggerResistance = 0.84f,
                WanderSpeed = 0.6f, InvestigateSpeed = 1.6f, ChaseSpeed = 3.4f, TurnSpeed = 95f,
                AttackDamage = 34f, AttackCooldown = 2.2f, AttackWindup = 0.75f,
                AttackRange = 3.2f,
                SightRange = 30f, FieldOfView = 130f, MemorySeconds = 40f,
                HearingMultiplier = 1.2f,
                Scale = 2.5f, SkinTint = new Color(0.72f, 0.78f, 0.70f),
                StrideCyclesPerMetre = 0.30f, LurchDegrees = 9f
            },
            new ZombieArchetype
            {
                // The wood. Rifle-vulnerable like every bear, which keeps the level's own
                // lesson intact at the size where it matters most.
                Kind = ZombieKind.BossBear, Name = "The Warden", Weight = 0f,
                Health = 1500f, StaggerResistance = 0.82f,
                WanderSpeed = 0.9f, InvestigateSpeed = 2.4f, ChaseSpeed = 5.2f, TurnSpeed = 130f,
                AttackDamage = 36f, AttackCooldown = 2.2f, AttackWindup = 0.7f,
                AttackRange = 3.6f,
                SightRange = 32f, FieldOfView = 120f, MemorySeconds = 40f,
                HearingMultiplier = 1.4f,
                RifleDamageMultiplier = 1.7f,
                Scale = 2.1f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.42f, LurchDegrees = 5f
            },
            new ZombieArchetype
            {
                // The school. The janitor's whole design is that he reaches you from
                // outside the distance everything else has taught you is safe; at this size
                // the mop covers most of a classroom.
                Kind = ZombieKind.BossJanitor, Name = "The Caretaker", Weight = 0f,
                Health = 1450f, StaggerResistance = 0.83f,
                WanderSpeed = 0.7f, InvestigateSpeed = 1.9f, ChaseSpeed = 3.9f, TurnSpeed = 110f,
                AttackDamage = 32f, AttackCooldown = 2.0f, AttackWindup = 0.65f,
                AttackRange = 5.4f,
                SightRange = 30f, FieldOfView = 125f, MemorySeconds = 40f,
                HearingMultiplier = 1.2f,
                Scale = 2.2f, SkinTint = new Color(0.62f, 0.66f, 0.62f),
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
                Health = 1350f, StaggerResistance = 0.78f,
                WanderSpeed = 1.2f, InvestigateSpeed = 3.4f, ChaseSpeed = 6.0f, TurnSpeed = 85f,
                AttackDamage = 34f, AttackCooldown = 2.2f, AttackWindup = 0.6f,
                AttackRange = 3.6f,
                SightRange = 34f, FieldOfView = 115f, MemorySeconds = 40f,
                HearingMultiplier = 1.3f,
                Scale = 1.9f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.5f, LurchDegrees = 4f
            },
            new ZombieArchetype
            {
                // The tomb. Keeps the shell — 0.55x from rifle rounds — so the answer at
                // the end of the level is the same as the answer all the way through it,
                // only now you need all of it.
                Kind = ZombieKind.BossScarab, Name = "The Queen", Weight = 0f,
                Health = 1250f, StaggerResistance = 0.76f,
                WanderSpeed = 1.1f, InvestigateSpeed = 3.0f, ChaseSpeed = 5.4f, TurnSpeed = 240f,
                AttackDamage = 28f, AttackCooldown = 1.8f, AttackWindup = 0.5f,
                AttackRange = 3.4f,
                SightRange = 26f, FieldOfView = 150f, MemorySeconds = 35f,
                HearingMultiplier = 1.5f,
                RifleDamageMultiplier = 0.55f,
                Scale = 3.2f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.9f, LurchDegrees = 3f
            },
            new ZombieArchetype
            {
                // The valley. The fastest boss by a distance, and the one you cannot simply
                // walk away from — the fight is about finding something to put between you.
                Kind = ZombieKind.BossJaguar, Name = "The Green Mother", Weight = 0f,
                Health = 1200f, StaggerResistance = 0.72f,
                WanderSpeed = 1.4f, InvestigateSpeed = 3.8f, ChaseSpeed = 6.2f, TurnSpeed = 260f,
                AttackDamage = 33f, AttackCooldown = 2.1f, AttackWindup = 0.55f,
                AttackRange = 3.8f,
                SightRange = 34f, FieldOfView = 125f, MemorySeconds = 45f,
                HearingMultiplier = 1.5f,
                RifleDamageMultiplier = 1.6f,
                Scale = 2.3f, SkinTint = new Color(1f, 1f, 1f),
                StrideCyclesPerMetre = 0.55f, LurchDegrees = 4f
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

        public static ZombieArchetype PickRandom()
        {
            float total = 0f;
            foreach (ZombieArchetype archetype in Catalogue) total += archetype.Weight;

            float roll = Random.Range(0f, total);
            foreach (ZombieArchetype archetype in Catalogue)
            {
                roll -= archetype.Weight;
                if (roll <= 0f) return archetype;
            }

            return Catalogue[0];
        }
    }
}
