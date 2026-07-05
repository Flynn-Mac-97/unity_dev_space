using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    // Drop on any scene GameObject that represents a `thing` authored in the
    // island JSON (a landmark, prop, sign, anything the player can talk about).
    // Holds only the id — all prose lives in the island content.
    //
    // Self-registers into a process-wide static list so the resolver can do a
    // proximity sweep without paying for FindObjectsOfType every turn. Cleared
    // automatically by Unity on scene unload because each entry holds a scene
    // reference and `OnDisable` removes it.
    [DisallowMultipleComponent]
    public class WorldThingLink : MonoBehaviour
    {
        [Tooltip("Id from the island JSON `things[].thingId`. Lower_snake_case.")]
        public string thingId;

        private static readonly System.Collections.Generic.List<WorldThingLink> s_All = new System.Collections.Generic.List<WorldThingLink>();

        public static System.Collections.Generic.IReadOnlyList<WorldThingLink> All => s_All;

        private void OnEnable()  { if (!s_All.Contains(this)) s_All.Add(this); }
        private void OnDisable() { s_All.Remove(this); }
    }

}
