using System;
using System.Collections.Generic;
using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Core
{
    /// <summary>
    /// Central typed event bus for gameplay events. Systems publish; managers subscribe.
    /// Events are plain C# structs (zero-alloc publish). One per scene on MANAGERS.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public class GameEventBus : MonoBehaviour
    {
        public static GameEventBus Instance { get; private set; }

        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        [Tooltip("Log every published event type. Off for production.")]
        [SerializeField] private bool _debugLog;

        // ── Subscribe / Unsubscribe ──────────────────────────────────────────────

        /// <summary>Subscribe a handler for event type T.</summary>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _subscribers[type] = list;
            }
            list.Add(handler);
        }

        /// <summary>Unsubscribe a handler for event type T.</summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var list))
                list.Remove(handler);
        }

        // ── Publish ─────────────────────────────────────────────────────────────

        /// <summary>Publish an event to all subscribers of type T.</summary>
        public void Publish<T>(T evt) where T : struct
        {
            if (_debugLog) Debug.Log($"[GameEventBus] {typeof(T).Name}");
            if (!_subscribers.TryGetValue(typeof(T), out var list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                try { ((Action<T>)list[i]).Invoke(evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // ── Singleton ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameEventBus] Duplicate; destroying {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

}
