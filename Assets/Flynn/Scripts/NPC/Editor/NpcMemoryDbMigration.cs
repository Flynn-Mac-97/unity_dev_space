using System;
using UnityEditor;
using UnityEngine;
using Flynn.Npc.Memory;

// Editor tool: copy a legacy NpcMemoryStore ScriptableObject into the LiteDB
// semantic-memory database. Facts and chat turns are inserted WITHOUT embeddings;
// SceneLlmManager.BackfillMemoryEmbeddings embeds them on next Play. Idempotent
// per fact via the DB's cosine dedup (after backfill) — but to keep a clean
// migration, run it once against an empty/cleared DB.
public static class NpcMemoryDbMigration
{
    [MenuItem("Flynn/NPC/Migrate Memory Store → DB")]
    public static void Migrate()
    {
        var manager = UnityEngine.Object.FindObjectOfType<SceneLlmManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Migrate Memory", "No SceneLlmManager found in the open scene. Open the dialogue scene first.", "OK");
            return;
        }
        if (manager.memoryStore == null)
        {
            EditorUtility.DisplayDialog("Migrate Memory", "SceneLlmManager has no NpcMemoryStore assigned (nothing to migrate).", "OK");
            return;
        }

        string model = manager.embeddingSettings != null ? manager.embeddingSettings.modelName : "all-minilm";
        int dim = manager.embeddingSettings != null ? manager.embeddingSettings.dimensions : 384;

        var db = new NpcMemoryDatabase();
        int npcCount = 0, factCount = 0, turnCount = 0;
        try
        {
            db.Open(manager.saveSlotId, model, dim);

            foreach (var entry in manager.memoryStore.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.npcId)) continue;
                npcCount++;

                if (entry.memoryFacts != null)
                    foreach (var raw in entry.memoryFacts)
                    {
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        SplitFact(raw, out string subject, out string text);
                        db.Memories.Insert(new MemoryDoc
                        {
                            NpcId = entry.npcId,
                            Subject = subject,
                            Text = text,
                            Importance = 1f,
                            CreatedUtc = DateTime.UtcNow,
                            Source = MemorySource.Dialogue,
                            Embedding = null, // backfilled at runtime
                        });
                        factCount++;
                    }

                if (entry.recentTurns != null)
                {
                    int idx = db.NextTurnIndex(entry.npcId);
                    foreach (var line in entry.recentTurns)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        SplitTurn(line, out string speaker, out string content);
                        if (string.IsNullOrWhiteSpace(content)) continue;
                        db.InsertChatTurn(new ChatTurnDoc
                        {
                            NpcId = entry.npcId,
                            Speaker = speaker,
                            Content = content,
                            TurnIndex = idx++,
                        });
                        turnCount++;
                    }
                }
            }
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Migrate Memory", "Migration failed: " + e.Message, "OK");
            return;
        }
        finally
        {
            db.Dispose();
        }

        EditorUtility.DisplayDialog("Migrate Memory",
            string.Format("Migrated {0} NPCs: {1} facts, {2} chat turns into slot '{3}'.\nEmbeddings backfill on next Play.",
                npcCount, factCount, turnCount, manager.saveSlotId),
            "OK");
    }

    // "[player] fact text" -> subject="player", text="fact text". Untagged -> "world".
    private static void SplitFact(string raw, out string subject, out string text)
    {
        string s = raw.Trim();
        subject = "world";
        text = s;
        if (s.StartsWith("[", StringComparison.Ordinal))
        {
            int end = s.IndexOf(']');
            if (end > 1)
            {
                subject = s.Substring(1, end - 1).Trim().ToLowerInvariant();
                text = s.Substring(end + 1).Trim();
            }
        }
    }

    // "Speaker: content" -> speaker, content.
    private static void SplitTurn(string line, out string speaker, out string content)
    {
        speaker = "Unknown";
        content = line.Trim();
        int sep = line.IndexOf(": ", StringComparison.Ordinal);
        if (sep > 0)
        {
            speaker = line.Substring(0, sep).Trim();
            content = line.Substring(sep + 2).Trim();
        }
    }
}
