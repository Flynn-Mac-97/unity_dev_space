using System.Collections;
using UnityEngine;



namespace Flynn.Npc
{
    public class DialogueTriggerSealEffect : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Renderer sealRenderer;
        [SerializeField] private Transform sealTransform;
        [SerializeField] private Light burstLight;

        [Header("Animation")]
        [SerializeField] private float duration = 1.4f;
        [SerializeField] private Vector3 sinkOffset = new Vector3(0f, -3.2f, 0f);

        [Header("Flash colors")]
        [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color endColor = new Color(0.1f, 0.12f, 0.18f, 1f);
        [SerializeField] private Color emissionFlash = new Color(2f, 2f, 2f, 1f);

        [Header("Light burst")]
        [SerializeField] private float burstIntensityPeak = 12f;
        [SerializeField] private float burstRangePeak = 8f;

        private bool _played;
        private MaterialPropertyBlock _block;
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_Color = Shader.PropertyToID("_Color");
        private static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");

        public void Play()
        {
            if (_played) return;
            _played = true;
            if (sealTransform == null) sealTransform = transform;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Vector3 startPos = sealTransform.position;
            Vector3 endPos = startPos + sinkOffset;
            Color startColor = flashColor;
            float startLightIntensity = burstLight != null ? burstLight.intensity : 0f;
            float startLightRange = burstLight != null ? burstLight.range : 0f;

            if (_block == null) _block = new MaterialPropertyBlock();
            if (sealRenderer != null) sealRenderer.GetPropertyBlock(_block);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);

                sealTransform.position = Vector3.Lerp(startPos, endPos, EaseOutQuad(p));

                if (sealRenderer != null)
                {
                    Color col = Color.Lerp(startColor, endColor, p);
                    _block.SetColor(s_BaseColor, col);
                    _block.SetColor(s_Color, col);
                    Color emit = Color.Lerp(emissionFlash, Color.black, p);
                    _block.SetColor(s_EmissionColor, emit);
                    sealRenderer.SetPropertyBlock(_block);
                }

                if (burstLight != null)
                {
                    float burst = 4f * p * (1f - p);
                    burstLight.intensity = Mathf.Lerp(startLightIntensity, burstIntensityPeak, burst);
                    burstLight.range = Mathf.Lerp(startLightRange, burstRangePeak, burst);
                }

                yield return null;
            }

            if (burstLight != null)
            {
                burstLight.intensity = 0f;
                burstLight.enabled = false;
            }

            gameObject.SetActive(false);
        }

        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    }

}
