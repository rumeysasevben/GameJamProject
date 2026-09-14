// A sprite seen in the water: drawn flipped by WaterReflection, then rippled
// sideways and broken into faint bands that drift down, fading out the further
// it gets from the waterline. Unlit, so it reads at night.
Shader "Fener/WaterReflection"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Amplitude ("Ripple Width", Range(0, 0.1)) = 0.018
        _Frequency ("Ripple Count", Float) = 26
        _Speed ("Ripple Speed", Float) = 2.2
        _BandCount ("Shimmer Bands", Float) = 34
        _BandSpeed ("Shimmer Speed", Float) = 1.6
        _BandDepth ("Shimmer Depth", Range(0, 1)) = 0.45
        _Fade ("Fade With Depth", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Amplitude;
            float _Frequency;
            float _Speed;
            float _BandCount;
            float _BandSpeed;
            float _BandDepth;
            float _Fade;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // The sprite is flipped, so its bottom edge (uv.y = 0) sits on
                // the waterline and uv.y grows with depth.
                float depth = saturate(i.uv.y);

                // Sideways ripple, calm at the waterline and wider below it.
                float wobble = sin(i.uv.y * _Frequency - _Time.y * _Speed) * _Amplitude * (0.35 + depth);
                fixed4 c = tex2D(_MainTex, float2(i.uv.x + wobble, i.uv.y)) * i.color;

                // Bands of light drifting down through the reflection.
                float band = 0.5 + 0.5 * sin(i.uv.y * _BandCount - _Time.y * _BandSpeed * 6.2831);
                c.a *= 1.0 - _BandDepth * band;

                // Fading out as it goes deeper.
                c.a *= 1.0 - _Fade * depth;
                return c;
            }
            ENDCG
        }
    }
}
