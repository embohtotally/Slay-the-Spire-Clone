Shader "Gameseed/FrierenShield/HexBarrier"
{
    // Frieren's Ordinary Defense Magic - Layer 1: golden hexagonal honeycomb barrier.
    // Fully analytic (zero textures): hex grid, facet gradient, Fresnel rim, localized
    // hit ripple wave (spec formula), radial geometric deploy reveal, staggered per-tile
    // dissolve, and crystalline shatter fragmentation. Driven per-instance via MPB.
    Properties
    {
        [HDR] _BaseColor ("Base Color (Amber Core)", Color) = (1.0, 0.878, 0.40, 1)   // #FFE066
        [HDR] _EdgeColor ("Edge Color (Gold Rim)", Color) = (1.0, 0.70, 0.0, 1)       // #FFB300
        _Intensity ("Intensity", Float) = 3.2
        _HexScale ("Hex Grid Scale", Float) = 8
        _EdgeThickness ("Edge Thickness", Range(0.01, 0.3)) = 0.10
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _FresnelBias ("Fresnel Bias", Range(0, 1)) = 0.06
        _HitPosOS ("Hit Position OS", Vector) = (0, 0, 0, 0)
        _HitTime ("Hit Time (seconds since hit)", Float) = 999
        _HitIntensity ("Hit Intensity", Float) = 0
        _RippleSpeed ("Ripple Speed", Float) = 1.6
        _RippleSharpness ("Ripple Sharpness", Float) = 14
        _RippleDamping ("Ripple Damping", Float) = 2.6
        _DeployT ("Deploy T", Range(0, 1)) = 1
        _DissolveT ("Dissolve T", Range(0, 1)) = 0
        _ShatterT ("Shatter T (crystalline fragmentation)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+120" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FrierenHexBarrier"
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
                float4 _BaseColor, _EdgeColor;
                float _Intensity, _HexScale, _EdgeThickness, _FresnelPower, _FresnelBias;
                float4 _HitPosOS;
                float _HitTime, _HitIntensity, _RippleSpeed, _RippleSharpness, _RippleDamping;
                float _DeployT, _DissolveT, _ShatterT;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            // --- Regular hexagonal grid coordinates (spec formula, verbatim) ---
            float2 HexCoords(float2 uv, float scale, out float2 hexIndex)
            {
                float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = fmod(uv * scale, r) - h;
                float2 b = fmod(uv * scale - h, r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                hexIndex = floor(uv * scale - gv);
                return gv; // Centered local coordinates [-0.5, 0.5]
            }

            float HexDist(float2 p)
            {
                p = abs(p);
                return max(dot(p, normalize(float2(1.0, 1.7320508))), p.x);
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.positionOS.xy;              // mesh-space plane coords (unit disc, radius 0.5)
                float r01 = saturate(length(p) * 2.0);    // 0 center .. 1 rim
                float t = _Time.y;

                // --- Primary hex evaluation: cell identity drives shatter direction / stagger ---
                float2 idx0;
                float2 gv0 = HexCoords(p, _HexScale, idx0);
                float cellHash = hash21(idx0 * 0.73 + 17.17);
                float2 cellCenter = p * _HexScale - gv0;

                // --- Crystalline shatter: per-cell radial scatter + gravity + staggered fade ---
                float shatterFade = 1.0 - smoothstep(cellHash, cellHash + 0.25, _ShatterT);
                float2 scattered = p;
                if (_ShatterT > 0.0001)
                {
                    float2 dir = cellCenter / (length(cellCenter) + 1e-4);
                    float prog = pow(_ShatterT, 1.4);
                    scattered = p + dir * prog * (0.22 + 0.30 * cellHash)
                                  + float2(0.0, -0.55) * prog * prog;
                }

                // --- Secondary hex evaluation at (possibly scattered) coords: visible pattern ---
                float2 hexIndex;
                float2 gv = HexCoords(scattered, _HexScale, hexIndex);
                float hd = HexDist(gv);                    // 0 cell center .. 0.5 cell edge
                float edgeHash = hash21(hexIndex * 1.31 + 3.7);

                // Faceted interior: dim center, brighter toward cell edge (honeycomb depth)
                float facet = lerp(0.30, 0.95, smoothstep(0.02, 0.42, hd));
                facet *= 0.85 + 0.30 * hash21(hexIndex + floor(t * 2.0));   // per-cell energy surge

                // Energetic boundaries
                float edge = smoothstep(0.5 - _EdgeThickness, 0.5 - _EdgeThickness * 0.30, hd);
                float edgeGlow = edge * (0.75 + 0.45 * sin(t * 5.0 + edgeHash * 6.28318));

                // --- Localized hit ripple wave (spec formula, verbatim) ---
                float distToHit = length(IN.positionOS.xy - _HitPosOS.xy);
                float waveFront = _HitTime * _RippleSpeed;
                float waveDelta = abs(distToHit - waveFront);
                float ripple = exp(-waveDelta * _RippleSharpness) * exp(-_HitTime * _RippleDamping) * _HitIntensity;

                // Localized bright flare at the exact impact point (Layer 3, part 1)
                float flare = exp(-distToHit * distToHit * 90.0) * exp(-_HitTime * 6.0) * _HitIntensity;

                // Fresnel rim + structural rim arc toward the disc boundary
                float3 viewDir = normalize(GetCurrentViewPosition() - IN.positionWS);
                float facing = saturate(dot(normalize(IN.normalWS), viewDir));
                float fres = _FresnelBias + pow(1.0 - facing, _FresnelPower);
                float rimArc = smoothstep(0.62, 0.98, r01);

                // Deploy: radial geometric reveal, cells pop in from the center outward
                float deployR = _DeployT * 1.12;
                float alive = smoothstep(deployR, deployR - 0.10, r01 + cellHash * 0.05);
                float revealFront = smoothstep(deployR + 0.08, deployR - 0.02, r01)
                                  * smoothstep(deployR - 0.18, deployR - 0.06, r01)
                                  * (1.0 - _DeployT);

                // Dissolve: staggered per-tile fade with a bright ember flash per dying tile
                float dTh = _DissolveT * 1.15;
                float aliveD = 1.0 - smoothstep(cellHash, cellHash + 0.16, dTh);
                float ember = smoothstep(cellHash, cellHash + 0.05, dTh)
                            * (1.0 - smoothstep(cellHash + 0.10, cellHash + 0.16, dTh));

                float vis = alive * aliveD * shatterFade;
                if (vis <= 0.0001) return half4(0, 0, 0, 0);

                float hitBoost = 1.0 + ripple * 2.2 + flare * 2.5;
                float burst = (1.0 - _ShatterT) * (1.0 - _ShatterT) * 1.5;   // shatter ignition flash

                float3 col = _BaseColor.rgb * facet
                           + _EdgeColor.rgb * (edgeGlow * (1.0 + ripple * 3.0) + fres * 0.9 + rimArc * 0.55)
                           + _BaseColor.rgb * flare * 1.6;

                float intensity = _Intensity * vis * hitBoost * (1.0 + ember * 2.0 + revealFront * 2.5 + burst);
                return half4(col * intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
