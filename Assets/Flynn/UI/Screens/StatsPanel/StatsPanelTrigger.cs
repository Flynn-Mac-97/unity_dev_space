using UnityEngine;
using Flynn.Npc;
using Flynn.Transmitter;

namespace Flynn.UI.Screens.StatsPanel
{
    /// <summary>
    /// Bridge between Flynn's NpcInteraction.RangeChanged and David's StatsPanelController.
    /// When the player enters range of an NPC that also has a TransmitterStation component,
    /// the StatsPanel is shown; when they leave, it is hidden.
    /// Lives in Flynn.Runtime so it can reference both Flynn types and David.Runtime types.
    /// </summary>
    [RequireComponent(typeof(StatsPanelController))]
    public class StatsPanelTrigger : MonoBehaviour
    {
        private StatsPanelController _statsPanel;

        private void Awake()
        {
            _statsPanel = GetComponent<StatsPanelController>();
        }

        private void OnEnable()
        {
            NpcInteraction.RangeChanged += HandleRangeChanged;
        }

        private void OnDisable()
        {
            NpcInteraction.RangeChanged -= HandleRangeChanged;
        }

        private void HandleRangeChanged(NpcInteraction npc, bool inRange)
        {
            if (npc.GetComponent("TransmitterStation") == null) return;
            if (inRange) _statsPanel.Open();
            else _statsPanel.Close();
        }
    }
}
