using System.Collections.Generic;
using UnityEngine;
using static ZombieHouse.Audio.ProceduralAudio;

namespace ZombieHouse.Audio
{
    public enum Sfx
    {
        Gunshot, GunshotHeavy, GunshotSmg, GatlingSpin, DryFire, ReloadOut, ReloadIn,
        ImpactWall, ImpactFlesh, ImpactCritical, ShellDrop,
        MacheteSwing, MacheteFlesh, MacheteMetal,
        Footstep, FootstepSand, Jump, Land,
        ZombieIdle, ZombieAlert, ZombieAttack, ZombieBite, ZombieHurt, ZombieDeath,
        PlayerHurt, Heartbeat,
        PickupAmmo, PickupHealth,
        SurvivorCall, SurvivorSaved,
        CellLift, MotorStart, MotorHum, BeltFeed,
        FlashlightClick, FlashlightDie, BatterySwap,
        ExitOpen, Victory, Defeat,
        Ambience, Music, MusicForest, MusicTomb, MusicJungle,

        // The two levels that had been borrowing someone else's bed.
        MusicTown, MusicSchool,

        // One tension layer per bed, mixed over it and faded in by ThreatMeter. Each is
        // exactly as long as the bed it sits on, or they drift apart within a minute.
        TensionHouse, TensionForest, TensionTown, TensionSchool, TensionTomb, TensionJungle,

        // Appended, never inserted. Everything above keeps the index it already had, which
        // is what stops a rebuild-less scene from changing its music track.
        ZombieRise, LightPop,

        // Merryland. Appended, like everything else here.
        MusicPark, TensionPark
    }

    /// <summary>
    /// The recipes. Each sound is a handful of synth layers with their own envelopes,
    /// mixed and normalised. Sounds with several entries are picked from at random so
    /// repeated footsteps and groans do not sound machine-gunned.
    ///
    /// Tuning here is the fastest way to change how the game feels — every number is
    /// audible. Building the whole bank takes a few milliseconds at startup.
    /// </summary>
    public static class SoundBank
    {
        public static Dictionary<Sfx, AudioClip[]> Build(int seed = 1337)
        {
            var rng = new System.Random(seed);

            return new Dictionary<Sfx, AudioClip[]>
            {
                { Sfx.ZombieRise,     Many(2, i => ZombieRise(rng, i)) },
                { Sfx.LightPop,       new[] { LightPop(rng) } },

                { Sfx.Gunshot,        new[] { Gunshot(rng) } },
                { Sfx.GunshotHeavy,   new[] { GunshotHeavy(rng) } },
                { Sfx.GunshotSmg,     Many(3, i => GunshotSmg(rng, i)) },
                { Sfx.GatlingSpin,    new[] { GatlingSpin() } },
                { Sfx.DryFire,        new[] { DryFire(rng) } },
                { Sfx.ReloadOut,      new[] { MagOut(rng) } },
                { Sfx.ReloadIn,       new[] { MagIn(rng) } },

                { Sfx.ImpactWall,     Many(3, i => ImpactWall(rng, i)) },
                { Sfx.ImpactFlesh,    Many(2, i => ImpactFlesh(rng, i)) },
                { Sfx.ImpactCritical, new[] { ImpactCritical(rng) } },
                { Sfx.ShellDrop,      Many(2, i => ShellDrop(rng, i)) },

                { Sfx.MacheteSwing,   Many(2, i => MacheteSwing(rng, i)) },
                { Sfx.MacheteFlesh,   Many(2, i => MacheteFlesh(rng, i)) },
                { Sfx.MacheteMetal,   new[] { MacheteMetal(rng) } },


                { Sfx.Footstep,       Many(4, i => Footstep(rng, i)) },
                { Sfx.FootstepSand,   Many(5, i => FootstepSand(rng, i)) },
                { Sfx.Jump,           new[] { Jump(rng) } },
                { Sfx.Land,           new[] { Land(rng) } },

                // Eight variants across a wide pitch and length spread, so a room full of
                // them sounds like a room full of different people rather than one loop.
                { Sfx.ZombieIdle,     Many(8, i => ZombieGrumble(rng, 32f + i * 3.4f, 1.7f + i * 0.28f)) },
                { Sfx.ZombieAlert,    new[] { ZombieAlert(rng) } },
                { Sfx.ZombieAttack,   Many(2, i => ZombieAttack(rng, i)) },
                { Sfx.ZombieBite,     Many(3, i => ZombieBite(rng, i)) },
                { Sfx.ZombieHurt,     Many(2, i => ZombieHurt(rng, i)) },
                { Sfx.ZombieDeath,    new[] { ZombieDeath(rng) } },

                { Sfx.PlayerHurt,     Many(2, i => PlayerHurt(rng, i)) },
                { Sfx.Heartbeat,      new[] { Heartbeat() } },

                { Sfx.PickupAmmo,     new[] { PickupAmmo(rng) } },
                { Sfx.PickupHealth,   new[] { PickupHealth() } },

                { Sfx.SurvivorCall,   Many(3, i => SurvivorCall(rng, i)) },
                { Sfx.SurvivorSaved,  new[] { SurvivorSaved(rng) } },

                { Sfx.CellLift,       new[] { CellLift(rng) } },
                { Sfx.BeltFeed,       new[] { BeltFeed(rng) } },
                { Sfx.MotorStart,     new[] { MotorStart(rng) } },
                { Sfx.MotorHum,       new[] { MotorHum() } },

                { Sfx.FlashlightClick, Many(2, i => FlashlightClick(rng, i)) },
                { Sfx.FlashlightDie,  new[] { FlashlightDie(rng) } },
                { Sfx.BatterySwap,    new[] { BatterySwap(rng) } },

                { Sfx.ExitOpen,       new[] { ExitOpen() } },
                { Sfx.Victory,        new[] { Victory() } },
                { Sfx.Defeat,         new[] { Defeat() } },

                { Sfx.Ambience,       new[] { Ambience(rng) } },
                { Sfx.Music,          new[] { HauntedMansion(rng) } },
                { Sfx.MusicForest,    new[] { HauntedForest(rng) } },
                { Sfx.MusicTomb,      new[] { HauntedTomb(rng) } },
                { Sfx.MusicJungle,    new[] { NightJungle(rng) } },
                { Sfx.MusicTown,      new[] { DustAndBone(rng) } },
                { Sfx.MusicSchool,    new[] { EmptyClassrooms(rng) } },

                { Sfx.TensionHouse,   new[] { Tension(48f, 55.00f, 0.9f, rng) } },
                { Sfx.TensionForest,  new[] { Tension(56f, 49.00f, 1.1f, rng) } },
                { Sfx.TensionTown,    new[] { Tension(56f, 58.27f, 0.8f, rng) } },
                { Sfx.TensionSchool,  new[] { Tension(48f, 65.41f, 1.0f, rng) } },
                { Sfx.TensionTomb,    new[] { Tension(64f, 43.65f, 1.25f, rng) } },
                { Sfx.TensionJungle,  new[] { Tension(72f, 48.99f, 1.15f, rng) } },

                { Sfx.MusicPark,      new[] { WaltzForNobody(rng) } },
                { Sfx.TensionPark,    new[] { Tension(60f, 61.74f, 0.95f, rng) } }
            };
        }

        private static AudioClip[] Many(int count, System.Func<int, AudioClip> make)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++) clips[i] = make(i);
            return clips;
        }

        // ---- weapon ---------------------------------------------------------

        private static AudioClip Gunshot(System.Random rng)
        {
            float length = 0.5f;

            // Crack: broadband noise, almost instant decay.
            var crack = Buffer(length);
            AddNoise(crack, 1f, rng);
            HighPass(crack, 700f);
            ApplyPercussiveEnvelope(crack, 0.0004f, 42f);

            // Body: the mid-range punch that makes it read as a firearm, not a hiss.
            var body = Buffer(length);
            AddNoise(body, 1f, rng);
            LowPass(body, 1800f);
            ApplyPercussiveEnvelope(body, 0.0008f, 20f);

            // Thump: the low end you feel.
            var thump = Buffer(length);
            AddSine(thump, 150f, 42f, 1f);
            ApplyPercussiveEnvelope(thump, 0.001f, 26f);

            var mix = Buffer(length);
            Mix(mix, crack, 0.85f);
            Mix(mix, body, 0.7f);
            Mix(mix, thump, 0.6f);

            // Slapback off the walls of a house-sized room.
            AddEcho(mix, 0.048f, 0.28f, 3);
            Saturate(mix, 1.4f);
            Normalize(mix, 0.95f);
            return ToClip("Gunshot", mix);
        }

        /// <summary>
        /// The Desert Eagle: one enormous bang and then almost nothing.
        ///
        /// It used to have two echo layers — a slapback and a long tail — and the result
        /// was a canyon rather than a gun. All of that is gone bar a single very short,
        /// very quiet reflection. What replaces it is **more bang**: a harder transient, a
        /// heavier body, and a low thump that arrives with the crack instead of after it,
        /// with the whole thing over in a third of a second.
        ///
        /// The lesson is worth keeping: reverb makes a sound *bigger* but a transient
        /// makes it *louder*, and for a handgun going off in your hands it is the
        /// transient you want.
        /// </summary>
        private static AudioClip GunshotHeavy(System.Random rng)
        {
            float length = 0.62f;

            // Crack: the transient. Faster attack and a much faster decay than before, so
            // it hits rather than swells.
            var crack = Buffer(length);
            AddNoise(crack, 1f, rng);
            HighPass(crack, 320f);
            LowPass(crack, 8500f);
            ApplyPercussiveEnvelope(crack, 0.0002f, 34f);

            // Body: the mid punch that gives it weight.
            var body = Buffer(length);
            AddNoise(body, 1f, rng);
            LowPass(body, 1200f);
            ApplyPercussiveEnvelope(body, 0.0006f, 17f);

            // Thump: the part you feel, and it lands with the crack rather than behind it.
            var thump = Buffer(length);
            AddSine(thump, 150f, 26f, 1f);
            ApplyPercussiveEnvelope(thump, 0.0008f, 13f);

            var mix = Buffer(length);
            Mix(mix, crack, 1f);
            Mix(mix, body, 0.95f);
            Mix(mix, thump, 1f);

            // One short reflection, quiet, purely so it does not sound recorded in a
            // vacuum. No long tail: this is a bang, not a canyon.
            AddEcho(mix, 0.035f, 0.14f, 2);

            Saturate(mix, 2.1f);
            Normalize(mix, 1f);
            return ToClip("GunshotHeavy", mix);
        }

        /// <summary>
        /// The Uzi. A 9 mm out of a short barrel is a flat, dry *snap* — almost no body
        /// and no low end at all, which is exactly what makes it sound small next to the
        /// .50. Three variants, because at 800 rounds a minute one sample repeating is a
        /// buzz rather than a burst.
        /// </summary>
        private static AudioClip GunshotSmg(System.Random rng, int variant)
        {
            float length = 0.28f;

            var crack = Buffer(length);
            AddNoise(crack, 1f, rng);
            HighPass(crack, 900f + variant * 120f);
            ApplyPercussiveEnvelope(crack, 0.0002f, 62f);

            var body = Buffer(length);
            AddNoise(body, 1f, rng);
            LowPass(body, 2400f);
            ApplyPercussiveEnvelope(body, 0.0005f, 40f);

            var thump = Buffer(length);
            AddSine(thump, 190f, 90f, 0.7f);
            ApplyPercussiveEnvelope(thump, 0.0008f, 46f);

            var mix = Buffer(length);
            Mix(mix, crack, 1f);
            Mix(mix, body, 0.55f);
            Mix(mix, thump, 0.4f);

            // The bolt clattering, which is most of what an open-bolt SMG sounds like.
            var bolt = Buffer(length);
            AddNoise(bolt, 1f, rng);
            HighPass(bolt, 2600f);
            ApplyPercussiveEnvelope(bolt, 0.0015f, 70f);
            DelayInto(mix, bolt, 0.02f, 0.35f);

            Saturate(mix, 1.5f);
            Normalize(mix, 0.72f);
            return ToClip("GunshotSmg" + variant, mix);
        }

        /// <summary>
        /// The barrels turning: a seamless loop of electric motor and mechanism, played
        /// back at a pitch that rides the spin-up so it whines up and coasts down.
        ///
        /// It has to be a loop rather than a one-shot because the spin can be interrupted
        /// at any point — release the trigger half way and it winds back down from
        /// wherever it got to. A fixed sample would have to be cut off mid-note.
        /// </summary>
        private static AudioClip GatlingSpin()
        {
            const float length = 1f;
            var mix = Buffer(length);

            // Motor: a saw and its octave, both exact multiples of 1/1 Hz so it loops.
            AddSaw(mix, 92f, 92f, 0.34f);
            AddSaw(mix, 184f, 184f, 0.16f);
            AddSine(mix, 46f, 46f, 0.3f);

            // Mechanism: a rattle at the barrel-passing rate, which is what stops it
            // sounding like a hair dryer.
            var mechanism = Buffer(length);
            AddSine(mechanism, 276f, 276f, 0.2f);
            AddSine(mechanism, 552f, 552f, 0.08f);
            Mix(mix, mechanism, 0.5f);

            LowPass(mix, 3200f);
            MakeSeamless(mix, 0.08f);
            Normalize(mix, 0.5f);
            return ToClip("GatlingSpin", mix, true);
        }

        /// <summary>
        /// A body pushing itself up off the floor: cloth dragging, a wet joint, a breath in.
        ///
        /// Built in that order deliberately, because the order is the story. The drag comes
        /// first and is the quietest part — it is the sound the player half-hears and turns
        /// towards. The breath lands last and is the one that gets them, and it is a breath
        /// *in*, because inhalation is what something does before it makes a noise at you.
        /// </summary>
        private static AudioClip ZombieRise(System.Random rng, int variant)
        {
            var data = Buffer(1.15f);

            // Cloth and grit dragging over floorboards. Filtered noise with a slow swell.
            AddNoise(data, 0.55f, rng);
            SweepLowPass(data, 900f, 320f);
            ApplyEnvelope(data, 0.30f, 0.55f);

            // The joint. A short creak that warbles, so it reads as something under load
            // rather than as a synthesiser doing a downward sweep.
            var creak = Buffer(0.55f);
            AddSaw(creak, 148f + variant * 21f, 96f, 0.38f);
            ApplyWarble(creak, 45f, 7.5f);
            LowPass(creak, 1500f);
            ApplyEnvelope(creak, 0.06f, 0.34f);
            Mix(data, creak, 0.7f);

            // The breath. Formants are what stop this being wind — three resonant peaks and
            // noise stops sounding like air and starts sounding like a throat.
            var breath = Buffer(0.62f);
            AddNoise(breath, 0.7f, rng);
            AddFormants(breath, 520f, 1180f, 2400f);
            ApplyReverseEnvelope(breath, 0.44f, 0.07f);
            Mix(data, breath, 0.85f);

            Normalize(data, 0.82f);
            return ToClip("ZombieRise" + variant, data);
        }

        /// <summary>
        /// A filament letting go: a glass tick, a mains-frequency buzz, and a small collapse.
        ///
        /// The buzz is the detail that sells it. A bulb failing is not a pure pop — for a
        /// fraction of a second the arc is still carrying current, and the ear knows the
        /// sound of mains hum well enough to notice when it is missing, even if nobody could
        /// name what they were listening for.
        /// </summary>
        private static AudioClip LightPop(System.Random rng)
        {
            var data = Buffer(0.42f);

            // The tick: glass and tungsten, bright and gone.
            AddNoise(data, 1f, rng);
            HighPass(data, 3200f);
            ApplyPercussiveEnvelope(data, 0.0004f, 120f);

            // The arc, at mains frequency and its octave. Loud briefly, then nothing.
            var arc = Buffer(0.42f);
            AddSine(arc, 50f, 50f, 0.5f);
            AddSine(arc, 100f, 97f, 0.32f);
            ApplyCrackle(arc, rng, burstsPerSecond: 55f, burstSeconds: 0.008f);
            ApplyPercussiveEnvelope(arc, 0.001f, 26f);
            Mix(data, arc, 0.75f);

            // The last of the light dying away, an octave down and falling.
            var fade = Buffer(0.42f);
            AddSine(fade, 1400f, 240f, 0.22f);
            ApplyPercussiveEnvelope(fade, 0.002f, 16f);
            Mix(data, fade, 0.5f);

            Normalize(data, 0.72f);
            return ToClip("LightPop", data);
        }

        private static AudioClip DryFire(System.Random rng)
        {
            var data = Buffer(0.09f);
            AddNoise(data, 1f, rng);
            HighPass(data, 2200f);
            ApplyPercussiveEnvelope(data, 0.0003f, 90f);

            var tick = Buffer(0.09f);
            AddSine(tick, 1900f, 900f, 0.6f);
            ApplyPercussiveEnvelope(tick, 0.0003f, 110f);

            Mix(data, tick, 0.5f);
            Normalize(data, 0.5f);
            return ToClip("DryFire", data);
        }

        /// <summary>
        /// A person calling out. Two syllables, the second falling away — the shape of
        /// "over here" rather than the words, which is as close to speech as anything in
        /// this project gets.
        ///
        /// It has to be unmistakably human within a fraction of a second, because it is
        /// the only thing separating a survivor from something worth shooting. That means
        /// a voiced fundamental with real formants over it, where the zombie grumble is a
        /// sawtooth with the life filtered out of it.
        /// </summary>
        private static AudioClip SurvivorCall(System.Random rng, int variant)
        {
            const float length = 1.5f;
            var mix = Buffer(length);

            // Higher and thinner for one of the three, so the level is not one person
            // recorded three times.
            float root = variant == 2 ? 232f : 188f + variant * 16f;

            var first = Voice(root, root * 1.06f, 0.42f, rng);
            DelayInto(mix, first, 0.04f, 1f);

            // The second syllable drops a fourth and trails off: a call, not a shout.
            var second = Voice(root * 0.76f, root * 0.68f, 0.62f, rng);
            DelayInto(mix, second, 0.56f, 0.9f);

            AddEcho(mix, 0.13f, 0.22f);
            Normalize(mix, 0.6f);
            return ToClip("SurvivorCall" + variant, mix);
        }

        /// <summary>
        /// One voiced syllable: a fundamental with its harmonics, two formant bands to
        /// make it a vowel, vibrato so it is not a machine, and breath under it.
        /// </summary>
        private static float[] Voice(float startHz, float endHz, float seconds, System.Random rng)
        {
            var note = Buffer(seconds);

            AddSaw(note, startHz, endHz, 0.5f);
            AddSine(note, startHz * 2f, endHz * 2f, 0.16f);
            AddSine(note, startHz * 3f, endHz * 3f, 0.07f);

            // The vowel. Two peaks around 700 and 1150 Hz read as an open "eh".
            var formant = Buffer(seconds);
            Mix(formant, note, 1f);
            HighPass(formant, 620f);
            LowPass(formant, 1300f);
            Mix(note, formant, 0.9f);

            var breath = Buffer(seconds);
            AddNoise(breath, 1f, rng);
            HighPass(breath, 1800f);
            LowPass(breath, 5200f);
            Mix(note, breath, 0.05f);

            // Vibrato, uneven, because the person is frightened rather than singing.
            for (int i = 0; i < note.Length; i++)
            {
                float t = (float)i / SampleRate;
                note[i] *= 1f + 0.09f * Mathf.Sin(2f * Mathf.PI * 5.4f * t)
                              + 0.04f * Mathf.Sin(2f * Mathf.PI * 8.9f * t);
            }

            ApplyEnvelope(note, seconds * 0.16f, seconds * 0.5f);
            LowPass(note, 3400f);
            return note;
        }

        /// <summary>The breath they have been holding, and two notes going up for once.</summary>
        private static AudioClip SurvivorSaved(System.Random rng)
        {
            const float length = 1.9f;
            var mix = Buffer(length);

            var exhale = Buffer(0.8f);
            AddNoise(exhale, 1f, rng);
            HighPass(exhale, 700f);
            LowPass(exhale, 3800f);
            ApplyEnvelope(exhale, 0.06f, 0.6f);
            DelayInto(mix, exhale, 0f, 0.35f);

            // A rising fifth: the only unambiguously good sound in the game.
            var low = Buffer(0.7f);
            AddSine(low, 392f, 392f, 0.5f);      // G4
            AddSine(low, 784f, 784f, 0.16f);
            ApplyEnvelope(low, 0.03f, 0.5f);
            DelayInto(mix, low, 0.42f, 1f);

            var high = Buffer(1.1f);
            AddSine(high, 587.33f, 587.33f, 0.5f);   // D5
            AddSine(high, 1174.66f, 1174.66f, 0.16f);
            ApplyEnvelope(high, 0.03f, 0.85f);
            DelayInto(mix, high, 0.74f, 1f);

            AddEcho(mix, 0.16f, 0.25f);
            Normalize(mix, 0.5f);
            return ToClip("SurvivorSaved", mix);
        }

        /// <summary>Something heavy coming off the floor: a grunt of effort under a metal scrape.</summary>
        /// <summary>A belt box thumping down and a length of link feeding into the gun.</summary>
        private static AudioClip BeltFeed(System.Random rng)
        {
            var mix = Buffer(0.9f);

            var thud = Buffer(0.9f);
            AddSine(thud, 190f, 70f, 1f);
            ApplyPercussiveEnvelope(thud, 0.0015f, 22f);
            Mix(mix, thud, 0.8f);

            // The link: a fast rattle of small metal parts.
            var link = Buffer(0.9f);
            AddNoise(link, 1f, rng);
            HighPass(link, 2200f);
            ApplyCrackle(link, rng, 55f);
            ApplyEnvelope(link, 0.03f, 0.45f);
            DelayInto(mix, link, 0.12f, 0.55f);

            var latch = Buffer(0.9f);
            AddSine(latch, 900f, 520f, 0.6f);
            ApplyPercussiveEnvelope(latch, 0.0004f, 70f);
            DelayInto(mix, latch, 0.62f, 0.7f);

            Normalize(mix, 0.62f);
            return ToClip("BeltFeed", mix);
        }

        private static AudioClip CellLift(System.Random rng)
        {
            var mix = Buffer(0.7f);

            var scrape = Buffer(0.7f);
            AddNoise(scrape, 1f, rng);
            HighPass(scrape, 900f);
            LowPass(scrape, 4200f);
            ApplyEnvelope(scrape, 0.03f, 0.42f);
            Mix(mix, scrape, 0.35f);

            var clunk = Buffer(0.7f);
            AddSine(clunk, 165f, 84f, 1f);
            ApplyPercussiveEnvelope(clunk, 0.002f, 16f);
            Mix(mix, clunk, 0.9f);

            Normalize(mix, 0.6f);
            return ToClip("CellLift", mix);
        }

        /// <summary>
        /// A starter motor catching: the whine climbing, a clatter as it engages, and
        /// then it settles. The rising pitch is the whole story — it is the first thing
        /// in the level that gets better rather than worse.
        /// </summary>
        private static AudioClip MotorStart(System.Random rng)
        {
            const float length = 2.4f;
            var mix = Buffer(length);

            // The starter: a saw sweeping up, thin and electrical.
            var whine = Buffer(1.5f);
            AddSaw(whine, 120f, 430f, 0.5f);
            AddSine(whine, 240f, 860f, 0.2f);
            ApplyEnvelope(whine, 0.25f, 0.5f);
            BandLimit(whine, 200f, 5200f);
            DelayInto(mix, whine, 0f, 1f);

            // The clatter of it engaging, part way through.
            var clatter = Buffer(0.5f);
            AddNoise(clatter, 1f, rng);
            HighPass(clatter, 700f);
            ApplyCrackle(clatter, rng, 40f);
            ApplyEnvelope(clatter, 0.01f, 0.3f);
            DelayInto(mix, clatter, 0.85f, 0.5f);

            // And then it is running.
            var settle = Buffer(1.1f);
            AddSaw(settle, 108f, 96f, 0.45f);
            AddSine(settle, 54f, 48f, 0.4f);
            LowPass(settle, 1400f);
            ApplyEnvelope(settle, 0.12f, 0.5f);
            DelayInto(mix, settle, 1.3f, 1f);

            Normalize(mix, 0.7f);
            return ToClip("MotorStart", mix);
        }

        /// <summary>The motor running: a seamless four-second loop you can hear across the level.</summary>
        private static AudioClip MotorHum()
        {
            const float length = 4f;
            var mix = Buffer(length);

            // Exact multiples of 1/4 Hz so the loop is genuinely seamless.
            AddSine(mix, 48f, 48f, 0.5f);
            AddSine(mix, 96f, 96f, 0.22f);
            AddSine(mix, 144.25f, 144.25f, 0.10f);
            AddSaw(mix, 24f, 24f, 0.16f);

            LowPass(mix, 900f);
            MakeSeamless(mix, 0.4f);
            Normalize(mix, 0.5f);
            return ToClip("MotorHum", mix, true);
        }

        /// <summary>Both ends of a band, in the order that keeps the pass band intact.</summary>
        private static void BandLimit(float[] data, float lowHz, float highHz)
        {
            HighPass(data, lowHz);
            LowPass(data, highHz);
        }

        /// <summary>
        /// The torch switch: a small plastic snap, nothing like the gun's steel. Two
        /// variants so on and off are not the identical sample back to back.
        /// </summary>
        private static AudioClip FlashlightClick(System.Random rng, int variant)
        {
            var data = Buffer(0.07f);
            AddNoise(data, 1f, rng);
            HighPass(data, 3000f);
            ApplyPercussiveEnvelope(data, 0.0002f, 150f);

            var snap = Buffer(0.07f);
            AddSine(snap, 2600f - variant * 300f, 1400f, 0.5f);
            ApplyPercussiveEnvelope(snap, 0.0002f, 170f);

            Mix(data, snap, 0.45f);
            Normalize(data, 0.4f);
            return ToClip("FlashlightClick" + variant, data);
        }

        /// <summary>
        /// The cell giving up: the filament sagging away rather than a switch. Falling
        /// pitch and no attack, so it reads as something failing, not something pressed.
        /// </summary>
        private static AudioClip FlashlightDie(System.Random rng)
        {
            var data = Buffer(0.5f);
            AddSine(data, 320f, 60f, 0.7f);
            AddNoise(data, 0.25f, rng);
            LowPass(data, 1400f);
            ApplyEnvelope(data, 0.02f, 0.42f);
            Normalize(data, 0.42f);
            return ToClip("FlashlightDie", data);
        }

        /// <summary>Dead cell out, fresh cell in: two knocks with a pocket rattle between.</summary>
        private static AudioClip BatterySwap(System.Random rng)
        {
            var data = Buffer(0.55f);

            var outKnock = Buffer(0.55f);
            AddSine(outKnock, 300f, 150f, 0.8f);
            ApplyPercussiveEnvelope(outKnock, 0.001f, 34f);
            Mix(data, outKnock, 0.7f);

            var rattle = Buffer(0.55f);
            AddNoise(rattle, 1f, rng);
            HighPass(rattle, 1800f);
            ApplyCrackle(rattle, rng, 30f);
            ApplyEnvelope(rattle, 0.05f, 0.22f);
            Mix(data, rattle, 0.3f);

            // The seat lands late, so the swap has a beginning and an end you can hear.
            var seat = Buffer(0.55f);
            AddSine(seat, 240f, 120f, 1f);
            ApplyPercussiveEnvelope(seat, 0.0012f, 24f);
            DelayInto(data, seat, 0.34f, 0.8f);

            Normalize(data, 0.55f);
            return ToClip("BatterySwap", data);
        }

        /// <summary>Mixes <paramref name="source"/> into <paramref name="destination"/> starting late.</summary>
        private static void DelayInto(float[] destination, float[] source, float delaySeconds, float gain)
        {
            int offset = Samples(delaySeconds);
            for (int i = 0; i + offset < destination.Length && i < source.Length; i++)
                destination[i + offset] += source[i] * gain;
        }

        private static AudioClip MagOut(System.Random rng)
        {
            var data = Buffer(0.22f);
            AddNoise(data, 1f, rng);
            HighPass(data, 1200f);
            LowPass(data, 6000f);
            ApplyPercussiveEnvelope(data, 0.0005f, 55f);

            var clunk = Buffer(0.22f);
            AddSine(clunk, 420f, 220f, 0.8f);
            ApplyPercussiveEnvelope(clunk, 0.001f, 40f);

            Mix(data, clunk, 0.6f);
            Normalize(data, 0.55f);
            return ToClip("ReloadOut", data);
        }

        private static AudioClip MagIn(System.Random rng)
        {
            var data = Buffer(0.28f);
            AddNoise(data, 1f, rng);
            HighPass(data, 900f);
            ApplyPercussiveEnvelope(data, 0.0005f, 38f);

            // Heavier, lower seat than the mag release.
            var seat = Buffer(0.28f);
            AddSine(seat, 260f, 130f, 1f);
            ApplyPercussiveEnvelope(seat, 0.0012f, 26f);

            Mix(data, seat, 0.75f);
            Saturate(data, 1.2f);
            Normalize(data, 0.62f);
            return ToClip("ReloadIn", data);
        }

        private static AudioClip ShellDrop(System.Random rng, int variant)
        {
            var data = Buffer(0.3f);
            AddNoise(data, 1f, rng);
            HighPass(data, 3000f);
            ApplyPercussiveEnvelope(data, 0.0003f, 60f);

            var ring = Buffer(0.3f);
            AddSine(ring, 3400f + variant * 500f, 2600f + variant * 400f, 0.5f);
            ApplyPercussiveEnvelope(ring, 0.0005f, 22f);

            Mix(data, ring, 0.6f);
            AddEcho(data, 0.07f, 0.25f, 2);
            Normalize(data, 0.28f);
            return ToClip("ShellDrop" + variant, data);
        }

        // ---- machete --------------------------------------------------------

        private static AudioClip MacheteSwing(System.Random rng, int variant)
        {
            // A whoosh is filtered noise that rises and falls as the blade passes the ear.
            float length = 0.42f;
            var data = Buffer(length);
            AddNoise(data, 1f, rng);
            LowPass(data, 1400f + variant * 350f);
            HighPass(data, 260f);

            // Bell-shaped envelope: quietest at the ends, loudest as it sweeps past.
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / data.Length;
                data[i] *= Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * Mathf.PI);
            }

            Normalize(data, 0.42f);
            return ToClip("MacheteSwing" + variant, data);
        }

        private static AudioClip MacheteFlesh(System.Random rng, int variant)
        {
            // A chop: wet body, a bony crack, and a low thump underneath.
            float length = 0.4f;

            var wet = Buffer(length);
            AddNoise(wet, 1f, rng);
            LowPass(wet, 800f + variant * 120f);
            ApplyPercussiveEnvelope(wet, 0.001f, 24f);

            var crack = Buffer(length);
            AddNoise(crack, 1f, rng);
            HighPass(crack, 1500f);
            ApplyPercussiveEnvelope(crack, 0.0005f, 60f);

            var thump = Buffer(length);
            AddSine(thump, 120f, 45f, 1f);
            ApplyPercussiveEnvelope(thump, 0.002f, 20f);

            Mix(wet, crack, 0.45f);
            Mix(wet, thump, 0.9f);
            Saturate(wet, 1.5f);
            Normalize(wet, 0.85f);
            return ToClip("MacheteFlesh" + variant, wet);
        }

        private static AudioClip MacheteMetal(System.Random rng)
        {
            // Steel into masonry: a bright ring with a long metallic tail.
            float length = 0.6f;

            var strike = Buffer(length);
            AddNoise(strike, 1f, rng);
            HighPass(strike, 2200f);
            ApplyPercussiveEnvelope(strike, 0.0004f, 45f);

            var ring = Buffer(length);
            AddSine(ring, 2600f, 2350f, 0.5f);
            AddSine(ring, 3900f, 3700f, 0.3f);
            AddSine(ring, 5400f, 5200f, 0.15f);
            ApplyPercussiveEnvelope(ring, 0.001f, 9f);

            Mix(strike, ring, 0.8f);
            AddEcho(strike, 0.05f, 0.22f, 2);
            Normalize(strike, 0.6f);
            return ToClip("MacheteMetal", strike);
        }






        // ---- impacts --------------------------------------------------------

        private static AudioClip ImpactWall(System.Random rng, int variant)
        {
            var data = Buffer(0.25f);
            AddNoise(data, 1f, rng);
            LowPass(data, 3000f + variant * 900f);
            HighPass(data, 250f);
            ApplyPercussiveEnvelope(data, 0.0004f, 48f);

            var thud = Buffer(0.25f);
            AddSine(thud, 220f - variant * 30f, 90f, 0.7f);
            ApplyPercussiveEnvelope(thud, 0.001f, 40f);

            Mix(data, thud, 0.5f);
            Normalize(data, 0.5f);
            return ToClip("ImpactWall" + variant, data);
        }

        private static AudioClip ImpactFlesh(System.Random rng, int variant)
        {
            // Wet and dull: heavy low pass, no high content at all.
            var data = Buffer(0.3f);
            AddNoise(data, 1f, rng);
            LowPass(data, 700f + variant * 150f);
            ApplyPercussiveEnvelope(data, 0.002f, 26f);

            var thump = Buffer(0.3f);
            AddSine(thump, 130f, 55f, 1f);
            ApplyPercussiveEnvelope(thump, 0.003f, 22f);

            Mix(data, thump, 0.85f);
            Saturate(data, 1.3f);
            Normalize(data, 0.7f);
            return ToClip("ImpactFlesh" + variant, data);
        }

        private static AudioClip ImpactCritical(System.Random rng)
        {
            // Headshot: the flesh thump plus a sharp crack on top.
            var data = Buffer(0.35f);
            AddNoise(data, 1f, rng);
            LowPass(data, 900f);
            ApplyPercussiveEnvelope(data, 0.001f, 22f);

            var crack = Buffer(0.35f);
            AddNoise(crack, 1f, rng);
            HighPass(crack, 1800f);
            ApplyPercussiveEnvelope(crack, 0.0004f, 55f);

            var thump = Buffer(0.35f);
            AddSine(thump, 110f, 40f, 1f);
            ApplyPercussiveEnvelope(thump, 0.002f, 18f);

            Mix(data, crack, 0.55f);
            Mix(data, thump, 0.9f);
            Saturate(data, 1.5f);
            Normalize(data, 0.85f);
            return ToClip("ImpactCritical", data);
        }

        // ---- movement -------------------------------------------------------

        private static AudioClip Footstep(System.Random rng, int variant)
        {
            var data = Buffer(0.2f);
            AddNoise(data, 1f, rng);
            LowPass(data, 900f + variant * 220f);
            ApplyPercussiveEnvelope(data, 0.002f, 38f + variant * 4f);

            // A little board creak under the step.
            var creak = Buffer(0.2f);
            AddSine(creak, 190f + variant * 25f, 120f, 0.5f);
            ApplyPercussiveEnvelope(creak, 0.004f, 30f);

            Mix(data, creak, 0.35f);
            Normalize(data, 0.34f);
            return ToClip("Footstep" + variant, data);
        }

        /// <summary>
        /// Shuffling through sand. Deliberately unlike the wooden footstep: no impact, no
        /// thump and no ring, because sand absorbs all of that. What is left is a soft
        /// broadband hiss that swells as the foot drags and dies away, with a fine grain
        /// under it — sand is thousands of tiny collisions rather than one.
        /// </summary>
        private static AudioClip FootstepSand(System.Random rng, int variant)
        {
            float length = 0.36f;

            var shuffle = Buffer(length);
            AddNoise(shuffle, 1f, rng);
            HighPass(shuffle, 380f + variant * 60f);    // strip the low end out entirely
            LowPass(shuffle, 4600f + variant * 500f);   // soft top, not a hiss of static

            // Fast swell, long gentle tail — a drag, not a strike.
            for (int i = 0; i < shuffle.Length; i++)
            {
                float t = (float)i / shuffle.Length;
                shuffle[i] *= Mathf.Pow(t, 0.45f) * Mathf.Exp(-4.2f * t);
            }

            // The grain of individual sand particles.
            var grain = Buffer(length);
            AddNoise(grain, 1f, rng);
            HighPass(grain, 2200f);
            ApplyCrackle(grain, rng, 220f, 0.005f, 0.12f);

            for (int i = 0; i < grain.Length; i++)
            {
                float t = (float)i / grain.Length;
                grain[i] *= Mathf.Pow(t, 0.4f) * Mathf.Exp(-5f * t);
            }

            // A trace of the hard floor under the sand: a faint high tick, far back in the
            // mix. Without it this is sand on a beach; with it, sand on marble.
            var marble = Buffer(length);
            AddNoise(marble, 1f, rng);
            HighPass(marble, 6000f);
            ApplyPercussiveEnvelope(marble, 0.0004f, 120f);

            Mix(shuffle, grain, 0.35f);
            Mix(shuffle, marble, 0.16f);
            Normalize(shuffle, 0.17f);
            return ToClip("FootstepSand" + variant, shuffle);
        }

        private static AudioClip Jump(System.Random rng)
        {
            var data = Buffer(0.18f);
            AddNoise(data, 1f, rng);
            LowPass(data, 1400f);
            ApplyPercussiveEnvelope(data, 0.002f, 40f);
            Normalize(data, 0.3f);
            return ToClip("Jump", data);
        }

        private static AudioClip Land(System.Random rng)
        {
            var data = Buffer(0.3f);
            AddNoise(data, 1f, rng);
            LowPass(data, 700f);
            ApplyPercussiveEnvelope(data, 0.002f, 26f);

            var thud = Buffer(0.3f);
            AddSine(thud, 120f, 50f, 1f);
            ApplyPercussiveEnvelope(thud, 0.003f, 24f);

            Mix(data, thud, 0.8f);
            Normalize(data, 0.5f);
            return ToClip("Land", data);
        }

        // ---- zombie voice ---------------------------------------------------

        /// <summary>
        /// The zombie voice: a slow, low human grumble with a wet crackle running
        /// through it — closer to something sputtering in a pan than to a monster roar.
        ///
        /// Four layers: a very low sawtooth for the vocal cords, a sub-octave chest
        /// rumble, a gated crackle for the sizzle, and a little breath. Everything is
        /// heavily low-passed so it sits in the chest rather than the throat.
        /// </summary>
        private static AudioClip ZombieGrumble(System.Random rng, float fundamental, float length)
        {
            // Vocal cords — slow wobble, not a fast tremolo. Kept very low and dark.
            var voice = Buffer(length);
            AddSaw(voice, fundamental, fundamental * 0.86f, 0.55f, 20, 1.7f, 0.055f);
            LowPass(voice, 360f);
            ApplyEnvelope(voice, length * 0.34f, length * 0.5f);

            // Chest: a fifth below, felt more than heard. Going a full octave down from a
            // fundamental this low would drop out of range on most speakers entirely.
            var chest = Buffer(length);
            AddSine(chest, fundamental * 0.67f, fundamental * 0.62f, 0.7f, 1.2f, 0.07f);
            ApplyEnvelope(chest, length * 0.3f, length * 0.55f);

            // The fry: irregular pops, band-limited so they read as wet, not as static.
            // Density varies per clip so some walkers rattle and others just rumble.
            float crackleDensity = 18f + (float)rng.NextDouble() * 34f;

            var crackle = Buffer(length);
            AddNoise(crackle, 1f, rng);
            HighPass(crackle, 700f);
            LowPass(crackle, 3200f);
            ApplyCrackle(crackle, rng, crackleDensity, 0.022f, 0.05f);
            ApplyEnvelope(crackle, length * 0.28f, length * 0.45f);

            var breath = Buffer(length);
            AddNoise(breath, 1f, rng);
            LowPass(breath, 700f);
            ApplyEnvelope(breath, length * 0.35f, length * 0.5f);

            var mix = Buffer(length);
            Mix(mix, voice, 1f);
            Mix(mix, chest, 0.55f + (float)rng.NextDouble() * 0.5f);
            Mix(mix, crackle, 0.18f + (float)rng.NextDouble() * 0.28f);
            Mix(mix, breath, 0.08f + (float)rng.NextDouble() * 0.16f);

            Saturate(mix, 1.4f);
            Normalize(mix, 0.40f);
            return ToClip("ZombieGrumble" + Mathf.RoundToInt(fundamental), mix);
        }

        /// <summary>
        /// The bite itself: a jaw snapping shut, then teeth in flesh. Played when an
        /// attack actually connects, so it doubles as the "you have been hit" cue.
        /// </summary>
        private static AudioClip ZombieBite(System.Random rng, int variant)
        {
            float length = 0.5f;

            // Jaw snap — a hard, short click.
            var snap = Buffer(length);
            AddNoise(snap, 1f, rng);
            HighPass(snap, 1400f);
            ApplyPercussiveEnvelope(snap, 0.0004f, 70f);

            var bone = Buffer(length);
            AddSine(bone, 520f - variant * 60f, 190f, 0.7f);
            ApplyPercussiveEnvelope(bone, 0.0008f, 46f);

            // Wet tearing underneath.
            var wet = Buffer(length);
            AddNoise(wet, 1f, rng);
            LowPass(wet, 850f);
            ApplyPercussiveEnvelope(wet, 0.004f, 14f);

            // A short guttural surge as it clamps down.
            var growl = Buffer(length);
            AddSaw(growl, 90f + variant * 12f, 62f, 0.55f, 12, 7f, 0.06f);
            LowPass(growl, 900f);
            ApplyPercussiveEnvelope(growl, 0.01f, 9f);

            Mix(snap, bone, 0.7f);
            Mix(snap, wet, 0.8f);
            Mix(snap, growl, 0.85f);

            Saturate(snap, 1.8f);
            Normalize(snap, 0.9f);
            return ToClip("ZombieBite" + variant, snap);
        }

        private static AudioClip ZombieAlert(System.Random rng)
        {
            // The grumble rising into a growl — the "it has seen you" cue. Still low and
            // human, just louder and climbing.
            float length = 1.3f;

            var voice = Buffer(length);
            AddSaw(voice, 44f, 82f, 0.65f, 18, 2.6f, 0.06f);
            LowPass(voice, 700f);
            ApplyEnvelope(voice, 0.09f, 0.4f);

            var chest = Buffer(length);
            AddSine(chest, 30f, 54f, 0.6f, 1.8f, 0.08f);
            ApplyEnvelope(chest, 0.1f, 0.45f);

            var crackle = Buffer(length);
            AddNoise(crackle, 1f, rng);
            HighPass(crackle, 800f);
            LowPass(crackle, 3400f);
            ApplyCrackle(crackle, rng, 42f, 0.02f, 0.07f);
            ApplyEnvelope(crackle, 0.08f, 0.4f);

            Mix(voice, chest, 0.8f);
            Mix(voice, crackle, 0.34f);
            Saturate(voice, 1.8f);
            Normalize(voice, 0.58f);
            return ToClip("ZombieAlert", voice);
        }

        private static AudioClip ZombieAttack(System.Random rng, int variant)
        {
            // The lunge: the grumble breaking into a snarl, but kept in the chest.
            float length = 0.7f;

            var voice = Buffer(length);
            AddSaw(voice, 56f + variant * 8f, 94f + variant * 12f, 0.72f, 16, 7f, 0.07f);
            LowPass(voice, 1000f);
            ApplyPercussiveEnvelope(voice, 0.02f, 5.5f);

            var crackle = Buffer(length);
            AddNoise(crackle, 1f, rng);
            HighPass(crackle, 600f);
            LowPass(crackle, 3000f);
            ApplyCrackle(crackle, rng, 55f, 0.016f, 0.1f);
            ApplyPercussiveEnvelope(crackle, 0.012f, 7f);

            var snarl = Buffer(length);
            AddNoise(snarl, 1f, rng);
            LowPass(snarl, 1800f);
            HighPass(snarl, 250f);
            ApplyPercussiveEnvelope(snarl, 0.01f, 8f);

            Mix(voice, crackle, 0.4f);
            Mix(voice, snarl, 0.28f);
            Saturate(voice, 1.9f);
            Normalize(voice, 0.62f);
            return ToClip("ZombieAttack" + variant, voice);
        }

        private static AudioClip ZombieHurt(System.Random rng, int variant)
        {
            float length = 0.4f;
            var voice = Buffer(length);
            AddSaw(voice, 150f + variant * 25f, 90f, 0.7f, 10, 12f, 0.07f);
            LowPass(voice, 1500f);
            ApplyPercussiveEnvelope(voice, 0.006f, 11f);

            var breath = Buffer(length);
            AddNoise(breath, 1f, rng);
            LowPass(breath, 1800f);
            ApplyPercussiveEnvelope(breath, 0.004f, 14f);

            Mix(voice, breath, 0.3f);
            Saturate(voice, 1.8f);
            Normalize(voice, 0.7f);
            return ToClip("ZombieHurt" + variant, voice);
        }

        private static AudioClip ZombieDeath(System.Random rng)
        {
            // Long fall in pitch, trailing into breath.
            float length = 1.6f;
            var voice = Buffer(length);
            AddSaw(voice, 130f, 42f, 0.7f, 12, 5f, 0.04f);
            LowPass(voice, 1100f);
            ApplyPercussiveEnvelope(voice, 0.02f, 2.4f);

            var breath = Buffer(length);
            AddNoise(breath, 1f, rng);
            LowPass(breath, 900f);
            ApplyPercussiveEnvelope(breath, 0.05f, 2.8f);

            Mix(voice, breath, 0.35f);
            Saturate(voice, 1.6f);
            Normalize(voice, 0.8f);
            return ToClip("ZombieDeath", voice);
        }

        // ---- player ---------------------------------------------------------

        private static AudioClip PlayerHurt(System.Random rng, int variant)
        {
            float length = 0.35f;
            var voice = Buffer(length);
            AddSaw(voice, 165f + variant * 20f, 120f, 0.6f, 8, 8f, 0.03f);
            LowPass(voice, 1300f);
            ApplyPercussiveEnvelope(voice, 0.008f, 12f);

            var breath = Buffer(length);
            AddNoise(breath, 1f, rng);
            LowPass(breath, 2000f);
            ApplyPercussiveEnvelope(breath, 0.005f, 15f);

            Mix(voice, breath, 0.4f);
            Normalize(voice, 0.65f);
            return ToClip("PlayerHurt" + variant, voice);
        }

        private static AudioClip Heartbeat()
        {
            float length = 1.0f;
            var data = Buffer(length);

            var first = Buffer(length);
            AddSine(first, 62f, 38f, 1f);
            ApplyPercussiveEnvelope(first, 0.004f, 22f);
            Mix(data, first, 1f);

            // Second, softer beat offset by ~0.26 s.
            var second = Buffer(length);
            AddSine(second, 55f, 34f, 0.75f);
            ApplyPercussiveEnvelope(second, 0.004f, 24f);

            int offset = Samples(0.26f);
            for (int i = data.Length - 1; i >= offset; i--)
                data[i] += second[i - offset];

            Normalize(data, 0.8f);
            return ToClip("Heartbeat", data);
        }

        private static AudioClip PickupAmmo(System.Random rng)
        {
            var data = Buffer(0.28f);
            AddNoise(data, 1f, rng);
            HighPass(data, 2500f);
            ApplyPercussiveEnvelope(data, 0.001f, 30f);

            var tone = Buffer(0.28f);
            AddSine(tone, 700f, 1050f, 0.7f);
            ApplyPercussiveEnvelope(tone, 0.004f, 14f);

            Mix(data, tone, 0.8f);
            Normalize(data, 0.5f);
            return ToClip("PickupAmmo", data);
        }

        private static AudioClip PickupHealth()
        {
            var data = Buffer(0.5f);
            AddSine(data, 440f, 660f, 0.6f);
            AddSine(data, 660f, 880f, 0.35f);
            ApplyEnvelope(data, 0.02f, 0.3f);
            Normalize(data, 0.5f);
            return ToClip("PickupHealth", data);
        }

        // ---- stingers -------------------------------------------------------

        private static AudioClip ExitOpen()
        {
            float length = 1.6f;
            var data = Buffer(length);
            AddSine(data, 220f, 220f, 0.5f);
            AddSine(data, 330f, 330f, 0.4f);
            AddSine(data, 440f, 440f, 0.3f);
            AddSine(data, 110f, 110f, 0.4f);
            ApplyEnvelope(data, 0.35f, 0.7f);
            Normalize(data, 0.55f);
            return ToClip("ExitOpen", data);
        }

        private static AudioClip Victory()
        {
            float length = 2.0f;
            var data = Buffer(length);

            // Rising major arpeggio: G - B - D - G.
            float[] notes = { 196f, 246.9f, 293.7f, 392f };
            int noteLength = data.Length / notes.Length;

            for (int n = 0; n < notes.Length; n++)
            {
                var note = Buffer((float)noteLength / SampleRate + 0.5f);
                AddSine(note, notes[n], notes[n], 0.5f);
                AddSine(note, notes[n] * 2f, notes[n] * 2f, 0.22f);
                ApplyPercussiveEnvelope(note, 0.01f, 2.6f);

                int offset = n * noteLength;
                for (int i = 0; i < note.Length && offset + i < data.Length; i++)
                    data[offset + i] += note[i];
            }

            Normalize(data, 0.6f);
            return ToClip("Victory", data);
        }

        private static AudioClip Defeat()
        {
            float length = 2.2f;
            var data = Buffer(length);

            // Falling minor: D - Bb - F - D, an octave down.
            float[] notes = { 146.8f, 116.5f, 87.3f, 73.4f };
            int noteLength = data.Length / notes.Length;

            for (int n = 0; n < notes.Length; n++)
            {
                var note = Buffer((float)noteLength / SampleRate + 0.7f);
                AddSaw(note, notes[n], notes[n] * 0.99f, 0.4f, 6);
                LowPass(note, 800f);
                ApplyPercussiveEnvelope(note, 0.02f, 2f);

                int offset = n * noteLength;
                for (int i = 0; i < note.Length && offset + i < data.Length; i++)
                    data[offset + i] += note[i];
            }

            Saturate(data, 1.3f);
            Normalize(data, 0.6f);
            return ToClip("Defeat", data);
        }

        // ---- ambience -------------------------------------------------------

        /// <summary>
        /// Haunted mansion music: a 48-second loop of a broken music box over a low
        /// organ drone, in D minor.
        ///
        /// The trick to making it unsettling rather than merely sad is irregularity — the
        /// notes are spaced unevenly, occasionally struck twice, and detuned slightly
        /// against themselves, the way a mechanism that has not been wound in decades
        /// would play. A tritone drone sits underneath and never resolves.
        /// </summary>
        /// <summary>
        /// The wood at night: a 56-second loop with no melody in it at all.
        ///
        /// The mansion gets a music box because a house is a made thing with a history.
        /// Outside there is nothing that was ever tuned, so this is wind, a drone two
        /// octaves below anything you would call a note, and three sounds that arrive on
        /// their own schedule — a bowed harmonic swelling out of nowhere, a trunk cracking
        /// somewhere behind you, and a two-note call at the edge of hearing that could be
        /// a bird and probably is not. Nothing resolves and nothing repeats on the beat,
        /// so the loop never announces itself.
        /// </summary>
        private static AudioClip HauntedForest(System.Random rng)
        {
            const float length = 56f;
            var mix = Buffer(length);

            // --- wind: two bands of noise breathing at different rates ------------
            var wind = Buffer(length);
            AddNoise(wind, 1f, rng);
            LowPass(wind, 460f);
            HighPass(wind, 70f);
            Swell(wind, 1f / 19f, 0.30f, 1f);
            Mix(mix, wind, 0.55f);

            // The higher band is the wind actually moving through leaves, and it gusts
            // harder and less often than the body of it underneath.
            var gust = Buffer(length);
            AddNoise(gust, 1f, rng);
            HighPass(gust, 900f);
            LowPass(gust, 3200f);
            Swell(gust, 1f / 31f, 0.05f, 1f);
            Mix(mix, gust, 0.16f);

            // --- drone: D0 against a copy of itself, detuned so the two beat slowly ---
            var drone = Buffer(length);
            AddSine(drone, 18.36f, 18.36f, 0.55f);   // D0, felt more than heard
            AddSine(drone, 18.52f, 18.52f, 0.55f);   // and again, a fraction sharp
            AddSine(drone, 36.71f, 36.71f, 0.28f);   // D1 to give it a floor
            AddSine(drone, 55.00f, 55.00f, 0.10f);   // A1, the only interval that is kind
            ApplyEnvelope(drone, 6f, 6f);
            Mix(mix, drone, 1f);

            // --- things that happen ------------------------------------------------
            // Bowed harmonics, high and thin, entering on no particular schedule.
            float[] harmonics = { 293.66f, 349.23f, 440.00f, 466.16f, 587.33f };
            float at = 3f + (float)rng.NextDouble() * 4f;
            while (at < length - 9f)
            {
                float frequency = harmonics[rng.Next(harmonics.Length)];
                AddBowedSwell(mix, frequency, at, 4.5f + (float)rng.NextDouble() * 3f, 0.16f, rng);
                at += 7f + (float)rng.NextDouble() * 9f;
            }

            // Something heavy shifting: a trunk, or a branch coming down out of sight.
            at = 6f + (float)rng.NextDouble() * 6f;
            while (at < length - 4f)
            {
                AddWoodCrack(mix, at, rng);
                at += 11f + (float)rng.NextDouble() * 13f;
            }

            // And twice in the loop, the call. Two notes, falling, a long way off.
            AddDistantCall(mix, 14f + (float)rng.NextDouble() * 5f, rng);
            AddDistantCall(mix, 38f + (float)rng.NextDouble() * 6f, rng);

            // --- harmonic movement --------------------------------------------
            AddDroneProgression(mix, 48.99f, new[] { 0, -4, 1, -6 }, length, 0.34f);

            // --- whispers on the wind ------------------------------------------
            // The wood is the one level where the wind can plausibly be hiding a voice, so
            // it is the one that gets them. Far too quiet to make out, on purpose.
            float whisperAt = 8f + (float)rng.NextDouble() * 6f;
            while (whisperAt < length - 6f)
            {
                AddWhisper(mix, whisperAt, 3.2f, rng);
                whisperAt += 14f + (float)rng.NextDouble() * 11f;
            }

            MakeSeamless(mix, 3f);
            Normalize(mix, 0.30f);
            return ToClip("HauntedForest", mix, true);
        }

        /// <summary>
        /// Multiplies a buffer by a slow sine, so a flat noise bed breathes. Rate is in
        /// Hz — use a whole fraction of the loop length and the swell loops with it.
        /// </summary>
        private static void Swell(float[] data, float rateHz, float floorLevel, float peak)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * rateHz * t);
                data[i] *= Mathf.Lerp(floorLevel, peak, lfo * lfo);
            }
        }

        /// <summary>A long bowed note: no attack to speak of, and it leaves before you place it.</summary>
        private static void AddBowedSwell(float[] destination, float frequency, float at,
                                          float duration, float amplitude, System.Random rng)
        {
            var note = Buffer(duration);

            // A touch of drift on the pitch is what separates a bow from an oscillator.
            AddSine(note, frequency, frequency * (0.995f + (float)rng.NextDouble() * 0.004f), amplitude);
            AddSine(note, frequency * 2.01f, frequency * 2f, amplitude * 0.22f);
            AddSine(note, frequency * 3.02f, frequency * 3f, amplitude * 0.08f);

            // Half the note is the attack: it swells in rather than starting.
            ApplyEnvelope(note, duration * 0.5f, duration * 0.45f);
            LowPass(note, 2400f);
            DelayInto(destination, note, at, 1f);
        }

        /// <summary>A trunk giving somewhere out in the dark, with the wood's echo on it.</summary>
        private static void AddWoodCrack(float[] destination, float at, System.Random rng)
        {
            var crack = Buffer(1.6f);

            AddNoise(crack, 1f, rng);
            HighPass(crack, 400f);
            LowPass(crack, 2600f);
            ApplyPercussiveEnvelope(crack, 0.001f, 42f);

            var body = Buffer(1.6f);
            AddSine(body, 128f, 74f, 0.9f);
            ApplyPercussiveEnvelope(body, 0.002f, 26f);
            Mix(crack, body, 0.7f);

            // Far away and between trees: quiet, dull, and it comes back at you.
            LowPass(crack, 1500f);
            AddEcho(crack, 0.21f, 0.4f);
            Normalize(crack, 0.30f);
            DelayInto(destination, crack, at, 1f);
        }

        /// <summary>
        /// Two notes, the second a minor third below the first, so far off and so
        /// low-passed that you cannot tell what made it. That is the entire point.
        /// </summary>
        private static void AddDistantCall(float[] destination, float at, System.Random rng)
        {
            var call = Buffer(2.4f);

            float high = 740f + (float)rng.NextDouble() * 60f;
            var first = Buffer(0.55f);
            AddSine(first, high, high * 0.99f, 0.8f);
            ApplyEnvelope(first, 0.09f, 0.34f);
            DelayInto(call, first, 0f, 1f);

            var second = Buffer(0.7f);
            AddSine(second, high * 0.84f, high * 0.82f, 0.8f);
            ApplyEnvelope(second, 0.11f, 0.45f);
            DelayInto(call, second, 0.62f, 1f);

            LowPass(call, 1100f);
            AddEcho(call, 0.33f, 0.45f);
            Normalize(call, 0.16f);
            DelayInto(destination, call, at, 1f);
        }

        /// <summary>
        /// The tomb: 64 seconds, and the most openly frightening of the three beds.
        ///
        /// The mansion is a music box and the wood is wind; a pyramid is neither, so this
        /// is built out of the three things that make a space feel wrong:
        ///
        /// **A drone that beats.** Two sines a fifth of a hertz apart, so they drift in and
        /// out of phase and the room seems to breathe. Nothing in it changes, and it never
        /// quite settles.
        ///
        /// **A shifted-scale motif.** A few notes on the Phrygian dominant — the flattened
        /// second is what makes it read as *somewhere else* rather than merely as minor —
        /// struck on something metallic and left to ring under a long delay.
        ///
        /// **Breath.** A slow filtered-noise swell every dozen seconds or so, at the very
        /// edge of hearing, which the ear insists on hearing as something alive. That layer
        /// does more work than the other two together, and it is the cheapest.
        /// </summary>
        private static AudioClip HauntedTomb(System.Random rng)
        {
            const float length = 64f;
            var mix = Buffer(length);

            // --- the beating drone -------------------------------------------
            var drone = Buffer(length);
            AddSine(drone, 43.65f, 43.65f, 0.5f);    // F1
            AddSine(drone, 43.85f, 43.85f, 0.5f);    // and a fifth of a hertz above it
            AddSine(drone, 87.31f, 87.31f, 0.2f);
            AddSine(drone, 65.41f, 65.41f, 0.12f);   // C2, an empty fifth over the F
            ApplyEnvelope(drone, 7f, 7f);
            Mix(mix, drone, 1f);

            // --- the motif: F Phrygian dominant, struck on metal ---------------
            // F, Gb, A, Bb, C, Db, Eb — the gap between the flat second and the major
            // third is the interval that does not belong to any music you know.
            float[] scale = { 349.23f, 369.99f, 440.00f, 466.16f, 523.25f, 554.37f, 622.25f };
            int[] phrase = { 0, 1, 0, 4, 3, 1, 0, 2, 1, 0 };

            float at = 4f;
            int step = 0;

            while (at < length - 8f)
            {
                float frequency = scale[phrase[step % phrase.Length]];
                if (rng.NextDouble() < 0.25) frequency *= 0.5f;

                AddTombChime(mix, frequency, at, rng);

                // Long, uneven gaps. A regular pulse becomes a rhythm, and a rhythm is
                // something you can settle into — which is the opposite of the point.
                at += 3.2f + (float)rng.NextDouble() * 4.5f;
                if (rng.NextDouble() < 0.2) at += 3f;
                step++;
            }

            // --- breath ---------------------------------------------------------
            at = 6f + (float)rng.NextDouble() * 5f;
            while (at < length - 6f)
            {
                AddTombBreath(mix, at, rng);
                at += 11f + (float)rng.NextDouble() * 9f;
            }

            // --- harmonic movement --------------------------------------------
            // The beating F stays; the progression moves underneath it, so the two
            // disagree about what key the room is in.
            AddDroneProgression(mix, 43.65f, new[] { 0, -5, 1, -7 }, length, 0.30f);

            // --- bowed metal, and the floor dropping out ------------------------
            float bowAt = 9f + (float)rng.NextDouble() * 8f;
            while (bowAt < length - 8f)
            {
                AddBowedMetal(mix, 174.61f * Mathf.Pow(2f, rng.Next(0, 3) / 12f), bowAt, 5.5f, rng);
                bowAt += 19f + (float)rng.NextDouble() * 13f;
            }

            // A sub that falls below what a laptop speaker can reproduce. On headphones it
            // is the bottom going out of the room; on speakers you get the dread with no
            // explanation at all, which is arguably better.
            AddSubDrop(mix, 55f, 22f, 21f, 6f, 0.45f);
            AddSubDrop(mix, 48f, 19f, 47f, 7f, 0.40f);

            MakeSeamless(mix, 4f);
            Normalize(mix, 0.32f);
            return ToClip("HauntedTomb", mix, true);
        }

        /// <summary>One struck note on something metal and old, with a long tail on it.</summary>
        private static void AddTombChime(float[] destination, float frequency, float at,
                                         System.Random rng)
        {
            var note = Buffer(3.4f);

            AddSine(note, frequency, frequency * 0.998f, 0.5f);
            AddSine(note, frequency * 2.76f, frequency * 2.74f, 0.16f);   // inharmonic
            AddSine(note, frequency * 5.4f, frequency * 5.3f, 0.06f);

            ApplyPercussiveEnvelope(note, 0.004f, 1.5f);
            LowPass(note, 4200f);
            AddEcho(note, 0.42f, 0.42f, 4);
            Normalize(note, 0.4f + (float)rng.NextDouble() * 0.15f);

            DelayInto(destination, note, at, 1f);
        }

        /// <summary>
        /// Something breathing in the dark: a slow swell of band-limited noise. Deliberately
        /// too quiet to identify, because the moment you can identify it, it stops working.
        /// </summary>
        private static void AddTombBreath(float[] destination, float at, System.Random rng)
        {
            var breath = Buffer(4.5f);

            AddNoise(breath, 1f, rng);
            HighPass(breath, 180f);
            LowPass(breath, 900f);

            // Two lobes: in, and then out.
            ApplyEnvelope(breath, 1.6f, 2.4f);
            for (int i = 0; i < breath.Length; i++)
            {
                float t = (float)i / SampleRate;
                breath[i] *= 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 0.9f);
            }

            Normalize(breath, 0.22f);
            DelayInto(destination, breath, at, 1f);
        }

        /// <summary>
        /// The jungle: 72 seconds, and the only bed here that is mostly *not* music.
        ///
        /// The tomb works by being wrong — a beating drone, a scale that belongs nowhere.
        /// A jungle at night is frightening for the opposite reason: it is completely,
        /// densely alive, and none of it is on your side. So this is built out of four
        /// layers that are all things rather than notes:
        ///
        /// **Insects.** A wide band of noise around 4 kHz, amplitude-modulated at about
        /// 14 Hz so it pulses the way a field of cicadas does. It is on the whole time,
        /// and it is the reason you cannot hear anything approaching.
        ///
        /// **The cut-out.** Every twenty seconds or so the insects stop dead for two
        /// beats and then come back. Nothing else happens. That silence is the single most
        /// effective thing in this file, and it costs one multiply — because when the
        /// jungle goes quiet, something moved.
        ///
        /// **A hollow log.** Low struck wood on a slow irregular pulse, far off. It is the
        /// only pitched material, and it is deliberately too sparse to become a rhythm.
        ///
        /// **Calls.** Bird shrieks that stop mid-note, and a monkey whoop somewhere in the
        /// canopy — the same trick as the wood's distant call, but closer and less polite.
        /// </summary>
        private static AudioClip NightJungle(System.Random rng)
        {
            const float length = 72f;
            var mix = Buffer(length);

            // --- the insect bed ------------------------------------------------
            var insects = Buffer(length);
            AddNoise(insects, 1f, rng);
            HighPass(insects, 2600f);
            LowPass(insects, 6200f);

            // Two modulators an octave apart: the fast one is the chirp, the slow one is
            // the swell of a whole field of them breathing together.
            for (int i = 0; i < insects.Length; i++)
            {
                float t = (float)i / SampleRate;
                float chirp = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 14.2f * t);
                float field = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.11f * t + 1.1f);
                insects[i] *= chirp * field;
            }

            // The cut-outs. Everything else keeps going; only the insects stop, which is
            // what makes it read as the wildlife noticing something rather than as a fade.
            float silenceAt = 16f + (float)rng.NextDouble() * 6f;
            while (silenceAt < length - 10f)
            {
                float duration = 1.8f + (float)rng.NextDouble() * 1.4f;
                SilenceRange(insects, silenceAt, duration, 0.35f);
                silenceAt += 19f + (float)rng.NextDouble() * 9f;
            }

            Normalize(insects, 0.2f);
            Mix(mix, insects, 1f);

            // --- the low bed ----------------------------------------------------
            // Wet air and running water, an octave below anything you would call a note.
            var air = Buffer(length);
            AddSine(air, 48.99f, 48.99f, 0.4f);      // G1
            AddSine(air, 73.42f, 73.42f, 0.16f);     // D2, an open fifth over it
            ApplyEnvelope(air, 8f, 8f);
            Mix(mix, air, 0.75f);

            var river = Buffer(length);
            AddNoise(river, 1f, rng);
            LowPass(river, 420f);
            HighPass(river, 90f);
            Normalize(river, 0.1f);
            Mix(mix, river, 1f);

            // --- the log --------------------------------------------------------
            float[] scale = { 98.00f, 110.00f, 130.81f, 146.83f };   // G2 minor pentatonic-ish
            float at = 5f;
            int step = 0;

            while (at < length - 8f)
            {
                AddHollowLog(mix, scale[(step * 3 + 1) % scale.Length], at, rng);
                at += 4.5f + (float)rng.NextDouble() * 5.5f;
                step++;
            }

            // --- calls ------------------------------------------------------------
            at = 9f + (float)rng.NextDouble() * 6f;
            while (at < length - 6f)
            {
                if (rng.NextDouble() < 0.45) AddMonkeyWhoop(mix, at, rng);
                else AddCutBirdCall(mix, at, rng);

                at += 8f + (float)rng.NextDouble() * 10f;
            }

            // --- harmonic movement --------------------------------------------
            AddDroneProgression(mix, 48.99f, new[] { 0, 2, -3, -8 }, length, 0.28f);

            // --- something coming, that never arrives ---------------------------
            // Reverse swells timed to land near the insect cut-outs, so the two coincide
            // now and then without being locked together: the bugs stop, something grows,
            // and then nothing happens. The valley's best trick, twice over.
            float swellAt = 14f + (float)rng.NextDouble() * 8f;
            while (swellAt < length - 8f)
            {
                AddReverseSwell(mix, 73.42f, swellAt, 4.2f, rng);
                swellAt += 21f + (float)rng.NextDouble() * 11f;
            }

            MakeSeamless(mix, 4f);
            Normalize(mix, 0.34f);
            return ToClip("NightJungle", mix, true);
        }

        /// <summary>
        /// Ducks a stretch of a buffer to near silence with short ramps either side, so
        /// the hole has edges rather than clicks.
        /// </summary>
        private static void SilenceRange(float[] buffer, float at, float duration, float floor)
        {
            int start = Mathf.Clamp((int)(at * SampleRate), 0, buffer.Length - 1);
            int end = Mathf.Clamp(start + (int)(duration * SampleRate), 0, buffer.Length);
            int ramp = Mathf.Max(1, (int)(0.12f * SampleRate));

            for (int i = start; i < end; i++)
            {
                float into = Mathf.Clamp01((float)(i - start) / ramp);
                float outOf = Mathf.Clamp01((float)(end - i) / ramp);
                buffer[i] *= Mathf.Lerp(1f, floor, Mathf.Min(into, outOf));
            }
        }

        /// <summary>A struck hollow log: a low woody thud with a short pitched ring.</summary>
        private static void AddHollowLog(float[] destination, float frequency, float at,
                                         System.Random rng)
        {
            var note = Buffer(1.6f);

            AddSine(note, frequency, frequency * 0.985f, 0.6f);
            AddSine(note, frequency * 2.1f, frequency * 2.05f, 0.14f);

            var knock = Buffer(1.6f);
            AddNoise(knock, 1f, rng);
            HighPass(knock, frequency * 1.8f);
            LowPass(knock, frequency * 5f);
            ApplyPercussiveEnvelope(knock, 0.0006f, 40f);

            Mix(note, knock, 0.5f);
            ApplyPercussiveEnvelope(note, 0.003f, 6f);
            LowPass(note, 2200f);
            AddEcho(note, 0.28f, 0.24f, 2);
            Normalize(note, 0.2f + (float)rng.NextDouble() * 0.08f);

            DelayInto(destination, note, at, 1f);
        }

        /// <summary>
        /// A bird call that stops mid-note. The stop is the whole idea: a call that ends
        /// properly is scenery, and a call that is cut off is an event.
        /// </summary>
        private static void AddCutBirdCall(float[] destination, float at, System.Random rng)
        {
            var call = Buffer(1.4f);

            float high = 1250f + (float)rng.NextDouble() * 700f;
            AddSine(call, high, high * 1.35f, 0.7f);          // a rising shriek
            AddSine(call, high * 2.01f, high * 2.6f, 0.2f);

            ApplyEnvelope(call, 0.05f, 0.05f);

            // Cut it dead somewhere in the middle, with a two-millisecond ramp so it is a
            // stop rather than a click.
            int cut = (int)((0.3f + (float)rng.NextDouble() * 0.35f) * SampleRate);
            int ramp = (int)(0.002f * SampleRate);
            for (int i = cut; i < call.Length; i++)
                call[i] *= Mathf.Clamp01(1f - (float)(i - cut) / ramp);

            HighPass(call, 700f);
            AddEcho(call, 0.24f, 0.3f, 3);
            Normalize(call, 0.16f);

            DelayInto(destination, call, at, 1f);
        }

        /// <summary>
        /// A whoop up in the canopy: a swooping tone with a rough edge on it, answered
        /// once from further off. Two of them makes it a troop rather than an animal.
        /// </summary>
        private static void AddMonkeyWhoop(float[] destination, float at, System.Random rng)
        {
            var whoop = Buffer(2.6f);

            float low = 320f + (float)rng.NextDouble() * 90f;

            var first = Buffer(0.75f);
            AddSine(first, low, low * 2.2f, 0.8f);           // up
            AddSine(first, low * 1.5f, low * 3.1f, 0.25f);
            ApplyEnvelope(first, 0.06f, 0.28f);
            Saturate(first, 1.6f);                            // the rasp
            DelayInto(whoop, first, 0f, 1f);

            var answer = Buffer(0.8f);
            AddSine(answer, low * 0.88f, low * 1.9f, 0.7f);
            ApplyEnvelope(answer, 0.08f, 0.34f);
            LowPass(answer, 1400f);                           // further away
            DelayInto(whoop, answer, 1.1f, 0.55f);

            AddEcho(whoop, 0.31f, 0.34f, 3);
            Normalize(whoop, 0.19f);

            DelayInto(destination, whoop, at, 1f);
        }

        /// <summary>
        /// A drone that *moves*.
        ///
        /// Every bed in this game used to be one held chord for its whole length, and that
        /// is the single biggest reason they stopped being frightening after two minutes:
        /// a sound that never changes stops being information and becomes furniture. The
        /// ear tunes out anything perfectly steady — it is built to.
        ///
        /// So the root walks. Each step crossfades over several seconds into the next, and
        /// because the steps are long and unevenly spaced you rarely catch the moment it
        /// happens; you just notice the room is in a different key than it was. That is a
        /// much more useful kind of unease than a change you can hear arriving.
        ///
        /// The intervals are chosen to avoid resolving. A progression that returns home
        /// gives relief, and relief is the one thing a horror bed must never provide.
        /// </summary>
        private static void AddDroneProgression(float[] mix, float rootHz, int[] semitones,
                                                float length, float amplitude = 0.45f)
        {
            float step = length / semitones.Length;

            for (int i = 0; i < semitones.Length; i++)
            {
                float hz = rootHz * Mathf.Pow(2f, semitones[i] / 12f);

                // Each voice is as long as two steps and starts half a step early, so
                // consecutive roots overlap and the change is a bleed rather than a cut.
                var voice = Buffer(step * 2f);

                AddSine(voice, hz, hz, 0.5f);
                AddSine(voice, hz * 1.5f, hz * 1.5f, 0.16f);      // an open fifth over it
                AddSine(voice, hz * 2f, hz * 2.003f, 0.10f);      // and a beating octave

                ApplyEnvelope(voice, step * 0.8f, step * 0.8f);

                DelayInto(mix, voice, i * step - step * 0.5f, amplitude);
            }
        }

        /// <summary>
        /// Something that might be a voice, at the level where you cannot be sure.
        ///
        /// Band-limited noise with formant peaks, kept far too quiet to make out. The
        /// listener does the work — and whatever they decide they heard is worse than
        /// anything that could have been written.
        /// </summary>
        private static void AddWhisper(float[] destination, float at, float seconds,
                                       System.Random rng, bool childlike = false)
        {
            var whisper = Buffer(seconds);

            AddNoise(whisper, 1f, rng);

            // A child's voice sits higher and thinner: first formant down, second up.
            if (childlike) AddFormants(whisper, 480f, 2100f, 3100f);
            else AddFormants(whisper, 680f, 1180f, 2500f);

            // Syllables. Without this it is a hiss; with it, it has a rhythm and the ear
            // insists on hearing speech in it.
            for (int i = 0; i < whisper.Length; i++)
            {
                float t = (float)i / SampleRate;
                float syllable = 0.35f + 0.65f * Mathf.Pow(
                    Mathf.Abs(Mathf.Sin(Mathf.PI * (2.7f + (float)rng.NextDouble() * 0.02f) * t)), 2.2f);
                whisper[i] *= syllable;
            }

            ApplyEnvelope(whisper, seconds * 0.35f, seconds * 0.45f);
            Normalize(whisper, 0.085f);

            DelayInto(destination, whisper, at, 1f);
        }

        /// <summary>
        /// A swell that grows and then simply stops. See ApplyReverseEnvelope — the missing
        /// decay is the whole effect, because nothing in nature ends that way.
        /// </summary>
        private static void AddReverseSwell(float[] destination, float rootHz, float at,
                                            float seconds, System.Random rng)
        {
            var swell = Buffer(seconds);

            AddSine(swell, rootHz, rootHz * 1.06f, 0.5f);
            AddSine(swell, rootHz * 2.02f, rootHz * 2.16f, 0.22f);
            AddNoise(swell, 0.18f, rng);

            // The filter opens as it grows, so it gets brighter as well as louder — the
            // sensation of something approaching rather than merely turning up.
            SweepLowPass(swell, 300f, 4200f);
            ApplyReverseEnvelope(swell, seconds * 0.92f);
            Normalize(swell, 0.26f);

            DelayInto(destination, swell, at, 1f);
        }

        /// <summary>Bowed metal: a long inharmonic swell, the sound of a saw or a glass rim.</summary>
        private static void AddBowedMetal(float[] destination, float frequency, float at,
                                          float seconds, System.Random rng)
        {
            var bowed = Buffer(seconds);

            AddSine(bowed, frequency, frequency * 1.004f, 0.5f);
            AddSine(bowed, frequency * 2.76f, frequency * 2.78f, 0.13f);
            AddSine(bowed, frequency * 4.1f, frequency * 4.06f, 0.06f);

            ApplyEnvelope(bowed, seconds * 0.4f, seconds * 0.45f);

            // The warble is what says *bowed* rather than *synthesised*: a hand on a bow
            // cannot hold a pitch to the cent, and the ear knows it.
            ApplyWarble(bowed, 22f, 0.7f);
            Normalize(bowed, 0.15f + (float)rng.NextDouble() * 0.05f);

            DelayInto(destination, bowed, at, 1f);
        }

        /// <summary>
        /// The tension layer: what fades in over a bed when something is hunting you.
        ///
        /// One parameterised generator rather than six bespoke ones, and that is a
        /// deliberate call. A level's *identity* is carried by its bed — the tomb's beating
        /// drone, the valley's cicadas. What tension sounds like is close to universal, and
        /// six hand-written variants would drift apart in quality and give the game six
        /// different opinions about what danger feels like. What changes per level is the
        /// root note, so the layer sits in the same key as the bed underneath it, and how
        /// hard it pushes.
        ///
        /// Three things, none of them a tune:
        ///
        /// **A pulse.** The root, tremolo'd at around three a second. A steady pulse is the
        /// oldest trick there is for raising a heart rate, and it works because it is close
        /// enough to a fast heartbeat to be mistaken for one.
        ///
        /// **A minor second.** A tone one semitone above the root, held. Two notes that
        /// close together beat against each other and the ear reads the result as *wrong*
        /// rather than as music — it is the same interval doing the same job as the tomb
        /// bed's fifth-of-a-hertz detune, only much less subtle.
        ///
        /// **Irregular low hits.** Struck, spaced unevenly so they never become a rhythm.
        /// A rhythm is something you can settle into.
        ///
        /// It is deliberately *not* a melody. This layer comes and goes with the threat
        /// meter, which means it can arrive halfway through a phrase and leave halfway
        /// through another; anything with a tune would sound broken doing that.
        /// </summary>
        private static AudioClip Tension(float length, float rootHz, float push,
                                         System.Random rng)
        {
            var mix = Buffer(length);

            // --- the pulse -------------------------------------------------
            var pulse = Buffer(length);
            AddSine(pulse, rootHz, rootHz, 0.6f);
            AddSine(pulse, rootHz * 2f, rootHz * 2f, 0.22f);

            for (int i = 0; i < pulse.Length; i++)
            {
                float t = (float)i / SampleRate;

                // The pulse quickens across the loop and resets, so even held at full
                // threat it never settles into something you can ignore.
                float rate = 2.6f + 1.1f * (t / length);
                float gate = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * rate * t)), 1.6f);
                pulse[i] *= gate;
            }

            Mix(mix, pulse, 0.9f * push);

            // --- the minor second ------------------------------------------
            var grind = Buffer(length);
            float second = rootHz * 1.05946f;          // one semitone up
            AddSine(grind, rootHz * 4f, rootHz * 4f, 0.16f);
            AddSine(grind, second * 4f, second * 4f, 0.16f);
            ApplyEnvelope(grind, 3f, 3f);
            LowPass(grind, 1800f);
            Mix(mix, grind, 0.55f * push);

            // --- the hits ----------------------------------------------------
            float at = 2f + (float)rng.NextDouble() * 3f;
            while (at < length - 2f)
            {
                AddTensionHit(mix, rootHz * 0.5f, at, push, rng);
                at += 2.4f + (float)rng.NextDouble() * 4.2f;
            }

            MakeSeamless(mix, 2.5f);
            Normalize(mix, 0.30f);
            return ToClip("Tension" + (int)rootHz, mix, true);
        }

        /// <summary>One struck low note with a fast attack and a short tail.</summary>
        private static void AddTensionHit(float[] destination, float frequency, float at,
                                          float push, System.Random rng)
        {
            var hit = Buffer(1.8f);

            AddSine(hit, frequency, frequency * 0.94f, 0.8f);
            AddSine(hit, frequency * 1.5f, frequency * 1.44f, 0.2f);

            var edge = Buffer(1.8f);
            AddNoise(edge, 1f, rng);
            LowPass(edge, 900f);
            ApplyPercussiveEnvelope(edge, 0.001f, 26f);
            Mix(hit, edge, 0.35f);

            ApplyPercussiveEnvelope(hit, 0.004f, 3.2f);
            Normalize(hit, (0.24f + (float)rng.NextDouble() * 0.10f) * push);

            DelayInto(destination, hit, at, 1f);
        }

        /// <summary>
        /// The town: 56 seconds of dry, wide-open nothing.
        ///
        /// It had been playing the *forest's* bed — wind and a cold drone, in an Old West
        /// street at low sun. This is the opposite of the wood in every way that matters:
        /// warm rather than cold, sparse rather than dense, and built around silence with
        /// things in it rather than a continuous texture. A street is the one place in this
        /// game with somewhere to look, and the score should leave room for that.
        ///
        /// A single plucked string, an open fifth that hangs, and the creak of something
        /// wooden moving in the heat.
        /// </summary>
        /// <summary>
        /// WALTZ FOR NOBODY — the park's calliope, still playing to an empty midway.
        ///
        /// A waltz because a waltz is what these organs played and because three-four is the
        /// time signature of something turning: it is carousel music, and the carousel is
        /// forty metres away with nothing on it.
        ///
        /// The melody is deliberately simple and deliberately in a major key. Bending it
        /// minor would be the obvious move and it would be the wrong one — a sad tune in an
        /// abandoned park is just sad, whereas a cheerful one is *indifferent*, and
        /// indifference is the thing that makes a place feel like it does not need you.
        ///
        /// Sixty seconds, which pairs with TensionPark. They start on one dspTime tick and
        /// loop together forever; a mismatch drifts them apart inside a minute.
        /// </summary>
        private static AudioClip WaltzForNobody(System.Random rng)
        {
            const float length = 60f;
            var mix = Buffer(length);

            // The tune. G major, and it goes nowhere in particular -- a fairground organ
            // plays a phrase and then plays it again, which is how you know it is a machine.
            float[] melody = { 392.00f, 493.88f, 587.33f, 493.88f, 392.00f, 329.63f,
                               440.00f, 523.25f, 659.25f, 523.25f, 440.00f, 392.00f };

            const float beat = 0.42f;          // brisk three-four
            int step = 0;
            float t = 0f;

            while (t < length - 2f)
            {
                // Every eighth bar the mechanism sticks and the melody stops dead. The
                // silence is the loudest thing in the piece.
                bool stuck = (step / 3) % 8 == 7;

                if (!stuck)
                {
                    float hz = melody[step % melody.Length];
                    bool downbeat = step % 3 == 0;

                    // Two ranks, a few cents apart. This is the entire character of the
                    // sound: one rank is a tone, two ranks slightly out is an ORGAN.
                    var pipe = Buffer(beat * 1.6f);
                    AddSaw(pipe, hz, hz, downbeat ? 0.34f : 0.24f);
                    AddSaw(pipe, hz * 1.006f, hz * 1.006f, downbeat ? 0.30f : 0.20f);
                    AddSine(pipe, hz * 2f, hz * 2f, 0.10f);

                    // A pipe speaks with a chiff and then holds; it does not fade in.
                    ApplyEnvelope(pipe, 0.012f, beat * 1.1f);
                    LowPass(pipe, 2600f);

                    DelayInto(mix, pipe, t, 1f);
                }

                // The oom-pah underneath, which keeps going even through the stuck bars —
                // the bellows do not care that the melody has stopped.
                if (step % 3 == 0)
                {
                    var bass = Buffer(beat * 1.2f);
                    AddSine(bass, 98.00f, 97.4f, 0.30f);
                    AddSaw(bass, 98.00f, 97.4f, 0.10f);
                    ApplyEnvelope(bass, 0.010f, beat * 0.9f);
                    LowPass(bass, 700f);
                    DelayInto(mix, bass, t, 1f);
                }

                t += beat;
                step++;
            }

            // Wow and flutter over the whole thing. A wandering read head is the difference
            // between "a synthesiser played a waltz" and "a machine is playing a waltz".
            ApplyWarble(mix, 22f, 0.34f);

            // The room: an empty park at night is mostly air, so the organ arrives with a
            // long dull tail and no early reflections worth speaking of.
            AddEcho(mix, 0.31f, 0.26f, 4);
            LowPass(mix, 3200f);

            // And underneath it all, the sound of the place itself: wind through a chain
            // link fence and something metal a long way off.
            var wind = Buffer(length);
            AddNoise(wind, 0.30f, rng);
            SweepLowPass(wind, 420f, 180f);
            ApplyWarble(wind, 60f, 0.06f);
            Mix(mix, wind, 0.5f);

            // A root that walks and never comes home. G - E - C - A: it keeps sounding
            // like it is about to resolve to G and never does, which is the same trick every
            // other bed in this game uses and the reason none of them wear out.
            AddDroneProgression(mix, 49.00f, new[] { 0, -3, -7, -11 }, length, 0.30f);

            MakeSeamless(mix, 2.2f);
            Normalize(mix, 0.52f);
            return ToClip("WaltzForNobody", mix, true);
        }

        private static AudioClip DustAndBone(System.Random rng)
        {
            const float length = 56f;
            var mix = Buffer(length);

            // A low open fifth, barely there. Two notes, no third — so it is neither major
            // nor minor, which is what makes it read as *empty* rather than as sad.
            var drone = Buffer(length);
            AddSine(drone, 58.27f, 58.27f, 0.42f);     // Bb1
            AddSine(drone, 87.31f, 87.31f, 0.18f);     // F2
            ApplyEnvelope(drone, 8f, 8f);
            Mix(mix, drone, 0.8f);

            // Dust in the air: a very quiet high band that swells and falls.
            var dust = Buffer(length);
            AddNoise(dust, 1f, rng);
            HighPass(dust, 2200f);
            for (int i = 0; i < dust.Length; i++)
            {
                float t = (float)i / SampleRate;
                dust[i] *= 0.4f + 0.6f * Mathf.Sin(2f * Mathf.PI * 0.07f * t);
            }
            Normalize(dust, 0.06f);
            Mix(mix, dust, 1f);

            // The plucks. Bb minor pentatonic, spaced wide, and often left alone for six or
            // seven seconds — the gaps are the instrument.
            float[] scale = { 116.54f, 138.59f, 155.56f, 174.61f, 207.65f };
            int[] phrase = { 0, 2, 1, 0, 3, 2, 4, 2, 1, 0 };

            float at = 3f;
            int step = 0;

            while (at < length - 6f)
            {
                AddPluck(mix, scale[phrase[step % phrase.Length]], at, rng);

                at += 3.4f + (float)rng.NextDouble() * 4.4f;
                if (rng.NextDouble() < 0.25) at += 3.2f;
                step++;
            }

            // Something wooden, moving. A shutter, a sign, a floorboard on a porch.
            at = 7f + (float)rng.NextDouble() * 6f;
            while (at < length - 5f)
            {
                AddCreak(mix, at, rng);
                at += 13f + (float)rng.NextDouble() * 11f;
            }

            // --- harmonic movement --------------------------------------------
            AddDroneProgression(mix, 58.27f, new[] { 0, 3, -1, -5 }, length, 0.30f);

            // --- a piano nobody has tuned in twenty years -----------------------
            // Warble is doing all the work here. The same note played straight is a piano;
            // played through a wandering pitch it is a piano in a building that is falling
            // down, and the town is entirely about things that have been left.
            float pianoAt = 12f + (float)rng.NextDouble() * 9f;
            while (pianoAt < length - 8f)
            {
                var note = Buffer(4.5f);
                float hz = 116.54f * Mathf.Pow(2f, (rng.Next(0, 4) * 2) / 12f);

                AddSine(note, hz, hz * 0.999f, 0.5f);
                AddSine(note, hz * 2f, hz * 2.01f, 0.2f);
                AddSine(note, hz * 3.01f, hz * 3.03f, 0.08f);
                ApplyPercussiveEnvelope(note, 0.004f, 0.9f);
                ApplyWarble(note, 45f, 0.55f);
                LowPass(note, 2600f);
                AddEcho(note, 0.26f, 0.3f, 3);
                Normalize(note, 0.13f);

                DelayInto(mix, note, pianoAt, 1f);
                pianoAt += 15f + (float)rng.NextDouble() * 13f;
            }

            MakeSeamless(mix, 4f);
            Normalize(mix, 0.32f);
            return ToClip("DustAndBone", mix, true);
        }

        /// <summary>A plucked string: a hard attack, a long ring, and a little body noise.</summary>
        private static void AddPluck(float[] destination, float frequency, float at,
                                     System.Random rng)
        {
            var note = Buffer(3.2f);

            AddSine(note, frequency, frequency * 0.999f, 0.55f);
            AddSine(note, frequency * 2f, frequency * 1.995f, 0.22f);
            AddSine(note, frequency * 3f, frequency * 2.99f, 0.09f);

            var attack = Buffer(3.2f);
            AddNoise(attack, 1f, rng);
            HighPass(attack, frequency * 2f);
            ApplyPercussiveEnvelope(attack, 0.0004f, 60f);
            Mix(note, attack, 0.28f);

            ApplyPercussiveEnvelope(note, 0.002f, 1.4f);
            LowPass(note, 5200f);

            // A short, dry reflection. A street has no reverb worth speaking of, and piling
            // one on would put the whole level indoors.
            AddEcho(note, 0.11f, 0.16f, 2);
            Normalize(note, 0.20f + (float)rng.NextDouble() * 0.08f);

            DelayInto(destination, note, at, 1f);
        }

        /// <summary>Timber under heat: a slow rising groan that stops rather than ends.</summary>
        private static void AddCreak(float[] destination, float at, System.Random rng)
        {
            var creak = Buffer(2.2f);

            float low = 180f + (float)rng.NextDouble() * 120f;
            AddSine(creak, low, low * 1.6f, 0.5f);
            AddNoise(creak, 0.25f, rng);

            BandLimit(creak, low * 0.7f, low * 4f);
            ApplyEnvelope(creak, 0.6f, 0.5f);
            Saturate(creak, 1.8f);
            Normalize(creak, 0.11f);

            DelayInto(destination, creak, at, 1f);
        }

        /// <summary>
        /// The school: 48 seconds, and the most unpleasant of the six.
        ///
        /// It had been playing the mansion's music box, which is a reasonable instrument for
        /// a haunted house and completely wrong for a primary school — a music box is
        /// *antique*, and nothing here is old. What a school has instead is the sound of
        /// children, and the horror is that there are none.
        ///
        /// So: a nursery-rhyme shape on a toy glockenspiel, played too slowly and a
        /// semitone flat in places, over the mains hum of a building whose lights are still
        /// on. The tune is deliberately almost-recognisable and never quite resolves.
        /// </summary>
        private static AudioClip EmptyClassrooms(System.Random rng)
        {
            const float length = 48f;
            var mix = Buffer(length);

            // Mains hum. 50 Hz and its third harmonic, the sound of a building nobody
            // turned off, sitting under everything.
            var hum = Buffer(length);
            AddSine(hum, 50f, 50f, 0.35f);
            AddSine(hum, 150f, 150f, 0.12f);
            AddSine(hum, 100f, 100.2f, 0.08f);
            ApplyEnvelope(hum, 5f, 5f);
            Mix(mix, hum, 0.7f);

            // C major on a toy instrument, which is the joke: the key is bright and nothing
            // else about it is. Two of the notes are bent flat, so the tune keeps almost
            // arriving somewhere and does not.
            float[] scale = { 523.25f, 587.33f, 659.25f, 698.46f, 783.99f };
            int[] phrase = { 0, 1, 2, 0, 2, 1, 0, 3, 2, 1, 4, 2 };

            float at = 2.5f;
            int step = 0;

            while (at < length - 5f)
            {
                float frequency = scale[phrase[step % phrase.Length]];

                // Every fourth note or so sags. A whole tune out of tune is a broken
                // instrument; one note out of tune is something wrong with the room.
                if (step % 4 == 3) frequency *= 0.972f;

                AddToyChime(mix, frequency, at, rng);

                // Slower than a child would play it, and unevenly.
                at += 1.5f + (float)rng.NextDouble() * 1.1f;
                if (rng.NextDouble() < 0.18) at += 2.4f;
                step++;
            }

            // --- harmonic movement --------------------------------------------
            // Under a nominally major tune, so the bass keeps contradicting the melody.
            AddDroneProgression(mix, 65.41f, new[] { 0, -1, -5, 2 }, length, 0.26f);

            // --- children, which is the worst thing this level can offer --------
            // Formants tuned thin and high. A school is defined by the sound of children;
            // an empty one that still has that sound in it is doing something no amount of
            // dissonance could.
            float voiceAt = 9f + (float)rng.NextDouble() * 6f;
            while (voiceAt < length - 6f)
            {
                AddWhisper(mix, voiceAt, 2.8f, rng, childlike: true);
                voiceAt += 13f + (float)rng.NextDouble() * 10f;
            }

            MakeSeamless(mix, 3f);
            Normalize(mix, 0.30f);
            return ToClip("EmptyClassrooms", mix, true);
        }

        /// <summary>A struck metal bar: bright, inharmonic, and gone quickly.</summary>
        private static void AddToyChime(float[] destination, float frequency, float at,
                                        System.Random rng)
        {
            var note = Buffer(2.4f);

            AddSine(note, frequency, frequency * 0.998f, 0.5f);
            AddSine(note, frequency * 2.76f, frequency * 2.75f, 0.18f);   // inharmonic
            AddSine(note, frequency * 5.4f, frequency * 5.38f, 0.07f);

            ApplyPercussiveEnvelope(note, 0.002f, 2.6f);
            LowPass(note, 7000f);

            // A corridor's worth of reflection, which is what says "institution".
            AddEcho(note, 0.19f, 0.34f, 4);
            Normalize(note, 0.17f + (float)rng.NextDouble() * 0.07f);

            DelayInto(destination, note, at, 1f);
        }

        private static AudioClip HauntedMansion(System.Random rng)
        {
            const float length = 48f;
            var mix = Buffer(length);

            // --- drone: D1 and A1, plus an unresolved tritone that fades in and out ---
            var drone = Buffer(length);
            AddSine(drone, 36.71f, 36.71f, 0.5f);    // D1
            AddSine(drone, 55.00f, 55.00f, 0.3f);    // A1
            AddSine(drone, 73.42f, 73.42f, 0.16f);   // D2
            ApplyEnvelope(drone, 4f, 4f);

            var tritone = Buffer(length);
            AddSine(tritone, 51.91f, 51.91f, 0.22f); // G#1 against the D — never resolves
            for (int i = 0; i < tritone.Length; i++)
            {
                float t = (float)i / SampleRate;
                // Very slow swell, so the dissonance creeps in rather than arriving.
                tritone[i] *= 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (1f / 24f) * t - 1.5f);
            }

            Mix(mix, drone, 1f);
            Mix(mix, tritone, 1f);

            // --- music box melody, D natural minor -------------------------------
            float[] scale = { 587.33f, 659.25f, 698.46f, 783.99f, 880.00f, 932.33f, 1046.50f };
            //                D5       E5       F5       G5       A5       Bb5      C6

            // A wandering phrase that keeps returning to the tonic without settling.
            int[] phrase = { 0, 4, 2, 5, 4, 0, 3, 2, 6, 4, 5, 2, 0, 1, 2, 4 };

            float time = 1.5f;
            int step = 0;

            while (time < length - 3f)
            {
                int note = phrase[step % phrase.Length];
                float frequency = scale[note];

                // Occasionally drop an octave for a heavier chime.
                if (rng.NextDouble() < 0.18) frequency *= 0.5f;

                AddMusicBoxNote(mix, frequency, time, 0.34f, rng);

                // A stuck mechanism sometimes strikes the same tooth twice.
                if (rng.NextDouble() < 0.14)
                    AddMusicBoxNote(mix, frequency, time + 0.16f, 0.16f, rng);

                // Uneven spacing: mostly slow, with occasional long silences.
                float gap = 1.1f + (float)rng.NextDouble() * 1.4f;
                if (rng.NextDouble() < 0.2) gap += 1.8f;

                time += gap;
                step++;
            }

            // --- harmonic movement --------------------------------------------
            // D, down to B, out to Ab, up to C. The Ab is a tritone from the D and the C
            // never resolves back, so the house keeps arriving somewhere slightly wrong.
            AddDroneProgression(mix, 36.71f, new[] { 0, -3, 6, -2 }, length, 0.40f);

            // --- the box, played backwards -------------------------------------
            // A music box you can hear winding *up* into silence. The mansion's signature
            // instrument turned inside out, which is a nastier idea than adding a new one.
            float reverseAt = 11f + (float)rng.NextDouble() * 7f;
            while (reverseAt < length - 7f)
            {
                AddReverseSwell(mix, 293.66f, reverseAt, 3.4f, rng);
                reverseAt += 17f + (float)rng.NextDouble() * 12f;
            }

            MakeSeamless(mix, 2.5f);
            Normalize(mix, 0.34f);
            return ToClip("HauntedMansion", mix, true);
        }

        /// <summary>One music box chime: two slightly detuned partials with a long decay.</summary>
        private static void AddMusicBoxNote(float[] target, float frequency, float atSeconds,
                                            float amplitude, System.Random rng)
        {
            int start = Samples(atSeconds);
            if (start >= target.Length) return;

            float noteLength = Mathf.Min(3.2f, (float)(target.Length - start) / SampleRate);
            var note = Buffer(noteLength);

            // Detuning the second partial gives the beating warble of an old mechanism.
            float detune = 1f + (float)(rng.NextDouble() * 0.006 - 0.003);

            AddSine(note, frequency, frequency * 0.999f, amplitude);
            AddSine(note, frequency * 2f * detune, frequency * 2f * detune, amplitude * 0.34f);
            AddSine(note, frequency * 3f, frequency * 3f, amplitude * 0.12f);

            // The hammer striking the tooth.
            var strike = Buffer(noteLength);
            AddNoise(strike, 1f, rng);
            HighPass(strike, 3500f);
            ApplyPercussiveEnvelope(strike, 0.0004f, 90f);
            Mix(note, strike, 0.05f);

            ApplyPercussiveEnvelope(note, 0.004f, 2.6f);
            AddEcho(note, 0.28f, 0.3f, 2);

            for (int i = 0; i < note.Length && start + i < target.Length; i++)
                target[start + i] += note[i];
        }

        private static AudioClip Ambience(System.Random rng)
        {
            // 8 s bed. The drone frequencies are exact multiples of 1/8 Hz so they loop
            // perfectly; the noise layer is crossfaded to hide its seam.
            float length = 8f;

            var drone = Buffer(length);
            AddSine(drone, 40.5f, 40.5f, 0.5f);   // 324 cycles in 8 s
            AddSine(drone, 61f, 61f, 0.3f);       // 488 cycles
            AddSine(drone, 81f, 81f, 0.15f);      // 648 cycles

            var wind = Buffer(length);
            AddNoise(wind, 1f, rng);
            LowPass(wind, 320f);
            LowPass(wind, 320f);                  // second pass = steeper slope

            // Slow swell so the bed breathes instead of sitting flat.
            for (int i = 0; i < wind.Length; i++)
            {
                float t = (float)i / SampleRate;
                wind[i] *= 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.125f * t);
            }

            var mix = Buffer(length);
            Mix(mix, drone, 1f);
            Mix(mix, wind, 0.5f);

            MakeSeamless(mix, 0.75f);
            Normalize(mix, 0.32f);
            return ToClip("Ambience", mix, true);
        }
    }
}
