Shader "Flynn/ProjectedShadow2D"
{
    // 2D sprite silhouette shadow for pure XY-plane scenes.
    // Shadow2DManager positions each shadow sprite behind the caster,
    // offset in the direction away from the light source, and stretched
    // along that direction (lower sun = longer shadow).
    // This shader stamps the sprite as a uniform dark silhouette with
    // tip fade and penumbra, ignoring the sprite's RGB.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.1, 0.14, 0.22, 0.5)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _TipFade ("Tip Fade (toward cast end)", Range(0, 1)) = 0.4
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
        _PenumbraScale ("Penumbra Scale", Range(0, 0.05)) = 0.008
        _ContactBoost ("Contact Darkening", Range(0, 1)) = 0.3
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

        // Primary pass for URP 2D renderer
        Pass
        {
            Name "Default"
            Tags { "LightMode" = "Universal2D" }

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

            // Per-renderer values from Shadow2DManager via MaterialPropertyBlock.
            // Sun shadow = silhouette flipped across the sprite base, squashed
            // and sheared: a feature at height H above the base lands at
            // (ShearX*H, BaseY - SquashY*H) — the shadow lies on the ground
            // pointing away from the sun instead of standing up as a dark clone.
            float _ShearX;
            float _SquashY;
            float _BaseY;
            float _SpriteVMin;
            float _SpriteVInvH;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float h01 = saturate((v.uv.y - _SpriteVMin) * _SpriteVInvH);
                float yb = v.vertex.y - _BaseY;      // height above sprite base
                float2 pos;
                pos.x = v.vertex.x + _ShearX * yb;
                pos.y = _BaseY - yb * _SquashY;

                o.vertex = TransformObjectToHClip(float3(pos, v.vertex.z));
                o.uv = v.uv;  // SpriteRenderer provides atlas-correct UVs; no TRANSFORM_TEX needed
                o.color = v.color;
                return o;
            }

            half SampleSilhouette(float2 uv, float radius)
            {
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

                float h01 = saturate((i.uv.y - _SpriteVMin) * _SpriteVInvH);

                float radius = _PenumbraScale * h01 * (1.0 + _SquashY);
                half texA = SampleSilhouette(i.uv, radius);
                texA *= i.color.a;
                clip(texA - _AlphaCutoff);

                float fade = 1.0 - smoothstep(0.2, 1.0, h01) * _TipFade;
                float contact = 1.0 - h01;
                fade *= 1.0 + _ContactBoost * contact * contact;

                half a = saturate(texA * _ShadowColor.a * _Opacity * fade);
                half3 rgb = _ShadowColor.rgb * a;
                return half4(rgb, a);
            }
        ENDHLSL
        }

        // Fallback pass for non-2D URP renderers
        Pass
        {
            Name "Forward"
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

            float _ShearX;
            float _SquashY;
            float _BaseY;
            float _SpriteVMin;
            float _SpriteVInvH;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float yb = v.vertex.y - _BaseY;
                float2 pos;
                pos.x = v.vertex.x + _ShearX * yb;
                pos.y = _BaseY - yb * _SquashY;

                v.vertex.xy = pos;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half SampleSilhouette(float2 uv, float radius)
            {
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

                float h01 = saturate((i.uv.y - _SpriteVMin) * _SpriteVInvH);

                float radius = _PenumbraScale * h01 * (1.0 + _SquashY);
                half texA = SampleSilhouette(i.uv, radius);
                texA *= i.color.a;
                clip(texA - _AlphaCutoff);

                float fade = 1.0 - smoothstep(0.2, 1.0, h01) * _TipFade;
                float contact = 1.0 - h01;
                fade *= 1.0 + _ContactBoost * contact * contact;

                half a = saturate(texA * _ShadowColor.a * _Opacity * fade);
                half3 rgb = _ShadowColor.rgb * a;
                return half4(rgb, a);
            }
        ENDHLSL
        }
    }
}
