using UnityEngine;
using UnityEngine.Events;
using Flynn.Common;

namespace Flynn.Demo
{
    /// <summary>
    /// Fires once when the player walks into the end zone (simple distance
    /// poll against the PlayerAnchor — no collider/layer coupling).
    /// </summary>
    public class DemoEndTrigger : MonoBehaviour
    {
        [SerializeField] private PlayerAnchor _playerAnchor;
        [SerializeField] private float _radius = 0.9f;

        public UnityEvent onEntered;

        private bool _fired;

        private void Update()
        {
            if (_fired) return;
            if (_playerAnchor == null || !_playerAnchor.HasPlayer) return;
            if (Vector2.Distance(_playerAnchor.Current.position, transform.position) > _radius) return;

            _fired = true;
            onEntered?.Invoke();
        }
    }
}
