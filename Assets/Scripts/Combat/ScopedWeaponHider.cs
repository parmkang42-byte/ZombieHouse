using UnityEngine;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Hides the first-person weapon models while looking through a telescopic sight.
    ///
    /// The view model is rendered by the same camera as the world, so dropping the field
    /// of view to scope in magnifies the gun in your hands just as much as the target —
    /// at 18 degrees the rifle swells to roughly four times its normal screen size and
    /// sits squarely in the middle of the scope. Hiding it is the standard answer: the
    /// scope picture is what you are meant to be looking at, and the weapon has no reason
    /// to be drawn behind it.
    ///
    /// The alternative is a second camera rendering the view model at a fixed field of
    /// view, which is worth doing if the weapon ever needs to stay visible while scoped.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class ScopedWeaponHider : MonoBehaviour
    {
        [SerializeField] private WeaponSwitcher switcher;

        private Renderer[] _renderers;
        private bool _hidden;
        private int _lastIndex = -1;

        private void Start()
        {
            if (switcher == null) switcher = GetComponent<WeaponSwitcher>();
            if (switcher == null) switcher = GetComponentInParent<WeaponSwitcher>();

            // Everything parented to the camera: both guns, the launcher tube and the
            // katana. Holstered weapons are inactive, so they must be included now.
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate()
        {
            if (_renderers == null) return;

            Weapon current = switcher != null ? switcher.Current : null;
            bool scoped = current != null && current.IsScoped;
            int index = switcher != null ? switcher.Index : 0;

            // Re-apply on a weapon change too: the gun that was just drawn had its
            // renderers left in whatever state they were in when it was holstered.
            if (scoped == _hidden && index == _lastIndex) return;

            _hidden = scoped;
            _lastIndex = index;

            foreach (Renderer renderer in _renderers)
                if (renderer != null) renderer.enabled = !scoped;
        }
    }
}
