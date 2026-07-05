using System;
using UnityEngine;
using Flynn.Events;

using Flynn.Core;
using Flynn.Interactables;

namespace Flynn.Tutorial
{
    /// <summary>What gameplay event completes a tutorial beat.</summary>
    public enum TutorialTrigger
    {
        SwingWrench,       // any wrench swing
        GatherResource,    // a resource node depleted
        ScanTarget,        // a scan completed
        PowerTransmitter,  // transmitter crossed into "powered"
    }

    /// <summary>
    /// Drives the soft tutorial as a simple ordered list of beats. Each beat shows a
    /// prompt and completes when its trigger fires on the event bus; then the next beat
    /// begins. Publishes <see cref="TutorialBeatChanged"/> (HUD/world-tag shows the
    /// prompt) and <see cref="TutorialCompleted"/>.
    ///
    /// Pure event subscriber — it knows nothing about the systems it watches, only their
    /// events. One per scene, e.g. on the transmitter's tutorial NPC.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [Serializable]
        public struct Beat
        {
            [TextArea] public string Prompt;
            public TutorialTrigger Trigger;
        }

        [Tooltip("Ordered tutorial beats. Each completes when its trigger event fires.")]
        [SerializeField] private Beat[] _beats;
        [Tooltip("Begin the first beat automatically on Start.")]
        [SerializeField] private bool _autoStart = true;

        private int _index = -1;

        private TutorialTrigger? CurrentTrigger =>
            _index >= 0 && _beats != null && _index < _beats.Length
                ? _beats[_index].Trigger
                : (TutorialTrigger?)null;

        private void OnEnable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Subscribe<ToolSwingStarted>(OnSwing);
            bus.Subscribe<ResourceDepleted>(OnGather);
            bus.Subscribe<ScanCompleted>(OnScan);
            bus.Subscribe<TransmitterVentureStateChanged>(OnPower);
        }

        private void OnDisable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Unsubscribe<ToolSwingStarted>(OnSwing);
            bus.Unsubscribe<ResourceDepleted>(OnGather);
            bus.Unsubscribe<ScanCompleted>(OnScan);
            bus.Unsubscribe<TransmitterVentureStateChanged>(OnPower);
        }

        private void Start()
        {
            if (_autoStart) Advance();
        }

        /// <summary>Begin the tutorial from the first beat (e.g. called by the intro NPC).</summary>
        public void StartTutorial()
        {
            _index = -1;
            Advance();
        }

        private void OnSwing(ToolSwingStarted e) => TryComplete(TutorialTrigger.SwingWrench);
        private void OnGather(ResourceDepleted e) => TryComplete(TutorialTrigger.GatherResource);
        private void OnScan(ScanCompleted e) => TryComplete(TutorialTrigger.ScanTarget);
        private void OnPower(TransmitterVentureStateChanged e)
        {
            if (e.Open) TryComplete(TutorialTrigger.PowerTransmitter);
        }

        private void TryComplete(TutorialTrigger trigger)
        {
            if (CurrentTrigger == trigger) Advance();
        }

        private void Advance()
        {
            _index++;
            if (_beats == null || _index >= _beats.Length)
            {
                Publish(new TutorialCompleted());
                return;
            }
            Publish(new TutorialBeatChanged(_index, _beats[_index].Prompt));
        }

        private static void Publish<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }
    }
}
