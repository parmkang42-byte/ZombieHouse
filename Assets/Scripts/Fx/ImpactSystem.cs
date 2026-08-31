using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Combat;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// Turns a bullet impact into everything the player perceives: particles, a decal
    /// and a sound. It listens to <see cref="Weapon.Impacted"/>, so adding another weapon
    /// later needs no changes here — only that the new weapon raises the same event.
    /// </summary>
    public class ImpactSystem : MonoBehaviour
    {
        [Header("Counts")]
        [SerializeField] private int wallDustCount = 10;
        [SerializeField] private int bloodMistCount = 30;
        [SerializeField] private int bloodMistCritical = 60;

        [Header("Decals")]
        [SerializeField] private float bulletHoleSize = 0.11f;
        [SerializeField] private float bloodPoolSize = 0.75f;
        [Tooltip("Extra spatter thrown around the main pool, so blood reads as splashed not stamped.")]
        [SerializeField] private int spatterPerHit = 3;
        [SerializeField] private float spatterSpread = 1.1f;
        [SerializeField] private Color bulletHoleColour = new Color(0.05f, 0.04f, 0.03f, 0.85f);
        [SerializeField] private Color bloodColour = new Color(0.32f, 0.02f, 0.02f, 0.8f);
        [SerializeField] private float bloodDropDistance = 3f;
        [SerializeField] private LayerMask decalSurfaces = 1;

        [Header("Audio")]
        [SerializeField] private float impactVolume = 0.7f;

        private Weapon[] _weapons;
        private DecalPool _decals;
        private ParticleSystem _wallDust;
        private ParticleSystem _bloodMist;

        private void Awake()
        {
            _decals = GetComponentInChildren<DecalPool>();
            if (_decals == null)
            {
                var go = new GameObject("Decals");
                go.transform.SetParent(transform, false);
                _decals = go.AddComponent<DecalPool>();
            }

            _wallDust = ParticleFactory.Create("WallDust", transform, ParticleFactory.WallDust);
            _bloodMist = ParticleFactory.Create("BloodMist", transform, ParticleFactory.BloodMist);
        }

        private void Start()
        {
            // Every gun, holstered ones included — a weapon that is swapped in later must
            // still produce impacts, and its GameObject is inactive right now.
            _weapons = FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Weapon weapon in _weapons)
                if (weapon != null) weapon.Impacted += OnImpact;
        }

        private void OnDestroy()
        {
            if (_weapons == null) return;

            foreach (Weapon weapon in _weapons)
                if (weapon != null) weapon.Impacted -= OnImpact;
        }

        private void OnImpact(ImpactEvent impact)
        {
            if (impact.HitCharacter) OnFleshImpact(impact);
            else OnWorldImpact(impact);
        }

        private void OnWorldImpact(ImpactEvent impact)
        {
            SpawnWorldFx(impact.Point, impact.Normal);
            GameAudio.PlayAt(Sfx.ImpactWall, impact.Point, impactVolume, 0.12f);
        }

        private void OnFleshImpact(ImpactEvent impact)
        {
            SpawnFleshFx(impact.Point, impact.Normal, impact.Critical);
            GameAudio.PlayAt(impact.Critical ? Sfx.ImpactCritical : Sfx.ImpactFlesh,
                             impact.Point, impactVolume * (impact.Critical ? 1.15f : 1f), 0.1f);
        }

        /// <summary>
        /// Visual half of a world hit, without the sound. The machete uses this so it can
        /// play its own steel-on-stone ring instead of a bullet impact.
        /// </summary>
        public void SpawnWorldFx(Vector3 point, Vector3 normal)
        {
            ParticleFactory.Burst(_wallDust, point, normal, wallDustCount);

            if (_decals != null)
                _decals.Place(point, normal, bulletHoleColour, bulletHoleSize * Random.Range(0.8f, 1.25f));
        }

        /// <summary>A raw blood burst with an explicit particle count, for severed stumps.</summary>
        public void BurstBlood(Vector3 point, Vector3 direction, int count)
        {
            ParticleFactory.Burst(_bloodMist, point, direction, count);
        }

        /// <summary>Visual half of a flesh hit. Pass a bigger scale for heavier wounds.</summary>
        public void SpawnFleshFx(Vector3 point, Vector3 normal, bool critical, float scale = 1f)
        {
            int count = Mathf.RoundToInt((critical ? bloodMistCritical : bloodMistCount) * scale);
            ParticleFactory.Burst(_bloodMist, point, normal, count);

            // The body is moving, so the mark goes on whatever is underneath instead.
            if (_decals == null) return;

            RaycastHit floor;
            if (Physics.Raycast(point, Vector3.down, out floor, bloodDropDistance,
                                decalSurfaces, QueryTriggerInteraction.Ignore))
            {
                float size = bloodPoolSize * (critical ? 1.5f : 1f) * scale * Random.Range(0.75f, 1.3f);
                _decals.Place(floor.point, floor.normal, bloodColour, size);
            }

            SpatterAround(point, critical, scale);
        }

        /// <summary>
        /// Throws a few smaller marks around the hit. One clean pool under every body
        /// looks stamped on; scattered spatter of varying size reads as a mess.
        /// </summary>
        private void SpatterAround(Vector3 point, bool critical, float scale)
        {
            int count = Mathf.RoundToInt(spatterPerHit * scale * (critical ? 1.8f : 1f));

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spatterSpread * scale;
                Vector3 from = point + new Vector3(offset.x, 0.1f, offset.y);

                RaycastHit surface;
                if (!Physics.Raycast(from, Vector3.down, out surface, bloodDropDistance + 0.5f,
                                     decalSurfaces, QueryTriggerInteraction.Ignore))
                    continue;

                float size = bloodPoolSize * scale * Random.Range(0.18f, 0.5f);
                Color colour = bloodColour;
                colour.a *= Random.Range(0.6f, 1f);

                _decals.Place(surface.point, surface.normal, colour, size);
            }
        }
    }
}
