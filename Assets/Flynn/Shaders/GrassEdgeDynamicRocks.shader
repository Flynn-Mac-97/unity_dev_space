// GrassEdge.shader
// Built-in render pipeline.
// Edge strip for a 2.5D SpriteShape: grass on top, rock sprite cards hanging from the seam.

Shader "Custom/GrassEdgeDynamicRocks"
{
    Properties
    {
        _MainTex            ("Base Texture (can be white)",     2D)     = "white" {}
        _Color              ("Base Tint",                       Color)  = (0.35, 0.72, 0.28, 1)

        [Header(Droop)]
        _Droop              ("Droop Amount",                    Float)  = 0.15
        _DroopStart         ("Droop Start (UV)",               Range(0,1)) = 0.2

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

        [Header(Drop Shadow)]
        _ShadowColor        ("Shadow Color",                    Color)  = (0.05, 0.05, 0.1, 0.45)
        _ShadowOffset       ("Shadow Offset (XY world units)",  Vector) = (0.08, -0.35, 0, 0)
        _ShadowDroopMult    ("Shadow Droop Multiplier",         Float)  = 1.2

        [Header(Rock Sprite Cards)]
        _RockSpriteTex      ("Rock Sprite Atlas (RGBA)",        2D)     = "white" {}
        _RockSpriteCols     ("Atlas Columns",                   Float)  = 4
        _RockSpriteRows     ("Atlas Rows",                      Float)  = 1
        _RockSpacing        ("Spacing Between Sprites (world)", Float)  = 0.7
        _RockSize           ("Sprite Size (world units)",       Float)  = 0.9
        _RockTopOffset      ("Vertical Offset from Seam",       Float)  = 0.0
        _RockYJitter        ("Random Vertical Jitter",          Float)  = 0.1
        _RockTint           ("Rock Tint",                       Color)  = (1,1,1,1)
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

        // ---------------------------------------------------------------
        // ROCK SPRITE CARDS
        // ---------------------------------------------------------------
        // Renders flat, front-facing rock sprite cards spaced evenly along
        // the top seam of the edge strip. No bending, no droop — they hang
        // straight down in world XY and follow the seam wherever it goes.
        Pass
        {
            Name "ROCK_SPRITES"

            CGPROGRAM
            #pragma vertex   rockVert
            #pragma fragment rockFrag
            #include "UnityCG.cginc"

            sampler2D _RockSpriteTex;
            float     _RockSpriteCols, _RockSpriteRows;
            float     _RockSpacing, _RockSize;
            float     _RockTopOffset, _RockYJitter;
            fixed4    _RockTint;

            struct appdata_r
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2r
            {
                float4 pos      : SV_POSITION;
                float2 worldXY  : TEXCOORD0;
                float  seamY    : TEXCOORD1; // world Y of the seam (top of strip) for this column
            };

            float hash11(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            v2r rockVert(appdata_r v)
            {
                // World position of THIS vertex (no droop, no warping).
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Reconstruct the seam world Y for this column:
                //   uv.y = 0 is the top seam, uv.y = 1 is the bottom of the strip.
                //   The strip is uniform height in object space, but we don't need
                //   the exact height — we just need a Y that follows the seam.
                //   Trick: pass the seam Y through by lifting the bottom verts up
                //   to where the top seam would be. Since the strip is thin,
                //   we approximate by: seamY = worldPos.y + uv.y * stripHeight.
                //   We don't know stripHeight here, so instead we just expand the
                //   strip's vertical extent ourselves and use the *top* row as the
                //   anchor for everything.
                //
                // Simplest robust approach: render the strip as a fixed-height
                // card hanging straight down from the seam. The seam itself is
                // wherever uv.y == 0 verts naturally are. We push the bottom
                // verts (uv.y == 1) down by _RockSize in WORLD space.

                // Anchor the top row at its natural position; push bottom row down.
                worldPos.y -= v.uv.y * _RockSize;
                // Optional offset
                worldPos.y -= _RockTopOffset;

                v2r o;
                o.pos     = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldXY = worldPos.xy;
                // seamY for this fragment = the top edge of THIS card = worldPos.y when uv.y was 0
                // i.e. worldPos.y + uv.y * _RockSize (undo the push we just did)
                o.seamY   = worldPos.y + v.uv.y * _RockSize;
                return o;
            }

            fixed4 rockFrag(v2r i) : SV_Target
            {
                // Which slot along X is this fragment in?
                float slotF  = i.worldXY.x / _RockSpacing;
                float slotI  = floor(slotF);
                float slotFr = slotF - slotI;          // 0..1 within the slot

                // Per-slot random values
                float r1 = hash11(slotI * 12.9898);    // atlas column
                float r2 = hash11(slotI * 78.233);     // atlas row
                float r3 = hash11(slotI * 39.425);     // Y jitter

                // Pick a random tile from the atlas
                float ai = floor(r1 * _RockSpriteCols);
                float aj = floor(r2 * _RockSpriteRows);

                // Local UV within the sprite card.
                //   X: slotFr already 0..1 across the slot, but we want the sprite
                //      centered with size _RockSize within a slot of width _RockSpacing.
                //   Y: based on distance from the seam (i.seamY) downward.
                float halfRatio = 0.5 * _RockSize / _RockSpacing;
                float spriteU   = (slotFr - 0.5) / (2.0 * halfRatio) + 0.5;

                // Y jitter shifts the card up/down a bit per slot.
                float yJit      = (r3 - 0.5) * 2.0 * _RockYJitter;
                float yFromTop  = (i.seamY - i.worldXY.y) + yJit;  // 0 at seam, grows downward
                float spriteV   = yFromTop / _RockSize;

                // Outside the sprite card? discard.
                if (spriteU < 0.0 || spriteU > 1.0 || spriteV < 0.0 || spriteV > 1.0)
                    discard;

                // Sample atlas tile.
                // spriteV=0 is the top of the card, but Unity UVs have V=0 at the
                // bottom, so flip it before indexing into the atlas.
                float2 atlasUV = (float2(spriteU, 1.0 - spriteV) + float2(ai, aj))
                               / float2(_RockSpriteCols, _RockSpriteRows);

                fixed4 tex = tex2D(_RockSpriteTex, atlasUV) * _RockTint;
                return tex;
            }
            ENDCG
        }

        // ---------------------------------------------------------------
        // DROP SHADOW
        // ---------------------------------------------------------------
        Pass
        {
            Name "SHADOW"

            CGPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _ShadowColor;
            float4    _ShadowOffset;
            float     _ShadowDroopMult;
            float     _Droop;
            float     _DroopStart;

            struct appdata_s { float4 vertex:POSITION; float2 texcoord:TEXCOORD0; };
            struct v2s       { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };

            v2s shadowVert(appdata_s v)
            {
                float droopFactor = saturate(v.texcoord.y) * saturate(1.0 - _DroopStart);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPos.y -= _Droop * droopFactor * _ShadowDroopMult;
                worldPos.x += _ShadowOffset.x;
                worldPos.y += _ShadowOffset.y;

                v2s o;
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv     = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 shadowFrag(v2s i) : SV_Target
            {
                float alpha = tex2D(_MainTex, i.uv).a;
                return fixed4(_ShadowColor.rgb, _ShadowColor.a * alpha);
            }
            ENDCG
        }

        // ---------------------------------------------------------------
        // GRASS
        // ---------------------------------------------------------------
        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
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
                float2 worldXZ   : TEXCOORD1;
            };

            v2f vert(appdata_t v)
            {
                float droopFactor = saturate(v.texcoord.y) * saturate(1.0 - _DroopStart);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPos.y -= _Droop * droopFactor;

                v2f o;
                o.vertex  = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.color   = v.color * _Color;
                o.baseUV  = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldXZ = worldPos.xz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.baseUV) * i.color;

                float2 windDir    = normalize(_WindDirection.xz + float2(0.001, 0));
                float  windDrift  = _Time.y * _WindSpeed;
                float2 windOffset = windDir * windDrift * _WindStrength
                                  + sin(_Time.y * _WindSpeed * 0.7 + i.worldXZ.x * 0.5) * _WindStrength * float2(0.5, 0.5);

                float2 detailUV  = i.worldXZ / _DetailScale + windOffset;
                fixed4 detail    = tex2D(_DetailTex, detailUV);
                fixed3 withDetail = lerp(base.rgb, base.rgb * detail.rgb * 2.0, _DetailStrength);

                float2 varUV     = i.worldXZ / _VariationScale;
                float  varNoise  = tex2D(_VariationTex, varUV).r;
                fixed3 varColour = lerp(_VariationDark.rgb, _VariationLight.rgb, varNoise);
                fixed3 varied    = lerp(withDetail, withDetail * varColour, _VariationStrength);

                return fixed4(varied, base.a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}