using System;
using System.Collections.Generic;
using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Npc
{
    public class NpcInteraction : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private KeyCode interactHotkey = KeyCode.E;

        public static event Action<NpcInteraction, bool> RangeChanged;

        public string NpcId
        {
            get
            {
                var link = GetComponent<NpcAuthoringLink>();
                return link != null ? link.npcId : null;
            }
        }

        public NpcContent ResolveNpcContent()
        {
            var hub = ResolveHub();
            return hub != null ? hub.GetNpc(NpcId) : null;
        }

        private bool _menuVisible;
        private bool _inRange;

        private void Update()
        {
            if (player == null) return;

            bool dialogueOpen = DialogueManager.IsDialogueOpen;
            bool inRange = Vector3.Distance(transform.position, player.position) <= interactRange;

            if (inRange != _inRange)
            {
                _inRange = inRange;
                RangeChanged?.Invoke(this, inRange);
            }

            SetMenuVisible(inRange && !dialogueOpen);

            if (inRange && !dialogueOpen && Input.GetKeyDown(interactHotkey))
            {
                OnTalk();
            }
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuRoot == null || _menuVisible == visible) return;
            _menuVisible = visible;
            menuRoot.SetActive(visible);

            if (visible)
            {
                var builder = menuRoot.GetComponent<NpcRadialMenuBuilder>();
                if (builder != null) builder.Bind(this);
            }
        }

        public void OnTalk()
        {
            DialogueManager dialogueManager = DialogueManager.Instance;

            if (dialogueManager == null)
            {
                Debug.LogWarning("[NPC] Talk clicked but no DialogueManager was found in scene.");
                return;
            }

            var link = GetComponent<NpcAuthoringLink>();
            if (link == null || string.IsNullOrWhiteSpace(link.npcId))
            {
                Debug.LogWarning("[NPC] Talk clicked but no NpcAuthoringLink with an npcId is on this GameObject.");
                return;
            }

            dialogueManager.OpenAgent(link.npcId, link.portraitSprite, GetComponent<NpcRelationshipState>(), transform.position);
        }

        private static IslandContentHub ResolveHub()
        {
            var mgr = SceneLlmManager.Instance;
            return mgr != null ? mgr.islandContent : null;
        }
    }

}
