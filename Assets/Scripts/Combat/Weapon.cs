using System;
using UnityEngine;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Combat
{
    /// <summary>Where a bullet landed and what it hit — consumed by audio and decals.</summary>
    public struct ImpactEvent
    {
        public Vector3 Point;
        public Vector3 Normal;
        public bool HitCharacter;
        public bool Critical;
    }

    /// <summary>
    /// Hitscan firearm. Raycasts from the camera, applies damage through IDamageable
    /// (so Hitbox children can multiply it), kicks the view, and makes noise the AI hears.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string weaponName = "Pistol";
        [SerializeField] private bool automatic = false;
        [Tooltip("Also fires on the dedicated rifle key (Space), not just the trigger.")]
        [SerializeField] private bool alsoFiresOnRifleKey;
        [Tooltip("Aiming looks through a telescopic sight, so the HUD shows a scope view.")]
        [SerializeField] private bool scoped;

        [Tooltip("A found weapon rather than one of yours: when the last round is gone "
                 + "the gun goes with it. The gatling gun is the only thing that sets this.")]
        [SerializeField] private bool discardWhenEmpty;

        [Header("Rotary barrels")]
        [Tooltip("Seconds the barrels must spin before a round comes out. Zero for every "
                 + "weapon that is not a gatling gun.")]
        [SerializeField] private float spinUpSeconds;

        [Tooltip("Seconds the barrels take to wind down once the trigger is released.")]
        [SerializeField] private float spinDownSeconds = 1.1f;

        /// <summary>True while actually looking down a telescopic sight.</summary>
        public bool IsScoped => scoped && IsAiming && !IsReloading;
        public bool HasScope => scoped;

        [Header("Ballistics")]
        [SerializeField] private float damage = 34f;
        [Tooltip("What this weapon's rounds count as. The rifle fires RifleRound, which "
                 + "some targets — the zombie bear — are far more vulnerable to.")]
        [SerializeField] private DamageKind damageKind = DamageKind.Bullet;
        [SerializeField] private float range = 80f;
        [SerializeField] private float fireInterval = 0.16f;
        [SerializeField] private int pelletsPerShot = 1;

        [Header("Accuracy (degrees of cone half-angle)")]
        // A pistol shot from a braced stance is far more accurate than one thrown out
        // while sprinting, so the gap between the two is deliberately wide.
        [SerializeField] private float baseSpread = 0.35f;
        [SerializeField] private float movingSpreadBonus = 1.8f;
        [SerializeField] private float sprintSpreadBonus = 4.5f;
        [SerializeField] private float crouchSpreadMultiplier = 0.45f;
        [SerializeField] private float aimSpreadMultiplier = 0.18f;

        [Header("Ammo")]
        [SerializeField] private int magazineSize = 15;      // a service pistol's magazine
        [SerializeField] private int reserveAmmo = 75;
        [SerializeField] private int maxReserveAmmo = 180;

        [Tooltip("What one ammunition box is worth to this weapon. Zero lets the box "
                 + "decide, which is every weapon but the belt-fed one.")]
        [SerializeField] private int ammoBoxRounds;
        [SerializeField] private float reloadTime = 1.9f;    // mag out, mag in, slide release

        [Header("Recoil")]
        [SerializeField] private float recoilPitch = 1.4f;
        [SerializeField] private float recoilYaw = 0.4f;

        [Tooltip("How this weapon climbs and walks under fire. See RecoilProfile.")]
        [SerializeField] private RecoilProfile recoil = new RecoilProfile();

        [Header("Aiming")]
        [SerializeField] private float hipFov = 70f;
        [SerializeField] private float aimFov = 52f;
        [SerializeField] private float fovLerpSpeed = 12f;

        [Header("Noise")]
        [SerializeField] private float shotNoiseRadius = 45f;

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private MouseLook mouseLook;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Light muzzleFlash;
        [SerializeField] private LayerMask hitMask = ~0;

        public string WeaponName => weaponName;
        public int AmmoInMagazine { get; private set; }
        public int ReserveAmmo => reserveAmmo;
        public int MagazineSize => magazineSize;
        public float Damage => damage;

        /// <summary>Everything left: magazine plus reserve. What a scavenged gun counts down.</summary>
        public int TotalAmmo => AmmoInMagazine + reserveAmmo;

        /// <summary>True for a weapon that is thrown away rather than reloaded, once dry.</summary>
        public bool DiscardWhenEmpty => discardWhenEmpty;

        /// <summary>
        /// How far the barrels have spun up, 0–1. Read by the barrel spinner for the
        /// rotation and by the audio for the whine's pitch.
        ///
        /// This is the whole character of a rotary gun: the trigger does not fire it, it
        /// *starts* it, and there is close to a second between deciding to shoot and
        /// anything coming out. Everything else in the game answers instantly, so the one
        /// weapon that does not is the one you have to commit with.
        /// </summary>
        public float SpinFraction { get; private set; } = 1f;

        /// <summary>True for a weapon whose barrels have to be brought up to speed.</summary>
        public bool IsRotary => spinUpSeconds > 0f;

        /// <summary>True while the barrels are turning but not yet fast enough to fire.</summary>
        public bool IsSpinningUp => IsRotary && SpinFraction > 0f && SpinFraction < 1f;
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }

        /// <summary>Current cone half-angle in degrees — the HUD sizes the crosshair from this.</summary>
        public float CurrentSpread { get; private set; }

        /// <summary>0..1 through a reload, for the view model animation.</summary>
        public float ReloadProgress { get; private set; }

        public event Action Fired;
        public event Action DryFired;
        public event Action<bool> HitConfirmed;   // true = critical
        public event Action<ImpactEvent> Impacted;
        public event Action ReloadStarted;
        public event Action ReloadFinished;

        /// <summary>
        /// The last round has left a discard-when-empty weapon. Whatever is holding it is
        /// expected to take it away — the weapon does not remove itself, because it does
        /// not know whether it is in a switcher, on a rack, or in a test.
        /// </summary>
        public event Action Depleted;
        public event Action AmmoChanged;

        // Seconds still to wait before this can fire again, rather than an absolute
        // Time.time stamp. Same reasoning as the reload timer two floors down: Time.time
        // does not advance in edit mode, so anything keyed to it is untestable there —
        // a headless test that dry-fires once is then locked out for ever and quietly
        // stops exercising the thing it was written to exercise.
        private float _fireCooldown;
        private float _reloadElapsed;
        private float _flashUntil;

        private void Awake()
        {
            AmmoInMagazine = magazineSize;

            // Rotary weapons start stopped; everything else is permanently at speed, so
            // the spin gate in TryFire is a no-op for them.
            SpinFraction = IsRotary ? 0f : 1f;

            if (viewCamera == null) viewCamera = GetComponentInParent<Camera>();
            if (viewCamera == null) viewCamera = Camera.main;
            if (mouseLook == null) mouseLook = GetComponentInParent<MouseLook>();
            if (playerController == null) playerController = GetComponentInParent<PlayerController>();
            if (muzzle == null) muzzle = transform;

            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void Update()
        {
            if (!GameManager.GameplayActive)
            {
                if (muzzleFlash != null) muzzleFlash.enabled = false;
                return;
            }

            IsAiming = InputReader.AimHeld && !IsReloading;
            CurrentSpread = ComputeSpread();

            if (viewCamera != null)
            {
                float targetFov = IsAiming ? aimFov : hipFov;
                viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);
            }

            if (InputReader.ReloadPressed) TryReload();
            TickReload(Time.deltaTime);
            TickCooldown(Time.deltaTime);

            bool holdingTrigger = InputReader.FireHeld
                                 || (alsoFiresOnRifleKey && InputReader.RifleFireHeld);

            TickSpin(Time.deltaTime, holdingTrigger);

            bool wantsFire = automatic ? InputReader.FireHeld : InputReader.FirePressed;

            // The rifle answers to Space as well as the trigger, so it can be drawn and
            // fired with one key without giving up normal left-click firing.
            if (alsoFiresOnRifleKey)
                wantsFire |= automatic ? InputReader.RifleFireHeld : InputReader.RifleFirePressed;

            if (wantsFire) TryFire();

            if (muzzleFlash != null && muzzleFlash.enabled && Time.time > _flashUntil)
                muzzleFlash.enabled = false;
        }

        private float ComputeSpread()
        {
            float spread = baseSpread;

            if (playerController != null)
            {
                if (playerController.IsSprinting) spread += sprintSpreadBonus;
                else if (playerController.CurrentSpeed > 0.5f) spread += movingSpreadBonus;

                if (playerController.IsCrouching) spread *= crouchSpreadMultiplier;
            }

            if (IsAiming) spread *= aimSpreadMultiplier;
            return spread;
        }

        /// <summary>
        /// Whether pulling the trigger right now would put a round downrange. The test
        /// for the holster-mid-reload bug asserts on this rather than on IsReloading, so
        /// it is checking what the player experiences: the gun works, or it does not.
        /// </summary>
        public bool CanFire => !IsReloading && AmmoInMagazine > 0 && _fireCooldown <= 0f;

        /// <summary>
        /// Runs down the gap between shots. Driven from Update, and public for the same
        /// reason <see cref="TickReload"/> is: a test has no frames.
        /// </summary>
        public void TickCooldown(float deltaTime)
        {
            if (_fireCooldown > 0f) _fireCooldown = Mathf.Max(0f, _fireCooldown - deltaTime);
        }

        /// <summary>
        /// Winds the barrels up while the trigger is held and down when it is not.
        /// Public so a test can run a gatling gun up to speed without a mouse.
        /// </summary>
        public void TickSpin(float deltaTime, bool holdingTrigger)
        {
            if (!IsRotary)
            {
                SpinFraction = 1f;
                return;
            }

            // Reloading stops the barrels: you cannot hold one at speed while changing
            // a belt, and it means an interrupted burst costs the spin-up again.
            bool winding = holdingTrigger && !IsReloading && AmmoInMagazine > 0;

            float rate = winding
                ? deltaTime / Mathf.Max(0.05f, spinUpSeconds)
                : -deltaTime / Mathf.Max(0.05f, spinDownSeconds);

            SpinFraction = Mathf.Clamp01(SpinFraction + rate);
        }

        public void TryFire()
        {
            if (IsReloading || _fireCooldown > 0f) return;

            // Empty first, spin gate second, and the order is load-bearing.
            //
            // These two checks used to be the other way round, and for a rotary weapon
            // that was a deadlock you could not talk your way out of. TickSpin stops
            // winding the barrels the instant the magazine reaches zero — see
            // `AmmoInMagazine > 0` in its `winding` term — so on the very next frame
            // SpinFraction is below 1 and the spin gate returns early. The auto-reload
            // lives *below* that gate, so it was never reached: the gatling gun ran its
            // belt dry, went silent with three hundred rounds in reserve, and never
            // reloaded itself the way every other weapon in the game does. It did not
            // even dry-fire, so there was no click to tell you what had happened.
            //
            // Asking "is it empty" before "are the barrels up" costs nothing — a weapon
            // with no rounds in it was never going to fire this frame anyway.
            if (AmmoInMagazine <= 0)
            {
                _fireCooldown = 0.25f;
                DryFired?.Invoke();
                if (reserveAmmo > 0) TryReload();
                return;
            }

            // Barrels not yet at speed: the trigger is spinning them, not firing them.
            if (IsRotary && SpinFraction < 1f) return;

            _fireCooldown = fireInterval;
            AmmoInMagazine--;
            AmmoChanged?.Invoke();

            // Raised the moment the last round leaves the barrel, not on the click after
            // it: a scavenged weapon should disappear as it empties, not once you have
            // stood there pulling a dead trigger.
            if (discardWhenEmpty && TotalAmmo <= 0) Depleted?.Invoke();

            for (int i = 0; i < Mathf.Max(1, pelletsPerShot); i++)
                FireOnePellet();

            if (mouseLook != null)
            {
                Vector2 kick = recoil.NextKick(IsAiming);
                mouseLook.AddRecoil(kick.x, kick.y, recoil.uncorrectedShare);
            }

            if (muzzleFlash != null)
            {
                muzzleFlash.enabled = true;
                _flashUntil = Time.time + 0.045f;
            }

            Noise.Emit(transform.position, shotNoiseRadius);
            Fired?.Invoke();
        }

        private void FireOnePellet()
        {
            if (viewCamera == null) return;

            Transform cam = viewCamera.transform;
            Vector3 origin = cam.position;
            Vector3 direction = ConeDirection(cam.forward, CurrentSpread);

            Vector3 endPoint = origin + direction * range;
            RaycastHit hit;

            if (Physics.Raycast(origin, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    bool critical = false;
                    var hitbox = hit.collider.GetComponent<Hitbox>();
                    if (hitbox != null) critical = hitbox.IsCritical;

                    var info = new DamageInfo(damage, hit.point, hit.normal, direction, gameObject, critical);
                    info.Kind = damageKind;
                    target.TakeDamage(info);
                    HitConfirmed?.Invoke(critical);
                    ImpactFx.Impact(hit.point, hit.normal, critical ? new Color(1f, 0.35f, 0.35f) : new Color(0.7f, 0.1f, 0.1f));

                    Impacted?.Invoke(new ImpactEvent
                    {
                        Point = hit.point,
                        Normal = hit.normal,
                        HitCharacter = true,
                        Critical = critical
                    });
                }
                else if (target != null)
                {
                    // A corpse. It still stops the bullet and still bleeds — it just
                    // cannot be hurt any further.
                    Impacted?.Invoke(new ImpactEvent
                    {
                        Point = hit.point,
                        Normal = hit.normal,
                        HitCharacter = true,
                        Critical = false
                    });
                }
                else
                {
                    ImpactFx.Impact(hit.point, hit.normal, new Color(0.85f, 0.8f, 0.6f), 0.06f);

                    Impacted?.Invoke(new ImpactEvent
                    {
                        Point = hit.point,
                        Normal = hit.normal,
                        HitCharacter = false,
                        Critical = false
                    });
                }
            }

            Vector3 tracerStart = muzzle != null ? muzzle.position : origin;
            ImpactFx.Tracer(tracerStart, endPoint, new Color(1f, 0.9f, 0.55f, 0.85f));
        }

        private static Vector3 ConeDirection(Vector3 forward, float halfAngleDegrees)
        {
            if (halfAngleDegrees <= 0.0001f) return forward;

            // Pick a perpendicular axis, tilt by a random angle, then roll around the aim vector.
            Vector3 axis = Vector3.Cross(forward, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(forward, Vector3.right); // looking straight up/down
            axis.Normalize();

            // Qualified: this file imports System, so bare Random is ambiguous.
            float angle = UnityEngine.Random.Range(0f, halfAngleDegrees);
            float roll = UnityEngine.Random.Range(0f, 360f);
            Quaternion rotation = Quaternion.AngleAxis(roll, forward) * Quaternion.AngleAxis(angle, axis);
            return rotation * forward;
        }

        /// <summary>
        /// Starts a reload. Bound to Shift and R.
        ///
        /// **A reload throws away whatever was left in the magazine** on everything except
        /// the belt-fed gatling. That turns reloading into a decision rather than a reflex:
        /// topping up between rooms is free when the magazine is nearly empty and expensive
        /// when it is nearly full, so "should I reload now or push on with nine rounds" is a
        /// real question with a real cost attached.
        /// </summary>
        public void TryReload()
        {
            if (IsReloading || AmmoInMagazine >= magazineSize || reserveAmmo <= 0) return;

            // Refuse a reload that would leave the player worse off than they started.
            //
            // This is the one place the discard rule gets a guard, and it is for the end of
            // a level rather than the middle: with 10 in the magazine and 2 in reserve, a
            // faithful discard hands back a magazine of 2 and destroys 10 rounds for
            // nothing. Nobody has ever meant to do that. Everywhere else the cost is the
            // point, so it stands.
            if (!IsRotary && reserveAmmo < AmmoInMagazine) return;

            IsReloading = true;
            _reloadElapsed = 0f;
            ReloadProgress = 0f;
            ReloadStarted?.Invoke();
            AmmoChanged?.Invoke();
        }

        /// <summary>
        /// Advances a reload in progress. Driven from Update, and public so a test can
        /// run one through without waiting two seconds.
        ///
        /// This used to be a coroutine, and that was a bug you could feel: holstering a
        /// gun mid-reload deactivates its GameObject, Unity stops the routine where it
        /// stands, and IsReloading stayed true for ever — the weapon could then never
        /// fire and never reload again, because both check that flag first. Holding the
        /// progress in a field instead means a holstered gun simply stops advancing and
        /// picks the reload up where it left off when you draw it again.
        /// </summary>
        private void OnDisable()
        {
            // Put it away mid-burst and the muzzle settles while it is on your back.
            if (recoil != null) recoil.Reset();
        }

        public void TickReload(float deltaTime)
        {
            if (!IsReloading) return;

            _reloadElapsed += deltaTime;
            ReloadProgress = Mathf.Clamp01(_reloadElapsed / reloadTime);
            if (_reloadElapsed < reloadTime) return;

            if (IsRotary)
            {
                // A belt is not a magazine. You do not throw one away to put a fresh one on,
                // and at a hundred rounds a discard would cost a quarter of the gun's whole
                // supply for tapping the key at the wrong moment. Belt-fed guns top up.
                int needed = magazineSize - AmmoInMagazine;
                int topUp = Mathf.Min(needed, reserveAmmo);
                AmmoInMagazine += topUp;
                reserveAmmo -= topUp;
            }
            else
            {
                // The old magazine goes on the floor with whatever was still in it. Note the
                // assignment rather than the += that used to be here: the rounds already in
                // the gun are not carried over, they are gone.
                int taken = Mathf.Min(magazineSize, reserveAmmo);
                AmmoInMagazine = taken;
                reserveAmmo -= taken;
            }

            IsReloading = false;
            ReloadProgress = 1f;
            ReloadFinished?.Invoke();
            AmmoChanged?.Invoke();
        }

        /// <summary>
        /// Takes rounds from an ammunition box. Returns how much was actually taken, so a
        /// pickup can refuse to be consumed when the gun is full.
        ///
        /// A box is worth a different number of rounds to different weapons, which is what
        /// <see cref="ammoBoxRounds"/> is for. A box that gives the Desert Eagle 24 rounds
        /// — ten shots, a real resupply — gives the gatling gun a tenth of a second of
        /// fire, and a weapon you cannot meaningfully reload from anything in the world is
        /// one you stop carrying. Belt-fed weapons override the box's own number.
        /// </summary>
        public int AddAmmo(int amount)
        {
            // A yellow box is small-arms ammunition: it feeds the Desert Eagle, the rifle
            // and the Uzi, and it does nothing at all for a belt-fed gun. The gatling has
            // its own supply — the green belt crates — and keeping the two apart is what
            // makes the crates worth crossing a level for. Without this the yellow boxes
            // quietly became a second, more common source for it.
            if (IsRotary) return 0;

            if (ammoBoxRounds > 0) amount = ammoBoxRounds;
            if (amount <= 0 || reserveAmmo >= maxReserveAmmo) return 0;

            int before = reserveAmmo;
            reserveAmmo = Mathf.Min(maxReserveAmmo, reserveAmmo + amount);
            AmmoChanged?.Invoke();
            return reserveAmmo - before;
        }

        /// <summary>
        /// What one ammunition box is worth to this weapon, and the most it may carry in
        /// reserve. Zero rounds leaves the box's own number alone, which is every weapon
        /// but the gatling gun.
        /// </summary>
        public void ConfigureResupply(int roundsPerBox, int reserveCeiling)
        {
            ammoBoxRounds = Mathf.Max(0, roundsPerBox);
            if (reserveCeiling > 0) maxReserveAmmo = reserveCeiling;
        }

        /// <summary>What one ammunition box gives this weapon; 0 means the box decides.</summary>
        public int AmmoBoxRounds => ammoBoxRounds;
        public int MaxReserveAmmo => maxReserveAmmo;

        public void SetHitMask(LayerMask mask)
        {
            hitMask = mask;
        }

        /// <summary>Gives this weapon its own dedicated fire key in addition to the trigger.</summary>
        public void SetFiresOnRifleKey(bool enabled)
        {
            alsoFiresOnRifleKey = enabled;
        }

        /// <summary>Marks this weapon as having a telescopic sight.</summary>
        public void SetScoped(bool enabled)
        {
            scoped = enabled;
        }

        /// <summary>
        /// Sets this weapon's numbers. Used by the scene setup to build a rifle and a
        /// pistol from the same component with different characteristics.
        /// </summary>
        public void ConfigureStats(string displayName, float shotDamage, float shotRange,
                                   float interval, int magazine, int reserve,
                                   float reload, float spread, float recoilDegrees,
                                   float hipFieldOfView, float aimFieldOfView, float noiseRadius)
        {
            weaponName = displayName;
            damage = shotDamage;
            range = shotRange;
            fireInterval = interval;
            magazineSize = magazine;
            AmmoInMagazine = magazine;
            reserveAmmo = reserve;
            reloadTime = reload;
            baseSpread = spread;
            recoilPitch = recoilDegrees;
            hipFov = hipFieldOfView;
            aimFov = aimFieldOfView;
            shotNoiseRadius = noiseRadius;
        }

        /// <summary>The live recoil pattern, so the setup can tune it and a test can read it.</summary>
        public RecoilProfile Recoil => recoil;

        /// <summary>
        /// Sets how this weapon kicks. Called by the scene setup, because recoil is part
        /// of a weapon's identity in the same way its damage is.
        /// </summary>
        public void ConfigureRecoil(float vertical, float horizontal, float climbPerShot,
                                    float maximumClimb, float uncorrectedShare, float settleSeconds = 0.45f)
        {
            recoil.verticalDegrees = vertical;
            recoil.horizontalDegrees = horizontal;
            recoil.climbPerShot = climbPerShot;
            recoil.maximumClimb = maximumClimb;
            recoil.uncorrectedShare = Mathf.Clamp01(uncorrectedShare);
            recoil.settleSeconds = settleSeconds;

            // Keep the old single number meaningful for anything still reading it.
            recoilPitch = vertical;
            recoilYaw = horizontal;
        }

        /// <summary>
        /// Fills a weapon to a fixed total and sets whether it survives running dry.
        /// Used by the Uzi power-up, which arrives with exactly 200 rounds and no way to
        /// find more.
        /// </summary>
        public void ConfigureAsPowerUp(int totalRounds, bool discardOnEmpty)
        {
            discardWhenEmpty = discardOnEmpty;

            AmmoInMagazine = Mathf.Min(magazineSize, totalRounds);
            reserveAmmo = Mathf.Max(0, totalRounds - AmmoInMagazine);
            maxReserveAmmo = Mathf.Max(maxReserveAmmo, reserveAmmo);

            IsReloading = false;
            ReloadProgress = 1f;
            AmmoChanged?.Invoke();
        }

        /// <summary>
        /// Sets the barrel spin-up and spin-down. Zero spin-up leaves a weapon behaving
        /// exactly as it always has, which is every weapon but the gatling gun.
        /// </summary>
        public void ConfigureRotary(float spinUp, float spinDown)
        {
            spinUpSeconds = spinUp;
            spinDownSeconds = spinDown;
            SpinFraction = spinUp > 0f ? 0f : 1f;
        }

        /// <summary>Full auto or one shot per pull. The gatling gun is automatic.</summary>
        public void SetAutomatic(bool value)
        {
            automatic = value;
        }

        /// <summary>
        /// Adds linked ammunition to a scavenged weapon. Returns how many rounds were
        /// taken, so a belt crate you have no room for stays on the floor.
        ///
        /// Separate from AddAmmo because that one is capped by maxReserveAmmo, which for
        /// the gatling gun is the whole point of the weapon: it is not a reserve you top
        /// up incidentally, it is a belt somebody left in a crate.
        /// </summary>
        public int FeedBelt(int roundsOffered, int ceiling)
        {
            if (roundsOffered <= 0) return 0;

            int room = Mathf.Max(0, ceiling - TotalAmmo);
            int taken = Mathf.Min(roundsOffered, room);
            if (taken <= 0) return 0;

            reserveAmmo += taken;
            maxReserveAmmo = Mathf.Max(maxReserveAmmo, reserveAmmo);

            AmmoChanged?.Invoke();
            return taken;
        }

        /// <summary>Sets what this weapon's rounds count as. See <see cref="DamageKind"/>.</summary>
        public void SetDamageKind(DamageKind kind)
        {
            damageKind = kind;
        }

        /// <summary>Called by the scene setup. Everything else resolves itself in Awake.</summary>
        public void ConfigureReferences(Transform muzzleTransform, Light flash, LayerMask mask)
        {
            muzzle = muzzleTransform;
            muzzleFlash = flash;
            hitMask = mask;
        }
    }
}
