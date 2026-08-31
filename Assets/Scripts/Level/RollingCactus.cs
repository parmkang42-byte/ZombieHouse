using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Level
{
    /// <summary>
    /// A ball of cactus rolling down the street on the wind, forever.
    ///
    /// It exists to make the street move. A level this still reads as a diorama: the only
    /// thing that ever crosses your view is something trying to kill you, so every scrap
    /// of movement in the corner of your eye is a threat and you learn to stop looking.
    /// Give the wind something to push and the street stops being a photograph — and the
    /// first few times, one of these coming out of an alley will absolutely make you turn
    /// and put a .50 round through it.
    ///
    /// **No colliders anywhere.** It has to cross the street, the boardwalks and the
    /// alley mouths without carving the NavMesh, blocking a horse, or eating a bullet
    /// meant for something behind it. It rolls through the world rather than over it, and
    /// steers around the buildings by arithmetic instead of physics.
    /// </summary>
    public class RollingCactus : MonoBehaviour
    {
        [Header("Wind")]
        [Tooltip("Metres a second down the street. Gusts multiply this.")]
        [SerializeField] private float speed = 4.5f;
        [SerializeField] private float gustStrength = 0.55f;
        [SerializeField] private float gustPeriod = 6f;

        [Tooltip("How far it wanders across the street as it goes.")]
        [SerializeField] private float driftAmplitude = 2.6f;
        [SerializeField] private float driftPeriod = 4.5f;

        [Header("Extent")]
        [Tooltip("Set by the generator: how far along the street it may roll before it "
                 + "is blown back to the other end.")]
        [SerializeField] private float halfLength = 62f;
        [SerializeField] private float halfWidth = 7f;

        [SerializeField] private float radius = 0.55f;

        private Vector3 _origin;
        private float _phase;
        private float _direction = 1f;
        private Vector3 _tumbleAxis;

        /// <summary>Called by the generator, which knows the street's dimensions.</summary>
        public void Configure(Vector3 streetOrigin, float streetHalfLength, float streetHalfWidth,
                              float ballRadius, float windSpeed, float startPhase, float direction)
        {
            _origin = streetOrigin;
            halfLength = streetHalfLength;
            halfWidth = streetHalfWidth;
            radius = ballRadius;
            speed = windSpeed;
            _phase = startPhase;
            _direction = direction < 0f ? -1f : 1f;
        }

        private void Awake()
        {
            // A tumbling ball does not spin about a tidy axis; it wobbles as it goes.
            _tumbleAxis = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.25f, 0.25f), 1f).normalized;
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            float dt = Time.deltaTime;
            _phase += dt;

            // Gusting rather than a constant push, so a street full of these does not
            // look like a conveyor belt.
            float gust = 1f + Mathf.Sin(_phase * (Mathf.PI * 2f / gustPeriod)) * gustStrength;
            float along = speed * gust * _direction;

            Vector3 position = transform.position;
            position.z += along * dt;

            // Cross-street wander. Clamped to the street, so it never rolls into a wall.
            float acrossTarget = Mathf.Sin(_phase * (Mathf.PI * 2f / driftPeriod)) * driftAmplitude;
            position.x = Mathf.Clamp(_origin.x + acrossTarget, _origin.x - halfWidth, _origin.x + halfWidth);
            position.y = _origin.y + radius;

            // Off the end: the wind puts it back at the other end rather than deleting it,
            // so the street always has the same number of them rolling down it.
            float travelled = position.z - _origin.z;
            if (Mathf.Abs(travelled) > halfLength)
            {
                position.z = _origin.z - Mathf.Sign(travelled) * halfLength;
                _phase = Random.Range(0f, 10f);
            }

            transform.position = position;

            // Rotation follows the distance covered, so it never looks like it is
            // skidding — the same trick the walkers' stride uses.
            float degrees = (Mathf.Abs(along) * dt / Mathf.Max(0.05f, radius)) * Mathf.Rad2Deg;
            transform.Rotate(_tumbleAxis, degrees * Mathf.Sign(along), Space.World);
        }
    }
}
