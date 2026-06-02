using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcInteraction : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private float interactRange = 2.5f;

    [Header("Actions")]
    [Tooltip("Actions surfaced in the radial menu. The first available action is bound to the hotkey.")]
    [SerializeField] private List<NpcAction> actions = new List<NpcAction>();

    [SerializeField] private KeyCode interactHotkey = KeyCode.E;

    public static event Action<NpcInteraction, bool> RangeChanged;

    public IReadOnlyList<NpcAction> Actions => actions;

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

        bool dialogueOpen = Time.timeScale == 0f;
        bool inRange = Vector3.Distance(transform.position, player.position) <= interactRange;

        if (inRange != _inRange)
        {
            _inRange = inRange;
            RangeChanged?.Invoke(this, inRange);
        }

        SetMenuVisible(inRange && !dialogueOpen);

        if (inRange && !dialogueOpen && Input.GetKeyDown(interactHotkey))
        {
            InvokeDefaultAction();
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
            if (builder != null) builder.Bind(BuildContext(), actions);
        }
    }

    public NpcInteractionContext BuildContext()
    {
        return new NpcInteractionContext
        {
            npc = this,
            npcId = NpcId,
            npcContent = ResolveNpcContent(),
            player = player,
        };
    }

    public void InvokeDefaultAction()
    {
        var ctx = BuildContext();
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            if (a == null) continue;
            if (!a.IsAvailable(ctx)) continue;
            a.Execute(ctx);
            return;
        }
        OnTalk();
    }

    public void OnTalk()
    {
        DialogueManager dialogueManager = DialogueManager.Instance != null
            ? DialogueManager.Instance
            : FindObjectOfType<DialogueManager>();

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

        dialogueManager.OpenAgent(link.npcId, link.portraitSprite, GetComponent<NpcRelationshipState>());
    }

    private static IslandContentHub ResolveHub()
    {
        var mgr = SceneLlmManager.Instance != null ? SceneLlmManager.Instance : FindObjectOfType<SceneLlmManager>();
        return mgr != null ? mgr.islandContent : null;
    }
}
