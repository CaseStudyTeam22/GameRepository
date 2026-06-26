// 空間を包む内壁用の URP unlit シェーダ。シーンに置いた球の内側に
// 正距円筒（equirectangular）のパノラマ画像を貼る。球の正面を裏返し
// （Cull Front）、内側からだけ見える内壁にする。
//
// UV は球メッシュの UV ではなく、物体空間の方向から経度・緯度で求める。
// こうすると頂点の UV の継ぎ目や極の収束に影響されず、どんな球でも
// パノラマ画像が継ぎ目なく貼れる。
//
// ライティングは受けない。空間の背景として表示するだけで、
// 影は落とさず受けもしない。大きさ・位置は球の Transform で調整する。
Shader "GamblingAction/SpaceDome"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (Equirectangular)", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        // パノラマ画像を水平に回す角度。空間の向きを合わせるのに使う。
        _Rotation ("Rotation", Range(0, 360)) = 0
        // 全体の色相をずらす量。0 でそのまま、1 で一周ぶん回す。
        _HueShift ("Hue Shift", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            // 正面を裏返し、球の内壁だけを描く。
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Rotation;
                float  _HueShift;
            CBUFFER_END

            // RGB と HSV を相互変換する。色相だけをずらすために使う。
            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 球の中心からの方向を経度・緯度に変換して正距円筒 UV を作る。
                float3 dir = normalize(IN.positionOS);
                float rot = radians(_Rotation);
                float u = (atan2(dir.z, dir.x) + rot) / (2.0 * PI) + 0.5;
                float v = asin(clamp(dir.y, -1.0, 1.0)) / PI + 0.5;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u, v));
                half4 col = tex * _Tint;

                // 色相を _HueShift ぶん回す。彩度・明度はそのまま保つ。
                float3 hsv = RgbToHsv(col.rgb);
                hsv.x = frac(hsv.x + _HueShift);
                col.rgb = HsvToRgb(hsv);

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
