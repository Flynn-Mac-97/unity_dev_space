Shader "Flynn/StyledWater2D"
{
    Properties
    {
        _MainTex ("Water Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Water Animation)]
        _ScrollX ("Scroll Speed X", Float) = 0.08
        _ScrollY ("Scroll Speed Y", Float) = 0.03
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.1)) = 0.02
        _WaveFrequency ("Wave Frequency", Float) = 2.0
        _PixelSnap ("Pixel Snap Size", Float) = 0.0625

        [Header(Edge Outline)]
        _EdgeColor ("Edge Color", Color) = (0.12, 0.18, 0.32, 1)
        _EdgeWidth ("Edge Width", Range(0.001, 0.1)) = 0.03
        _EdgeThreshold ("Edge Alpha Threshold", Range(0.01, 0.5)) = 0.15

        [Header(Intersection Outline)]
        _SubmersionMask ("Submersion Mask (RenderTexture)", 2D) = "white" {}
        _IntersectColor ("Intersection Color", Color) = (0.3, 0.55, 0.8, 0.8)
        _IntersectWidth ("Intersection Width", Range(1, 8)) = 2.0

        [Header(Pixel Art)]
        _PosterizeSteps ("Posterize Steps", Range(2, 8)) = 4
        _ColorRamp ("Color Ramp", 2D) = "white" {}

        // Stencil for UI
        _StencilComp ("Stencil Comp", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Op", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        // Pass 1: forward-rendered unlit water
        Pass
        {
            Name "Water2D"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            half4 _Color;
            float _ScrollX;
            float _ScrollY;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _PixelSnap;

            half4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeThreshold;

            TEXTURE2D(_SubmersionMask);
            SAMPLER(sampler_SubmersionMask);
            float4 _SubmersionMask_TexelSize;
            half4 _IntersectColor;
            float _IntersectWidth;

            float _PosterizeSteps;
            TEXTURE2D(_ColorRamp);
            SAMPLER(sampler_ColorRamp);

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
                float4 screenPos : TEXCOORD1;
                float2 localPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Snap(float v, float snapSize)
            {
                return snapSize > 0 ? floor(v / snapSize + 0.5) * snapSize : v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                // Local position for mesh-edge detection
                o.localPos = v.vertex.xy;
                return o;
            }

            half4 SampleWater(float2 uv, float time)
            {
                float2 scrollUV = uv;
                float sx = Snap(_ScrollX * time, _PixelSnap * 0.5);
                float sy = Snap(_ScrollY * time, _PixelSnap * 0.5);
                scrollUV.x += sx;
                scrollUV.y += sy;
                float waveOffset = sin(Snap(uv.x * 6.2831, _PixelSnap) + Snap(_WaveFrequency * time, _PixelSnap * 0.5)) * _WaveAmplitude;
                scrollUV.y += waveOffset;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV);
            }

            // Detect UV-space edges using screen-space derivatives
            float DetectUVEdge(float2 uv)
            {
                // Screen-space derivatives tell us how fast UV changes per pixel
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);

                // Sample neighbors in screen-space
                float2 uvRight = uv + dx * _EdgeWidth;
                float2 uvUp    = uv + dy * _EdgeWidth;
                float2 uvLeft  = uv - dx * _EdgeWidth;
                float2 uvDown  = uv - dy * _EdgeWidth;

                // Check if neighbor UVs are still in [0,1] range (inside the mesh)
                // Pixels near the mesh border will have neighbors that wrap or go out of bounds
                float edgeH = step(0.0, uvLeft.x) * step(uvLeft.x, 1.0) *
                               step(0.0, uvLeft.y) * step(uvLeft.y, 1.0);
                float edgeHR = step(0.0, uvRight.x) * step(uvRight.x, 1.0) *
                                step(0.0, uvRight.y) * step(uvRight.y, 1.0);
                float edgeV = step(0.0, uvUp.x) * step(uvUp.x, 1.0) *
                               step(0.0, uvUp.y) * step(uvUp.y, 1.0);
                float edgeVD = step(0.0, uvDown.x) * step(uvDown.x, 1.0) *
                                step(0.0, uvDown.y) * step(uvDown.y, 1.0);

                // Also detect proximity to UV border (0 or 1)
                float2 absDist = min(uv, 1.0 - uv);
                float borderDist = min(absDist.x, absDist.y);
                float borderEdge = 1.0 - smoothstep(0.0, _EdgeWidth, borderDist);

                // Compare with neighbor validity
                float neighborEdge = (1.0 - edgeH) + (1.0 - edgeHR) + (1.0 - edgeV) + (1.0 - edgeVD);
                neighborEdge = saturate(neighborEdge);

                return max(neighborEdge, borderEdge);
            }

            // Detect edges in the submersion mask
            float DetectMaskEdge(float2 screenUV)
            {
                float2 texel = _SubmersionMask_TexelSize.xy * _IntersectWidth;
                float c = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV).a;
                float r = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV + float2(texel.x, 0)).a;
                float l = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV - float2(texel.x, 0)).a;
                float u = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV + float2(0, texel.y)).a;
                float d = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV - float2(0, texel.y)).a;

                float gx = r - l;
                float gy = u - d;
                float edge = sqrt(gx * gx + gy * gy);

                float minN = min(min(r, l), min(u, d));
                float borderEdge = c > 0.01 && minN < 0.01 ? 1.0 : 0.0;

                return saturate(max(edge, borderEdge));
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 water = SampleWater(i.uv, _Time.y) * i.color;

                // Edge outline (UV border detection)
                float edgeMask = DetectUVEdge(i.uv);
                half3 finalColor = lerp(water.rgb, _EdgeColor.rgb, edgeMask * _EdgeColor.a);

                // Intersection outline
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float maskEdge = DetectMaskEdge(screenUV);
                finalColor = lerp(finalColor, _IntersectColor.rgb, maskEdge * _IntersectColor.a * 0.7);

                // Submersion tint
                float maskVal = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV).a;
                finalColor = lerp(finalColor, _IntersectColor.rgb * 0.5, maskVal * 0.3);

                // Posterize for pixel art
                float lum = dot(finalColor, float3(0.2126, 0.7152, 0.0722));
                float steps = max(2.0, _PosterizeSteps);
                float posterLum = floor(lum * steps + 0.5) / steps;
                half3 rampColor = SAMPLE_TEXTURE2D(_ColorRamp, sampler_ColorRamp, float2(posterLum, 0.5)).rgb;
                finalColor = lerp(finalColor, rampColor, 0.2);

                half4 result;
                result.rgb = finalColor;
                result.a = water.a * i.color.a;
                result.rgb *= result.a;
                return result;
            }
            ENDHLSL
        }

        // Pass 2: Fallback for non-2D light modes (mesh renderers, scene view)
        Pass
        {
            Name "WaterFallback"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            half4 _Color;
            float _ScrollX;
            float _ScrollY;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _PixelSnap;
            half4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeThreshold;
            TEXTURE2D(_SubmersionMask);
            SAMPLER(sampler_SubmersionMask);
            float4 _SubmersionMask_TexelSize;
            half4 _IntersectColor;
            float _IntersectWidth;
            float _PosterizeSteps;
            TEXTURE2D(_ColorRamp);
            SAMPLER(sampler_ColorRamp);

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
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Snap(float v, float snapSize)
            {
                return snapSize > 0 ? floor(v / snapSize + 0.5) * snapSize : v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 scrollUV = i.uv;
                float sx = Snap(_ScrollX * _Time.y, _PixelSnap * 0.5);
                float sy = Snap(_ScrollY * _Time.y, _PixelSnap * 0.5);
                scrollUV.x += sx;
                scrollUV.y += sy;
                float waveOffset = sin(Snap(i.uv.x * 6.2831, _PixelSnap) + Snap(_WaveFrequency * _Time.y, _PixelSnap * 0.5)) * _WaveAmplitude;
                scrollUV.y += waveOffset;

                half4 water = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV) * i.color;

                // Edge detection using UV border proximity
                float2 absDist = min(i.uv, 1.0 - i.uv);
                float borderDist = min(absDist.x, absDist.y);
                float edgeMask = 1.0 - smoothstep(0.0, _EdgeWidth, borderDist);

                half3 finalColor = lerp(water.rgb, _EdgeColor.rgb, edgeMask * _EdgeColor.a);

                // Intersection
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 texel = _SubmersionMask_TexelSize.xy * _IntersectWidth;
                float c = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV).a;
                float r = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV + float2(texel.x, 0)).a;
                float l = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV - float2(texel.x, 0)).a;
                float u = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV + float2(0, texel.y)).a;
                float d = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV - float2(0, texel.y)).a;
                float gx = r - l;
                float gy = u - d;
                float maskEdge = saturate(sqrt(gx * gx + gy * gy));
                float minN = min(min(r, l), min(u, d));
                maskEdge = max(maskEdge, c > 0.01 && minN < 0.01 ? 1.0 : 0.0);

                finalColor = lerp(finalColor, _IntersectColor.rgb, maskEdge * _IntersectColor.a * 0.7);
                float maskVal = SAMPLE_TEXTURE2D(_SubmersionMask, sampler_SubmersionMask, screenUV).a;
                finalColor = lerp(finalColor, _IntersectColor.rgb * 0.5, maskVal * 0.3);

                // Posterize
                float lum = dot(finalColor, float3(0.2126, 0.7152, 0.0722));
                float steps = max(2.0, _PosterizeSteps);
                float posterLum = floor(lum * steps + 0.5) / steps;
                half3 rampColor = SAMPLE_TEXTURE2D(_ColorRamp, sampler_ColorRamp, float2(posterLum, 0.5)).rgb;
                finalColor = lerp(finalColor, rampColor, 0.2);

                half4 result;
                result.rgb = finalColor;
                result.a = water.a * i.color.a;
                result.rgb *= result.a;
                return result;
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
