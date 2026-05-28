using System;
using UnityEngine;

public class NpcRelationshipState : MonoBehaviour
{
    [SerializeField] private NpcDialogueAgentConfig agentConfig;

    [Header("Live values (0-100)")]
    [Range(0, 100)] public int trust;
    [Range(0, 100)] public int affection;
    [Range(0, 100)] public int suspicion;

    public NpcRelationshipDefaults.PlayerStatusTag playerStatus = NpcRelationshipDefaults.PlayerStatusTag.Unknown;

    public event Action OnChanged;

    public NpcDialogueAgentConfig AgentConfig => agentConfig;

    public void Bind(NpcDialogueAgentConfig config)
    {
        agentConfig = config;
        ResetToDefaults();
    }

    private void Awake()
    {
        if (agentConfig == null)
        {
            var link = GetComponent<NpcDialogueAuthoringLink>();
            if (link != null) agentConfig = link.agentConfig;
        }
        ResetToDefaults();
    }

    public void ResetToDefaults()
    {
        if (agentConfig == null || agentConfig.relationship == null) return;
        var d = agentConfig.relationship;
        trust = d.startingTrust;
        affection = d.startingAffection;
        suspicion = d.startingSuspicion;
        playerStatus = d.initialPlayerStatus;
        OnChanged?.Invoke();
    }

    public void AdjustTrust(int delta) { trust = Mathf.Clamp(trust + delta, 0, 100); OnChanged?.Invoke(); }
    public void AdjustAffection(int delta) { affection = Mathf.Clamp(affection + delta, 0, 100); OnChanged?.Invoke(); }
    public void AdjustSuspicion(int delta) { suspicion = Mathf.Clamp(suspicion + delta, 0, 100); OnChanged?.Invoke(); }
}
