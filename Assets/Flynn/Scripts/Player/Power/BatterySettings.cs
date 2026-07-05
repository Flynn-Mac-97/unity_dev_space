using UnityEngine;


using Flynn.Events;
using Flynn.UI.Core;

namespace Flynn.Player
{
    /// <summary>
    /// Configurable battery cost model. Designers can toggle passive drain, set per-action
    /// costs, and enable infinite battery debug mode for sandbox testing without the
    /// battery constantly interfering.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Player/Battery Settings", fileName = "BatterySettings")]
    public class BatterySettings : ScriptableObject
    {
        [Header("Passive Drain")]
        [Tooltip("Whether the battery drains passively over time.")]
        public bool enablePassiveDrain = true;
        [Tooltip("Battery points drained per second while passive drain is enabled.")]
        public float passiveDrainPerSecond = 0.5f;

        [Header("Action Costs")]
        [Tooltip("Whether actions drain the battery.")]
        public bool enableActionDrain = true;

        [Tooltip("Battery cost per wrench swing.")]
        public float swingCost = 2f;
        [Tooltip("Battery cost per wrench throw.")]
        public float throwCost = 5f;
        [Tooltip("Battery cost to start a grapple (flat).")]
        public float grappleCost = 8f;
        [Tooltip("Battery cost per second while actively pulling with the rope.")]
        public float pullCostPerSecond = 3f;
        [Tooltip("Battery cost per second while scanning (future).")]
        public float scanCostPerSecond = 5f;
        [Tooltip("Battery cost per second while restoring (future).")]
        public float restoreCostPerSecond = 5f;

        [Header("Thresholds")]
        [Tooltip("Battery level (0-100) below which the BatteryLow event fires.")]
        public int lowBatteryThreshold = 20;

        [Header("Debug")]
        [Tooltip("When true, CanSpend always returns true and Spend is a no-op. For sandbox testing.")]
        public bool infiniteBatteryDebug = false;
    }

}
