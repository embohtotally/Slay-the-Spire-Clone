Shader "Gameseed/FrierenShield/Circle"
{
    // Frieren's Ordinary Defense Magic - Layer 2: golden magic circle backing plane.
    // Analytic polar runes: counter-rotating ticked outer ring + rune ring, concentric
    // circles, orbiting planetary teeth, soft circular mask, deploy reveal, dissolve/shatter fade.
    Properties
    {
        [HDR] _CircleColor ("Circle Color", Color) = (1.0, 0.85, 0.45, 1)
        _CircleIntensity ("Circle Intensity", Float) = 3.5
        _OuterSpeed ("Outer Ring Speed (CW)", Float) = 0.45
        _InnerSpeed ("Rune Ring Speed (CCW)", Float) = -0.70
        _TickCount ("Outer Tick Count", Float) = 24
        _RuneCount ("Rune Count", Float) = 9
        _OuterRadius ("Outer Ring Radius", Float) = 0.86
        _InnerRadius ("Rune Ring Radius", Float) = 0.64
        _OuterWidth ("Outer Ring Width", Float) = 0.045
        _InnerWidth ("Rune Ring Width", Float) = 0.06
        _DeployT ("Deploy T", Range(0, 1)) = 1
        _DissolveT ("Dissolve T", Range(0, 1)) = 0
        _ShatterT ("Shatter T", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+120" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FrierenShieldCircle"
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
                float _CircleIntensity, _OuterSpeed, _InnerSpeed, _TickCount, _RuneCount;
                float _OuterRadius, _InnerRadius, _OuterWidth, _InnerWidth;
                float _DeployT, _DissolveT, _ShatterT, _Seed;
            CBUFFER_END

            #define TAU 6.28318530718

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float t = _Time.y + _Seed * 19.0;

                // --- Outer ticked ring (rotates one way) ---
                float aO = ang + t * _OuterSpeed;
                float ticks = 0.45 + 0.55 * pow(abs(sin(aO * _TickCount * 0.5)), 0.55);
                float ringO = smoothstep(_OuterWidth, 0.0, abs(r - _OuterRadius)) * ticks;

                // --- Inner rune ring (counter-rotates; per-segment glyph widths/strokes) ---
                float aI = ang - t * _InnerSpeed;
                float segF = frac(aI / TAU * _RuneCount + 100.0);
                float segId = floor(aI / TAU * _RuneCount + 100.0);
                float h1 = hash11(segId * 0.917 + _Seed);
                float w = 0.22 + 0.55 * h1;
                float glyph = smoothstep(w, w - 0.10, segF) * smoothstep(0.0, 0.05, segF);
                float strokes = 0.45 + 0.55 * sin(segF * 12.566 * (0.6 + h1) + h1 * 6.2831);
                float ringI = smoothstep(_InnerWidth, 0.0, abs(r - _InnerRadius)) * (0.30 + 0.70 * glyph * strokes);

                // --- Concentric circles ---
                float c1 = smoothstep(0.014, 0.0, abs(r - 0.46));
                float c2 = smoothstep(0.010, 0.0, abs(r - 0.28));
                float rim = smoothstep(0.012, 0.0, abs(r - 0.965));

                // --- Planetary teeth: bright nodes orbiting the outer ring ---
                float teeth = 0.0;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float pa = t * _OuterSpeed + i * 2.0943951;
                    float d = abs(atan2(sin(aO - pa), cos(aO - pa)));
                    teeth += exp(-d * d * 260.0);
                }
                teeth *= smoothstep(_OuterWidth * 1.6, 0.0, abs(r - _OuterRadius)) * 1.6;

                float core = exp(-r * 5.5) * 0.55;
                float pat = ringO + ringI + c1 + c2 + rim + teeth + core;

                // --- Deploy reveal + dissolve/shatter fade + smooth circular alpha mask ---
                float revealR = _DeployT * 1.12;
                float reveal = smoothstep(revealR, revealR - 0.10, r);
                float edge = smoothstep(revealR + 0.10, revealR - 0.02, r)
                           * smoothstep(revealR - 0.16, revealR - 0.04, r)
                           * (1.0 - _DeployT);
                float fade = 1.0 - saturate(max(_DissolveT, _ShatterT) * 1.25);
                float mask = smoothstep(1.0, 0.965, r);

                float intensity = _CircleIntensity * (pat * reveal + edge * 1.8) * fade * mask;
                return half4(_CircleColor.rgb * intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
