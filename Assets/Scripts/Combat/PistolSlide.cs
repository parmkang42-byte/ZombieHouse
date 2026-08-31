using UnityEngine;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Cycles the slide. It snaps back on each shot and returns under spring pressure,
    /// and when the magazine runs dry it locks back and stays there until you reload —
    /// which is the clearest "you are empty" signal a pistol has, and means you can read
    /// the gun's state without looking at the ammo counter.
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public class PistolSlide : MonoBehaviour
    {
        [Header("Travel")]
        [SerializeField] private float travel = 0.032f;
        [SerializeField] private float recoilSpeed = 90f;
        [SerializeField] private float returnSpeed = 26f;

        [Header("References")]
        [SerializeField] private Transform slide;

        private Weapon _weapon;
        private Vector3 _restPosition;
        private float _offset;
        private bool _cycling;

        public void Configure(Transform slideTransform)
        {
            slide = slideTransform;
        }

        private void Awake()
        {
            _weapon = GetComponent<Weapon>();
            if (slide == null) slide = transform.Find("Slide");
            if (slide != null) _restPosition = slide.localPosition;
        }

        private void OnEnable()
        {
            if (_weapon != null) _weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (_weapon != null) _weapon.Fired -= OnFired;
        }

        private void OnFired()
        {
            _cycling = true;
            _offset = travel;
        }

        private void LateUpdate()
        {
            if (slide == null || _weapon == null) return;

            // Dry and not reloading: hold the slide open on the empty magazine.
            bool lockedBack = _weapon.AmmoInMagazine <= 0 && !_weapon.IsReloading;

            float target;
            if (lockedBack)
            {
                target = travel;
                _cycling = false;
            }
            else if (_cycling)
            {
                target = 0f;
                if (_offset <= 0.0005f) _cycling = false;
            }
            else
            {
                target = 0f;
            }

            // Snapping back is violent; returning is a spring, so the speeds differ.
            float speed = _offset < target ? recoilSpeed : returnSpeed;
            _offset = Mathf.MoveTowards(_offset, target, speed * Time.deltaTime * travel * 30f);

            slide.localPosition = _restPosition - new Vector3(0f, 0f, _offset);
        }
    }
}
