Shader "Gameseed/Spellbeam/MagicCircle"
{
    Properties
    {
        [HDR] _CircleColor ("Circle Color", Color) = (1, 0.5, 0.85, 1)
        _CircleIntensity ("Circle Intensity", Float) = 4
        _RingASpeed ("Ring A Rotation", Float) = 0.55
        _RingBSpeed ("Ring B Rotation", Float) = -0.85
        _TickA ("Ring A Tick Count", Float) = 18
        _TickB ("Ring B Tick Count", Float) = 9
        _RingWidthA ("Ring A Width", Float) = 0.045
        _RingWidthB ("Ring B Width", Float) = 0.035
        _HexRadius ("Hexagram Radius", Float) = 0.46
        _ChargeT ("Charge T", Range(0, 1)) = 0
        _DissipateT ("Dissipate T", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+130" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "SpellbeamMagicCircle"
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
                float4 _CircleColor;
                float _CircleIntensity, _RingASpeed, _RingBSpeed, _TickA, _TickB;
                float _RingWidthA, _RingWidthB, _HexRadius;
                float _ChargeT, _DissipateT, _Seed;
            CBUFFER_END

            #define PI  3.14159265359

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
                float a01 = frac(ang / 6.28318530718 + 1.0) * 6.28318530718;
                float t = _Time.y + _Seed * 17.0;

                float aA = ang + t * _RingASpeed;
                float bandA = smoothstep(_RingWidthA, 0.0, abs(r - 0.86));
                float ticksA = 0.55 + 0.45 * pow(abs(sin(aA * _TickA * 0.5)), 0.5);
                float ringA = bandA * ticksA;

                float aB = ang + t * _RingBSpeed;
                float bandB = smoothstep(_RingWidthB, 0.0, abs(r - 0.66));
                float ticksB = 0.6 + 0.4 * pow(abs(sin(aB * _TickB * 0.5)), 0.6);
                float ringB = bandB * ticksB;

                const float SIXTH = PI / 6.0;
                float f1 = abs(fmod(a01 + SIXTH, 2.0 * SIXTH) - SIXTH);
                float f2 = abs(fmod(a01 + 2.0 * SIXTH, 2.0 * SIXTH) - SIXTH);
                float hex1 = smoothstep(0.028, 0.0, abs(r - _HexRadius / max(cos(f1), 0.72)));
                float hex2 = smoothstep(0.022, 0.0, abs(r - _HexRadius * 0.62 / max(cos(f2), 0.72)));
                float hex = max(hex1, hex2) * 0.85;

                float coreGlow = exp(-r * 5.0) * 0.6;
                float pat = max(max(ringA, ringB), hex) + coreGlow;

                float chargeR = _ChargeT * 1.12;
                float reveal = smoothstep(chargeR, chargeR - 0.08, r);
                float edge = smoothstep(chargeR + 0.10, chargeR - 0.02, r)
                           * smoothstep(chargeR - 0.16, chargeR - 0.02, r);
                float pulse = lerp(0.65 + 0.35 * sin(t * 16.0), 1.0, _ChargeT);
                float fade = 1.0 - _DissipateT;

                float intensity = _CircleIntensity * (pat * reveal + edge * 1.8) * pulse * fade;
                return half4(_CircleColor.rgb * intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
