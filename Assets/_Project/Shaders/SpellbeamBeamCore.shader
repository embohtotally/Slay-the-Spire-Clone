Shader "Gameseed/Spellbeam/BeamCore"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color", Color) = (1, 0.35, 0.75, 1)
        [HDR] _HotColor ("Hot Color", Color) = (1, 1, 1, 1)
        _CoreIntensity ("Core Intensity", Float) = 6
        _CorePower ("Core Facing Power", Range(0.5, 8)) = 3
        _NoiseFrequency ("Axial Noise Frequency", Float) = 6
        _FlowSpeed ("Flow Speed (world-tiles/s)", Float) = 9
        _ErosionLo ("Erosion Low", Range(0, 1)) = 0.18
        _ErosionHi ("Erosion High", Range(0, 1)) = 0.62
        _HeadFlare ("Head Flare", Float) = 2.2
        _Flicker ("Flicker", Range(0, 1)) = 0.15
        _HeadT ("Head T", Range(0, 1)) = 0
        _TailT ("Tail T", Range(0, 1)) = 0
        _PowerT ("Power T", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+120" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "SpellbeamCore"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor, _HotColor;
                float _CoreIntensity, _CorePower, _NoiseFrequency, _FlowSpeed;
                float _ErosionLo, _ErosionHi, _HeadFlare, _Flicker;
                float _HeadT, _TailT, _PowerT, _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0;
                                float3 normalWS : TEXCOORD1; float axial : TEXCOORD2; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.axial = IN.positionOS.z;
                return OUT;
            }

            float hash11(float p) { p = frac(p * 0.1031); p *= p + 33.33; p *= p + p; return frac(p); }
            float vnoise(float x)
            {
                float i = floor(x), f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(hash11(i), hash11(i + 1.0), u);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCurrentViewPosition() - IN.positionWS);
                float facing = saturate(dot(normalize(IN.normalWS), viewDir));
                float t = _Time.y + _Seed * 37.0;

                float n = vnoise(IN.axial * _NoiseFrequency - t * _FlowSpeed) * 0.65
                        + vnoise(IN.axial * _NoiseFrequency * 2.7 - t * _FlowSpeed * 1.6 + 11.31) * 0.35;
                float erosion = smoothstep(_ErosionLo, _ErosionHi, n);
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(t * 61.0 + IN.axial * 40.0));

                float body = smoothstep(_TailT - 0.04, _TailT + 0.01, IN.axial)
                           * (1.0 - smoothstep(_HeadT - 0.015, _HeadT, IN.axial));

                float core = pow(facing, _CorePower);
                float headFlare = _HeadFlare * pow(saturate(1.0 - abs(_HeadT - IN.axial) / 0.12), 3.0);

                float3 col = _CoreColor.rgb * (0.35 + 0.65 * facing) + _HotColor.rgb * core;
                float intensity = _CoreIntensity * lerp(0.25, 1.0, erosion) * flicker * body * _PowerT
                                + _CoreIntensity * headFlare * core * body * _PowerT;

                return half4(col * intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
