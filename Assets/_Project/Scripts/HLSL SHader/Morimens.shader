Shader "Custom/IdleBreathing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Speed ("Breathing Speed", Float) = 2.0
        _Amount ("Movement Amount", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Speed;
            float _Amount;

            v2f vert (appdata v)
            {
                v2f o;
                
                // Calculate the sine wave based on time
                // _Time.y is time in seconds since the game started
                float wave = sin(_Time.y * _Speed); 
                
                // 1. Bobbing: Move the whole mesh up and down
                v.vertex.y += wave * _Amount;

                // 2. Stretching (Optional): Make the top vertices move more than the bottom
                // Assuming the pivot is at the bottom (y = 0)
                // v.vertex.y += v.vertex.y * wave * (_Amount * 0.5);

                // Convert from object space to screen space
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}