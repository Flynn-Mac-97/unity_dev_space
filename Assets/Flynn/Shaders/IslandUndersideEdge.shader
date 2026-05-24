// IslandUndersideEdge.shader
// Built-in render pipeline.
// Designed for the underside of floating islands on a SpriteShape edge strip.
//
// UV.y=1 is the outer perimeter edge of the strip — this is the cliff top, pinned flush
// to the island's visible edge. UV.y=0 is the inner seam, which becomes the cliff base,
// displaced _CliffScale world units along local +Z (world -Y for rotation 90,0,0).
//
// Rock strata bands, world-space rock texture, a bottom alpha fade, and a
// drop shadow pass are all self-contained on the edge-strip geometry.

Shader "Custom/IslandUndersideEdge"
{
    Properties
    {
        [Header(Rock Surface)]
        _RockTex            ("Rock Texture",                    2D)             = "white" {}
        _RockColor          ("Rock Base Color",                 Color)          = (0.52, 0.47, 0.42, 1)
        _RockTexScale       ("Rock Texture World Scale",        Float)          = 2.0

        [Header(Cliff Shape)]
        _CliffScale         ("Cliff Scale (world units)",          Float)  = 1.8
        _SeamOffset         ("Seam Offset — slide cliff top to edge", Float)  = 0.0

        [Header(Strata Bands)]
        _StrataCount        ("Strata Band Count",               Range(1,10))    = 5
        _StrataDarken       ("Per-Band Darken Amount",          Range(0,0.4))   = 0.08
        _StrataSharpness    ("Band Edge Sharpness",             Range(1,20))    = 6.0

        [Header(Bottom Fade)]
        _BottomFadeStart    ("Bottom Fade Start (0-1 UV)",      Range(0,1))     = 0.75
        _BottomFadeSharpness("Bottom Fade Sharpness",           Range(1,20))    = 4.0

        [Header(Drop Shadow)]
        _ShadowColor        ("Shadow Color",                    Color)          = (0.04, 0.04, 0.08, 0.5)
        _ShadowOffset       ("Shadow Offset (XY world units)",  Vector)         = (0.0, -0.5, 0, 0)

        [Header(Grass Edge)]
        _MainTex            ("Base Texture",                    2D)             = "white" {}
        _Color              ("Base Tint",                       Color)          = (0.35, 0.72, 0.28, 1)
        _Droop              ("Droop Amount",                    Float)          = 0.15
        _DroopStart         ("Droop Start (UV)",                Range(0,1))     = 0.2

        [Header(Detail Overlay)]
        _DetailTex          ("Detail Texture (grass/noise)",    2D)             = "white" {}
        _DetailScale        ("Detail World Tile Scale",         Float)          = 2.0
        _DetailStrength     ("Detail Blend Strength",           Range(0,1))     = 0.45

        [Header(Colour Variation)]
        _VariationTex       ("Variation Noise (greyscale)",     2D)             = "gray" {}
        _VariationScale     ("Variation World Tile Scale",      Float)          = 6.0
        _VariationStrength  ("Variation Strength",              Range(0,0.4))   = 0.12
        _VariationDark      ("Variation Dark Tint",             Color)          = (0.2, 0.4, 0.1, 1)
        _VariationLight     ("Variation Light Tint",            Color)          = (0.6, 0.85, 0.3, 1)

        [Header(Wind)]
        _WindSpeed          ("Wind Speed",                      Float)          = 0.4
        _WindStrength       ("Wind UV Distort Strength",        Float)          = 0.015
        _WindDirection      ("Wind Direction (XZ)",             Vector)         = (1, 0, 0, 0)
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

        // ─────────────────────────────────────────────────────────────
        // Pass 1 — Drop Shadow
        // Offsets the cliff geometry straight down by _ShadowOffset.y,
        // projecting a soft shadow beneath the floating island.
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "SHADOW"

            CGPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #include "UnityCG.cginc"

            fixed4  _ShadowColor;
            float4  _ShadowOffset;
            float   _CliffScale;
            float   _SeamOffset;
            float   _BottomFadeStart;
            float   _BottomFadeSharpness;

            struct appdata_s
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
            };

            struct v2s
            {
                float4 pos      : SV_POSITION;
                float  localV   : TEXCOORD0;
            };

            v2s shadowVert(appdata_s v)
            {
                // UV.y=1 is the outer perimeter edge of the strip — anchor cliff top here.
                // UV.y=0 is the inner seam — this becomes the cliff base.
                float t = 1.0 - saturate(v.uv.y);

                float3 worldPos  = mul(unity_ObjectToWorld, v.vertex).xyz;
                // Local +Z is world -Y for a SpriteShape with rotation (90,0,0).
                // _SeamOffset shifts every vertex uniformly so the cliff top can be
                // slid to align with the outer visible edge of the texture.
                float3 extrudeDir = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
                worldPos += extrudeDir * (t * _CliffScale + _SeamOffset);
                worldPos.x += _ShadowOffset.x;
                worldPos.y += _ShadowOffset.y;

                v2s o;
                o.pos    = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.localV = t;
                return o;
            }

            fixed4 shadowFrag(v2s i) : SV_Target
            {
                float bottomFade = 1.0 - saturate(
                    pow(saturate((i.localV - _BottomFadeStart) / (1.0 - _BottomFadeStart + 0.001)),
                        _BottomFadeSharpness));
                float topFade = saturate(i.localV * 4.0);
                return fixed4(_ShadowColor.rgb, _ShadowColor.a * bottomFade * topFade);
            }
            ENDCG
        }

        // ─────────────────────────────────────────────────────────────
        // Pass 2 — Rock Cliff Face
        // Drops the strip straight down in Y.  Strata bands and a
        // bottom fade are applied in the fragment shader.
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ROCK_UNDERSIDE"

            CGPROGRAM
            #pragma vertex   rockVert
            #pragma fragment rockFrag
            #include "UnityCG.cginc"

            sampler2D _RockTex;
            fixed4    _RockColor;
            float     _RockTexScale;

            float     _CliffScale;
            float     _SeamOffset;

            float     _StrataCount;
            float     _StrataDarken;
            float     _StrataSharpness;

            float     _BottomFadeStart;
            float     _BottomFadeSharpness;

            struct appdata_r
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2r
            {
                float4 pos     : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
                float  localV  : TEXCOORD1;
            };

            v2r rockVert(appdata_r v)
            {
                // UV.y=1 is the outer perimeter edge of the strip — anchor cliff top here.
                // UV.y=0 is the inner seam — this becomes the cliff base.
                float t = 1.0 - saturate(v.uv.y);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                // Local +Z = world -Y for this SpriteShape (rotation 90,0,0).
                // _SeamOffset shifts all verts uniformly so the cliff top aligns
                // with the outer visible edge of the texture strip.
                float3 extrudeDir = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
                worldPos += extrudeDir * (t * _CliffScale + _SeamOffset);

                v2r o;
                o.pos     = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldXZ = worldPos.xz;
                o.localV  = t;
                return o;
            }

            fixed4 rockFrag(v2r i) : SV_Target
            {
                // World-space tiled rock texture
                float2 rockUV = i.worldXZ / _RockTexScale;
                fixed4 rock   = tex2D(_RockTex, rockUV) * _RockColor;

                // Strata bands — smooth sin-based repeating darkening
                float bandT    = frac(i.localV * _StrataCount);
                float bandEdge = pow(sin(bandT * 3.14159), 1.0 / _StrataSharpness);
                float darkMult = 1.0 - bandEdge * _StrataDarken;

                // Bottom alpha fade
                float bottomFade = 1.0 - saturate(
                    pow(saturate((i.localV - _BottomFadeStart) / (1.0 - _BottomFadeStart + 0.001)),
                        _BottomFadeSharpness));

                return fixed4(rock.rgb * darkMult, rock.a * bottomFade);
            }
            ENDCG
        }

        // ─────────────────────────────────────────────────────────────
        // Pass 3 — Grass Edge
        // Renders the grass fronds on top of the rock face.
        // UV.y=1 is the outer perimeter (frond attachment point).
        // Droop pushes those vertices along local +Z (world -Y) so fronds
        // hang off the island edge, matching GrassEdge behaviour.
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "GRASS"

            CGPROGRAM
            #pragma vertex   grassVert
            #pragma fragment grassFrag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;

            float     _Droop;
            float     _DroopStart;

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

            struct appdata_g
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            struct v2g
            {
                float4 pos      : SV_POSITION;
                fixed4 color    : COLOR;
                float2 baseUV   : TEXCOORD0;
                float2 worldXZ  : TEXCOORD1;
            };

            v2g grassVert(appdata_g v)
            {
                // UV.y=1 is the outer perimeter frond tip, UV.y=0 is the inner seam.
                // Droop pushes the tip along local +Z (world -Y for rotation 90,0,0)
                // so fronds hang downward off the island edge.
                float droopFactor = saturate(v.uv.y) * saturate(1.0 - _DroopStart);

                float3 worldPos   = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 extrudeDir = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
                worldPos += extrudeDir * (_Droop * droopFactor);

                v2g o;
                o.pos     = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.color   = v.color * _Color;
                o.baseUV  = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldXZ = worldPos.xz;
                return o;
            }

            fixed4 grassFrag(v2g i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.baseUV) * i.color;

                float2 windDir    = normalize(_WindDirection.xz + float2(0.001, 0));
                float  windDrift  = _Time.y * _WindSpeed;
                float2 windOffset = windDir * windDrift * _WindStrength
                                  + sin(_Time.y * _WindSpeed * 0.7 + i.worldXZ.x * 0.5) * _WindStrength * float2(0.5, 0.5);

                float2 detailUV   = i.worldXZ / _DetailScale + windOffset;
                fixed4 detail     = tex2D(_DetailTex, detailUV);
                fixed3 withDetail = lerp(base.rgb, base.rgb * detail.rgb * 2.0, _DetailStrength);

                float2 varUV      = i.worldXZ / _VariationScale;
                float  varNoise   = tex2D(_VariationTex, varUV).r;
                fixed3 varColour  = lerp(_VariationDark.rgb, _VariationLight.rgb, varNoise);
                fixed3 varied     = lerp(withDetail, withDetail * varColour, _VariationStrength);

                return fixed4(varied, base.a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
