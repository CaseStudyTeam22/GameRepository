// 画面空間の後処理で輪郭線を描くシェーダ。URP の FullScreenPassRendererFeature から使う。
// 深度テクスチャと法線テクスチャの不連続を検出し、その境界に線を乗せる。
// 隣接して置いたマス同士は深度・法線が連続するため内側に線は出ず、
// 塊全体の外周だけが背景との差で縁取られる。
//
// 縁の検出と線の太さを分離する。検出は固定の 1 画素幅 Sobel で行い、凸角でも
// 抜けなく拾う。太さは検出した縁を _OutlineWidth の半径ぶん外へ膨張させて作る。
// こうすると線は本物の縁から外へ伸びるため、凸角の先端でも欠けない。
// _DepthThreshold/_NormalThreshold で検出感度を調整する。
Shader "GamblingAction/Outline"
{
    Properties
    {
        // 精密な輪郭線の色。
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        // 物体の内側の線（折り目やマスの境目）の太さ。
        _OutlineWidthInner ("Outline Width Inner", Range(1, 10)) = 1
        // 物体の外周（背景との境）の線の太さ。内側より太くすると外周が強調される。
        _OutlineWidthOuter ("Outline Width Outer", Range(1, 10)) = 2
        // 外周とみなす深度差のしきい値。大きいほど一番外の輪郭だけが外周扱いになる。
        _OuterEdgeThreshold ("Outer Edge Threshold", Float) = 5
        // 深度差で縁を検出する感度。小さいほど細かい段差も線になる。
        _DepthThreshold ("Depth Threshold", Float) = 0.5
        // 法線差で縁を検出する感度。小さいほど浅い折れ目も線になる。
        _NormalThreshold ("Normal Threshold", Range(0, 1)) = 0.4

        [Header(Depth Width Falloff)]
        // この距離より手前で線が最も太くなる。カメラからの距離（ワールド単位）。
        _WidthNearDist ("Near Distance (thickest)", Float) = 31
        // この距離より奥で線が最も細くなる。カメラからの距離（ワールド単位）。
        _WidthFarDist ("Far Distance (thinnest)", Float) = 36
        // 遠方での太さの倍率。1で変化なし、0.4なら遠くは手前の40％の太さ。
        _WidthFarScale ("Far Width Scale", Range(0, 1)) = 0.4

        [Header(Warp Underlay)]
        // 下地に敷く様式化の線の色。精密な線と別色にすると重なりが出る。
        _WarpOutlineColor ("Warp Underlay Color", Color) = (0, 0, 0, 1)
        // 下地の線がどれだけ外へはみ出すか（太さ）。0で下地を描かない。
        _WarpOutlineWidth ("Warp Underlay Width", Range(0, 20)) = 6
        // 四隅をどれだけ引き伸ばすか。0で歪みなし。大きすぎると線が割れるので0.01〜0.02が目安。
        _WarpStrength ("Warp Strength", Range(0, 0.03)) = 0
        // 歪み方の種。整数を変えると別の傾き方になる。好みの値を選ぶ。
        _WarpSeed ("Warp Seed", Float) = 0
        // 歪みが時間で切り替わる速さ。0で固定、上げると手描き風に時々パッと変わる。
        _WarpBoilSpeed ("Warp Boil Speed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float  _OutlineWidthInner;
            float  _OutlineWidthOuter;
            float  _OuterEdgeThreshold;
            float  _DepthThreshold;
            float  _NormalThreshold;
            float  _WidthNearDist;
            float  _WidthFarDist;
            float  _WidthFarScale;
            float4 _WarpOutlineColor;
            float  _WarpOutlineWidth;
            float  _WarpStrength;
            float  _WarpSeed;
            float  _WarpBoilSpeed;

            float SampleLinearDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            // シードから -1..1 の 2D 乱数ベクトルを作る。四隅ごとの変位量に使う。
            float2 WarpRand(float seed)
            {
                float2 p = float2(seed, seed * 1.37);
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123) * 2.0 - 1.0;
            }

            // そのUVが前景（物体）かどうか。深度が遠クリップ面付近なら背景とみなす。
            float IsSolid(float2 uv)
            {
                return SampleSceneDepth(uv) > 1e-6 ? 1.0 : 0.0;
            }

            // 画面 UV を矩形の四隅変位で双線形にゆがめる（PS の自由変形）。
            // 四隅それぞれに乱数の変位を与え、uv の位置で線形補間する。
            // 線形補間なので、元が直線なら歪んでも直線のまま（弧にならない）。
            float2 WarpCorners(float2 uv, float seed, float strength)
            {
                float2 c00 = WarpRand(seed + 0.0);  // 左下
                float2 c10 = WarpRand(seed + 1.0);  // 右下
                float2 c01 = WarpRand(seed + 2.0);  // 左上
                float2 c11 = WarpRand(seed + 3.0);  // 右上
                float2 bottom = lerp(c00, c10, uv.x);
                float2 top    = lerp(c01, c11, uv.x);
                float2 offset = lerp(bottom, top, uv.y);
                return uv + offset * strength;
            }

            // 指定 UV が縁かどうかと、その深度差・縁の深度を返す。
            //   x: 縁なら 1、そうでなければ 0
            //   y: 深度差の大きさ（外周と内側の線を区別するのに使う）
            //   z: 縁の前景側の深度（近傍の最小深度）。手前を太く奥を細くする倍率の算出に使う。
            // 検出は固定 1 画素幅の Sobel。太さには関与しないため凸角でも抜けない。
            float3 DetectEdge(float2 uv, float2 px)
            {
                float2 oTL = float2(-px.x,  px.y);
                float2 oT  = float2( 0,     px.y);
                float2 oTR = float2( px.x,  px.y);
                float2 oL  = float2(-px.x,  0);
                float2 oR  = float2( px.x,  0);
                float2 oBL = float2(-px.x, -px.y);
                float2 oB  = float2( 0,    -px.y);
                float2 oBR = float2( px.x, -px.y);

                // 深度の Sobel。
                float dTL = SampleLinearDepth(uv + oTL);
                float dT  = SampleLinearDepth(uv + oT);
                float dTR = SampleLinearDepth(uv + oTR);
                float dL  = SampleLinearDepth(uv + oL);
                float dR  = SampleLinearDepth(uv + oR);
                float dBL = SampleLinearDepth(uv + oBL);
                float dB  = SampleLinearDepth(uv + oB);
                float dBR = SampleLinearDepth(uv + oBR);
                float depthGx = (dTR + 2.0 * dR + dBR) - (dTL + 2.0 * dL + dBL);
                float depthGy = (dTL + 2.0 * dT + dTR) - (dBL + 2.0 * dB + dBR);
                float depthDiff = sqrt(depthGx * depthGx + depthGy * depthGy);

                // 縁の前景側の深度。近傍の最小深度を取り、背景側ではなく手前の面の深度を使う。
                float minDepth = min(min(min(dTL, dT), min(dTR, dL)), min(min(dR, dBL), min(dB, dBR)));

                // 注目点の法線と視線の傾きで深度閾値を補正し、斜面の偽の縁を抑える。
                float3 nC = SampleSceneNormals(uv);
                float3 viewDir = normalize(_WorldSpaceCameraPos - ComputeWorldSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_VP));
                float NdotV = saturate(dot(nC, viewDir));
                float grazing = 1.0 / max(NdotV, 0.05);
                // 硬い step ではなく閾値手前から滑らかに立ち上げ、細い隙間の縁の
                // ギザつき・破線化を抑える。
                float depthThr = _DepthThreshold * grazing;
                float depthEdge = smoothstep(depthThr * 0.5, depthThr, depthDiff);

                // 法線の Sobel。
                float3 nTL = SampleSceneNormals(uv + oTL);
                float3 nT  = SampleSceneNormals(uv + oT);
                float3 nTR = SampleSceneNormals(uv + oTR);
                float3 nL  = SampleSceneNormals(uv + oL);
                float3 nR  = SampleSceneNormals(uv + oR);
                float3 nBL = SampleSceneNormals(uv + oBL);
                float3 nB  = SampleSceneNormals(uv + oB);
                float3 nBR = SampleSceneNormals(uv + oBR);
                float3 normalGx = (nTR + 2.0 * nR + nBR) - (nTL + 2.0 * nL + nBL);
                float3 normalGy = (nTL + 2.0 * nT + nTR) - (nBL + 2.0 * nB + nBR);
                float normalDiff = sqrt(dot(normalGx, normalGx) + dot(normalGy, normalGy));
                float normalEdge = smoothstep(_NormalThreshold * 0.5, _NormalThreshold, normalDiff);

                float isEdge = saturate(max(depthEdge, normalEdge));
                return float3(isEdge, depthDiff, minDepth);
            }

            // 縁を検出し、その縁を _OutlineWidth の半径ぶん外へ膨張させて太さを作る。
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 rawUV = input.texcoord.xy;
                // 検出は常に 1 画素幅。横縦で同じ画素数になるようアスペクト比で揃える。
                float aspect = _BlitTexture_TexelSize.z / _BlitTexture_TexelSize.w;
                float2 px = _BlitTexture_TexelSize.yy;
                px.x /= aspect;

                // ----- 精密レイヤー：従来どおりの正確な輪郭線。歪ませず rawUV で検出する。-----
                float maxRadius = max(_OutlineWidthInner, _OutlineWidthOuter);
                float edge = DetectEdge(rawUV, px).x;

                const int DIRS = 16;
                const int RINGS = 6;
                for (int i = 0; i < DIRS; i++)
                {
                    float ang = (6.2831853 / DIRS) * i;
                    float2 dir = float2(cos(ang), sin(ang));
                    for (int r = 1; r <= RINGS; r++)
                    {
                        float dist = maxRadius * ((float)r / RINGS); // この方向のサンプル距離（画素）
                        float2 sampleUV = rawUV + dir * px * dist;
                        float3 e = DetectEdge(sampleUV, px);

                        // その縁の深度から、手前を太く奥を細くする倍率を求める。
                        float t = smoothstep(_WidthNearDist, _WidthFarDist, e.z);
                        float depthScale = lerp(1.0, _WidthFarScale, t);

                        // 縁が外周（深度差大）なら外側の太さ、そうでなければ内側の太さ。倍率を掛ける。
                        float isOuter = step(_OuterEdgeThreshold, e.y);
                        float baseWidth = lerp(_OutlineWidthInner, _OutlineWidthOuter, isOuter);
                        float widthHere = baseWidth * depthScale;

                        // 太さの境界を約 1 画素で滑らかに減衰させ、段差や硬いふちを防ぐ。
                        float covered = e.x * (1.0 - smoothstep(widthHere - 1.0, widthHere, dist));
                        edge = max(edge, covered);
                    }
                }

                // ----- 下地レイヤー：四隅変位で歪ませたシルエットを外へ太らせた様式化の線。-----
                // 物体の下に敷き、外周からはみ出した分だけが見える。歪んだ UV でシルエットを引く。
                // 物体に覆われた画素（自身がシルエット内）には描かないことで「下敷き」に見せる。
                float warpEdge = 0.0;
                if (_WarpStrength > 0.0 && _WarpOutlineWidth > 0.0 && IsSolid(rawUV) < 0.5)
                {
                    float boil = floor(_Time.y * _WarpBoilSpeed);
                    float2 wuv = WarpCorners(rawUV, _WarpSeed + boil, _WarpStrength);
                    // 歪んだ UV と元の UV の両方でシルエットを外膨張させ、和（max）をとる。
                    // 元の UV ぶんが下限となり、歪みは外側にしか効かない。内側へ引っ込まず、
                    // 線は常に元の輪郭以上に外へ出る。
                    for (int wi = 0; wi < DIRS; wi++)
                    {
                        float wa = (6.2831853 / DIRS) * wi;
                        float2 wd = float2(cos(wa), sin(wa));
                        for (int wr = 1; wr <= RINGS; wr++)
                        {
                            float wdist = _WarpOutlineWidth * ((float)wr / RINGS);
                            float falloff = 1.0 - smoothstep(_WarpOutlineWidth - 1.0, _WarpOutlineWidth, wdist);
                            float coveredWarp = IsSolid(wuv    + wd * px * wdist) * falloff;
                            float coveredOrig = IsSolid(rawUV  + wd * px * wdist) * falloff;
                            warpEdge = max(warpEdge, max(coveredWarp, coveredOrig));
                        }
                    }
                }

                // ----- 合成：シーン → 下地の様式線 → 精密な輪郭線 の順に重ねる。-----
                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, rawUV, _BlitMipLevel);
                half3 col = lerp(sceneColor.rgb, _WarpOutlineColor.rgb, warpEdge * _WarpOutlineColor.a);
                col = lerp(col, _OutlineColor.rgb, edge * _OutlineColor.a);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
