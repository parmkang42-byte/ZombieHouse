using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Level
{
    /// <summary>
    /// The winch motor beside the door. Dead until you bring it the power cell, and the
    /// door will not open while it is dead — whatever else you have done.
    ///
    /// It is the third condition on the way out, and it is the one that sends you back
    /// across the level rather than forward through it: the cell is always at the far end
    /// from the motor, so fitting it means carrying something heavy through everything you
    /// have already stirred up.
    /// </summary>
    public class DoorMotor : MonoBehaviour
    {
        [SerializeField] private float reach = 2.8f;
        [SerializeField] private float spinUpSeconds = 1.4f;

        public bool Powered { get; private set; }

        private Transform _player;
        private Light _lamp;
        private Renderer _drumRenderer;
        private MaterialPropertyBlock _mpb;
        private Transform _drum;
        private float _spinUpRemaining;
        private AudioSource _hum;

        private void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _player = playerObject.transform;

            _lamp = GetComponentInChildren<Light>();
            _drum = transform.Find("Drum");
            if (_drum != null) _drumRenderer = _drum.GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            if (GameManager.Instance != null) GameManager.Instance.RegisterMotor(this);
            Paint();
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            if (_spinUpRemaining > 0f)
            {
                _spinUpRemaining -= Time.deltaTime;
                if (_drum != null) _drum.Rotate(Vector3.forward, 420f * Time.deltaTime, Space.Self);

                if (_spinUpRemaining <= 0f) FinishSpinUp();
                return;
            }

            if (Powered)
            {
                if (_drum != null) _drum.Rotate(Vector3.forward, 90f * Time.deltaTime, Space.Self);
                return;
            }

            if (_player == null) return;
            if (Vector3.Distance(_player.position, transform.position) > reach) return;

            if (PowerCell.Carried == null)
            {
                InteractPrompt.Request("NEEDS A POWER CELL");
                return;
            }

            InteractPrompt.Request("FIT THE POWER CELL");
            if (InputReader.InteractPressed) FitCell();
        }

        private void FitCell()
        {
            PowerCell cell = PowerCell.Carried;
            if (cell == null || Powered) return;

            cell.ConsumeIntoMotor();
            _spinUpRemaining = spinUpSeconds;

            GameAudio.PlayAt(Sfx.MotorStart, transform.position, 0.95f);
        }

        private void FinishSpinUp()
        {
            Powered = true;
            Paint();

            if (GameManager.Instance != null) GameManager.Instance.ReportMotorPowered(this);

            // A running motor is a landmark you can hear from down the street, and a
            // reminder that the way out is live now.
            _hum = GameAudio.AttachSource(gameObject, 0.35f);
            if (_hum != null)
            {
                _hum.clip = GameAudio.Get(Sfx.MotorHum);
                _hum.loop = true;
                _hum.spatialBlend = 1f;
                _hum.maxDistance = 34f;
                if (_hum.clip != null) _hum.Play();
            }
        }

        /// <summary>Red and dark, or green and lit. Nothing subtle about it.</summary>
        private void Paint()
        {
            Color colour = Powered ? new Color(0.25f, 0.95f, 0.45f) : new Color(0.85f, 0.15f, 0.12f);

            if (_lamp != null)
            {
                _lamp.color = colour;
                _lamp.intensity = Powered ? 2.2f : 0.5f;
                _lamp.range = Powered ? 9f : 4.5f;
            }

            if (_drumRenderer == null) return;

            _drumRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", colour * (Powered ? 2f : 0.35f));
            _drumRenderer.SetPropertyBlock(_mpb);
        }
    }
}
