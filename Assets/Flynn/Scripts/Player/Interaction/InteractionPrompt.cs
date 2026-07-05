using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Player.Interaction
{
    /// <summary>
    /// A world-anchored interaction prompt: which key, what action, an optional target name, and the
    /// transform to float the prompt over. Rendered generically by the HUD — neither the HUD nor the
    /// aimer know concrete interactable types. Anything interactable provides one of these by
    /// implementing <see cref="IInteractionPromptProvider"/> (E Pick Up, E Inspect, E Talk, Q Grapple…).
    /// </summary>
    public readonly struct InteractionPrompt
    {
        public readonly string Key;     // "E", "Q", …
        public readonly string Verb;    // "Pick Up", "Inspect", "Talk", "Grapple"
        public readonly string Label;   // optional target name (e.g. item / NPC name)
        public readonly Transform Anchor;

        public InteractionPrompt(string key, string verb, Transform anchor, string label = null)
        {
            Key = key;
            Verb = verb;
            Anchor = anchor;
            Label = label;
        }

        public bool IsValid => Anchor != null && !string.IsNullOrEmpty(Verb);

        /// <summary>Display text, e.g. "[E]  Pick Up Wrench" or "[Q]  Grapple".</summary>
        public string Display =>
            string.IsNullOrEmpty(Label) ? $"[{Key}]  {Verb}" : $"[{Key}]  {Verb} {Label}";
    }

    /// <summary>
    /// Implemented by any world object the player can target for a prompted interaction. The aimer
    /// surfaces the hovered provider; the HUD renders its prompt. Add new interactables by implementing
    /// this — no changes to the aimer or HUD required.
    /// </summary>
    public interface IInteractionPromptProvider
    {
        /// <summary>Return true and a valid prompt when an interaction is currently offered.</summary>
        bool TryGetPrompt(out InteractionPrompt prompt);
    }

}
