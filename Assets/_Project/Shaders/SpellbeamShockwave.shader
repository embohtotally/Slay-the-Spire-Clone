Shader "Gameseed/Spellbeam/Shockwave"
{
    Properties
    {
        [HDR] _RingColor ("Ring Color", Color) = (0.95, 0.28, 0.90, 1)
        [HDR] _FlashColor ("Flash Color", Color) = (1, 0.96, 1, 1)
        _RingIntensity ("Ring Intensity", Float) = 1.4
        _FlashIntensity ("Flash Intensity", Float) = 1.6
        _RingWidth ("Ring Width", Range(0.02, 0.3)) = 0.09
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
            Blend SrcAlpha OneMinusSrcAlpha
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
                if (r > 1.0) discard;

                float ang = atan2(p.y, p.x);
                float R = _RingT;

                // Crisp expanding magenta-violet shockwave ring
                float ringMask = smoothstep(_RingWidth * (0.30 + R * 0.75), 0.005, abs(r - R))
                               * pow(saturate(1.0 - R), 1.35);

                // Sharp anime impact starlight rays
                float streakRays = pow(abs(sin(ang * _StreakCount + _Seed * 13.0)), 18.0)
                                 * smoothstep(0.95, 0.08, r)
                                 * pow(saturate(1.0 - _FlashT), 1.5);

                // Tight, punchy white-hot center impact spark (never a giant flat grey blob!)
                float flashCore = pow(saturate(1.0 - _FlashT), 2.2) * pow(saturate(1.0 - r * 1.45), 3.0);

                float alpha = saturate(ringMask * 0.95 + streakRays * 0.85 + flashCore);
                if (alpha <= 0.004) discard;

                float3 ringCol = lerp(_RingColor.rgb, _FlashColor.rgb, saturate(ringMask * 0.45)) * clamp(_RingIntensity, 0.9, 1.8);
                float3 flashCol = lerp(_RingColor.rgb, _FlashColor.rgb, saturate(flashCore * 0.85 + 0.25)) * clamp(_FlashIntensity, 1.0, 2.0);
                float3 col = lerp(ringCol, flashCol, saturate(flashCore + streakRays * 0.6));

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
