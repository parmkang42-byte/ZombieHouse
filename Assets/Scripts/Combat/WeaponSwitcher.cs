using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Holds the player's guns and swaps between them on middle mouse.
    ///
    /// Only the drawn weapon's GameObject is active, which means an inactive gun's Update
    /// never runs — no need for any "am I equipped" checks scattered through Weapon,
    /// WeaponFx or the view model. They simply stop existing while holstered.
    /// </summary>
    public class WeaponSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject[] weapons = new GameObject[0];
        [SerializeField] private int startingIndex;
        [SerializeField] private float switchCooldown = 0.35f;

        [Tooltip("Weapon that its own fire key also draws. -1 for none.")]
        [SerializeField] private int quickDrawIndex = -1;

        [Tooltip("The scavenged weapon, built with the rig but not carried until you find "
                 + "one. It joins the slots when picked up and leaves them when it runs dry.")]
        [SerializeField] private GameObject powerUpWeapon;

        public Weapon Current { get; private set; }

        /// <summary>How many weapons are on the wheel right now.</summary>
        public int SlotCount => weapons == null ? 0 : weapons.Length;
        public int Index { get; private set; }

        /// <summary>Raised on every swap, so the HUD can rebind to the new gun.</summary>
        public event Action<Weapon> Switched;

        private float _nextSwitchTime;

        /// <summary>True while the scavenged weapon is in your hands or on your back.</summary>
        public bool HasPowerUp => powerUpWeapon != null && System.Array.IndexOf(weapons, powerUpWeapon) >= 0;

        /// <summary>Tells the switcher which weapon is the scavenged one. It starts stowed.</summary>
        public void ConfigurePowerUp(GameObject weapon)
        {
            powerUpWeapon = weapon;
            if (weapon != null) weapon.SetActive(false);

            ListenForEmpty();
        }

        /// <summary>
        /// Subscribes to the scavenged weapon running dry, so it can be taken away.
        ///
        /// Called from both ConfigurePowerUp and Start, and guarded against subscribing
        /// twice, because neither one alone is enough: an event handler is not something
        /// a scene can serialise, so Start has to do it in a built level — and Start does
        /// not run in edit mode, so a test would otherwise be checking a switcher that
        /// silently never listens.
        /// </summary>
        private void ListenForEmpty()
        {
            if (_listeningForEmpty || powerUpWeapon == null) return;

            var weapon = powerUpWeapon.GetComponent<Weapon>();
            if (weapon == null) return;

            weapon.Depleted += DiscardPowerUp;
            _listeningForEmpty = true;
        }

        private bool _listeningForEmpty;

        public void Configure(GameObject[] available, int startAt = 0, int quickDraw = -1)
        {
            weapons = available;
            startingIndex = Mathf.Clamp(startAt, 0, Mathf.Max(0, available.Length - 1));
            quickDrawIndex = quickDraw;
        }

        private void Start()
        {
            Equip(startingIndex);

            // The scavenged weapon takes itself out of your hands when it runs dry.
            ListenForEmpty();
        }

        private void OnDestroy()
        {
            if (!_listeningForEmpty || powerUpWeapon == null) return;

            var weapon = powerUpWeapon.GetComponent<Weapon>();
            if (weapon != null) weapon.Depleted -= DiscardPowerUp;
            _listeningForEmpty = false;
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            if (weapons == null || weapons.Length == 0) return;

            // Number keys pick a weapon outright. Middle mouse is convenient but sits on a
            // button that mouse vendor software often reassigns, so it must not be the
            // only way to change weapon.
            if (InputReader.SelectSlotOnePressed) { Equip(0); return; }
            if (InputReader.SelectSlotTwoPressed && weapons.Length > 1) { Equip(1); return; }
            if (InputReader.SelectSlotThreePressed && weapons.Length > 2) { Equip(2); return; }

            // Pressing the rifle's own key while it is holstered draws it; the shot then
            // comes from the weapon itself on the same or the next press.
            if (quickDrawIndex >= 0 && quickDrawIndex < weapons.Length
                && InputReader.RifleFirePressed && Index != quickDrawIndex)
            {
                Equip(quickDrawIndex);
                return;
            }

            if (weapons.Length < 2) return;
            if (!InputReader.SwitchWeaponPressed || Time.time < _nextSwitchTime) return;

            _nextSwitchTime = Time.time + switchCooldown;

            Equip(NextWheelSlot());
        }

        /// <summary>
        /// Picks up the scavenged weapon: fills it, adds a slot for it and draws it.
        /// Finding a second one while you still have the first simply reloads it to full,
        /// which is the least surprising thing that can happen.
        /// </summary>
        public bool GrantPowerUp(int totalRounds)
        {
            if (powerUpWeapon == null) return false;

            var weapon = powerUpWeapon.GetComponent<Weapon>();
            if (weapon != null) weapon.ConfigureAsPowerUp(totalRounds, true);

            if (!HasPowerUp)
            {
                var grown = new GameObject[weapons.Length + 1];
                weapons.CopyTo(grown, 0);
                grown[weapons.Length] = powerUpWeapon;
                weapons = grown;
            }

            Equip(System.Array.IndexOf(weapons, powerUpWeapon));
            return true;
        }

        [Tooltip("The most rounds the scavenged weapon can hold at once.")]
        [SerializeField] private int powerUpAmmoCeiling = 600;

        [Tooltip("The most rounds a belt-fed weapon can hold. Belt crates stop there, and "
             + "it matches the weapon's own reserve ceiling on purpose: two different "
             + "numbers would mean a crate could fill it past what a box may top up to.")]
        [SerializeField] private int beltFedAmmoCeiling = 800;

        /// <summary>
        /// Feeds a belt crate to whichever carried weapon has barrels to feed.
        ///
        /// Keyed on the weapon being rotary rather than on which slot it is in: the
        /// gatling gun started life as the scavenged weapon and is now part of the
        /// standing loadout, and a belt crate should not have cared either way.
        /// </summary>
        public int FeedBeltFed(int rounds)
        {
            if (weapons == null) return 0;

            foreach (GameObject slot in weapons)
            {
                if (slot == null) continue;

                var weapon = slot.GetComponent<Weapon>();
                if (weapon == null || !weapon.IsRotary) continue;

                return weapon.FeedBelt(rounds, beltFedAmmoCeiling);
            }

            return 0;
        }

        /// <summary>Feeds the scavenged weapon specifically. Zero if it is not carried.</summary>
        public int FeedPowerUp(int rounds)
        {
            if (!HasPowerUp || powerUpWeapon == null) return 0;

            var weapon = powerUpWeapon.GetComponent<Weapon>();
            return weapon == null ? 0 : weapon.FeedBelt(rounds, powerUpAmmoCeiling);
        }

        /// <summary>
        /// Takes the scavenged weapon away again — it is empty and there is no more
        /// ammunition for it anywhere. Drops you back to your own sidearm.
        /// </summary>
        public void DiscardPowerUp()
        {
            if (!HasPowerUp) return;

            var kept = new List<GameObject>(weapons);
            kept.Remove(powerUpWeapon);
            weapons = kept.ToArray();

            powerUpWeapon.SetActive(false);
            Equip(0);
        }

        /// <summary>
        /// The slot the wheel would select right now: simply the next one along.
        ///
        /// Normally that is a three-way cycle — pistol, rifle, gatling gun and round
        /// again — and four-way while you are carrying an Uzi you found. It used to jump
        /// straight to the scavenged weapon instead, which was quicker to reach but meant
        /// the wheel did two different things depending on what you were holding; one
        /// button with one behaviour is worth more than the shortcut was.
        ///
        /// Split out from Update so a test can ask what the wheel would do without being
        /// able to press it — a headless test has no mouse.
        /// </summary>
        public int NextWheelSlot()
        {
            if (weapons == null || weapons.Length == 0) return 0;
            return (Index + 1) % weapons.Length;
        }

        public void Equip(int index)
        {
            if (weapons == null || weapons.Length == 0) return;

            Index = Mathf.Clamp(index, 0, weapons.Length - 1);

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                weapons[i].SetActive(i == Index);
            }

            Current = weapons[Index] != null ? weapons[Index].GetComponent<Weapon>() : null;

            // A dry mechanical clack is enough of a cue; the guns sound different anyway.
            GameAudio.Play2D(Sfx.ReloadOut, 0.4f, 0.1f);
            Switched?.Invoke(Current);
        }
    }
}
