using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Level
{
    /// <summary>
    /// The big battery. One per level, somewhere you have to go and get, and the door
    /// does not open without it.
    ///
    /// It is deliberately not a pickup you hoover up by walking over: you press Space to
    /// heave it onto your shoulder, and while you are carrying it you are **slower and
    /// cannot sprint**, because the whole point of an object like this is that fetching it
    /// costs you the thing you would otherwise use to stay alive. Carry it to the motor by
    /// the door and fit it there.
    ///
    /// Drop it with Space again if you would rather fight first and come back for it.
    /// </summary>
    public class PowerCell : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 2.4f;

        [Tooltip("How far the cell's charge hum carries. The only thing that helps you "
                 + "find it, and short on purpose: it rewards searching a room rather "
                 + "than standing in the corridor.")]
        [SerializeField] private float humRadius = 9f;
        [SerializeField] private float humVolume = 0.22f;

        [Tooltip("Where it rides while carried, relative to the camera.")]
        [SerializeField] private Vector3 carryOffset = new Vector3(0.42f, -0.42f, 0.62f);

        /// <summary>The cell the player is carrying, if any. Null when it is on the ground.</summary>
        public static PowerCell Carried { get; private set; }

        public bool IsCarried => Carried == this;

        private Transform _player;
        private Transform _camera;
        private PlayerController _controller;
        private Vector3 _groundPosition;
        private AudioSource _hum;

        private void Start()
        {
            _groundPosition = transform.position;
            StartHum();

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null) return;

            _player = playerObject.transform;
            _controller = playerObject.GetComponent<PlayerController>();

            Camera main = Camera.main;
            if (main != null && main.transform.IsChildOf(_player)) _camera = main.transform;
        }

        private void OnDestroy()
        {
            if (Carried == this) Carried = null;
        }

        /// <summary>
        /// A quiet electrical hum at the cell itself, audible only within a few metres.
        ///
        /// Something has to reward walking into the right room, or a randomly placed
        /// crate in an unlit two-storey house is a pixel hunt rather than a search. A
        /// short positional sound does that without telling you anything from the far end
        /// of a corridor — turn `humRadius` down to nothing if you want it pure.
        /// </summary>
        private void StartHum()
        {
            _hum = GameAudio.AttachSource(gameObject, humVolume);
            if (_hum == null) return;

            _hum.clip = GameAudio.Get(Sfx.MotorHum);
            if (_hum.clip == null) return;

            _hum.loop = true;
            _hum.spatialBlend = 1f;
            _hum.pitch = 1.7f;          // smaller than a winch motor, so it sits higher
            _hum.minDistance = 1.4f;
            _hum.maxDistance = humRadius;
            _hum.rolloffMode = AudioRolloffMode.Linear;
            _hum.Play();
        }

        private void Update()
        {
            if (!GameManager.GameplayActive || _player == null) return;

            if (IsCarried)
            {
                RideWithThePlayer();
                return;
            }

            // Spin slowly on the ground so it catches the torch and reads as a thing to
            // take rather than as scenery.
            transform.Rotate(Vector3.up, 22f * Time.deltaTime, Space.World);

            if (Vector3.Distance(_player.position, transform.position) > pickupRadius) return;
            if (Carried != null) return;   // already carrying one; nothing to say

            InteractPrompt.Request("LIFT THE POWER CELL");
            if (InputReader.InteractPressed) Lift();
        }

        private void RideWithThePlayer()
        {
            Transform anchor = _camera != null ? _camera : _player;
            transform.position = anchor.TransformPoint(carryOffset);
            transform.rotation = anchor.rotation;

            // Space puts it down again — you may well want your hands back in a hurry.
            InteractPrompt.Request("SET THE CELL DOWN");
            if (InputReader.InteractPressed) Drop();
        }

        public void Lift()
        {
            if (Carried != null) return;

            Carried = this;
            if (_controller != null) _controller.SetEncumbered(true);

            // Once it is on your shoulder the hum would be in your ear for the rest of
            // the level, so it stops. It has done its job.
            if (_hum != null) _hum.Stop();

            GameAudio.Play2D(Sfx.CellLift, 0.7f);
        }

        public void Drop()
        {
            if (Carried != this) return;

            Carried = null;
            if (_controller != null) _controller.SetEncumbered(false);

            // Put it on the floor under where it was being carried, not inside a wall.
            Vector3 at = transform.position;
            if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit hit, 6f,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                at = hit.point + Vector3.up * 0.35f;
            }
            else
            {
                at = _groundPosition;
            }

            transform.position = at;
            transform.rotation = Quaternion.identity;
            GameAudio.PlayAt(Sfx.CellLift, at, 0.5f);

            // Put down again, it goes back to humming so you can find it a second time.
            if (_hum != null && _hum.clip != null) _hum.Play();
        }

        /// <summary>Consumed by the motor. The cell is gone once it is fitted.</summary>
        public void ConsumeIntoMotor()
        {
            if (Carried == this) Carried = null;
            if (_controller != null) _controller.SetEncumbered(false);
            Destroy(gameObject);
        }
    }
}
