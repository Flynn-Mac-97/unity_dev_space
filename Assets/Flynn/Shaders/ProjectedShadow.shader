Shader "Flynn/ProjectedShadow"
{
    // Flat ground-cast silhouette shadow for billboarded 2.5D sprites.
    // The shadow quad is laid flat on the XZ ground by ShadowManager and given
    // the target's live sprite texture; this shader stamps it as a uniform dark
    // silhouette (ignoring the sprite's RGB).
    //
    // Projection model: a camera-facing sprite cast onto the ground by a
    // directional sun is an affine map — rotate to the sun's ground azimuth,
    // stretch by elevation, AND shear (lateral offsets stay along camera-right
    // while height projects along the azimuth). ShadowManager feeds the shear
    // terms per renderer; the vertex stage applies them so the base stays
    // pinned at the contact point and the silhouette leans away from the sun.
    // The fragment stage grows a penumbra blur and alpha falloff toward the
    // cast tip and darkens the contact end so objects feel grounded.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.16, 0.21, 0.32, 0.55)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _TipFade ("Tip Fade (toward head)", Range(0, 1)) = 0.35
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
        _PenumbraScale ("Penumbra Scale (UV blur at tip)", Range(0, 0.05)) = 0.012
        _ContactBoost ("Contact Darkening", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "Default"
            Tags { "LightMode" = "UniversalForward" }

        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                float _Opacity;
                float _TipFade;
                float _AlphaCutoff;
                float _PenumbraScale;
                float _ContactBoost;
            CBUFFER_END

            // Per-renderer values from ShadowManager via MaterialPropertyBlock.
            // _ShearAmt/_LatScale: shear terms in sprite-local space (sin/cos of the
            // angle between camera-right and the quad's lateral axis, length-corrected).
            // _SpriteVMin/_SpriteVInvH: map atlas UV.y to a 0..1 base→tip coordinate.
            // _ShadowStretch: sun-elevation length factor, scales penumbra growth.
            float _ShearAmt;
            float _LatScale;
            float _SpriteVMin;
            float _SpriteVInvH;
            float _ShadowStretch;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float2 p = v.vertex.xy;
                v.vertex.x = p.x * _LatScale;
                v.vertex.y = p.y + p.x * _ShearAmt;

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half SampleSilhouette(float2 uv, float radius)
            {
                // 5-tap cross blur; radius grows toward the tip (penumbra widening).
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * 0.4h;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( radius, 0)).a * 0.15h;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-radius, 0)).a * 0.15h;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0,  radius)).a * 0.15h;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -radius)).a * 0.15h;
                return a;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 0 at the contact point, 1 at the cast tip — atlas-safe.
                float h01 = saturate((i.uv.y - _SpriteVMin) * _SpriteVInvH);

                float radius = _PenumbraScale * h01 * max(_ShadowStretch, 1.0);
                half texA = SampleSilhouette(i.uv, radius);
                texA *= i.color.a;                      // respect SpriteRenderer alpha
                clip(texA - _AlphaCutoff);

                // Dissolve toward the cast tip, darken at the contact end.
                float fade = 1.0 - smoothstep(0.2, 1.0, h01) * _TipFade;
                float contact = 1.0 - h01;
                fade *= 1.0 + _ContactBoost * contact * contact;

                half a = saturate(texA * _ShadowColor.a * _Opacity * fade);
                half3 rgb = _ShadowColor.rgb * a;       // premultiplied alpha
                return half4(rgb, a);
            }
        ENDHLSL
        }
    }
}
