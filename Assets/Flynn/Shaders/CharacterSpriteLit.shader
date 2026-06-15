Shader "Flynn/CharacterSpriteLit"
{
    // URP 2D-lit sprite with outline + color pop for the player character.
    // Extends the standard Sprite-Lit-Default with outline and color grading.
    Properties
    {
        [PerRendererData] _MainTex ("Diffuse", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}

        // Legacy properties for fallback compatibility
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.04,0.02,0.0,1)
        _OutlineWidth ("Outline Width", Range(0, 4)) = 1.5
        _OutlineSoftness ("Outline Softness", Range(0, 2)) = 0.5

        [Header(Color Pop)]
        _Saturation ("Saturation", Range(0, 3)) = 1.4
        _Brightness ("Brightness", Range(-0.5, 0.5)) = 0.05
        _Contrast ("Contrast", Range(0, 3)) = 1.2
        _PosterizeSteps ("Posterize Steps", Range(2, 32)) = 12
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        // ── Pass 1: Main lit pass (2D shape lights) ──────────────────────
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment

            #pragma multi_compile_instancing
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #pragma multi_compile _ DEBUG_DISPLAY

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                half4   color       : COLOR;
                float2  uv          : TEXCOORD0;
                half2   lightingUV  : TEXCOORD1;
                #if defined(DEBUG_DISPLAY)
                float3  positionWS  : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;
            float4 _MainTex_TexelSize;

            // Outline
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;

            // Color pop
            float _Saturation;
            float _Brightness;
            float _Contrast;
            float _PosterizeSteps;

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

#ifdef UNITY_INSTANCING_ENABLED
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteFlip);
#endif
                o.positionCS = TransformObjectToHClip(v.positionOS);
                #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(v.positionOS);
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color * _RendererColor;
#ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
#endif
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            float SampleAlpha(float2 uv, float2 offset)
            {
                float2 sampleUv = uv + offset * _MainTex_TexelSize.xy;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUv).a;
            }

            half4 ApplyColorPop(half4 c)
            {
                c.rgb += _Brightness;
                c.rgb = (c.rgb - 0.5) * _Contrast + 0.5;

                float lum = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));
                c.rgb = lerp(lum.xxx, c.rgb, _Saturation);

                float steps = max(2.0, _PosterizeSteps);
                c.rgb = floor(c.rgb * steps + 0.5) / steps;
                return c;
            }

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                const half4 main = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);

                // ── Outline ──
                float maxNeighborAlpha = 0;
                float w = _OutlineWidth;
                float2 dirs[8] = {
                    float2(1,0), float2(-1,0), float2(0,1), float2(0,-1),
                    float2(0.707,0.707), float2(-0.707,0.707), float2(0.707,-0.707), float2(-0.707,-0.707)
                };
                for (float d = 1.0; d <= w; d += 1.0)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        float a = SampleAlpha(i.uv, dirs[j] * d);
                        maxNeighborAlpha = max(maxNeighborAlpha, a);
                    }
                }

                float outlineMask = maxNeighborAlpha * (1.0 - main.a);
                if (_OutlineSoftness > 0.001)
                    outlineMask = smoothstep(0.0, _OutlineSoftness * 0.1, outlineMask);

                half4 result;
                result.a = max(main.a, outlineMask * _OutlineColor.a);
                result.rgb = lerp(main.rgb * main.a, _OutlineColor.rgb, saturate(outlineMask));
                result.rgb = lerp(result.rgb, main.rgb * main.a, main.a);

                // Apply color pop to the sprite portion
                if (main.a > 0.01)
                {
                    half4 popped = main;
                    popped = ApplyColorPop(popped);
                    result.rgb = lerp(result.rgb, popped.rgb * popped.a, main.a);
                }

                // Run through 2D lighting
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(result.rgb, result.a, mask, surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);

                half4 lit = CombinedShapeLightShared(surfaceData, inputData);

                // Re-apply outline on top of lit result (outline should not be affected by 2D lights)
                lit.rgb = lerp(lit.rgb, _OutlineColor.rgb * _OutlineColor.a, saturate(outlineMask) * 0.8);

                return lit;
            }
            ENDHLSL
        }

        // ── Pass 2: Normals rendering (for 2D lighting system) ──────────
        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float4 tangent      : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                half4   color           : COLOR;
                float2  uv              : TEXCOORD0;
                half3   normalWS        : TEXCOORD1;
                half3   tangentWS       : TEXCOORD2;
                half3   bitangentWS     : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            half4 _NormalMap_ST;

            Varyings NormalsRenderingVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

#ifdef UNITY_INSTANCING_ENABLED
                attributes.positionOS = UnityFlipSprite(attributes.positionOS, unity_SpriteFlip);
#endif
                o.positionCS = TransformObjectToHClip(attributes.positionOS);
                o.uv = TRANSFORM_TEX(attributes.uv, _NormalMap);
                o.color = attributes.color;
                o.normalWS = -GetViewForwardDir();
                o.tangentWS = TransformObjectToWorldDir(attributes.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * attributes.tangent.w;
#ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
#endif
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            half4 NormalsRenderingFragment(Varyings i) : SV_Target
            {
                const half4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));

                return NormalsRenderingShared(mainTex, normalTS, i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz);
            }
            ENDHLSL
        }

        // ── Pass 3: Forward fallback (unlit) ─────────────────────────────
        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                float4  color           : COLOR;
                float2  uv              : TEXCOORD0;
                #if defined(DEBUG_DISPLAY)
                float3  positionWS  : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;
            float4 _MainTex_TexelSize;

            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;
            float _Saturation;
            float _Brightness;
            float _Contrast;
            float _PosterizeSteps;

            Varyings UnlitVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

#ifdef UNITY_INSTANCING_ENABLED
                attributes.positionOS = UnityFlipSprite(attributes.positionOS, unity_SpriteFlip);
#endif
                o.positionCS = TransformObjectToHClip(attributes.positionOS);
                o.uv = TRANSFORM_TEX(attributes.uv, _MainTex);
                o.color = attributes.color * _Color * _RendererColor;
#ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
#endif
                return o;
            }

            float SampleAlpha(float2 uv, float2 offset)
            {
                float2 sampleUv = uv + offset * _MainTex_TexelSize.xy;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUv).a;
            }

            float4 UnlitFragment(Varyings i) : SV_Target
            {
                float4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Outline
                float maxNeighborAlpha = 0;
                float w = _OutlineWidth;
                float2 dirs[8] = {
                    float2(1,0), float2(-1,0), float2(0,1), float2(0,-1),
                    float2(0.707,0.707), float2(-0.707,0.707), float2(0.707,-0.707), float2(-0.707,-0.707)
                };
                for (float d = 1.0; d <= w; d += 1.0)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        float a = SampleAlpha(i.uv, dirs[j] * d);
                        maxNeighborAlpha = max(maxNeighborAlpha, a);
                    }
                }
                float outlineMask = maxNeighborAlpha * (1.0 - mainTex.a);
                if (_OutlineSoftness > 0.001)
                    outlineMask = smoothstep(0.0, _OutlineSoftness * 0.1, outlineMask);

                mainTex.rgb = lerp(mainTex.rgb, _OutlineColor.rgb, saturate(outlineMask));
                mainTex.a = max(mainTex.a, outlineMask * _OutlineColor.a);

                // Color pop
                mainTex.rgb += _Brightness;
                mainTex.rgb = (mainTex.rgb - 0.5) * _Contrast + 0.5;
                float lum = dot(mainTex.rgb, float3(0.2126, 0.7152, 0.0722));
                mainTex.rgb = lerp(lum.xxx, mainTex.rgb, _Saturation);
                float steps = max(2.0, _PosterizeSteps);
                mainTex.rgb = floor(mainTex.rgb * steps + 0.5) / steps;

                return mainTex;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
