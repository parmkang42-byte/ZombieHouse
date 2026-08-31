using UnityEngine;
using ZombieHouse.Audio;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// One ejected casing. Plays a tink on its first bounce, then goes quiet so a pile
    /// of shells on the floor does not rattle forever.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Shell : MonoBehaviour
    {
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private int maxBounceSounds = 2;
        [SerializeField] private float bounceVolume = 0.35f;

        private Rigidbody _rigidbody;
        private float _hideAt;
        private int _soundsPlayed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>Re-arms a pooled casing at a new position.</summary>
        public void Launch(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            transform.SetPositionAndRotation(position, rotation);

            gameObject.SetActive(true);
            _rigidbody.isKinematic = false;

#if UNITY_6000_0_OR_NEWER
            _rigidbody.linearVelocity = velocity;
#else
            _rigidbody.velocity = velocity;
#endif
            _rigidbody.angularVelocity = angularVelocity;

            _soundsPlayed = 0;
            _hideAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (Time.time >= _hideAt) gameObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_soundsPlayed >= maxBounceSounds) return;

            _soundsPlayed++;
            GameAudio.PlayAt(Sfx.ShellDrop, transform.position, bounceVolume, 0.2f);
        }
    }
}
