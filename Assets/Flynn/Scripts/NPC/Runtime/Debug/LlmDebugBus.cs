using System;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Debug-only static bus. DialogueManager pushes per-stage records here; the
    // LlmDebugWindow controller subscribes. Static (rather than a ScriptableObject
    // channel) so the window can pick up events even if it spawned mid-run without
    // a wired reference — debug ergonomics over architectural purity.
    public static class LlmDebugBus
    {
        public enum Stage { Preprocess, Dialogue, Postprocess }

        public class StageEntry
        {
            public int turnNumber;
            public Stage stage;
            public string systemPrompt;
            // Chat history sent in alongside the system prompt — the N prior
            // user/assistant turns the model receives as context. Empty on the
            // first turn of a conversation.
            public string chatHistory;
            public int chatHistoryTurns;
            public string userPrompt;
            public string rawResponse;
            public string parsedSummary;
            public long elapsedMs;
            public string error;
        }

        public class TurnSummary
        {
            public int turnNumber;
            public string playerInput;
            public string finalReply;
            public string topic;
            public string[] events;
            public int trustDelta;
            public int affectionDelta;
            public int suspicionDelta;
            public int trustAfter;
            public int affectionAfter;
            public int suspicionAfter;
        }

        public static event Action<int, string> TurnStarted;
        public static event Action<StageEntry> StageCompleted;
        public static event Action<TurnSummary> TurnCompleted;
        public static event Action Cleared;

        public static void BeginTurn(int turnNumber, string playerInput)
            => TurnStarted?.Invoke(turnNumber, playerInput);

        public static void RecordStage(StageEntry entry)
            => StageCompleted?.Invoke(entry);

        public static void EndTurn(TurnSummary summary)
            => TurnCompleted?.Invoke(summary);

        public static void Clear()
            => Cleared?.Invoke();
    }

}
