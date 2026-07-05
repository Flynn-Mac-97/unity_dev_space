using System;
using System.Collections.Generic;
using UnityEngine;

using Flynn.Core;
using Flynn.Npc;
using Flynn.UI.Core;

namespace Flynn.Npc.Memory
{
    // SO event channel carrying the semantic-recall result of the most recent
    // dialogue turn, so designer-facing UI (the NPC Info HUD) can show which
    // authored knowledge / lived memories were actually sent to the LLM for a
    // given player input. DialogueManager raises it each turn; subscribers pull
    // Last / LastNpcId on demand and refresh on OnRaised.
    //
    // Holds a copy of the recall payload (not NpcMemoryDatabase.RecalledItem
    // directly) so the channel carries no DB-connection lifetime concerns.
    [CreateAssetMenu(menuName = "Flynn/NPC/Recalled Knowledge Channel", fileName = "RecalledKnowledge_Channel")]
    public class RecalledKnowledgeChannel : ScriptableObject
    {
        public struct RecalledEntry
        {
            public string Text;
            public string Subject;  // memory subject tag, or "knowledge"
            public string Source;   // dialogue | authored | gossip | knowledge
            public float Score;
        }

        private readonly List<RecalledEntry> _last = new List<RecalledEntry>();

        public string LastNpcId { get; private set; }
        public IReadOnlyList<RecalledEntry> Last => _last;

        // Raised after each turn's recall completes (including empty/null results).
        public event Action OnRaised;

        // Reset runtime state on enable so stale recall doesn't survive a domain
        // reload (Architect rule: SOs hold no live runtime state across sessions).
        private void OnEnable()
        {
            _last.Clear();
            LastNpcId = null;
        }

        public void Raise(string npcId, List<NpcMemoryDatabase.RecalledItem> items)
        {
            LastNpcId = npcId;
            _last.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    _last.Add(new RecalledEntry
                    {
                        Text = it.Text,
                        Subject = it.Subject,
                        Source = it.Source,
                        Score = it.Score
                    });
                }
            }
            OnRaised?.Invoke();
        }

        public void Clear()
        {
            _last.Clear();
            LastNpcId = null;
            OnRaised?.Invoke();
        }
    }
}
