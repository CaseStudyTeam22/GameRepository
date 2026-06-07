// 舞台マス用の URP トゥーンシェーダ。明暗を段階化したセル塗りに、
// 画面空間のハーフトーン網点を重ねる。
// 明マス・暗マスは _BaseColor の違いで使い分け、同一シェーダを共有する。
// 輪郭線はこのシェーダでは描かず、画面空間の後処理（Outline.shader）で描く。
//
// セル塗り    : 主光源の NdotL を _CelSteps 段に量子化する。
// ハーフトーン: 画面空間の格子で網点を生成。_DotColor で色を変えられ、
//               _DotRangeMin/_DotRangeMax で適用する明度帯を絞り、
//               _DotJitter でノイズによる揺らぎを加える。
Shader "GamblingAction/ToonTile"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1)

        [Header(Cel Shading)]
        _CelSteps ("Cel Steps", Range(1, 6)) = 3
        _ShadowTint ("Shadow Tint", Color) = (0.45, 0.45, 0.55, 1)

        [Header(Halftone)]
        _DotColor ("Dot Color", Color) = (0, 0, 0, 1)
        _DotScale ("Dot Density", Float) = 80
        _DotRangeMin ("Dot Range Min", Range(0, 1)) = 0.0
        _DotRangeMax ("Dot Range Max", Range(0, 1)) = 0.5
        _DotSoftness ("Dot Softness", Range(0.01, 0.5)) = 0.1
        _DotJitter ("Dot Jitter", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // 本体パス。セル塗りにハーフトーンを重ねる。
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _CelSteps;
                float4 _ShadowTint;
                float4 _DotColor;
                float  _DotScale;
                float  _DotRangeMin;
                float  _DotRangeMax;
                float  _DotSoftness;
                float  _DotJitter;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // 画面座標から疑似乱数を作る。網点の揺らぎに使う。
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 主光源の NdotL を段階化したセル塗り。
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;

                float steps = max(_CelSteps, 1.0);
                float celLit = floor(ndotl * steps) / steps;

                half3 litColor = _BaseColor.rgb * mainLight.color;
                half3 surface = lerp(_ShadowTint.rgb * _BaseColor.rgb, litColor, celLit);

                // 明度を求め、網点を適用する帯を _DotRangeMin/_DotRangeMax で絞る。
                float luminance = dot(surface, float3(0.299, 0.587, 0.114));
                float band = 1.0 - smoothstep(_DotRangeMin, _DotRangeMax, luminance);

                // 画面空間の格子で網点を作る。中心からの距離を閾値と比較する。
                float2 screenUV = IN.positionHCS.xy;
                float2 cell = screenUV / _DotScale;
                float2 cellId = floor(cell);
                float2 cellUV = frac(cell) - 0.5;
                // セルごとの乱数で点の大きさを揺らし、機械的な均一さを崩す。
                float jitter = (hash21(cellId) - 0.5) * _DotJitter;
                float radius = saturate(band + jitter);
                float dist = length(cellUV);
                float dot = 1.0 - smoothstep(radius - _DotSoftness, radius, dist);

                half3 finalColor = lerp(surface, _DotColor.rgb, dot * band);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
