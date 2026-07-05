using System;
using UnityEngine;



namespace Flynn.Npc
{
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
            // Try in Awake in case other Awake scripts need our trust value,
            // but also retry in Start since SceneLlmManager may not be ready yet.
            TryLoadTrust();
        }

        private void Start()
        {
            // SceneLlmManager.Awake has definitely run by now — re-init trust
            // in case Awake ran before the island content was loaded.
            if (trust == 0) TryLoadTrust();
        }

        public void ResetToDefaults()
        {
            var npc = ResolveContent();
            trust = npc != null ? npc.startingTrust : 0;
            OnChanged?.Invoke();
        }

        // DECISION: PlayerPrefs for trust persistence — simpler than a new DB
        // collection, and trust is a single int per NPC, not relational data.
        private static string TrustKey(string npcId) => $"Flynn.NPC.Trust.{npcId}";

        private void TryLoadTrust()
        {
            string id = NpcId;
            if (!string.IsNullOrEmpty(id) && PlayerPrefs.HasKey(TrustKey(id)))
            {
                trust = PlayerPrefs.GetInt(TrustKey(id));
                OnChanged?.Invoke();
            }
            else
            {
                ResetToDefaults();
            }
        }

        public void AdjustTrust(int delta)
        {
            trust = Mathf.Clamp(trust + delta, 0, 100);
            OnChanged?.Invoke();
            string id = NpcId;
            if (!string.IsNullOrEmpty(id))
                PlayerPrefs.SetInt(TrustKey(id), trust);
        }

        public NpcContent ResolveContent()
        {
            var mgr = SceneLlmManager.Instance;
            var hub = mgr != null ? mgr.islandContent : null;
            return hub != null ? hub.GetNpc(NpcId) : null;
        }
    }

}
