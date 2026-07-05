#ifndef FLYNN_WIND_INCLUDED
#define FLYNN_WIND_INCLUDED

// Global wind uniforms — set by WindManager via Shader.SetGlobalFloat.
float _WindStrength;
float _WindSpeed;
float2 _WindDirection;

// Per-sprite properties — set via MaterialPropertyBlock by WindManager.
float _WindWeight;     // 0–1, derived from ResourceNodeConfig.weight.
float2 _WindPivot;     // Sprite world position for per-sprite phase.
float _WindShake;      // Extra shake amplitude from contact (decays to 0).

/// Displaces a vertex for wind sway + contact shake.
/// step() gives rigid anchoring: bottom vertices (y<=0) stay, top (y>0) move.
void ApplyWind(inout float3 positionOS)
{
    // Rigid: 0 at/below pivot, 1 above pivot.
    float windWeight = step(0.0, positionOS.y);

    // Ambient wind — phase from per-sprite pivot so batched sprites sway individually.
    float phase = _WindPivot.x * 3.7 + _WindPivot.y * 2.3;
    float phase2 = _WindPivot.x * 1.9 - _WindPivot.y * 1.1;

    float2 dir = normalize(_WindDirection + 0.001);
    float sway = (sin(_Time.y * _WindSpeed + phase)
                + sin(_Time.y * _WindSpeed * 1.3 + phase2) * 0.3)
                * _WindStrength * _WindWeight;

    // Contact shake — high-frequency, decays via _WindShake being lerped to 0 by script.
    float shake = sin(_Time.y * 30.0 + phase) * _WindShake * _WindWeight;

    // Horizontal only — vertical displacement looks wrong in top-down 2D.
    positionOS.x += (sway + shake) * windWeight * dir.x;
}

#endif // FLYNN_WIND_INCLUDED
