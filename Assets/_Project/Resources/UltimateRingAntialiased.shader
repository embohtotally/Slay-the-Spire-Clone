Shader "PlushieOdyssey/UI/UltimateRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float4 ring : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float4 ring : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            float _UIVertexColorAlwaysGammaSpace;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_UIVertexColorAlwaysGammaSpace > 0)
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                #endif
                output.color = input.color * _Color;
                output.ring = input.ring;

                float2 pixelSize = output.position.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clipRect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.position.xy * 2 - clipRect.xy - clipRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float PixelWidth(float distance)
            {
                return max(length(float2(ddx(distance), ddy(distance))), 0.00001);
            }

            float EdgeCoverage(float distance)
            {
                return saturate(0.5 - distance / PixelWidth(distance));
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 position = input.ring.xy;
                float flags = floor(input.ring.z * 0.5);
                float innerRatio = input.ring.z - flags * 2;
                float sweep = abs(input.ring.w);
                float radius = length(position);
                float width = PixelWidth(radius);

                // Cover only the actual pixel boundary, not a fixed-width blur band.
                float coverage = saturate(0.5 + (1 - radius) / width);
                if (innerRatio > 0)
                    coverage -= saturate(0.5 + (innerRatio - radius) / width);

                if (sweep < 6.283085)
                {
                    position.x *= input.ring.w >= 0 ? 1 : -1;
                    float startDistance = -position.x;
                    float endDistance = dot(position, float2(cos(sweep), -sin(sweep)));
                    float start = fmod(flags, 2) > 0.5 ? EdgeCoverage(startDistance) : step(startDistance, 0);
                    float end = flags >= 2 ? EdgeCoverage(endDistance) : step(endDistance, 0);
                    coverage *= sweep <= 3.141593 ? min(start, end) : max(start, end);
                }

                fixed4 result = input.color;
                result.a *= saturate(coverage);
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                result.a *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
