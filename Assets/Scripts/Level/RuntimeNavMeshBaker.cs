using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Bakes a NavMesh from the scene's colliders at startup, using the core AI module
    /// (UnityEngine.AI.NavMeshBuilder). This deliberately avoids the com.unity.ai.navigation
    /// package: the house is generated at runtime, so a pre-baked NavMesh asset would be
    /// stale anyway, and there is no bake button to forget after editing the layout.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        [Header("Volume to bake (world space, centred on this object)")]
        [SerializeField] private Vector3 bakeVolumeSize = new Vector3(120f, 20f, 120f);
        [Tooltip("Default layer only — level geometry. Keeps pickups, the player and zombies out of the bake.")]
        [SerializeField] private LayerMask includeLayers = 1;

        [Header("Agent settings")]
        [SerializeField] private float agentRadius = 0.4f;
        [SerializeField] private float agentHeight = 1.9f;
        [SerializeField] private float agentClimb = 0.45f;
        [SerializeField] private float agentSlope = 45f;

        [Header("Behaviour")]
        [SerializeField] private bool bakeOnAwake = true;

        private NavMeshData _data;
        private NavMeshDataInstance _instance;
        private readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>();

        public bool HasBaked { get; private set; }

        private void Awake()
        {
            if (bakeOnAwake) Bake();
        }

        private void OnDisable()
        {
            if (_instance.valid) _instance.Remove();
            HasBaked = false;
        }

        [ContextMenu("Bake NavMesh Now")]
        public void Bake()
        {
            // CollectSources takes a WORLD-space volume...
            Bounds worldBounds = new Bounds(transform.position, bakeVolumeSize);

            _sources.Clear();
            NavMeshBuilder.CollectSources(
                worldBounds,
                includeLayers,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                _sources);

            if (_sources.Count == 0)
            {
                Debug.LogWarning("[RuntimeNavMeshBaker] No colliders found inside the bake volume.");
                return;
            }

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentClimb = agentClimb;
            settings.agentSlope = agentSlope;

            if (_instance.valid) _instance.Remove();

            // ...but BuildNavMeshData takes bounds RELATIVE to the position argument, so
            // it must be centred on zero. Passing the world bounds here as well offsets
            // the volume by its own centre — which silently lifts the bake off the ground
            // floor as the building gets taller.
            var localBounds = new Bounds(Vector3.zero, bakeVolumeSize);

            _data = NavMeshBuilder.BuildNavMeshData(
                settings,
                _sources,
                localBounds,
                transform.position,
                Quaternion.identity);

            if (_data == null)
            {
                Debug.LogError("[RuntimeNavMeshBaker] Bake produced no NavMesh data.");
                return;
            }

            _instance = NavMesh.AddNavMeshData(_data);
            HasBaked = _instance.valid;
        }

        public void SetBakeVolume(Vector3 center, Vector3 size)
        {
            transform.position = center;
            bakeVolumeSize = size;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position, bakeVolumeSize);
        }
    }
}
