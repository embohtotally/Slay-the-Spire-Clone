Shader "Gameseed/Spellbeam/BeamSheath"
{
    Properties
    {
        [HDR] _SheathColor ("Sheath Color", Color) = (0.68, 0.18, 1.0, 1)
        _SheathIntensity ("Sheath Intensity", Float) = 1.35
        _Arms ("Helix Arms", Float) = 3
        _TwistTiles ("Helix Twist Tiles", Float) = 2.8
        _HelixSpeed ("Helix Speed", Float) = 9
        _TendrilSharp ("Tendril Sharpness", Range(1, 16)) = 3.5
        _TendrilStrength ("Tendril Strength", Range(0, 2)) = 1.15
        _RingTiles ("Ring Tiles", Float) = 12
        _RingSpeed ("Ring Speed", Float) = 26
        _RingStrength ("Ring Strength", Range(0, 2)) = 0.75
        _NoiseFrequency ("Axial Noise Frequency", Float) = 5
        _FlowSpeed ("Flow Speed", Float) = 7
        _HeadT ("Head T", Range(0, 1)) = 0
        _TailT ("Tail T", Range(0, 1)) = 0
        _PowerT ("Power T", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+115" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "SpellbeamSheath"
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
                float4 _SheathColor;
                float _SheathIntensity, _Arms, _TwistTiles, _HelixSpeed, _TendrilSharp, _TendrilStrength;
                float _RingTiles, _RingSpeed, _RingStrength, _NoiseFrequency, _FlowSpeed;
                float _HeadT, _TailT, _PowerT, _Seed;
            CBUFFER_END

            #define TAU 6.28318530718

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0;
                                float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float axial : TEXCOORD3; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = IN.uv;
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
                float t = _Time.y + _Seed * 29.0;

                float ang = IN.uv.x * TAU;
                float helix = sin(ang * _Arms + IN.axial * _TwistTiles * TAU - t * _HelixSpeed);
                float tendril = pow(saturate(helix), _TendrilSharp)
                              * (0.45 + 0.55 * vnoise(IN.axial * _NoiseFrequency - t * _FlowSpeed));

                float rings = pow(saturate(sin(IN.axial * _RingTiles * TAU - t * _RingSpeed)), 7.0) * _RingStrength;

                float mask = saturate(max(tendril * _TendrilStrength, rings));
                float body = smoothstep(_TailT - 0.06, _TailT + 0.02, IN.axial)
                           * (1.0 - smoothstep(_HeadT - 0.03, _HeadT, IN.axial));

                // Soft feathered silhouette so the outer plasma sheath wraps around the white core with vivid violet color
                float feather = smoothstep(0.0, 0.35, facing);
                float3 col = lerp(_SheathColor.rgb * 0.72, lerp(_SheathColor.rgb, float3(1.0, 0.78, 1.0), 0.38), mask);
                col *= clamp(_SheathIntensity, 0.8, 1.6);

                float alpha = saturate((0.20 + 0.68 * mask) * feather * body * _PowerT);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
