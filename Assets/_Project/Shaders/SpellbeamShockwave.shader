Shader "Gameseed/Spellbeam/Shockwave"
{
    Properties
    {
        [HDR] _RingColor ("Ring Color", Color) = (1, 0.6, 0.9, 1)
        [HDR] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _RingIntensity ("Ring Intensity", Float) = 7
        _FlashIntensity ("Flash Intensity", Float) = 10
        _RingWidth ("Ring Width", Range(0.02, 0.3)) = 0.10
        _StreakCount ("Angular Streaks", Float) = 12
        _RingT ("Ring T (eased)", Range(0, 1)) = 1
        _FlashT ("Flash T", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+140" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "SpellbeamShockwave"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RingColor, _FlashColor;
                float _RingIntensity, _FlashIntensity, _RingWidth, _StreakCount;
                float _RingT, _FlashT, _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);

                float R = _RingT;
                float ring = smoothstep(_RingWidth * (0.35 + R), 0.0, abs(r - R))
                           * pow(saturate(1.0 - R), 1.6);
                float streaks = pow(abs(sin(ang * _StreakCount + _Seed * 13.0)), 24.0) * 1.6;
                float flash = pow(saturate(1.0 - _FlashT), 2.5) * smoothstep(0.85, 0.0, r);

                float3 col = _RingColor.rgb * ring * (1.0 + streaks)
                           + _FlashColor.rgb * flash * _FlashIntensity;
                return half4(col * _RingIntensity, 1.0);
            }
            ENDHLSL
        }
    }
}
