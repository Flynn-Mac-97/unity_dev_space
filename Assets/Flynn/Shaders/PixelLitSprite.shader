// PixelLitSprite — URP Forward lit sprite-sheet shader with live pixel-art post.
// Drop into Assets/, create material with shader "Custom/PixelLitSprite".
// Experiment sliders:
//   Pixelate Factor : 1 = off, 4 = every 4x4 texels become one chunky pixel
//   Color Steps     : posterize levels per channel (0 = off)
//   Alpha Cutoff    : hard cutout edge
// Lighting: main directional + additional point/spot lights, normal map supported.
Shader "Custom/PixelLitSprite"
{
    Properties
    {
        _MainTex ("Base Map (sprite sheet)", 2D) = "white" {}
        _BumpMap ("Normal Map (matching sheet)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1.0
        _PixelateFactor ("Pixelate Factor", Range(1, 12)) = 4
        _ColorSteps ("Color Steps (0 = off)", Range(0, 32)) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half _BumpScale;
                half _PixelateFactor;
                half _ColorSteps;
                half _Cutoff;
                half _AmbientBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;   // w = sign
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.tangentWS = float4(nrm.tangentWS, IN.tangentOS.w);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- pixelate: snap UV to a chunky texel grid ---
                float2 grid = _MainTex_TexelSize.zw / max(_PixelateFactor, 1.0);
                float2 uv = (floor(IN.uv * grid) + 0.5) / grid;

                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                clip(baseCol.a - _Cutoff);

                // --- posterize ---
                if (_ColorSteps >= 2)
                    baseCol.rgb = floor(baseCol.rgb * _ColorSteps) / (_ColorSteps - 1);

                // --- normal map (same snapped UV so lighting pixels match color pixels) ---
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float3 bitangent = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3 nWS = normalize(mul(nTS, half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS)));

                // --- lighting: lambert, main + additional lights ---
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = mainLight.color * mainLight.shadowAttenuation
                               * saturate(dot(nWS, mainLight.direction));

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, IN.positionWS);
                    lighting += l.color * l.distanceAttenuation * saturate(dot(nWS, l.direction));
                }
                #endif

                lighting += SampleSH(nWS) * _AmbientBoost;   // ambient/skybox term

                return half4(baseCol.rgb * lighting, 1);
            }
            ENDHLSL
        }

        // shadow caster so the sprite drops a real shadow with correct cutout
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half _BumpScale;
                half _PixelateFactor;
                half _ColorSteps;
                half _Cutoff;
                half _AmbientBoost;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V shadowVert(A IN)
            {
                V OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 shadowFrag(V IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
