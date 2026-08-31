using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.Fx;
using ZombieHouse.Player;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// The offhand katana. It is an arc rather than a raycast: one swing sweeps a cone in
    /// front of the player and can cut down three or four walkers packed in a doorway,
    /// hitting each of them exactly once.
    ///
    /// A katana takes limbs. Any cut that lands on an arm, a leg or a head severs it —
    /// the head is always fatal, a leg drops them, an arm just costs them an arm.
    ///
    /// It is deliberately quiet compared to the pistol — a shot pulls the whole house
    /// towards you, a blade only carries a few metres, which makes it the tool for
    /// clearing a room you do not want to announce yourself in.
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private float damage = 85f;
        [Tooltip("Reach of the swing. Effective reach is this plus sweepRadius, so 1.45 "
                 + "and 0.75 put the edge of the arc at about 2.2 m.")]
        [SerializeField] private float range = 1.45f;
        [SerializeField] private float arcHalfAngle = 55f;
        [SerializeField] private float sweepRadius = 0.75f;
        [SerializeField] private int maximumTargetsPerSwing = 4;

        [Header("Dismemberment")]
        [Tooltip("A clean blade takes the limb it lands on. Turn off for a blunt weapon.")]
        [SerializeField] private bool seversLimbs = true;

        [Header("Timing (seconds)")]
        [SerializeField] private float windup = 0.13f;
        [SerializeField] private float strike = 0.12f;
        [SerializeField] private float recovery = 0.4f;
        [SerializeField] private float cooldown = 0.72f;

        [Header("Noise heard by zombies (metres)")]
        [SerializeField] private float swingNoiseRadius = 4f;
        [SerializeField] private float hitNoiseRadius = 7f;

        [Header("View model poses (local to the camera)")]
        [SerializeField] private Vector3 windupPosition = new Vector3(-0.44f, -0.12f, 0.26f);
        [SerializeField] private Vector3 windupRotation = new Vector3(-8f, -42f, -58f);
        [SerializeField] private Vector3 strikePosition = new Vector3(0.30f, -0.34f, 0.46f);
        [SerializeField] private Vector3 strikeRotation = new Vector3(14f, 52f, 46f);
        [SerializeField] private float idleBobAmount = 0.012f;

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private LayerMask hitMask = ~0;

        public bool IsSwinging { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, _nextSwingTime - Time.time);

        public event Action Swung;
        public event Action<int> HitTargets;   // how many bodies the swing connected with

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _swingTime = -1f;
        private float _nextSwingTime;
        private bool _damageApplied;
        private float _bobPhase;

        private ImpactSystem _impacts;
        private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
        private readonly List<Collider> _sorted = new List<Collider>();

        private float TotalDuration => windup + strike + recovery;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;

            if (viewCamera == null) viewCamera = GetComponentInParent<Camera>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void Start()
        {
            _impacts = FindAnyObjectByType<ImpactSystem>();
        }

        public void ConfigureReferences(Camera camera, LayerMask mask)
        {
            viewCamera = camera;
            hitMask = mask;
        }

        private void Update()
        {
            if (GameManager.GameplayActive && InputReader.MeleePressed) TrySwing();

            AdvanceSwing();
            PoseViewModel();
        }

        public void TrySwing()
        {
            if (IsSwinging || Time.time < _nextSwingTime) return;

            IsSwinging = true;
            _swingTime = 0f;
            _damageApplied = false;
            _nextSwingTime = Time.time + cooldown;

            GameAudio.Play2D(Sfx.MacheteSwing, 0.5f, 0.1f);
            Noise.Emit(transform.position, swingNoiseRadius);
            Swung?.Invoke();
        }

        private void AdvanceSwing()
        {
            if (!IsSwinging) return;
            if (!GameManager.GameplayActive) return;

            _swingTime += Time.deltaTime;

            // Damage lands just after the blade starts moving, not at the button press.
            if (!_damageApplied && _swingTime >= windup + strike * 0.35f)
            {
                _damageApplied = true;
                ResolveHit();
            }

            if (_swingTime >= TotalDuration)
            {
                IsSwinging = false;
                _swingTime = -1f;
            }
        }

        // ---- hit resolution -------------------------------------------------

        private void ResolveHit()
        {
            if (viewCamera == null) return;

            Transform camera = viewCamera.transform;
            Vector3 origin = camera.position;
            Vector3 centre = origin + camera.forward * (range * 0.55f);

            Collider[] candidates = Physics.OverlapSphere(centre, sweepRadius, hitMask,
                                                          QueryTriggerInteraction.Ignore);

            // Most-centred colliders first, so a swing registers on the chest or head
            // rather than on whichever foot the physics query happened to return first.
            _sorted.Clear();
            _sorted.AddRange(candidates);
            _sorted.Sort((a, b) => AngleFrom(camera, a).CompareTo(AngleFrom(camera, b)));

            _alreadyHit.Clear();
            int connected = 0;

            foreach (Collider collider in _sorted)
            {
                if (connected >= maximumTargetsPerSwing) break;

                Vector3 toTarget = collider.bounds.center - origin;
                if (toTarget.magnitude > range + sweepRadius) continue;
                if (Vector3.Angle(camera.forward, toTarget) > arcHalfAngle) continue;

                var hitbox = collider.GetComponent<Hitbox>();
                IDamageable owner = hitbox != null
                    ? hitbox.OwnerDamageable
                    : collider.GetComponentInParent<IDamageable>();

                if (owner == null || !owner.IsAlive) continue;
                if (!_alreadyHit.Add(owner)) continue;

                Vector3 point = collider.ClosestPoint(origin);
                Vector3 normal = (origin - point).normalized;

                var info = new DamageInfo(damage, point, normal, camera.forward, gameObject,
                                          hitbox != null && hitbox.IsCritical);

                // Route through the hitbox so head and limb multipliers still apply.
                IDamageable receiver = hitbox != null ? (IDamageable)hitbox : owner;
                receiver.TakeDamage(info);

                if (_impacts != null)
                    _impacts.SpawnFleshFx(point, normal, info.IsCritical, 1.6f);

                GameAudio.PlayAt(Sfx.MacheteFlesh, point, 0.85f, 0.12f);

                // The cut lands after the damage, so a limb that was already fatal still
                // comes off rather than the body simply falling over intact.
                if (seversLimbs)
                {
                    var dismemberment = collider.GetComponentInParent<Enemies.ZombieDismemberment>();
                    if (dismemberment != null)
                        dismemberment.Sever(collider, camera.forward, point);
                }

                connected++;
            }

            if (connected > 0)
            {
                Noise.Emit(transform.position, hitNoiseRadius);
                HitTargets?.Invoke(connected);
                return;
            }

            // Nothing soft in range — see if the blade bit into the level instead.
            RaycastHit wall;
            if (Physics.Raycast(origin, camera.forward, out wall, range, hitMask,
                                QueryTriggerInteraction.Ignore))
            {
                if (_impacts != null) _impacts.SpawnWorldFx(wall.point, wall.normal);
                GameAudio.PlayAt(Sfx.MacheteMetal, wall.point, 0.55f, 0.1f);
                Noise.Emit(transform.position, hitNoiseRadius * 0.6f);
            }

            HitTargets?.Invoke(0);
        }

        private static float AngleFrom(Transform camera, Collider collider)
        {
            return Vector3.Angle(camera.forward, collider.bounds.center - camera.position);
        }

        // ---- view model -----------------------------------------------------

        private void PoseViewModel()
        {
            if (!IsSwinging)
            {
                // Rest: a slow drift so the blade is not welded to the screen.
                _bobPhase += Time.deltaTime * 1.4f;
                Vector3 drift = new Vector3(Mathf.Sin(_bobPhase) * idleBobAmount,
                                            Mathf.Cos(_bobPhase * 0.8f) * idleBobAmount * 0.6f,
                                            0f);

                transform.localPosition = Vector3.Lerp(transform.localPosition, _restPosition + drift, 8f * Time.deltaTime);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _restRotation, 8f * Time.deltaTime);
                return;
            }

            Vector3 position;
            Quaternion rotation;

            if (_swingTime < windup)
            {
                // Coil back over the left shoulder.
                float t = Mathf.SmoothStep(0f, 1f, _swingTime / Mathf.Max(0.001f, windup));
                position = Vector3.Lerp(_restPosition, windupPosition, t);
                rotation = Quaternion.Slerp(_restRotation, Quaternion.Euler(windupRotation), t);
            }
            else if (_swingTime < windup + strike)
            {
                // The cut itself: fast, and eased out so it lands with weight.
                float t = (_swingTime - windup) / Mathf.Max(0.001f, strike);
                t = 1f - (1f - t) * (1f - t);
                position = Vector3.Lerp(windupPosition, strikePosition, t);
                rotation = Quaternion.Slerp(Quaternion.Euler(windupRotation), Quaternion.Euler(strikeRotation), t);
            }
            else
            {
                // Recover to the hip.
                float t = Mathf.SmoothStep(0f, 1f, (_swingTime - windup - strike) / Mathf.Max(0.001f, recovery));
                position = Vector3.Lerp(strikePosition, _restPosition, t);
                rotation = Quaternion.Slerp(Quaternion.Euler(strikeRotation), _restRotation, t);
            }

            transform.localPosition = position;
            transform.localRotation = rotation;
        }
    }
}
