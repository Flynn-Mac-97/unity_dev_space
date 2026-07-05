using System;
using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Common
{
    /// <summary>
    /// Shared runtime integer backed by a ScriptableObject (Architect variable pattern).
    /// The serialized value is only the starting amount; the live value is non-serialized
    /// and resets on every play session / domain reload so the asset never bleeds state
    /// between runs. Listen to <see cref="OnChanged"/> to drive UI.
    ///
    /// Used for out-of-inventory currencies such as Echo Shards.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Variables/Int Variable", fileName = "IntVariable")]
    public class IntVariable : ScriptableObject
    {
        [Tooltip("Value the runtime amount is reset to when play begins.")]
        [SerializeField] private int _initialValue;

        [NonSerialized] private int _value;
        [NonSerialized] private bool _initialised;

        /// <summary>Raised whenever the value changes. Argument is the new value.</summary>
        public event Action<int> OnChanged;

        public int Value
        {
            get { EnsureInit(); return _value; }
            set
            {
                EnsureInit();
                if (_value == value) return;
                _value = value;
                OnChanged?.Invoke(_value);
            }
        }

        /// <summary>Add (or subtract) an amount and notify listeners.</summary>
        public void Add(int amount) => Value += amount;

        private void EnsureInit()
        {
            if (_initialised) return;
            _value = _initialValue;
            _initialised = true;
        }

        // Force a re-init on the next access after entering play / reloading the domain.
        private void OnEnable() => _initialised = false;
    }

}
