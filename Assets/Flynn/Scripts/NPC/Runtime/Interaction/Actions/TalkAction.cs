using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Actions/Talk Action", fileName = "Action_Talk")]
public class TalkAction : NpcAction
{
    private void Reset()
    {
        actionLabel = "Talk";
        hotkeyHint = "E";
    }

    public override void Execute(NpcInteractionContext context)
    {
        if (context == null || context.npc == null) return;
        context.npc.OnTalk();
    }
}
