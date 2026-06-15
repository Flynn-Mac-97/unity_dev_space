using UnityEngine;
using Flynn.Events;

namespace Flynn.Transmitter
{
    /// <summary>
    /// The way out of the start zone. Listens for the transmitter's venture state and
    /// enables/disables a barrier accordingly: powered = open, unpowered = locked in.
    /// Pure subscriber — holds no reference back to the station.
    /// </summary>
    public class TransmitterGate : MonoBehaviour
    {
        [Tooltip("Barrier object (collider + visual) shown when the exit is LOCKED.")]
        [SerializeField] private GameObject _barrier;

        private void OnEnable()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Subscribe<TransmitterVentureStateChanged>(OnVentureState);
        }

        private void OnDisable()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Unsubscribe<TransmitterVentureStateChanged>(OnVentureState);
        }

        private void OnVentureState(TransmitterVentureStateChanged e) => SetOpen(e.Open);

        private void SetOpen(bool open)
        {
            if (_barrier != null) _barrier.SetActive(!open);
        }
    }
}
