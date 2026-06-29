Shader "Custom/URP_2D_SimpleWind"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WindSpeed ("Wind Speed", Range(0, 10)) = 2.0
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.5
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        // These tags are critical for Unity to treat this as a 2D Sprite
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        // Standard 2D Sprite rendering settings
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;     // Captures the SpriteRenderer's Color tint
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _WindSpeed;
                float _WindStrength;
                float4 _WindDirection;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 1. Calculate phase using world X position to desync the swaying
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float phase = _Time.y * _WindSpeed + worldPos.x; 

                // 2. Generate the sine wave
                float windSine = sin(phase);

                // 3. The 2D Mask
                // A sprite is just a flat quad. UV.y = 0 is the bottom edge, UV.y = 1 is the top edge.
                // This ensures the bottom of your crop sprite stays planted.
                float mask = IN.uv.y;

                // 4. Calculate and apply displacement
                float3 displacement = _WindDirection.xyz * windSine * _WindStrength * mask;
                float3 newPosOS = IN.positionOS.xyz + displacement;

                OUT.positionCS = TransformObjectToHClip(newPosOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                
                // Pass the SpriteRenderer color to the fragment shader
                OUT.color = IN.color * _Color; 

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Sample the sprite texture and multiply it by the vertex color (tint)
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                
                // Optional: Multiply RGB by Alpha for premultiplied alpha blending
                // color.rgb *= color.a; 
                
                return color;
            }
            ENDHLSL
        }
    }
}