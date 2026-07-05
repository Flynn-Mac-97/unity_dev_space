using System.Collections;
using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    // Demo reaction: smoothly slide the host transform by `pushOffset` over `duration`.
    // Wired to a DialogueTriggerListener UnityEvent via Push().
    public class PillarPushReaction : MonoBehaviour
    {
        [Tooltip("World-space offset applied to the transform when Push() is called.")]
        public Vector3 pushOffset = new Vector3(2f, 0f, 0f);

        [Tooltip("Slide duration in seconds. Uses unscaled time so it still animates while dialogue holds Time.timeScale at 0.")]
        [Range(0.05f, 5f)]
        public float duration = 0.6f;

        private bool _pushing;

        public void Push()
        {
            if (_pushing) return;
            StartCoroutine(Slide());
        }

        private IEnumerator Slide()
        {
            _pushing = true;
            Vector3 start = transform.position;
            Vector3 end = start + pushOffset;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                // ease-out quad — looks like a shove that decelerates
                float eased = 1f - (1f - p) * (1f - p);
                transform.position = Vector3.Lerp(start, end, eased);
                yield return null;
            }
            transform.position = end;
            _pushing = false;
        }
    }

}
