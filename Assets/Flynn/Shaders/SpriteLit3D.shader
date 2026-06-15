Shader "Flynn/SpriteLit3D"
{
    // Lit billboard sprite for the 2.5D world under the Forward renderer.
    //
    // A camera-facing quad gives every pixel the same normal, so 3D lights only
    // contribute distance falloff — no directional shading. _NormalLift bends the
    // shading normal toward world up so sprites shade like the ground they stand
    // on: the sun's elevation matters and lamp pools read top-down-correct.
    // Lighting is Lambert: trilight ambient (SH) + main light + additional lights.
    Properties
    {
        [PerRendererData][MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.3

        _NormalLift ("Normal Lift (0 = face camera, 1 = world up)", Range(0, 1)) = 1

        [Toggle(_EMISSION)] _EmissionToggle ("Emission", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TransparentCutout"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite On

        Pass
        {
            Name "Default"
            Tags { "LightMode" = "UniversalForward" }

        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma shader_feature_local _EMISSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Cutoff;
                float _NormalLift;
                half4 _EmissionColor;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.positionWS = TransformObjectToWorld(v.vertex.xyz);
                o.vertex = TransformWorldToHClip(o.positionWS);

                float3 quadNormal = TransformObjectToWorldNormal(v.normal);
                o.normalWS = normalize(lerp(quadNormal, float3(0, 1, 0), _NormalLift));

                o.uv = v.uv;
                o.color = v.color * _BaseColor;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(albedo.a - _Cutoff);

                float3 n = normalize(i.normalWS);

                half3 lighting = SampleSH(n);

                Light mainLight = GetMainLight();
                lighting += mainLight.color * mainLight.distanceAttenuation
                          * saturate(dot(n, mainLight.direction));

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light light = GetAdditionalLight(li, i.positionWS);
                    lighting += light.color * light.distanceAttenuation
                              * saturate(dot(n, light.direction));
                }
                #endif

                half3 rgb = albedo.rgb * lighting;

                #ifdef _EMISSION
                rgb += _EmissionColor.rgb;
                #endif

                return half4(rgb, 1);
            }
        ENDHLSL
        }
    }
}
