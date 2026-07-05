using System.Collections;
using UnityEngine;
using Flynn.Npc;
using Flynn.Tutorial;

namespace Flynn.Demo
{
    /// <summary>
    /// Scripted objective flow for the island-1 demo (no LLM). Other components
    /// call the On* methods via UnityEvents; this drives ObjectiveTracker chips,
    /// ScanUI messages, the transmitter-stir moment and the gate opening.
    /// Objective ids use fresh demo.* keys so stale DB/PlayerPrefs state never
    /// collides with LLM-era signals.
    /// </summary>
    public class DemoFlowDirector : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Transmitter that visibly stirs when the pod stabilises.")]
        [SerializeField] private Transform _stirTarget;
        [SerializeField] private ScanUIController _scanUi;
        [Tooltip("Blocking gate object disabled by OpenGate().")]
        [SerializeField] private GameObject _gateBarrier;

        [Header("Timing")]
        [SerializeField] private float _wakeMessageDelay = 1.5f;

        private bool _burnerDone, _podDone, _talkDone, _tradeDone, _ended;

        private void Start()
        {
            StartCoroutine(WakeRoutine());
        }

        private IEnumerator WakeRoutine()
        {
            yield return new WaitForSeconds(_wakeMessageDelay);
            Show("Systems online. Pod power critical — the burner outside has collapsed.");
            Flynn.Npc.ObjectiveTracker.UnlockFromGameplay("demo.repair_burner",
                "The burner's collapsed — needs 3 wood, 2 stone");
        }

        // ── UnityEvent entry points ───────────────────────────────────────

        public void OnBurnerRepaired()
        {
            if (_burnerDone) return;
            _burnerDone = true;
            ObjectiveTracker.CompleteFromGameplay("demo.repair_burner");
            ObjectiveTracker.UnlockFromGameplay("demo.stabilise_pod",
                "Feed the burner wood. Wake the pod.");
            Show("Burner intact. Feed it wood — the pod needs power.");
        }

        public void OnPodStabilised()
        {
            if (_podDone) return;
            _podDone = true;
            ObjectiveTracker.CompleteFromGameplay("demo.stabilise_pod");
            StartCoroutine(StirRoutine());
        }

        public void OnFirstTalk()
        {
            if (_talkDone) return;
            _talkDone = true;
            ObjectiveTracker.CompleteFromGameplay("demo.meet_stranger");
            ObjectiveTracker.UnlockFromGameplay("demo.deliver_metal",
                "Bring 3 metal scrap to the stranger");
        }

        public void OnTradeCompleted()
        {
            if (_tradeDone) return;
            _tradeDone = true;
            ObjectiveTracker.CompleteFromGameplay("demo.deliver_metal");
            ObjectiveTracker.UnlockFromGameplay("demo.cross_gate", "The way is open. Cross.");
            OpenGate();
        }

        public void OnReachedGate()
        {
            if (_ended) return;
            _ended = true;
            ObjectiveTracker.CompleteFromGameplay("demo.cross_gate");
            var end = GetComponent<DemoEndScreen>();
            if (end != null) end.Show();
        }

        public void OpenGate()
        {
            if (_gateBarrier != null) _gateBarrier.SetActive(false);
            if (Flynn.Effects.CameraShake.Instance != null)
                Flynn.Effects.CameraShake.Instance.Shake(0.08f, 0.3f);
        }

        // ── Transmitter stir ──────────────────────────────────────────────

        private IEnumerator StirRoutine()
        {
            Show("Pod stabilised. Something else just drew power... by the southern gate.");

            if (_stirTarget != null)
            {
                Vector3 baseScale = _stirTarget.localScale;
                float t = 0f;
                const float dur = 0.8f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float pulse = 1f + 0.12f * Mathf.Sin(t / dur * Mathf.PI * 3f) * (1f - t / dur);
                    _stirTarget.localScale = baseScale * pulse;
                    yield return null;
                }
                _stirTarget.localScale = baseScale;
            }

            if (Flynn.Effects.CameraShake.Instance != null)
                Flynn.Effects.CameraShake.Instance.Shake(0.06f, 0.4f);
            CodexAudio.PlayTrustUp();

            ObjectiveTracker.UnlockFromGameplay("demo.meet_stranger",
                "Something stirs by the southern gate");
        }

        private void Show(string msg)
        {
            if (_scanUi != null) _scanUi.ShowMessage(msg, 6f);
        }
    }
}
