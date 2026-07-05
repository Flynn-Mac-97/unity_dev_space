using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Flynn.Core;
using Flynn.Events;

namespace Flynn.Npc
{
    /// <summary>
    /// Makes an NPC react to the world OUTSIDE the dialogue panel: subscribes to
    /// GameEventBus and speaks short authored barks in a world-space bubble, and
    /// raises a "!" new-topic indicator until the player next talks to them.
    /// Pools are serialized per NPC; defaults are the transmitter's fragmented voice.
    /// </summary>
    public class NpcBarkController : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Must match the NpcAuthoringLink npcId — used to clear the ! on dialogue open.")]
        [SerializeField] private string _npcId = "npc.transmitter_station";

        [Header("Bark behavior")]
        [SerializeField] private float _globalCooldown = 12f;
        [SerializeField] private float _perTriggerCooldown = 45f;
        [Tooltip("Chance to bark on chatter-prone events (resource depleted).")]
        [SerializeField, Range(0f, 1f)] private float _chatterChance = 0.3f;
        [Tooltip("Only bark about resources depleted within this range (0 = anywhere).")]
        [SerializeField] private float _resourceRange = 6f;
        [SerializeField] private Vector2 _bubbleOffset = new Vector2(0f, 1.0f);

        [Header("Bark pools (transmitter defaults)")]
        [SerializeField] private string[] _fedBarks =
        {
            "Warmth... I remember warmth.",
            "Yes. That. More of that.",
            "The signal steadies. Thank you.",
        };
        [SerializeField] private string[] _harvestBarks =
        {
            "The green gives itself back.",
            "Nothing is wasted here. Nothing was ever wasted.",
            "I hear the cutting. Careful hands.",
        };
        [SerializeField] private string[] _batteryLowBarks =
        {
            "Your cells are dimming, little one.",
            "Rest. Or feed. Do not simply fade.",
        };

        [Header("New-topic indicator")]
        [SerializeField] private bool _raiseNewTopicOnBark = true;

        private readonly Dictionary<string, float> _lastTriggerTime = new();
        private float _lastBarkTime = -999f;
        private TextMeshPro _indicator;
        private Coroutine _indicatorPulse;

        public bool HasNewTopic { get; private set; }

        private bool _subscribed;

        // GameEventBus.Instance may not exist yet in OnEnable (NPC can initialize
        // before MANAGERS) — retry in Start, which runs after every Awake.
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Subscribe<TransmitterFed>(OnFed);
            bus.Subscribe<ResourceDepleted>(OnResourceDepleted);
            bus.Subscribe<BatteryLow>(OnBatteryLow);
            bus.Subscribe<NpcDialogueOpened>(OnDialogueOpened);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Unsubscribe<TransmitterFed>(OnFed);
            bus.Unsubscribe<ResourceDepleted>(OnResourceDepleted);
            bus.Unsubscribe<BatteryLow>(OnBatteryLow);
            bus.Unsubscribe<NpcDialogueOpened>(OnDialogueOpened);
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnFed(TransmitterFed evt) => TryBark("fed", _fedBarks, 1f);

        private void OnResourceDepleted(ResourceDepleted evt)
        {
            if (_resourceRange > 0f &&
                Vector2.Distance(transform.position, evt.Position) > _resourceRange) return;
            TryBark("harvest", _harvestBarks, _chatterChance);
        }

        private void OnBatteryLow(BatteryLow evt) => TryBark("battery", _batteryLowBarks, 1f);

        private void OnDialogueOpened(NpcDialogueOpened evt)
        {
            if (evt.NpcId == _npcId) ClearNewTopic();
        }

        // ── Bark core ─────────────────────────────────────────────────────

        private void TryBark(string triggerKey, string[] pool, float chance)
        {
            if (pool == null || pool.Length == 0) return;
            if (DialogueManager.IsDialogueOpen) return;                    // never bark over the panel
            if (Time.time - _lastBarkTime < _globalCooldown) return;
            if (_lastTriggerTime.TryGetValue(triggerKey, out float last) &&
                Time.time - last < _perTriggerCooldown) return;
            if (chance < 1f && Random.value > chance) return;

            _lastBarkTime = Time.time;
            _lastTriggerTime[triggerKey] = Time.time;

            string line = pool[Random.Range(0, pool.Length)];
            Vector3 pos = transform.position + (Vector3)_bubbleOffset;
            BarkBubble.Spawn(pos, line, SortOrder() + 60);

            if (_raiseNewTopicOnBark) SetNewTopic();
        }

        private int SortOrder()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.sortingOrder + 50 : 5200;
        }

        // ── "!" indicator ─────────────────────────────────────────────────

        private void SetNewTopic()
        {
            HasNewTopic = true;
            if (_indicator == null)
            {
                var go = new GameObject("_newTopic");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = (Vector3)_bubbleOffset + new Vector3(0.35f, 0.25f, 0f);
                _indicator = go.AddComponent<TextMeshPro>();
                _indicator.text = "!";
                _indicator.fontSize = 3f;
                _indicator.color = Color.white;
                _indicator.alignment = TextAlignmentOptions.Center;
                _indicator.rectTransform.sizeDelta = new Vector2(0.4f, 0.4f);
                _indicator.sortingOrder = SortOrder() + 61;
            }
            _indicator.gameObject.SetActive(true);
            if (_indicatorPulse != null) StopCoroutine(_indicatorPulse);
            _indicatorPulse = StartCoroutine(IndicatorPulse());
        }

        private void ClearNewTopic()
        {
            HasNewTopic = false;
            if (_indicatorPulse != null) { StopCoroutine(_indicatorPulse); _indicatorPulse = null; }
            if (_indicator != null) _indicator.gameObject.SetActive(false);
        }

        private IEnumerator IndicatorPulse()
        {
            Transform t = _indicator.transform;
            while (true)
            {
                float s = 1f + 0.12f * Mathf.Sin(Time.time * 3.5f);
                t.localScale = Vector3.one * s;
                yield return null;
            }
        }
    }
}
