using UnityEngine;

namespace ZombieHouse.Audio
{
    /// <summary>
    /// A tiny synthesiser. Every sound in the game is built from these primitives at
    /// startup, so the project ships with no .wav files. The recipes live in
    /// <see cref="SoundBank"/>; this file is only the DSP.
    ///
    /// Everything works on a float[] of mono samples in -1..1 at <see cref="SampleRate"/>.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        public static int Samples(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
        }

        public static float[] Buffer(float seconds)
        {
            return new float[Samples(seconds)];
        }

        // ---- generators -----------------------------------------------------

        /// <summary>White noise added into the buffer.</summary>
        public static void AddNoise(float[] data, float amplitude, System.Random rng)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] += (float)(rng.NextDouble() * 2.0 - 1.0) * amplitude;
        }

        /// <summary>
        /// Sine with a linear frequency sweep. Vibrato is a slow frequency wobble;
        /// pass 0 for a steady tone.
        /// </summary>
        public static void AddSine(float[] data, float startHz, float endHz, float amplitude,
                                   float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            double phase = 0.0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / data.Length;
                float frequency = Mathf.Lerp(startHz, endHz, t);

                if (vibratoHz > 0f)
                    frequency *= 1f + Mathf.Sin(2f * Mathf.PI * vibratoHz * i / SampleRate) * vibratoDepth;

                phase += 2.0 * Mathf.PI * frequency / SampleRate;
                data[i] += Mathf.Sin((float)phase) * amplitude;
            }
        }

        /// <summary>
        /// Band-limited-ish sawtooth: the harmonic stack that gives voices and growls
        /// their rasp. Harmonics above Nyquist are skipped so it does not alias.
        /// </summary>
        public static void AddSaw(float[] data, float startHz, float endHz, float amplitude,
                                  int harmonics = 8, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            for (int h = 1; h <= harmonics; h++)
            {
                float harmonicAmplitude = amplitude / h;
                if (startHz * h > SampleRate * 0.45f && endHz * h > SampleRate * 0.45f) break;
                AddSine(data, startHz * h, endHz * h, harmonicAmplitude, vibratoHz, vibratoDepth);
            }
        }

        // ---- shaping --------------------------------------------------------

        /// <summary>Percussive shape: near-instant attack, exponential decay.</summary>
        public static void ApplyPercussiveEnvelope(float[] data, float attackSeconds, float decayRate)
        {
            int attack = Mathf.Max(1, Samples(attackSeconds));

            for (int i = 0; i < data.Length; i++)
            {
                float envelope = i < attack
                    ? (float)i / attack
                    : Mathf.Exp(-decayRate * (i - attack) / SampleRate);

                data[i] *= envelope;
            }
        }

        /// <summary>Sustained shape with explicit attack, hold and release fractions.</summary>
        public static void ApplyEnvelope(float[] data, float attackSeconds, float releaseSeconds)
        {
            int attack = Mathf.Max(1, Samples(attackSeconds));
            int release = Mathf.Max(1, Samples(releaseSeconds));
            int releaseStart = Mathf.Max(attack, data.Length - release);

            for (int i = 0; i < data.Length; i++)
            {
                float envelope = 1f;
                if (i < attack) envelope = (float)i / attack;
                else if (i >= releaseStart) envelope = 1f - (float)(i - releaseStart) / (data.Length - releaseStart);
                data[i] *= Mathf.Clamp01(envelope);
            }
        }

        /// <summary>
        /// Gates a buffer down to sparse irregular bursts — the sound of something
        /// frying. Used under the zombie voice to give the grumble a wet crackle
        /// instead of a smooth hum.
        /// </summary>
        public static void ApplyCrackle(float[] data, System.Random rng,
                                        float burstsPerSecond = 26f,
                                        float burstSeconds = 0.02f,
                                        float floorLevel = 0.06f)
        {
            var envelope = new float[data.Length];
            for (int i = 0; i < envelope.Length; i++) envelope[i] = floorLevel;

            float seconds = (float)data.Length / SampleRate;
            int bursts = Mathf.Max(1, Mathf.RoundToInt(burstsPerSecond * seconds));

            for (int b = 0; b < bursts; b++)
            {
                int start = rng.Next(0, data.Length);
                int length = Mathf.Max(4, Samples(burstSeconds * (0.35f + (float)rng.NextDouble())));
                float peak = 0.45f + (float)rng.NextDouble() * 0.55f;

                for (int i = 0; i < length && start + i < data.Length; i++)
                {
                    // Each pop is a sharp attack with an immediate decay.
                    float t = (float)i / length;
                    float value = peak * Mathf.Exp(-6f * t);
                    if (value > envelope[start + i]) envelope[start + i] = value;
                }
            }

            for (int i = 0; i < data.Length; i++) data[i] *= envelope[i];
        }

        /// <summary>One-pole low pass. Cheap, and exactly the "muffled" character we want.</summary>
        public static void LowPass(float[] data, float cutoffHz)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * Mathf.Max(1f, cutoffHz));
            float alpha = dt / (rc + dt);

            float previous = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                previous += alpha * (data[i] - previous);
                data[i] = previous;
            }
        }

        /// <summary>One-pole high pass — removes the mud so clicks stay crisp.</summary>
        public static void HighPass(float[] data, float cutoffHz)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * Mathf.Max(1f, cutoffHz));
            float alpha = rc / (rc + dt);

            float previousInput = data[0];
            float previousOutput = data[0];

            for (int i = 1; i < data.Length; i++)
            {
                float input = data[i];
                previousOutput = alpha * (previousOutput + input - previousInput);
                data[i] = previousOutput;
                previousInput = input;
            }
        }

        /// <summary>Feedback delay. A couple of taps is enough to suggest a room.</summary>
        public static void AddEcho(float[] data, float delaySeconds, float feedback, int taps = 3)
        {
            int delay = Samples(delaySeconds);
            if (delay <= 0 || delay >= data.Length) return;

            for (int tap = 1; tap <= taps; tap++)
            {
                int offset = delay * tap;
                if (offset >= data.Length) break;

                float gain = Mathf.Pow(feedback, tap);
                for (int i = offset; i < data.Length; i++)
                    data[i] += data[i - offset] * gain;
            }
        }

        /// <summary>
        /// Mixes one layer into another. Layers are built in separate buffers so each can
        /// get its own envelope and filtering before they are combined.
        /// </summary>
        public static void Mix(float[] destination, float[] source, float gain = 1f)
        {
            int length = Mathf.Min(destination.Length, source.Length);
            for (int i = 0; i < length; i++)
                destination[i] += source[i] * gain;
        }

        /// <summary>Scales the buffer so its loudest sample sits at <paramref name="peak"/>.</summary>
        public static void Normalize(float[] data, float peak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));

            if (max < 1e-6f) return;

            float scale = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
        }

        /// <summary>Soft clip — keeps loud transients from sounding like digital crunch.</summary>
        public static void Saturate(float[] data, float drive = 1.5f)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)System.Math.Tanh(data[i] * drive);
        }

        /// <summary>
        /// Crossfades the tail into the head so the clip can loop without a click.
        /// Used for the ambience bed, whose noise layer will not loop on its own.
        /// </summary>
        public static void MakeSeamless(float[] data, float fadeSeconds)
        {
            int fade = Mathf.Min(Samples(fadeSeconds), data.Length / 2);
            if (fade <= 1) return;

            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                int tail = data.Length - fade + i;
                data[i] = Mathf.Lerp(data[tail], data[i], t);
            }

            // The tail has now been folded in; taper it so the join is exact.
            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                int tail = data.Length - fade + i;
                data[tail] *= 1f - t;
            }
        }

        /// <summary>
        /// Swells into a hard stop instead of decaying away — an envelope run backwards.
        ///
        /// This is the most reliably unpleasant thing in the whole toolkit and it costs one
        /// multiply per sample. Every natural sound decays: something is struck, and the
        /// energy leaks away. A sound that grows steadily louder and then simply *ceases*
        /// has no physical cause, so the ear cannot place it and keeps trying. Horror
        /// soundtracks have leaned on it for fifty years and it has not worn out.
        ///
        /// The stop is the point, so there is deliberately no release: cutting a swell dead
        /// at its peak is what makes it land.
        /// </summary>
        public static void ApplyReverseEnvelope(float[] data, float swellSeconds,
                                                float curve = 2.4f)
        {
            int swell = Mathf.Clamp((int)(swellSeconds * SampleRate), 1, data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                if (i >= swell)
                {
                    data[i] = 0f;
                    continue;
                }

                data[i] *= Mathf.Pow((float)i / swell, curve);
            }
        }

        /// <summary>
        /// Slow pitch instability, the way worn tape or a dying music box wanders.
        ///
        /// Resamples the buffer through a read head that speeds up and slows down. A tone
        /// held at a perfectly constant pitch reads as *electronic*; one that drifts by a
        /// few cents reads as a mechanism, and a mechanism that is running badly is far more
        /// frightening than a synthesiser, because something has to be turning it.
        /// </summary>
        public static void ApplyWarble(float[] data, float depthCents, float rateHz)
        {
            var source = (float[])data.Clone();

            // Cents to a ratio: a hundred cents is a semitone, and a semitone is 2^(1/12).
            float depth = Mathf.Pow(2f, depthCents / 1200f) - 1f;

            double read = 0.0;

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;

                // Two rates, so the wander itself is not a rhythm.
                float wobble = Mathf.Sin(2f * Mathf.PI * rateHz * t)
                             + 0.4f * Mathf.Sin(2f * Mathf.PI * rateHz * 2.7f * t + 1.1f);

                read += 1.0 + depth * wobble;

                if (read >= source.Length - 1)
                {
                    data[i] = 0f;
                    continue;
                }

                int index = (int)read;
                float frac = (float)(read - index);
                data[i] = Mathf.Lerp(source[index], source[index + 1], frac);
            }
        }

        /// <summary>
        /// Three resonant peaks, which is roughly what turns noise into a voice.
        ///
        /// Vowels are formants — fixed resonances of the throat and mouth — so band-limited
        /// noise with peaks in the right places reads as *someone* rather than as hiss, even
        /// at a level where no word can be made out. That ambiguity is the useful part: the
        /// listener supplies the voice, and whatever they supply is worse than anything that
        /// could be recorded.
        ///
        /// Roughly an "ah" by default; drop f1 and raise f2 towards an "ee" for something
        /// thinner and more childlike.
        /// </summary>
        public static void AddFormants(float[] data, float f1 = 700f, float f2 = 1220f,
                                       float f3 = 2600f)
        {
            var dry = (float[])data.Clone();
            var voiced = new float[data.Length];

            float[] centres = { f1, f2, f3 };
            float[] gains = { 1f, 0.62f, 0.34f };

            for (int band = 0; band < centres.Length; band++)
            {
                var pass = (float[])dry.Clone();

                // A one-pole pair either side of the centre. Not a sharp resonator, but at
                // the level these sit in a mix a steep filter would be wasted.
                HighPass(pass, centres[band] * 0.80f);
                LowPass(pass, centres[band] * 1.25f);

                for (int i = 0; i < voiced.Length; i++) voiced[i] += pass[i] * gains[band];
            }

            for (int i = 0; i < data.Length; i++) data[i] = voiced[i];
        }

        /// <summary>
        /// A tone that falls out of hearing: felt rather than heard by the end.
        ///
        /// Descending sub-bass is the "something enormous is happening" cue, and it works
        /// below the frequency at which a listener can name what they are hearing — which
        /// on small speakers means they get the dread with none of the explanation.
        /// </summary>
        public static void AddSubDrop(float[] data, float startHz, float endHz,
                                      float atSeconds, float seconds, float amplitude = 0.6f)
        {
            int start = Mathf.Clamp((int)(atSeconds * SampleRate), 0, data.Length - 1);
            int length = Mathf.Min((int)(seconds * SampleRate), data.Length - start);

            double phase = 0.0;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;

                // Exponential rather than linear: pitch is logarithmic, so a linear sweep
                // sounds like it slows down as it falls.
                float hz = startHz * Mathf.Pow(endHz / startHz, t);

                phase += 2.0 * Mathf.PI * hz / SampleRate;

                // Fade out at the very end so it stops without a click.
                float fade = t < 0.85f ? 1f : (1f - t) / 0.15f;
                data[start + i] += Mathf.Sin((float)phase) * amplitude * fade;
            }
        }

        /// <summary>
        /// A low-pass whose cutoff travels across the buffer — a room slowly closing, or
        /// slowly opening. Static filtering is a colour; a moving one is an event.
        /// </summary>
        public static void SweepLowPass(float[] data, float startHz, float endHz)
        {
            float previous = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / data.Length;
                float cutoff = startHz * Mathf.Pow(endHz / startHz, t);

                float dt = 1f / SampleRate;
                float rc = 1f / (2f * Mathf.PI * Mathf.Max(20f, cutoff));
                float alpha = dt / (rc + dt);

                previous += alpha * (data[i] - previous);
                data[i] = previous;
            }
        }

        public static AudioClip ToClip(string name, float[] data, bool loop = false)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            if (loop) clip.hideFlags = HideFlags.None;
            return clip;
        }
    }
}
