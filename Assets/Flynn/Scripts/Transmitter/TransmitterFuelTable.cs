using System.Collections.Generic;
using UnityEngine;

using Flynn.Core;
using Flynn.Player;
using Flynn.UI.Core;

namespace Flynn.Transmitter
{
    /// <summary>
    /// Designer-authored map of which resources fuel a transmitter and how much power
    /// each unit restores. ScriptableObject so fuel values can be tuned without code.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Transmitter Fuel Table", fileName = "TransmitterFuelTable")]
    public class TransmitterFuelTable : ScriptableObject
    {
        [System.Serializable]
        public struct FuelEntry
        {
            public ItemDefinition Item;
            [Tooltip("Power restored per unit fed.")]
            public float PowerPerUnit;
        }

        [SerializeField] private List<FuelEntry> _fuels = new();

        public IReadOnlyList<FuelEntry> Fuels => _fuels;

        /// <summary>Power restored per unit of this item, or 0 if it isn't accepted fuel.</summary>
        public float PowerFor(ItemDefinition item)
        {
            if (item == null) return 0f;
            for (int i = 0; i < _fuels.Count; i++)
                if (_fuels[i].Item == item) return Mathf.Max(0f, _fuels[i].PowerPerUnit);
            return 0f;
        }
    }
}
