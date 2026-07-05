using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Flynn.Npc.Memory;

using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
public enum LlmProvider { Local, OpenRouter }

public class SceneLlmManager : MonoBehaviour
{
    public static SceneLlmManager Instance { get; private set; }

    [Header("Shared LLM Settings")]
    [Tooltip("Which provider every NPC in this scene uses.")]
    public LlmProvider provider = LlmProvider.Local;

    [Tooltip("Used when provider = Local. All NPC dialogue agents in this scene share it.")]
    public LocalModelSettings sharedLocalModelSettings;

    [Tooltip("Used when provider = OpenRouter. Configure via Flynn → NPC → OpenRouter Settings.")]
    public RemoteModelSettings sharedRemoteModelSettings;

    [Tooltip("Optional global switch for dialogue systems that read this manager.")]
    public bool llmEnabled = true;

    [Header("Prompt + Memory")]
    [Tooltip("Designer-facing SO bundling the global system prompt, the JSON output contract, and the memory budget.")]
    public LlmPromptConfig promptConfig;

    [Header("Island Content")]
    [Tooltip("Source of NPCs, things, community knowledge, and signals for this scene. Loads one island JSON.")]
    public IslandContentHub islandContent;

    [Tooltip("Shared event bus dialogue raises onto when an NPC fires a signal from its envelope. Scene-side DialogueTriggerListener components subscribe to this same channel.")]
    public DialogueTriggerChannel triggerChannel;

    [Header("Save Context")]
    [Tooltip("Informational label for the active slot. Determines the DB file path under persistentDataPath/npc_memory/.")]
    public string saveSlotId = "slot_0";

    [Header("Semantic Memory")]
    [Tooltip("Embedding model settings (Ollama all-minilm) for semantic recall. When unset, semantic memory is disabled and dialogue falls back to chat-history only.")]
    public EmbeddingSettings embeddingSettings;

    [Tooltip("Optional. SO channel dialogue raises each turn with the recalled knowledge/memories sent to the LLM. The NPC Info HUD subscribes to surface them for designers.")]
    public RecalledKnowledgeChannel recalledKnowledgeChannel;

    [Header("Player")]
    [Tooltip("Profile describing who the player character is. Used by NPCs to write player dialogue options in the right voice.")]
    public PlayerDialogueProfile playerProfile;

    [Header("Resolver")]
    [Tooltip("Player transform used by the resolver for nearest-WorldThingLink lookups. Optional.")]
    public Transform playerTransform;

    [Tooltip("Radius (world units) for nearest-WorldThingLink proximity resolution. 0 disables proximity matching.")]
    public float proximityResolveRadius = 4f;

    private NpcContextResolver _resolver;

    public NpcContextResolver Resolver
    {
        get
        {
            if (_resolver == null || _resolver_hubRef != islandContent)
            {
                _resolver = new NpcContextResolver(islandContent);
                _resolver_hubRef = islandContent;
            }
            return _resolver;
        }
    }
    private IslandContentHub _resolver_hubRef;

    // ── Semantic memory ───────────────────────────────────────────────────────
    public NpcMemoryDatabase MemoryDb { get; private set; }
    public OllamaEmbeddingProvider Embedder { get; private set; }

    // True only when both the embedding provider and the DB are usable. Dialogue
    // checks this before attempting semantic recall/writes; when false it degrades
    // gracefully to the chat-history-only path.
    public bool SemanticMemoryReady => MemoryDb != null && MemoryDb.IsOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple SceneLlmManager instances detected. The latest instance will be used.");
        Instance = this;
        InitSemanticMemory();
    }

    private void Start()
    {
        if (!SemanticMemoryReady) return;
        StartCoroutine(SeedThenHydrate());
        StartCoroutine(BackfillMemoryEmbeddings());
    }

    // Seed authored content from JSON into the DB, then re-hydrate the hub FROM
    // the DB so the DB is the runtime source. JSON stays the git-canonical seed;
    // the hub's POCOs (and thus resolver/prompt) now reflect the DB, including any
    // DB-only edits made later in the editor. Falls back to the JSON the hub
    // already parsed in Awake if anything is missing.
    private IEnumerator SeedThenHydrate()
    {
        if (islandContent == null || islandContent.Content == null) yield break;
        string islandId = islandContent.Content.islandId;

        if (Embedder != null)
            yield return StartCoroutine(IslandDbImporter.Import(islandContent.Content, MemoryDb, Embedder));
        else
            IslandContentDb.SeedAuthored(MemoryDb, islandContent.Content);

        if (MemoryDb == null || !MemoryDb.IsOpen) yield break;

        if (!islandContent.LoadFromDatabase(MemoryDb, islandId))
            Debug.LogWarning("[SceneLlmManager] DB hydrate failed; hub keeps its JSON-parsed content.");

        _resolver = null;
    }

    // Embeds any MemoryDoc rows lacking a vector — e.g. migrated from the legacy
    // ScriptableObject store, which has no embeddings. Runs once per load; rows
    // already embedded are skipped, so it's cheap on subsequent runs.
    private IEnumerator BackfillMemoryEmbeddings()
    {
        var pending = new System.Collections.Generic.List<MemoryDoc>();
        foreach (var m in MemoryDb.Memories.FindAll())
            if (m.Embedding == null || m.Embedding.Length == 0) pending.Add(m);

        foreach (var m in pending)
        {
            if (string.IsNullOrWhiteSpace(m.Text)) continue;
            float[] vec = null;
            yield return Embedder.Embed(m.Text, (v, e) =>
            {
                if (e != null) Debug.LogWarning("[SceneLlmManager] Backfill embed failed: " + e);
                else vec = v;
            });
            if (vec == null) continue;
            m.Embedding = vec;
            MemoryDb.Memories.Update(m);
        }
        if (pending.Count > 0) Debug.Log("[SceneLlmManager] Backfilled embeddings for " + pending.Count + " memories.");
    }

    private void OnDestroy()
    {
        if (MemoryDb != null) { MemoryDb.Dispose(); MemoryDb = null; }
        if (Instance == this) Instance = null;
    }

    private void InitSemanticMemory()
    {
        try
        {
            MemoryDb = new NpcMemoryDatabase();
            string embeddingModel = embeddingSettings != null ? embeddingSettings.modelName : "none";
            int dim = embeddingSettings != null ? embeddingSettings.dimensions : 0;
            MemoryDb.Open(saveSlotId, embeddingModel, dim);
            Debug.Log("[SceneLlmManager] Memory DB open at " + MemoryDb.Path);

            if (embeddingSettings != null)
            {
                Embedder = new OllamaEmbeddingProvider(embeddingSettings);
                Debug.Log("[SceneLlmManager] Embedding provider ready: " + embeddingModel + " (" + dim + "-dim)");
            }
            else
            {
                Debug.Log("[SceneLlmManager] No EmbeddingSettings — semantic recall disabled, keyword fallback will be used.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SceneLlmManager] Failed to open memory DB: " + e.Message + " — memory disabled.");
            MemoryDb = null;
            Embedder = null;
        }
    }

    public static string ContentHash(params string[] parts)
    {
        using (var md5 = MD5.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(string.Join("|", parts));
                byte[] hash = md5.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public bool HasValidSettings()
        {
            if (!llmEnabled) return false;
            return provider == LlmProvider.Local
                ? sharedLocalModelSettings != null
                : sharedRemoteModelSettings != null;
        }

        /// Build the request config for the dialogue model (per the active provider).
        /// Returns an invalid config if the manager isn't set up; callers should check IsValid.
        public LlmRequestConfig GetActiveDialogueConfig()
        {
            if (provider == LlmProvider.Local)
            {
                var s = sharedLocalModelSettings;
                if (s == null) return default;
                return new LlmRequestConfig
                {
                    endpointUrl = s.endpointUrl,
                    modelName = s.modelName,
                    temperature = s.temperature,
                    maxTokens = s.maxTokens,
                    timeoutSeconds = s.timeoutSeconds
                };
            }

            var r = sharedRemoteModelSettings;
            if (r == null) return default;
            string key = OpenRouterApiKey.Resolve(r.apiKeyEnvVar);
            if (string.IsNullOrEmpty(key))
                Debug.LogWarning("[SceneLlmManager] OpenRouter API key not found. Set it via Flynn → NPC → OpenRouter Settings, or set env var " + r.apiKeyEnvVar + ".");
            return new LlmRequestConfig
            {
                endpointUrl = r.endpointUrl,
                modelName = r.modelName,
                temperature = r.temperature,
                maxTokens = r.maxTokens,
                timeoutSeconds = r.timeoutSeconds,
                authorizationHeader = string.IsNullOrEmpty(key) ? null : "Bearer " + key,
                httpReferer = r.httpReferer,
                xTitle = r.xTitle,
                forceJsonMode = r.forceJsonMode,
                requireJsonProvider = r.requireJsonProvider,
                proxyUrl = r.proxyUrl
            };
        }
    }
}
