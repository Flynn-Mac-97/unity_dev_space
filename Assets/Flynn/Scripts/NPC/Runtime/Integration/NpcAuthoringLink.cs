using UnityEngine;


using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    // Scene-side link from a GameObject to an NPC authored in the island JSON.
    // Holds the id plus art assets that can't live in JSON (portrait sprite).
    // Replaces the old NpcDialogueAuthoringLink+NpcData pair.
    public class NpcAuthoringLink : MonoBehaviour
    {
        [Tooltip("Id from island JSON `npcs[].npcId`. e.g. 'npc.maren_wind_keeper'.")]
        public string npcId;

        [Header("Visuals")]
        [Tooltip("Portrait sprite shown in the dialogue panel. Optional.")]
        public Sprite portraitSprite;

        [Tooltip("Optional sprite renderer the dialogue system may read for in-world portrait overrides.")]
        public SpriteRenderer portraitSpriteRenderer;
    }

}
