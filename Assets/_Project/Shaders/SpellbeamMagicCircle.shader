Shader "Gameseed/Spellbeam/MagicCircle"
{
    Properties
    {
        [HDR] _CircleColor ("Circle Color", Color) = (0.95, 0.28, 0.88, 1)
        _CircleIntensity ("Circle Intensity", Float) = 1.45
        _RingASpeed ("Ring A Rotation", Float) = 0.75
        _RingBSpeed ("Ring B Rotation", Float) = -1.05
        _TickA ("Ring A Tick Count", Float) = 24
        _TickB ("Ring B Tick Count", Float) = 12
        _RingWidthA ("Ring A Width", Float) = 0.038
        _RingWidthB ("Ring B Width", Float) = 0.028
        _HexRadius ("Hexagram Radius", Float) = 0.48
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
                if (r > 1.0) discard;

                float ang = atan2(p.y, p.x);
                float a01 = frac(ang / 6.28318530718 + 1.0) * 6.28318530718;
                float t = _Time.y + _Seed * 17.0;

                // Outer arcane rune ring + crisp inner border lines
                float aA = ang + t * _RingASpeed;
                float bandA = smoothstep(_RingWidthA, 0.004, abs(r - 0.86));
                float hairlineOuter = smoothstep(0.016, 0.002, abs(r - 0.93));
                float ticksA = 0.40 + 0.60 * pow(abs(sin(aA * _TickA * 0.5)), 0.45);
                float ringA = max(bandA * ticksA, hairlineOuter * 0.85);

                // Counter-rotating inner sigil ring
                float aB = ang + t * _RingBSpeed;
                float bandB = smoothstep(_RingWidthB, 0.004, abs(r - 0.66));
                float ticksB = 0.45 + 0.55 * pow(abs(sin(aB * _TickB * 0.5)), 0.55);
                float ringB = bandB * ticksB;

                // Crisp double hexagram star
                const float SIXTH = PI / 6.0;
                float rotA = frac((ang + t * 0.45) / 6.28318530718 + 1.0) * 6.28318530718;
                float f1 = abs(fmod(rotA + SIXTH, 2.0 * SIXTH) - SIXTH);
                float f2 = abs(fmod(rotA + 2.0 * SIXTH, 2.0 * SIXTH) - SIXTH);
                float hex1 = smoothstep(0.026, 0.004, abs(r - _HexRadius / max(cos(f1), 0.72)));
                float hex2 = smoothstep(0.020, 0.003, abs(r - _HexRadius * 0.62 / max(cos(f2), 0.72)));
                float hex = max(hex1, hex2) * 0.95;

                // Delicate center bloom & soft outer halo
                float coreGlow = exp(-r * 5.8) * 0.55;
                float softHalo = smoothstep(1.0, 0.76, r) * smoothstep(0.55, 0.86, r) * 0.24;
                float sharpLines = max(max(ringA, ringB), hex);

                float chargeR = _ChargeT * 1.08;
                float reveal = smoothstep(chargeR, chargeR - 0.08, r);
                float edge = smoothstep(chargeR + 0.06, chargeR - 0.01, r)
                           * smoothstep(chargeR - 0.10, chargeR - 0.01, r) * 0.85;
                float pulse = lerp(0.80 + 0.20 * sin(t * 14.0), 1.0, _ChargeT);
                float fade = 1.0 - _DissipateT;

                float alpha = saturate(((sharpLines + coreGlow + softHalo) * reveal + edge) * pulse * fade);

                // Deep vibrant violet-magenta aura with crisp white-hot rune cores
                float3 deepTint = _CircleColor.rgb * 0.62;
                float3 brightRune = lerp(_CircleColor.rgb * 1.15, float3(1.0, 0.94, 1.0), saturate(sharpLines * 0.78 + coreGlow * 0.6));
                float3 col = lerp(deepTint, brightRune, saturate(sharpLines + coreGlow + edge)) * clamp(_CircleIntensity, 0.9, 1.8);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
