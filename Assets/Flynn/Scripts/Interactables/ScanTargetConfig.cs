using UnityEngine;


using Flynn.Player;
using Flynn.UI.Core;

namespace Flynn.Interactables
{
    /// <summary>
    /// Configuration data for a ScanTarget. Defines scan duration, battery cost, and what item is revealed.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Scan/Scan Target Config", fileName = "ScanTargetConfig")]
    public class ScanTargetConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display label used in hover UI.")]
        public string displayName = "Artifact";

        [Header("Scan Parameters")]
        [Tooltip("Time in seconds required to complete a scan.")]
        [Min(0.1f)]
        public float scanDuration = 3f;

        [Tooltip("Battery drain per second while actively scanning.")]
        [Min(0f)]
        public float batteryCostPerSecond = 5f;

        [Header("Reveal")]
        [Tooltip("Item definition granted when the scan completes.")]
        public ItemDefinition revealItem;

        [Tooltip("Number of items revealed upon completion.")]
        [Min(1)]
        public int revealCount = 1;

        [Header("Info reveal")]
        [Tooltip("Lines shown when the scan completes: resource stats, a lore fragment, a hint, etc.")]
        [TextArea] public string[] infoLines;
    }

}
