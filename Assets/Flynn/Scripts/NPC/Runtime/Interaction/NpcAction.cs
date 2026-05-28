using UnityEngine;

public abstract class NpcAction : ScriptableObject
{
    [Header("Display")]
    public string actionLabel = "Action";

    [Tooltip("Optional letter to surface as a hotkey hint in UI (e.g. 'E'). Not used for input binding.")]
    public string hotkeyHint = "";

    public virtual bool IsAvailable(NpcInteractionContext context) => true;

    public abstract void Execute(NpcInteractionContext context);
}
