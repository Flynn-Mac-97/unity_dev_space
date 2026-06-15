Shader "Flynn/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Saturation ("Saturation", Range(0, 2)) = 1.3
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Contrast ("Contrast", Range(0, 2)) = 1.0

        _ColorRamp ("Color Ramp (R)", 2D) = "white" {}
        _RampStrength ("Ramp Strength", Range(0, 1)) = 1.0
        _PosterizeSteps ("Posterize Steps", Range(2, 16)) = 6

        _ShadowTint ("Shadow Tint", Color) = (0,0,0,0)
        _ShadowAmount ("Shadow Amount", Range(0, 1)) = 0

        // UI stencil
        _StencilComp ("Stencil Comp", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Op", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            Tags { "LightMode" = "UniversalForward" }

        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_UI_CLIP_RECT UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Sprite2D.hlsl"

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
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color;
            float4 _MainTex_TexelSize;
            float _Saturation;
            float _Brightness;
            float _Contrast;
            TEXTURE2D(_ColorRamp);
            SAMPLER(sampler_ColorRamp);
            float _RampStrength;
            float _PosterizeSteps;
            half4 _ShadowTint;
            float _ShadowAmount;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.worldPosition = v.vertex;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                // Brightness
                c.rgb += _Brightness;

                // Contrast
                c.rgb = (c.rgb - 0.5) * _Contrast + 0.5;

                // Saturation — Rec.709 luminance
                float lum = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));
                c.rgb = lerp(lum.xxx, c.rgb, _Saturation);

                // Recalculate luminance after saturation
                lum = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));

                // Posterize: snap luminance to discrete steps
                float steps = max(2.0, _PosterizeSteps);
                float posterLum = floor(lum * steps + 0.5) / steps;

                // Color ramp lookup at the posterized luminance
                half3 rampColor = SAMPLE_TEXTURE2D(_ColorRamp, sampler_ColorRamp, float2(posterLum, 0.5)).rgb;

                half3 originalRgb = c.rgb;
                float rampLum = dot(rampColor, float3(0.2126, 0.7152, 0.0722));
                half3 rampTint = rampColor / max(0.05, rampLum);
                half3 ramped = saturate(posterLum * rampTint);

                c.rgb = lerp(originalRgb, ramped, _RampStrength);

                // Shadow overlay
                c.rgb = lerp(c.rgb, c.rgb * _ShadowTint.rgb + _ShadowTint.rgb * 0.5, _ShadowAmount * _ShadowTint.a);

                c.rgb *= c.a;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
        ENDHLSL
        }
    }
}
