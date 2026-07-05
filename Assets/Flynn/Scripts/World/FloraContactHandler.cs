using System.Collections;
using UnityEngine;

namespace Flynn.World
{
    /// <summary>
    /// Attaches to flora at spawn time. Detects player contact via trigger,
    /// applies wind shake to the sprite and slows the player while inside.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class FloraContactHandler : MonoBehaviour
    {
        [Tooltip("How much shake to apply when the player is inside.")]
        [SerializeField] private float _shakeAmount = 0.15f;
        [Tooltip("How fast shake decays after player leaves.")]
        [SerializeField] private float _shakeDecay = 5f;
        [Tooltip("Player speed multiplier while inside flora.")]
        [SerializeField] private float _playerSpeedMultiplier = 0.5f;

        private SpriteRenderer _spriteRenderer;
        private MaterialPropertyBlock _mpb;
        private Rigidbody2D _playerRb;
        private int _speedHandle = -1;
        private bool _playerInside;
        private float _currentShake;

        private static readonly int WindShakeID = Shader.PropertyToID("_WindShake");

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _mpb = new MaterialPropertyBlock();
            // Ensure the collider is a trigger
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerRb = other.GetComponent<Rigidbody2D>();
            var pc = other.GetComponent<Flynn.Player.PlayerController2D>();
            if (pc != null)
                _speedHandle = pc.AddSpeedModifier(_playerSpeedMultiplier);
            _playerInside = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var pc = other.GetComponent<Flynn.Player.PlayerController2D>();
            if (pc != null && _speedHandle >= 0)
                pc.RemoveSpeedModifier(_speedHandle);
            _playerInside = false;
            _playerRb = null;
        }

        private void Update()
        {
            // Apply shake while player is inside, decay after they leave
            if (_playerInside && _playerRb != null)
            {
                // Scale shake by player speed — idle player doesn't shake flora
                float playerSpeed = _playerRb.velocity.magnitude;
                _currentShake = Mathf.Min(1f, _shakeAmount * playerSpeed);
            }
            else
            {
                _currentShake = Mathf.MoveTowards(_currentShake, 0f, _shakeDecay * Time.deltaTime);
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(WindShakeID, _currentShake);
                _spriteRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
