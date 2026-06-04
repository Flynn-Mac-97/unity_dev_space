using System;
using UnityEngine;

/// <summary>
/// SO event channel raised on every successful resource hit. Carries the struck
/// <see cref="ResourceNode"/> and its remaining health fraction (0-1) so listeners
/// (the HUD world-anchored HP bar) can position and fill a bar without holding a direct
/// reference to the node. Decouples resources from UI per the Architect rules.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Resource/Hit Channel", fileName = "ResourceHitChannel")]
public class ResourceHitChannel : ScriptableObject
{
    /// <summary>(node, health01) — health01 is CurrentHealth / maxHealth after the hit.</summary>
    public event Action<ResourceNode, float> OnHit;

    public void Raise(ResourceNode node, float health01)
    {
        if (node == null) return;
        OnHit?.Invoke(node, Mathf.Clamp01(health01));
    }
}
