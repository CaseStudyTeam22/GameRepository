// SpriteRenderer 用の URP 対応「下から上へ印字」シェーダ。
// _DissolveAmount: 0 = 何も表示しない, 1 = 完全に表示。下端から上端へ満ちる。
// 満ちる上端は正弦波で波打ち、その境界に発光色を乗せる。
// 追加のテクスチャ不要。
Shader "GamblingAction/SpriteDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DissolveAmount ("Print Progress", Range(0,1)) = 1
        _EdgeWidth ("Edge Width", Range(0,0.5)) = 0.06
        [HDR] _EdgeColor ("Edge Glow Color", Color) = (1,0.6,0.1,1)
        _WaveAmplitude ("Wave Amplitude", Range(0,0.3)) = 0.04
        _WaveFrequency ("Wave Frequency", Float) = 8
        _WaveSpeed ("Wave Speed", Float) = 6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _WaveAmplitude;
                float  _WaveFrequency;
                float  _WaveSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _Color;

                // 進捗に応じて下端から満ちる上端の高さ。x に沿って正弦波で波打つ。
                // 始端・終端では波を弱め、上下端で欠けが出ないようにする（中央で最大の包絡）。
                float envelope = _DissolveAmount * (1.0 - _DissolveAmount) * 4.0;
                float wave = sin(IN.uv.x * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude * envelope;
                float edgeY = _DissolveAmount + wave;

                // 上端より上はまだ印字されていない。
                if (IN.uv.y > edgeY)
                    discard;

                // 上端直下の帯に発光色を乗せる（帯も波打つ）。
                float edge = smoothstep(edgeY - _EdgeWidth, edgeY, IN.uv.y);
                // 印字完了が近づくにつれ発光帯をフェードアウトする（完全表示時に頭頂へ線が残らない）。
                edge *= 1.0 - smoothstep(0.9, 1.0, _DissolveAmount);
                half3 rgb = lerp(tex.rgb, _EdgeColor.rgb, edge);
                half a = tex.a;

                // 何も印字していないときは完全透明。
                a *= step(0.0001, _DissolveAmount);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
