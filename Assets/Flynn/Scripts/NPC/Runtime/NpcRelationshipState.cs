using System;
using UnityEngine;

public class NpcRelationshipState : MonoBehaviour
{
    [Tooltip("Optional override. When empty, falls back to the sibling NpcAuthoringLink.npcId.")]
    [SerializeField] private string npcIdOverride;

    [Header("Live values (0-100)")]
    [Range(0, 100)] public int trust;

    public event Action OnChanged;

    public string NpcId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(npcIdOverride)) return npcIdOverride;
            var link = GetComponent<NpcAuthoringLink>();
            return link != null ? link.npcId : null;
        }
    }

    private void Awake()
    {
        ResetToDefaults();
    }

    public void ResetToDefaults()
    {
        var npc = ResolveContent();
        trust = npc != null ? npc.startingTrust : 0;
        OnChanged?.Invoke();
    }

    public void AdjustTrust(int delta) { trust = Mathf.Clamp(trust + delta, 0, 100); OnChanged?.Invoke(); }

    public NpcContent ResolveContent()
    {
        var mgr = SceneLlmManager.Instance != null ? SceneLlmManager.Instance : FindObjectOfType<SceneLlmManager>();
        var hub = mgr != null ? mgr.islandContent : null;
        return hub != null ? hub.GetNpc(NpcId) : null;
    }
}
