using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Apply the Custom/WaterFill material to the SpriteShapeRenderer on this GameObject.
/// All animation (scroll + wave) runs entirely in the shader via _Time — no per-frame
/// C# work required.  Expose this component to tweak scroll/wave values in the Inspector
/// at edit-time via MaterialPropertyBlock if needed, or just set them on the material asset.
/// </summary>
[RequireComponent(typeof(SpriteShapeRenderer))]
public class WaterAnimator : MonoBehaviour
{
    [Header("Shader Properties (mirrors Custom/WaterFill)")]
    [SerializeField] private float _scrollX       = 0.05f;
    [SerializeField] private float _scrollY       = 0.02f;
    [SerializeField] private float _waveAmplitude = 0.03f;
    [SerializeField] private float _waveFrequency = 1.5f;

    private static readonly int ScrollXId       = Shader.PropertyToID("_ScrollX");
    private static readonly int ScrollYId       = Shader.PropertyToID("_ScrollY");
    private static readonly int WaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");

    private SpriteShapeRenderer _renderer;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _renderer = GetComponent<SpriteShapeRenderer>();
        _mpb = new MaterialPropertyBlock();
        ApplyProperties();
    }

#if UNITY_EDITOR
    private void OnValidate() => ApplyProperties();
#endif

    private void ApplyProperties()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteShapeRenderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ScrollXId,       _scrollX);
        _mpb.SetFloat(ScrollYId,       _scrollY);
        _mpb.SetFloat(WaveAmplitudeId, _waveAmplitude);
        _mpb.SetFloat(WaveFrequencyId, _waveFrequency);
        _renderer.SetPropertyBlock(_mpb);
    }
}

