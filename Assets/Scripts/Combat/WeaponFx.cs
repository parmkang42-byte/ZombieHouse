using UnityEngine;
using ZombieHouse.Fx;
using ZombieHouse.Level;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// The gun's own effects: muzzle smoke and sparks, and a pool of ejected casings.
    /// Sits alongside <see cref="Weapon"/> and listens to its Fired event.
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public class WeaponFx : MonoBehaviour
    {
        [Header("Muzzle")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private int smokePerShot = 5;
        [SerializeField] private int sparksPerShot = 7;

        [Header("Shell ejection")]
        [SerializeField] private bool ejectShells = true;
        [SerializeField] private int shellPoolSize = 12;
        [SerializeField] private Vector3 ejectionPortLocal = new Vector3(0.055f, 0.02f, 0.04f);
        [SerializeField] private float ejectSpeed = 2.4f;
        [SerializeField] private float ejectUpSpeed = 1.8f;
        [SerializeField] private float ejectSpin = 14f;

        private Weapon _weapon;
        private ParticleSystem _smoke;
        private ParticleSystem _sparks;
        private Shell[] _shells;
        private int _nextShell;

        private void Awake()
        {
            _weapon = GetComponent<Weapon>();
            if (muzzle == null) muzzle = transform.Find("Muzzle");
            if (muzzle == null) muzzle = transform;

            // Effects live outside the weapon hierarchy: world-space particles must not
            // inherit the view model's bob and recoil, or the smoke jitters with the gun.
            var fxRoot = new GameObject("WeaponFx").transform;

            _smoke = ParticleFactory.Create("MuzzleSmoke", fxRoot, ParticleFactory.MuzzleSmoke);
            _sparks = ParticleFactory.Create("MuzzleSparks", fxRoot, ParticleFactory.MuzzleSparks);

            if (ejectShells) BuildShellPool(fxRoot);
        }

        private void OnEnable()
        {
            if (_weapon != null) _weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (_weapon != null) _weapon.Fired -= OnFired;
        }

        private void BuildShellPool(Transform parent)
        {
            _shells = new Shell[Mathf.Max(1, shellPoolSize)];

            for (int i = 0; i < _shells.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Shell_" + i;
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.014f, 0.014f, 0.032f);
                go.layer = 2;   // Ignore Raycast — casings must not stop bullets

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ProtoMaterials.AmmoBox;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.02f;

                _shells[i] = go.AddComponent<Shell>();
                go.SetActive(false);
            }
        }

        private void OnFired()
        {
            Vector3 position = muzzle.position;
            Vector3 forward = muzzle.forward;

            ParticleFactory.Burst(_smoke, position, forward, smokePerShot);
            ParticleFactory.Burst(_sparks, position, forward, sparksPerShot);

            EjectShell();
        }

        private void EjectShell()
        {
            if (_shells == null || _shells.Length == 0) return;

            Shell shell = _shells[_nextShell];
            _nextShell = (_nextShell + 1) % _shells.Length;

            Vector3 position = transform.TransformPoint(ejectionPortLocal);

            Vector3 velocity = transform.right * (ejectSpeed * Random.Range(0.8f, 1.2f))
                               + transform.up * (ejectUpSpeed * Random.Range(0.8f, 1.2f))
                               + transform.forward * Random.Range(-0.3f, 0.3f);

            Vector3 spin = Random.insideUnitSphere * ejectSpin;

            shell.Launch(position, Random.rotation, velocity, spin);
        }
    }
}
