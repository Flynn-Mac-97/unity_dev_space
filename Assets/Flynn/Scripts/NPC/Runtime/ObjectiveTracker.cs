using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flynn.Npc
{
    // Tracks objectives unlocked/completed by dialogue signals from the LLM.
    // Subscribes to DialogueTriggerChannel, filters on handler == "UnlockObjective"
    // or "CompleteObjective", and displays active objectives as UI chips.
    //
    // Placed on MANAGERS. The triggerChannel is resolved from SceneLlmManager.
    public class ObjectiveTracker : MonoBehaviour
    {
        public static ObjectiveTracker Instance { get; private set; }

        public enum ObjectiveState { Locked, Active, Completed }

        public class Objective
        {
            public string SignalId;
            public string Title;
            public string ThingId;
            public ObjectiveState State;
        }

        private readonly List<Objective> _objectives = new List<Objective>();
        private VisualElement _chipContainer;

        // DECISION: Subscribe in Start, not OnEnable. SceneLlmManager.Awake sets
        // Instance; OnEnable can fire before that if script order differs, causing
        // a silent null skip and objectives never being tracked.
        public static event System.Action OnObjectivesChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            SubscribeToTriggerChannel();
            LoadFiredObjectivesFromDb();
        }

        private void OnDisable()
        {
            var mgr = SceneLlmManager.Instance;
            if (mgr != null && mgr.triggerChannel != null)
                mgr.triggerChannel.OnRaised -= HandleTrigger;
        }

        private void SubscribeToTriggerChannel()
        {
            var mgr = SceneLlmManager.Instance;
            if (mgr != null && mgr.triggerChannel != null)
            {
                mgr.triggerChannel.OnRaised += HandleTrigger;
                Debug.Log("[ObjectiveTracker] Subscribed to DialogueTriggerChannel");
            }
            else
            {
                Debug.LogWarning("[ObjectiveTracker] SceneLlmManager or triggerChannel not ready at Start — objectives will not be tracked.");
            }
        }

        // DECISION: Objectives are in-memory, but fired signals persist in the DB.
        // On startup, reload any objective signals that were already fired so the
        // tracker and codex Tasks tab stay in sync across Play Mode sessions.
        private static string CompletedKey(string signalId) => $"Flynn.Objective.Completed.{signalId}";
        private static bool WasGameplayCompleted(string signalId) => PlayerPrefs.GetInt(CompletedKey(signalId), 0) == 1;
        private static void MarkGameplayCompleted(string signalId) => PlayerPrefs.SetInt(CompletedKey(signalId), 1);

        private void LoadFiredObjectivesFromDb()
        {
            var mgr = SceneLlmManager.Instance;
            if (mgr == null || mgr.MemoryDb == null || mgr.islandContent == null) return;

            var fired = mgr.MemoryDb.GetAllFiredSignals();
            foreach (var doc in fired)
            {
                var signal = mgr.islandContent.GetSignal(doc.SignalId);
                if (signal == null) continue;

                if (signal.handler == "UnlockObjective")
                {
                    if (_objectives.Exists(o => o.SignalId == doc.SignalId)) continue;
                    bool wasCompleted = WasGameplayCompleted(doc.SignalId);
                    _objectives.Add(new Objective
                    {
                        SignalId = doc.SignalId,
                        Title = ResolveTitle(signal),
                        ThingId = signal.thingId ?? "",
                        State = wasCompleted ? ObjectiveState.Completed : ObjectiveState.Active,
                    });
                    Debug.Log($"[ObjectiveTracker] Restored from DB: {doc.SignalId} (completed={wasCompleted})");
                }
                else if (signal.handler == "CompleteObjective")
                {
                    if (_objectives.Exists(o => o.SignalId == doc.SignalId)) continue;
                    _objectives.Add(new Objective
                    {
                        SignalId = doc.SignalId,
                        Title = ResolveTitle(signal),
                        ThingId = signal.thingId ?? "",
                        State = ObjectiveState.Completed,
                    });
                    Debug.Log($"[ObjectiveTracker] Restored from DB (completed): {doc.SignalId}");
                }
            }

            if (_objectives.Count > 0)
            {
                EnsureChipContainer();
                UpdateChips();
                OnObjectivesChanged?.Invoke();
            }
        }

        private void HandleTrigger(DialogueTriggerPayload payload)
        {
            if (string.IsNullOrEmpty(payload.handler)) return;

            switch (payload.handler)
            {
                case "UnlockObjective":
                    UnlockObjective(payload.triggerKey, payload.sourceNpcId);
                    break;
                case "CompleteObjective":
                    CompleteObjective(payload.triggerKey);
                    break;
            }
        }

        private void UnlockObjective(string signalId, string sourceNpcId)
        {
            // Check if already exists
            foreach (var obj in _objectives)
            {
                if (obj.SignalId == signalId)
                {
                    Debug.Log($"[ObjectiveTracker] Already tracked: {signalId}");
                    return;
                }
            }

            // Look up the signal in island content for a title
            string title = signalId;
            string thingId = "";
            var mgr = SceneLlmManager.Instance;
            if (mgr != null && mgr.islandContent != null)
            {
                var signal = mgr.islandContent.GetSignal(signalId);
                if (signal != null)
                {
                    title = ResolveTitle(signal);
                    thingId = signal.thingId ?? "";
                }
            }

            var objective = new Objective
            {
                SignalId = signalId,
                Title = title,
                ThingId = thingId,
                State = ObjectiveState.Active,
            };
            _objectives.Add(objective);

            Debug.Log($"[ObjectiveTracker] Objective unlocked: {signalId} — {title}");
            ShowObjectiveChip(objective);
            OnObjectivesChanged?.Invoke();
        }

        private void CompleteObjective(string signalId)
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                if (_objectives[i].SignalId == signalId)
                {
                    _objectives[i].State = ObjectiveState.Completed;
                    Debug.Log($"[ObjectiveTracker] Objective completed: {signalId}");
                    UpdateChips();
                    OnObjectivesChanged?.Invoke();
                    return;
                }
            }

            // Signal not found as an objective — create a completed entry anyway
            _objectives.Add(new Objective
            {
                SignalId = signalId,
                Title = signalId,
                State = ObjectiveState.Completed,
            });
            Debug.Log($"[ObjectiveTracker] Objective completed (auto-created): {signalId}");
            UpdateChips();
            OnObjectivesChanged?.Invoke();
        }

        /// <summary>
        /// Gameplay-driven unlock with an explicit title — no LLM/island content
        /// required. Mirrors UnlockObjective for scripted demo flows.
        /// </summary>
        public static void UnlockFromGameplay(string signalId, string title)
        {
            var tracker = Instance;
            if (tracker == null) return;

            foreach (var obj in tracker._objectives)
                if (obj.SignalId == signalId) return;

            var objective = new Objective
            {
                SignalId = signalId,
                Title = string.IsNullOrWhiteSpace(title) ? signalId : title,
                ThingId = "",
                State = ObjectiveState.Active,
            };
            tracker._objectives.Add(objective);
            Debug.Log($"[ObjectiveTracker] Objective unlocked (gameplay): {signalId} — {title}");
            tracker.ShowObjectiveChip(objective);
            OnObjectivesChanged?.Invoke();
        }

        public List<Objective> GetAllObjectives()
        {
            return new List<Objective>(_objectives);
        }

        private static string ResolveTitle(SignalContent signal)
        {
            if (signal == null) return "Unknown Objective";
            if (!string.IsNullOrWhiteSpace(signal.title)) return signal.title;
            if (!string.IsNullOrWhiteSpace(signal.description)) return signal.description;
            return signal.signalId;
        }

        public List<Objective> GetActiveObjectives()
        {
            var result = new List<Objective>();
            foreach (var obj in _objectives)
                if (obj.State == ObjectiveState.Active) result.Add(obj);
            return result;
        }

        /// <summary>
        /// Called by gameplay scripts when a world milestone is reached (e.g. relay
        /// activated, all solar collectors cleaned). Completes the matching objective
        /// and marks the signal as fired in the DB so the LLM doesn't re-fire it.
        /// </summary>
        public static void CompleteFromGameplay(string signalId)
        {
            var tracker = Instance;
            if (tracker == null) return;

            // Look up title from island content for auto-created entries
            string title = signalId;
            var mgr = SceneLlmManager.Instance;
            if (mgr != null && mgr.islandContent != null)
            {
                var signal = mgr.islandContent.GetSignal(signalId);
                if (signal != null) title = ResolveTitle(signal);
            }

            // Complete or auto-create
            bool found = false;
            for (int i = 0; i < tracker._objectives.Count; i++)
            {
                if (tracker._objectives[i].SignalId == signalId)
                {
                    tracker._objectives[i].State = ObjectiveState.Completed;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                tracker._objectives.Add(new Objective
                {
                    SignalId = signalId,
                    Title = title,
                    State = ObjectiveState.Completed,
                });
            }

            tracker.UpdateChips();
            OnObjectivesChanged?.Invoke();
            MarkGameplayCompleted(signalId);
            Debug.Log($"[ObjectiveTracker] Gameplay completed: {signalId}");

            // Mark in DB so LLM doesn't re-fire and LoadFiredObjectivesFromDb picks it up
            if (mgr != null && mgr.MemoryDb != null)
            {
                var npc = UnityEngine.Object.FindFirstObjectByType<NpcInteraction>();
                string npcId = npc != null ? npc.GetComponent<NpcAuthoringLink>()?.npcId : null;
                if (string.IsNullOrEmpty(npcId)) npcId = "gameplay";
                mgr.MemoryDb.MarkTriggerFired(npcId, signalId);
            }
        }

        // ── UI Chip Management ────────────────────────────────────────────────

        private void EnsureChipContainer()
        {
            if (_chipContainer != null) return;

            // Find any UIDocument in the scene to attach chips to
            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            UIDocument target = null;
            foreach (var doc in docs)
            {
                // Prefer PlayerHud or any always-visible panel
                if (doc.gameObject.name == "PlayerHud")
                {
                    target = doc;
                    break;
                }
            }
            if (target == null && docs.Length > 0) target = docs[0];
            if (target == null) return;

            var root = target.rootVisualElement;
            _chipContainer = new VisualElement { name = "objective-chips" };
            _chipContainer.AddToClassList("objective-chips");
            root.Add(_chipContainer);
            Debug.Log("[ObjectiveTracker] Chip container created");
        }

        private void ShowObjectiveChip(Objective obj)
        {
            EnsureChipContainer();
            if (_chipContainer == null) return;
            UpdateChips();
        }

        private void UpdateChips()
        {
            if (_chipContainer == null) return;
            _chipContainer.Clear();

            foreach (var obj in _objectives)
            {
                if (obj.State == ObjectiveState.Locked) continue;

                var chip = new VisualElement();
                chip.AddToClassList("objective-chip");
                if (obj.State == ObjectiveState.Completed)
                    chip.AddToClassList("completed");

                var symbol = new Label(obj.State == ObjectiveState.Completed ? "✓" : "○");
                symbol.AddToClassList("objective-chip-symbol");
                chip.Add(symbol);

                var title = new Label(obj.Title);
                title.AddToClassList("objective-chip-title");
                chip.Add(title);

                _chipContainer.Add(chip);
            }
        }
    }
}
