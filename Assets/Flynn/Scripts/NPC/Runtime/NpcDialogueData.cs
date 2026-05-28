using UnityEngine;

[CreateAssetMenu(menuName = "NPC/Dialogue Data")]
public class NpcDialogueData : ScriptableObject
{
    public string npcName = "Stranger";
    [TextArea(2, 5)]
    public string[] lines = { "Hello, traveller." };
}
