// GrassFill.shader
// Built-in render pipeline.
// Uses WORLD-SPACE XZ coordinates for UV tiling so the detail texture tiles
// evenly across the fill regardless of spline shape or UV stretching.
//
// Layer 1: Base color tint (from _Color + vertex colour from SpriteShape)
// Layer 2: Detail texture (grass blade / noise map) tiled over the surface
// Layer 3: Subtle noise-driven colour variation (darker/lighter patches)
// Layer 4: Gentle UV drift to simulate a very soft wind rustle on the detail

Shader "Custom/GrassFill"
{
    Properties
    {
        _MainTex            ("Base Texture (can be white)",     2D)     = "white" {}
        _Color              ("Base Tint",                       Color)  = (0.35, 0.72, 0.28, 1)

        [Header(Detail Overlay)]
        _DetailTex          ("Detail Texture (grass/noise)",    2D)     = "white" {}
        _DetailScale        ("Detail World Tile Scale",         Float)  = 2.0
        _DetailStrength     ("Detail Blend Strength",           Range(0,1)) = 0.45

        [Header(Colour Variation)]
        _VariationTex       ("Variation Noise (greyscale)",     2D)     = "gray" {}
        _VariationScale     ("Variation World Tile Scale",      Float)  = 6.0
        _VariationStrength  ("Variation Strength",              Range(0,0.4)) = 0.12
        _VariationDark      ("Variation Dark Tint",             Color)  = (0.2, 0.4, 0.1, 1)
        _VariationLight     ("Variation Light Tint",            Color)  = (0.6, 0.85, 0.3, 1)

        [Header(Wind)]
        _WindSpeed          ("Wind Speed",                      Float)  = 0.4
        _WindStrength       ("Wind UV Distort Strength",        Float)  = 0.015
        _WindDirection      ("Wind Direction (XZ)",             Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;

            sampler2D _DetailTex;
            float     _DetailScale;
            float     _DetailStrength;

            sampler2D _VariationTex;
            float     _VariationScale;
            float     _VariationStrength;
            fixed4    _VariationDark;
            fixed4    _VariationLight;

            float     _WindSpeed;
            float     _WindStrength;
            float4    _WindDirection;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 baseUV    : TEXCOORD0;
                // World XZ position passed to fragment for world-space tiling.
                float2 worldXZ   : TEXCOORD1;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex  = UnityObjectToClipPos(v.vertex);
                o.color   = v.color * _Color;
                o.baseUV  = TRANSFORM_TEX(v.texcoord, _MainTex);

                // Convert vertex to world space and pass XZ for world-space tiling.
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldXZ = worldPos.xz;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // --- Base ---
                fixed4 base = tex2D(_MainTex, i.baseUV) * i.color;

                // --- Wind drift (applied to detail UVs only) ---
                float2 windDir   = normalize(_WindDirection.xz + float2(0.001, 0));
                float  windDrift = _Time.y * _WindSpeed;
                float2 windOffset = windDir * windDrift * _WindStrength
                                  + sin(_Time.y * _WindSpeed * 0.7 + i.worldXZ.x * 0.5) * _WindStrength * float2(0.5, 0.5);

                // --- Detail overlay (world-space tiled) ---
                float2 detailUV  = i.worldXZ / _DetailScale + windOffset;
                fixed4 detail    = tex2D(_DetailTex, detailUV);
                // Multiply-blend the detail on top of the base colour.
                fixed3 withDetail = lerp(base.rgb, base.rgb * detail.rgb * 2.0, _DetailStrength);

                // --- Colour variation (large-scale world-space noise) ---
                float2 varUV     = i.worldXZ / _VariationScale;
                float  varNoise  = tex2D(_VariationTex, varUV).r;   // 0..1 greyscale
                fixed3 varColour = lerp(_VariationDark.rgb, _VariationLight.rgb, varNoise);
                fixed3 varied    = lerp(withDetail, withDetail * varColour, _VariationStrength);

                return fixed4(varied, base.a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
