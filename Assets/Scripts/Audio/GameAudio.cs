using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Audio
{
    /// <summary>
    /// Owns the synthesised clip library and a pool of AudioSources for one-shots.
    /// Sounds that belong to a moving object (a zombie's voice) get their own source on
    /// that object instead, so they track it — this pool is for fire-and-forget sounds.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        [Header("Mix")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.32f;

        [Tooltip("Which bed this level runs. The house gets the music box; the forest "
                 + "gets wind and a drone.")]
        [SerializeField] private Sfx musicTrack = Sfx.Music;

        [Tooltip("The layer mixed over the bed when something is hunting you. Must be the "
                 + "same length as the bed or the two drift apart within a minute.")]
        [SerializeField] private Sfx tensionTrack = Sfx.TensionHouse;

        [Tooltip("How loud the tension layer gets at full threat, relative to the bed.")]
        [Range(0f, 2f)] [SerializeField] private float tensionVolume = 1.15f;

        [Tooltip("Seconds for tension to arrive. Short: something is behind you now.")]
        [SerializeField] private float tensionAttack = 1.4f;

        [Tooltip("Seconds for it to leave. Deliberately much longer than the attack — "
                 + "danger should arrive suddenly and recede reluctantly, and that "
                 + "asymmetry is most of what makes a score feel like it is watching.")]
        [SerializeField] private float tensionRelease = 5f;

        [Header("Spatialisation")]
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 40f;

        [Header("Pool")]
        [SerializeField] private int poolSize = 24;

        private Dictionary<Sfx, AudioClip[]> _clips;
        private AudioSource[] _pool;
        private float[] _startedAt;
        private int _next;
        private AudioSource _uiSource;
        private AudioSource _ambienceSource;
        private AudioSource _musicSource;
        private AudioSource _tensionSource;
        private float _tension;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _clips = SoundBank.Build();
            BuildPool();
            StartAmbience();
            StartMusic();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildPool()
        {
            _pool = new AudioSource[Mathf.Max(4, poolSize)];
            _startedAt = new float[_pool.Length];

            for (int i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("SfxSource_" + i);
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                ConfigureSpatial(source);
                _pool[i] = source;
                _startedAt[i] = float.NegativeInfinity;
            }

            var uiObject = new GameObject("UiSource");
            uiObject.transform.SetParent(transform, false);
            _uiSource = uiObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;   // always 2D, never attenuated
        }

        private void ConfigureSpatial(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
        }

        private void StartAmbience()
        {
            AudioClip clip = Clip(Sfx.Ambience);
            if (clip == null) return;

            var go = new GameObject("Ambience");
            go.transform.SetParent(transform, false);

            _ambienceSource = go.AddComponent<AudioSource>();
            _ambienceSource.clip = clip;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.volume = ambienceVolume * masterVolume;
            _ambienceSource.Play();
        }

        /// <summary>
        /// The music bed runs on its own source so it can be balanced against the
        /// ambience — and muted entirely — without touching anything else.
        /// </summary>
        private void StartMusic()
        {
            AudioClip clip = Clip(musicTrack);
            if (clip == null) return;

            var go = new GameObject("Music");
            go.transform.SetParent(transform, false);

            _musicSource = go.AddComponent<AudioSource>();
            _musicSource.clip = clip;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
            _musicSource.volume = musicVolume * masterVolume;

            AudioClip layer = Clip(tensionTrack);
            if (layer == null)
            {
                _musicSource.Play();
                return;
            }

            var tensionObject = new GameObject("MusicTension");
            tensionObject.transform.SetParent(transform, false);

            _tensionSource = tensionObject.AddComponent<AudioSource>();
            _tensionSource.clip = layer;
            _tensionSource.loop = true;
            _tensionSource.spatialBlend = 0f;
            _tensionSource.volume = 0f;

            // Started on the same dsp tick rather than with two Play() calls a frame apart.
            // Two loops of the same length drift by whatever gap you leave between them, and
            // a bed and its tension layer sliding out of phase over ten minutes is the kind
            // of fault nobody diagnoses — it just gradually sounds worse.
            double startAt = AudioSettings.dspTime + 0.05;
            _musicSource.PlayScheduled(startAt);
            _tensionSource.PlayScheduled(startAt);

            if (!Mathf.Approximately(layer.length, clip.length))
            {
                Debug.LogWarning($"[GameAudio] {tensionTrack} is {layer.length:0.0}s against " +
                                 $"{musicTrack}'s {clip.length:0.0}s — they will drift apart.");
            }
        }

        /// <summary>
        /// Rides the tension layer on the threat meter. Fast in, slow out.
        /// </summary>
        private void Update()
        {
            TickSwitch(Time.unscaledDeltaTime);

            if (_tensionSource == null) return;

            float target = ThreatMeter.Level;
            float rate = target > _tension ? tensionAttack : tensionRelease;

            _tension = Mathf.MoveTowards(_tension, target,
                                         Time.unscaledDeltaTime / Mathf.Max(0.05f, rate));

            _tensionSource.volume = _tension * tensionVolume * musicVolume * masterVolume;
        }

        /// <summary>Where the tension layer currently sits, 0 to 1. For tests and the HUD.</summary>
        public static float Tension => Instance != null ? Instance._tension : 0f;

        /// <summary>Which bed is playing. Read by the test, and by anything wanting to restore it.</summary>
        public static Sfx CurrentTrack => Instance != null ? Instance.musicTrack : Sfx.Music;

        /// <summary>
        /// Swaps the bed for a different one, over a crossfade.
        ///
        /// Used by <see cref="ZombieHouse.Enemies.LevelBoss"/> when the boss wakes, and again
        /// when it dies. A hard cut is the obvious implementation and it is wrong: it reads
        /// as a bug or a scene change, whereas a second of one bed dying under another reads
        /// as something arriving.
        ///
        /// Safe to call with the track already playing — it returns immediately rather than
        /// restarting, which matters because Engage() can be reached from more than one path.
        /// </summary>
        public static void SwitchMusic(Sfx bed, Sfx tension, float fadeSeconds = 1.4f)
        {
            if (Instance == null) return;
            Instance.BeginSwitch(bed, tension, fadeSeconds);
        }

        private Sfx _pendingBed;
        private Sfx _pendingTension;
        private float _switchFade;
        private float _switchProgress = -1f;
        private AudioSource _outgoing;

        private void BeginSwitch(Sfx bed, Sfx tension, float fadeSeconds)
        {
            if (musicTrack == bed && _switchProgress < 0f) return;

            AudioClip clip = Clip(bed);
            if (clip == null) return;

            // The bed that is leaving keeps playing on its own source while it fades, so the
            // two genuinely overlap. Reusing one source and swapping its clip would be a cut
            // with extra steps.
            _outgoing = _musicSource;

            var go = new GameObject("Music");
            go.transform.SetParent(transform, false);

            _musicSource = go.AddComponent<AudioSource>();
            _musicSource.clip = clip;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
            _musicSource.volume = 0f;
            _musicSource.Play();

            AudioClip layer = Clip(tension);
            if (layer != null && _tensionSource != null)
            {
                _tensionSource.Stop();
                _tensionSource.clip = layer;
                _tensionSource.volume = 0f;
                _tensionSource.Play();
            }

            musicTrack = bed;
            tensionTrack = tension;

            _pendingBed = bed;
            _pendingTension = tension;
            _switchFade = Mathf.Max(0.05f, fadeSeconds);
            _switchProgress = 0f;
        }

        /// <summary>Drives a crossfade in progress. Called from Update.</summary>
        private void TickSwitch(float deltaTime)
        {
            if (_switchProgress < 0f) return;

            _switchProgress = Mathf.Min(1f, _switchProgress + deltaTime / _switchFade);

            float full = musicVolume * masterVolume;
            if (_musicSource != null) _musicSource.volume = _switchProgress * full;
            if (_outgoing != null) _outgoing.volume = (1f - _switchProgress) * full;

            if (_switchProgress < 1f) return;

            if (_outgoing != null) Destroy(_outgoing.gameObject);
            _outgoing = null;
            _switchProgress = -1f;
        }

        public static void SetMusicVolume(float volume)
        {
            if (Instance == null || Instance._musicSource == null) return;

            Instance.musicVolume = Mathf.Clamp01(volume);
            Instance._musicSource.volume = Instance.musicVolume * Instance.masterVolume;

            if (Instance._tensionSource != null)
            {
                Instance._tensionSource.volume =
                    Instance._tension * Instance.tensionVolume * Instance.musicVolume * Instance.masterVolume;
            }
        }

        // ---- lookup ---------------------------------------------------------

        public AudioClip Clip(Sfx sfx)
        {
            AudioClip[] variants;
            if (_clips == null || !_clips.TryGetValue(sfx, out variants) || variants.Length == 0)
                return null;

            return variants.Length == 1 ? variants[0] : variants[Random.Range(0, variants.Length)];
        }

        // ---- playback -------------------------------------------------------

        /// <summary>Positional one-shot. Pitch jitter keeps repeats from sounding identical.</summary>
        public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (Instance == null) return;
            Instance.PlayAtInternal(sfx, position, volume, pitchJitter);
        }

        /// <summary>Non-positional one-shot for player and UI sounds.</summary>
        public static void Play2D(Sfx sfx, float volume = 1f, float pitchJitter = 0f)
        {
            if (Instance == null) return;

            AudioClip clip = Instance.Clip(sfx);
            if (clip == null) return;

            Instance._uiSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            Instance._uiSource.PlayOneShot(clip, volume * Instance.masterVolume);
        }

        private void PlayAtInternal(Sfx sfx, Vector3 position, float volume, float pitchJitter)
        {
            AudioClip clip = Clip(sfx);
            if (clip == null) return;

            AudioSource source = TakeSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume * masterVolume;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        /// <summary>First free source; if they are all busy, steal the one playing longest.</summary>
        private AudioSource TakeSource()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                int index = (_next + i) % _pool.Length;
                if (_pool[index].isPlaying) continue;

                _next = (index + 1) % _pool.Length;
                _startedAt[index] = Time.unscaledTime;
                return _pool[index];
            }

            int oldest = 0;
            for (int i = 1; i < _pool.Length; i++)
                if (_startedAt[i] < _startedAt[oldest]) oldest = i;

            _startedAt[oldest] = Time.unscaledTime;
            return _pool[oldest];
        }

        /// <summary>
        /// Configures an AudioSource that lives on a moving object so it matches the
        /// pool's spatial settings. Callers keep and drive the source themselves.
        /// </summary>
        /// <summary>
        /// The ear. Cached, because finding it is a scene search and the footstep budget
        /// asks for it dozens of times a second; re-resolved if it goes away, which it
        /// does when the player dies and the rig is torn down.
        /// </summary>
        public static AudioListener Listener
        {
            get
            {
                if (_listener == null) _listener = Object.FindAnyObjectByType<AudioListener>();
                return _listener;
            }
        }

        private static AudioListener _listener;

        public static AudioSource AttachSource(GameObject owner, float volume = 1f)
        {
            var source = owner.AddComponent<AudioSource>();

            if (Instance != null)
            {
                Instance.ConfigureSpatial(source);
                source.volume = volume * Instance.masterVolume;
            }
            else
            {
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.volume = volume;
            }

            return source;
        }

        public static AudioClip Get(Sfx sfx)
        {
            return Instance == null ? null : Instance.Clip(sfx);
        }
    }
}
