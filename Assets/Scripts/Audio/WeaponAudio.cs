using UnityEngine;
using ZombieHouse.Combat;

namespace ZombieHouse.Audio
{
    /// <summary>
    /// The gun's sounds. These play 2D: it is the player's own weapon, so it should sit
    /// front and centre rather than being attenuated by distance to itself.
    /// Impact sounds are positional and handled by the ImpactSystem instead.
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public class WeaponAudio : MonoBehaviour
    {
        [Tooltip("Which report this weapon makes. The Desert Eagle uses GunshotHeavy.")]
        [SerializeField] private Sfx fireSound = Sfx.Gunshot;
        [SerializeField] private float gunshotVolume = 0.55f;
        [SerializeField] private float dryFireVolume = 0.5f;
        [SerializeField] private float reloadVolume = 0.6f;

        private Weapon _weapon;

        private void Awake()
        {
            _weapon = GetComponent<Weapon>();
        }

        private void OnEnable()
        {
            if (_weapon == null) return;
            _weapon.Fired += OnFired;
            _weapon.DryFired += OnDryFired;
            _weapon.ReloadStarted += OnReloadStarted;
            _weapon.ReloadFinished += OnReloadFinished;
        }

        private void OnDisable()
        {
            if (_weapon == null) return;
            _weapon.Fired -= OnFired;
            _weapon.DryFired -= OnDryFired;
            _weapon.ReloadStarted -= OnReloadStarted;
            _weapon.ReloadFinished -= OnReloadFinished;
        }

        /// <summary>Sets this weapon's report. Called by the scene setup.</summary>
        public void SetFireSound(Sfx sound, float volume = -1f)
        {
            fireSound = sound;
            if (volume > 0f) gunshotVolume = volume;
        }

        private void OnFired()
        {
            GameAudio.Play2D(fireSound, gunshotVolume, 0.05f);
        }

        private void OnDryFired()
        {
            GameAudio.Play2D(Sfx.DryFire, dryFireVolume, 0.08f);
        }

        private void OnReloadStarted()
        {
            GameAudio.Play2D(Sfx.ReloadOut, reloadVolume, 0.06f);
        }

        private void OnReloadFinished()
        {
            GameAudio.Play2D(Sfx.ReloadIn, reloadVolume, 0.06f);
        }
    }
}
