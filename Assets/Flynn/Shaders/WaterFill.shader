Shader "Custom/WaterFill"
{
    Properties
    {
        _MainTex        ("Fill Texture",    2D)     = "white" {}
        _Color          ("Tint",            Color)  = (1,1,1,1)
        _ScrollX        ("Scroll Speed X",  Float)  = 0.05
        _ScrollY        ("Scroll Speed Y",  Float)  = 0.02
        _WaveAmplitude  ("Wave Amplitude",  Float)  = 0.03
        _WaveFrequency  ("Wave Frequency",  Float)  = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
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
            float     _ScrollX;
            float     _ScrollY;
            float     _WaveAmplitude;
            float     _WaveFrequency;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.color    = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // Continuous scroll
                uv.x += _ScrollX * _Time.y;
                uv.y += _ScrollY * _Time.y;

                // Subtle sine wave on the Y axis
                uv.y += sin(uv.x * 6.2831 + _Time.y * _WaveFrequency) * _WaveAmplitude;

                fixed4 col = tex2D(_MainTex, uv) * i.color;
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
