using UnityEngine;

using Flynn.UI.Core;

namespace Flynn.Npc
{
    /// <summary>
    /// Mouse-hover + click interaction bridge for NPCs. Pairs with <see cref="NpcAuthoringLink"/>
    /// and a Collider2D. When the <see cref="MousePointer"/> raycast hovers this object and the
    /// player left-clicks, opens the <see cref="DialogueManager"/> for the linked NPC.
    /// </summary>
    [RequireComponent(typeof(NpcAuthoringLink))]
    public class NpcClickInteraction : MonoBehaviour
    {
        private NpcAuthoringLink _link;

        private void Awake()
        {
            _link = GetComponent<NpcAuthoringLink>();
        }

        private void Update()
        {
            // Dialogue is open (time is paused) — don't reopen.
            if (DialogueManager.IsDialogueOpen) return;

            var pointer = MousePointer.Instance;
            if (pointer == null) return;

            var hover = pointer.HoverObject;
            if (hover == null) return;
            if (hover != gameObject && !hover.transform.IsChildOf(transform)) return;

            if (!Input.GetMouseButtonDown(0)) return;
            if (string.IsNullOrWhiteSpace(_link.npcId)) return;

            var dm = DialogueManager.Instance;
            if (dm == null) return;

            dm.OpenAgent(_link.npcId, _link.portraitSprite, GetComponent<NpcRelationshipState>(), transform.position);
        }
    }
}
