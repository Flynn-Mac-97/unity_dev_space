using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Actions/Log Action (Debug)", fileName = "Action_Log")]
public class LogAction : NpcAction
{
    [TextArea(1, 3)]
    public string message = "Action invoked.";

    public override void Execute(NpcInteractionContext context)
    {
        string npcName = context != null && context.npc != null ? context.npc.gameObject.name : "?";
        Debug.Log(string.Format("[NpcAction] {0} -> {1}", npcName, message));
    }
}
