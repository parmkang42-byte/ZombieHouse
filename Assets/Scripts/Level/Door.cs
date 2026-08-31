using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.Enemies;
using ZombieHouse.Player;

namespace ZombieHouse.Level
{
    /// <summary>
    /// A door in a doorway: swings on a hinge, opens on Space, and is the best place in the
    /// game to be waiting for someone.
    ///
    /// **It is deliberately invisible to the NavMesh.** Doors sit on their own layer, and
    /// <see cref="RuntimeNavMeshBaker"/> bakes the Default layer only — so adding a hundred
    /// doors changes navigation by exactly nothing. That is not a shortcut, it is the whole
    /// design: every level's verification rests on the NavMesh, and a closed door that baked
    /// as a wall would seal rooms, strand spawns and invalidate all six of them at a stroke.
    /// The same rule the prop meshes follow — change the picture, never the pathing.
    ///
    /// Which leaves the question of what a zombie does at a closed door. The answer is that
    /// it shoves it: anything on the enemy layer that touches a door pushes it open and
    /// keeps walking. That is both what the pathing already assumes and better behaviour
    /// than the alternative, because a door swinging open on its own in front of you tells
    /// you something is coming through it before you can see what.
    ///
    /// The player gets no such courtesy and has to open it by hand, which is the point: a
    /// door is a decision. Opening one is loud, it is slow, and you cannot do it while
    /// aiming at what is behind it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Door : MonoBehaviour
    {
        [Header("Swing")]
        [Tooltip("How far it opens. Ninety-five rather than ninety so it visibly clears the "
                 + "frame instead of sitting flush inside it.")]
        [SerializeField] private float openAngle = 95f;

        [Tooltip("Seconds to swing. Slow enough that opening one is a commitment.")]
        [SerializeField] private float swingSeconds = 0.55f;

        [Header("Reach")]
        [Tooltip("How close you must be to work it.")]
        [SerializeField] private float interactRange = 2.4f;

        [Tooltip("How squarely you must be looking at it. 0.55 is a generous cone — a door "
                 + "is a big target and hunting for the exact pixel is not the game.")]
        [SerializeField] private float lookTolerance = 0.55f;

        [Header("Noise")]
        [Tooltip("How far the hinges carry. A door is one of the loudest things you can do, "
                 + "and every zombie that hears it comes to look.")]
        [SerializeField] private float noiseRadius = 16f;

        /// <summary>Raised the moment it starts to open, by whatever opened it.</summary>
        public event Action Opened;

        /// <summary>
        /// Every door currently in the level. The spawner walks this to decide which
        /// zombies get to lie in wait, which is cheaper and more exact than a physics
        /// overlap and does not depend on colliders being ready yet.
        /// </summary>
        public static readonly List<Door> All = new List<Door>();

        public bool IsOpen { get; private set; }
        public bool IsMoving => _swing > 0f && _swing < 1f;

        private Transform _leaf;
        private Transform _player;
        private Quaternion _closed;
        private Quaternion _open;
        private float _swing;                 // 0 shut, 1 wide
        private float _target;
        private readonly List<DoorAmbush> _waiting = new List<DoorAmbush>();

        /// <summary>
        /// Called by the generator once the leaf exists. Not Awake, because a generator
        /// builds these in edit mode where Awake never runs.
        /// </summary>
        public void Initialise(Transform leaf, float hingeSign)
        {
            _leaf = leaf;
            _closed = leaf.localRotation;
            _open = _closed * Quaternion.Euler(0f, openAngle * hingeSign, 0f);
        }

        /// <summary>Registers a lurker to be woken when this opens.</summary>
        public void Register(DoorAmbush ambusher)
        {
            if (ambusher != null && !_waiting.Contains(ambusher)) _waiting.Add(ambusher);
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _player = playerObject.transform;

            if (_leaf == null)
            {
                // Rebuilt from a saved scene rather than freshly generated: the hinge
                // rotations were never captured, so recover them from what is on disk.
                _leaf = transform.childCount > 0 ? transform.GetChild(0) : transform;
                _closed = _leaf.localRotation;
                _open = _closed * Quaternion.Euler(0f, openAngle, 0f);
            }
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            AdvanceSwing();

            if (IsOpen || _player == null) return;
            if (!PlayerIsAtIt()) return;

            if (InputReader.InteractPressed) Open();
        }

        private void AdvanceSwing()
        {
            if (Mathf.Approximately(_swing, _target)) return;

            _swing = Mathf.MoveTowards(_swing, _target,
                                       Time.deltaTime / Mathf.Max(0.05f, swingSeconds));

            // Eased, so it starts heavy and settles rather than moving at a constant rate.
            float eased = _swing * _swing * (3f - 2f * _swing);
            _leaf.localRotation = Quaternion.Slerp(_closed, _open, eased);
        }

        private bool PlayerIsAtIt()
        {
            Vector3 toDoor = transform.position - _player.position;
            toDoor.y = 0f;

            if (toDoor.sqrMagnitude > interactRange * interactRange) return false;

            Camera view = Camera.main;
            if (view == null) return true;

            return Vector3.Dot(view.transform.forward, toDoor.normalized) > lookTolerance;
        }

        /// <summary>
        /// Opens it, wakes anything waiting behind it, and tells the level it heard a door.
        /// Safe to call twice.
        /// </summary>
        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            _target = 1f;

            GameAudio.PlayAt(Sfx.ExitOpen, transform.position, 0.45f, 0.12f);
            Noise.Emit(transform.position, noiseRadius);

            // Whatever was crouched behind it gets up. Raised before the leaf has finished
            // swinging on purpose: the lunge should start while the door is still moving,
            // so the thing is already on you as the gap widens.
            for (int i = 0; i < _waiting.Count; i++)
                if (_waiting[i] != null) _waiting[i].Spring();

            Opened?.Invoke();
        }

        /// <summary>
        /// Anything on the enemy layer shoves it open and walks through. Doors are not in
        /// the NavMesh, so the pathing already assumed this — without it a zombie would
        /// walk through a closed door, which looks far worse than one that swings.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (IsOpen) return;
            if (other.GetComponentInParent<ZombieHealth>() == null) return;

            Open();
        }
    }
}
