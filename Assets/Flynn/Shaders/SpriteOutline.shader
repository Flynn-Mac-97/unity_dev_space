Shader "Flynn/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size (screen px)", Range(1, 16)) = 2

        // Vertical fade: outline invisible at/below _FadeLow, full at/above _FadeHigh.
        // Uses object-space Y (0 = pivot, negative = below) — atlas-independent.
        _FadeLow ("Outline Fade Low (Obj Y)", Float) = -0.3
        _FadeHigh ("Outline Fade High (Obj Y)", Float) = 0.1
        _WindWeight ("Wind Weight", Range(0, 1)) = 0.0
        _WindShake ("Wind Shake", Range(0, 1)) = 0.0

        // UI stencil (matches Flynn/Sprite)
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
            Tags { "LightMode" = "Universal2D" }

        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_UI_CLIP_RECT UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Wind.hlsl"

            struct appdata
            {
                float3 vertex : POSITION;
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
                float objY : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _RendererColor;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            half4 _OutlineColor;
            float _OutlineSize;
            float _FadeLow;
            float _FadeHigh;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

            #ifdef UNITY_INSTANCING_ENABLED
                v.vertex = UnityFlipSprite(v.vertex, unity_SpriteFlip);
            #endif

                ApplyWind(v.vertex);

                o.worldPosition = float4(v.vertex, 1.0);
                o.objY = v.vertex.y;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _RendererColor;

            #ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
            #endif

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Screen-space derivatives: offset is consistent regardless of
                // texture resolution, atlas packing, or object scale.
                float2 uvPerPixel = float2(
                    length(float2(ddx(i.uv.x), ddy(i.uv.x))),
                    length(float2(ddx(i.uv.y), ddy(i.uv.y)))
                );
                float2 texel = uvPerPixel * _OutlineSize;

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                half selfA = texColor.a;

                // Sample 8 neighbours for outline
                half outlineAlpha = 0;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x, -texel.y)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( 0,         -texel.y)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x, -texel.y)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x,  0)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x,  0)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x,  texel.y)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( 0,          texel.y)).a;
                outlineAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x,  texel.y)).a;

                float vGrad = smoothstep(_FadeLow, _FadeHigh, i.objY);
                float outlineMask = saturate(outlineAlpha) * (1 - selfA) * vGrad;

                // Sprite fill (premultiplied for Blend One OneMinusSrcAlpha)
                half spriteA = selfA * i.color.a;
                half4 spriteCol = half4(texColor.rgb * i.color.rgb * spriteA, spriteA);

                // Outline (premultiplied)
                half outlineA = saturate(_OutlineColor.a * outlineMask * i.color.a);
                half4 outlineCol = half4(_OutlineColor.rgb * outlineA, outlineA);

                // Combined: sprite + outline edges
                half4 c = spriteCol + outlineCol;

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
