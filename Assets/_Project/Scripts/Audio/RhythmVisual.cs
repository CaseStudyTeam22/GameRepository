using System.Collections;
using UnityEngine;

namespace GamblingAction.Audio
{
    [RequireComponent(typeof(RhythmSignal))]
    public class RhythmVisual : MonoBehaviour
    {
        [Header("ビート反応")]
        [SerializeField] private float m_MaxBounceHeight = 2.5f;

        [Header("小節反応")]
        [SerializeField] private float m_MinScaleMultiplier = 1.5f;
        [SerializeField] private float m_MaxScaleMultiplier = 3.5f;
        [SerializeField] private float m_RotateAngle = 30f;

        private RhythmSignal m_Signal;
        private Vector3 m_OriginPos;
        private Vector3 m_OriginScale;

        // スケール・回転用コルーチン
        private Coroutine m_ScaleCoroutine;
        private Coroutine m_RotateCoroutine;

        // バウンスはUpdate()のタイマーで管理する
        private bool m_IsBounceActive = false;
        private float m_BounceTimer = 0f;
        private float m_BounceHeight = 0f;
        private float m_MeasuredBeatDuration;
        private float m_LastBeatTime = -1f;

        // 小節間隔を自動計測した値
        private float m_MeasuredBarDuration;
        private float m_LastBarTime = -1f;

        void Awake()
        {
            m_Signal = GetComponent<RhythmSignal>();
            m_OriginPos = transform.position;
            m_OriginScale = transform.localScale;
            m_MeasuredBeatDuration = 0.5f; // 初回計測前のデフォルト値(BPM120想定)
            m_MeasuredBarDuration = 2f;   // 初回計測前のデフォルト値(BPM120・4/4想定)
        }

        void OnEnable()
        {
            m_Signal.OnBeat += HandleBeat;
            m_Signal.OnBar += HandleBar;
            m_Signal.OnEnd += HandleEnd;
        }

        void OnDisable()
        {
            m_Signal.OnBeat -= HandleBeat;
            m_Signal.OnBar -= HandleBar;
            m_Signal.OnEnd -= HandleEnd;
        }

        void Update()
        {
            // バウンスをタイマーで管理する
            if (m_IsBounceActive)
            {
                m_BounceTimer += Time.unscaledDeltaTime;
                float t = m_BounceTimer / m_MeasuredBeatDuration;

                if (t >= 1f)
                {
                    transform.position = m_OriginPos;
                    m_IsBounceActive = false;
                    Debug.Log("[RhythmVisual] バウンスリセット");
                }
                else
                {
                    float y = Mathf.Sin(Mathf.PI * t) * m_BounceHeight;
                    transform.position = m_OriginPos + Vector3.up * y;
                }
            }
        }

        // 拍ごと: バウンスのみ
        private void HandleBeat(float intensity)
        {
            // 拍間隔を自動計測する
            float now = Time.unscaledTime;
            if (m_LastBeatTime >= 0f)
                m_MeasuredBeatDuration = now - m_LastBeatTime;
            m_LastBeatTime = now;

            // タイマーをリセットしてバウンス開始
            m_BounceHeight = Mathf.Lerp(m_MaxBounceHeight * 0.3f, m_MaxBounceHeight, intensity);
            m_BounceTimer = 0f;
            m_IsBounceActive = true;
        }

        // 小節頭ごと: スケールパルス + 回転
        private void HandleBar(float intensity)
        {
            // 小節間隔を自動計測する
            float now = Time.unscaledTime;
            if (m_LastBarTime >= 0f)
                m_MeasuredBarDuration = now - m_LastBarTime;
            m_LastBarTime = now;

            // スケールパルス
            if (m_ScaleCoroutine != null) StopCoroutine(m_ScaleCoroutine);
            m_ScaleCoroutine = StartCoroutine(ScalePulseCoroutine(intensity));

            // 回転
            if (m_RotateCoroutine != null) StopCoroutine(m_RotateCoroutine);
            m_RotateCoroutine = StartCoroutine(RotateCoroutine(m_RotateAngle));
        }

        private void HandleEnd()
        {
            Debug.Log("[RhythmVisual] 再生終了");
        }

        // 大きさ専用: 小節頭にスケールが膨らんで元に戻る
        private IEnumerator ScalePulseCoroutine(float intensity)
        {
            float peakScaleMultiplier = Mathf.Lerp(m_MinScaleMultiplier, m_MaxScaleMultiplier, intensity);
            float dur = m_MeasuredBarDuration;
            float elapsed = 0f;

            try
            {
                while (elapsed < dur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / dur;
                    float scaleValue = Mathf.Lerp(1f, peakScaleMultiplier, Mathf.Sin(t * Mathf.PI));
                    transform.localScale = m_OriginScale * scaleValue;
                    yield return null;
                }
            }
            finally
            {
                transform.localScale = m_OriginScale;
                Debug.Log("[RhythmVisual] スケールリセット");
            }
        }

        // 回転専用: 小節頭ごとに指定角度だけ滑らかに回転する
        private IEnumerator RotateCoroutine(float angle)
        {
            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = startRotation * Quaternion.Euler(0f, angle, 0f);
            float dur = m_MeasuredBarDuration;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / dur;
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                transform.rotation = Quaternion.Lerp(startRotation, endRotation, smooth);
                yield return null;
            }
            transform.rotation = endRotation;
            Debug.Log("[RhythmVisual] 回転リセット");
        }
    }
}