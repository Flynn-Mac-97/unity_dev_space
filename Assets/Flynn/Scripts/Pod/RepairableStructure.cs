using System;
using UnityEngine;
using UnityEngine.Events;
using Flynn.Npc;
using Flynn.Player;
using Flynn.Tutorial;

namespace Flynn.Pod
{
    /// <summary>
    /// Generic staged repairable: each stage costs resources from the player's
    /// inventory and swaps the visual (broken-sparking → patched → running).
    /// Wire an Interactable's OnInteract to <see cref="TryAdvanceStage"/>.
    /// Used by the pod burner now; bridges/towers later.
    /// </summary>
    public class RepairableStructure : MonoBehaviour
    {
        [Serializable]
        public class RepairCost
        {
            public ItemDefinition item;
            public int count = 1;
        }

        [Serializable]
        public class RepairStage
        {
            [Tooltip("Resources consumed to advance INTO this stage.")]
            public RepairCost[] costs;
            [Tooltip("Sprite shown once this stage is reached (optional).")]
            public Sprite sprite;
        }

        [Header("Stages")]
        [Tooltip("Repair progression. Element N is reached after paying its costs.")]
        [SerializeField] private RepairStage[] _stages;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _renderer;

        [Header("Feedback")]
        [Tooltip("Toast shown on each stage advance (optional; uses ScanUI message).")]
        [SerializeField] private string _stageToast = "Repaired a section.";
        [SerializeField] private string _finalToast = "Fully repaired!";
        [Tooltip("Bark height offset for the shortfall line.")]
        [SerializeField] private float _barkYOffset = 0.6f;

        [Header("Events")]
        public UnityEvent onStageAdvanced;
        public UnityEvent onFullyRepaired;

        private int _stageIndex = -1; // -1 = broken, stages.Length-1 = done

        public bool IsFullyRepaired => _stages == null || _stageIndex >= _stages.Length - 1;

        /// <summary>Called by Interactable.OnInteract. Pays the next stage's costs if affordable.</summary>
        public void TryAdvanceStage()
        {
            if (IsFullyRepaired) return;

            var stage = _stages[_stageIndex + 1];
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            // Pre-count before consuming: TryConsume takes partial amounts,
            // and a failed repair must not eat resources.
            foreach (var cost in stage.costs)
            {
                if (cost == null || cost.item == null) continue;
                if (CountOf(inv, cost.item) < cost.count)
                {
                    BarkBubble.Spawn(transform.position + Vector3.up * _barkYOffset,
                        Shortfall(inv, stage), 10060);
                    return;
                }
            }

            foreach (var cost in stage.costs)
            {
                if (cost == null || cost.item == null) continue;
                inv.TryConsume(cost.item, cost.count);
            }

            _stageIndex++;
            if (_renderer != null && stage.sprite != null) _renderer.sprite = stage.sprite;

            CodexAudio.PlayCodexChime();
            var scanUi = FindObjectOfType<ScanUIController>();
            if (scanUi != null)
                scanUi.ShowMessage(IsFullyRepaired ? _finalToast : _stageToast, 4f);

            onStageAdvanced?.Invoke();
            if (IsFullyRepaired) onFullyRepaired?.Invoke();
        }

        private static int CountOf(PlayerInventory inv, ItemDefinition item)
        {
            int total = 0;
            for (int i = 0; i < inv.SlotCount; i++)
            {
                var slot = inv.GetSlot(i);
                if (slot.item == item) total += slot.count;
            }
            return total;
        }

        private string Shortfall(PlayerInventory inv, RepairStage stage)
        {
            foreach (var cost in stage.costs)
            {
                if (cost == null || cost.item == null) continue;
                int have = CountOf(inv, cost.item);
                if (have < cost.count)
                    return $"Needs {cost.count - have} more {cost.item.displayName}.";
            }
            return "Missing materials.";
        }
    }
}
