Shader "Gameseed/Spellbeam/BeamCore"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color", Color) = (0.95, 0.22, 0.85, 1)
        [HDR] _HotColor ("Hot Color", Color) = (1, 0.98, 1, 1)
        _CoreIntensity ("Core Intensity", Float) = 1.6
        _CorePower ("Core Facing Power", Range(0.5, 8)) = 3.2
        _NoiseFrequency ("Axial Noise Frequency", Float) = 6
        _FlowSpeed ("Flow Speed (world-tiles/s)", Float) = 11
        _ErosionLo ("Erosion Low", Range(0, 1)) = 0.14
        _ErosionHi ("Erosion High", Range(0, 1)) = 0.58
        _HeadFlare ("Head Flare", Float) = 1.5
        _Flicker ("Flicker", Range(0, 1)) = 0.12
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
            Blend SrcAlpha OneMinusSrcAlpha
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
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(t * 58.0 + IN.axial * 40.0));

                float body = smoothstep(_TailT - 0.04, _TailT + 0.015, IN.axial)
                           * (1.0 - smoothstep(_HeadT - 0.018, _HeadT + 0.002, IN.axial));

                // Vibrant saturated magenta-violet rim transitioning cleanly into a razor-sharp white-hot core
                float hotCore = pow(facing, _CorePower);
                float softEdge = pow(facing, 0.72);
                float headFlare = _HeadFlare * pow(saturate(1.0 - abs(_HeadT - IN.axial) / 0.11), 2.5);

                float3 deepGlow = _CoreColor.rgb * 0.85;
                float3 midGlow = lerp(deepGlow, _CoreColor.rgb * 1.25, erosion);
                float3 col = lerp(midGlow, _HotColor.rgb, saturate(hotCore * 1.35 + headFlare * 0.55));
                col *= lerp(1.0, clamp(_CoreIntensity, 1.0, 2.2), hotCore);

                float alpha = saturate(softEdge * lerp(0.75, 1.0, erosion) * flicker * body * _PowerT + headFlare * hotCore * body * _PowerT * 0.6);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
