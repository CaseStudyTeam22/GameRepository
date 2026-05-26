using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GamblingAction.Audio
{
    // IBeatClockを購読して拍の状態をUIで表示するスクリプト
    // 現在の拍をハイライト表示する
    public class BeatIndicatorUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("円を並べる親オブジェクト（HorizontalLayoutGroupを推奨）")]
        [SerializeField] private RectTransform m_BeatContainer;
        [Tooltip("BPM・拍数・BeatDuration・BarDurationを表示するテキスト")]
        [SerializeField] private TextMeshProUGUI m_InfoText;

        [Header("円の設定")]
        [SerializeField] private float m_CircleSize = 60f;
        [SerializeField] private Color m_ActiveColor = Color.cyan;
        [SerializeField] private Color m_InactiveColor = Color.gray;

        private IBeatClock m_BeatClock;
        private Image[] m_BeatCircles;
        private int m_LastBeatsPerBar = 0;

        private void Start()
        {
            BeatClock beatClock = FindFirstObjectByType<BeatClock>();

            if (beatClock == null)
            {
                Debug.LogError("[BeatIndicatorUI] BeatClockが見つかりません。シーンにBeatManagerが存在するか確認してください。");
                return;
            }

            m_BeatClock = beatClock;
            m_BeatClock.OnBeat += HandleBeat;
        }

        private void OnDestroy()
        {
            if (m_BeatClock == null)
            {
                return;
            }

            m_BeatClock.OnBeat -= HandleBeat;
        }

        private void Update()
        {
            if (m_BeatClock == null)
            {
                return;
            }

            // BeatsPerBarが変わったら円を再生成する
            // BGMが変わって拍子が変わった場合に自動追従する
            if (m_BeatClock.BeatsPerBar != m_LastBeatsPerBar)
            {
                RebuildCircles(m_BeatClock.BeatsPerBar);
                m_LastBeatsPerBar = m_BeatClock.BeatsPerBar;
            }

            // 情報テキストを更新する
            if (m_InfoText != null)
            {
                float bpm = m_BeatClock.BeatDuration > 0f
                    ? 60f / m_BeatClock.BeatDuration
                    : 0f;

                m_InfoText.text =
                    $"BPM: {bpm:F1}    " +
                    $"Beats: {m_BeatClock.BeatsPerBar}    " +
                    $"BeatDur: {m_BeatClock.BeatDuration:F3}s    " +
                    $"BarDur: {m_BeatClock.BarDuration:F3}s";
            }
        }

        // 現在の拍の円をハイライトする
        private void HandleBeat(int beat)
        {
            if (m_BeatCircles == null)
            {
                return;
            }

            for (int i = 0; i < m_BeatCircles.Length; i++)
            {
                m_BeatCircles[i].color = (i == beat - 1) ? m_ActiveColor : m_InactiveColor;
            }
        }

        // BeatsPerBarの数だけ円を生成する
        private void RebuildCircles(int count)
        {
            // 既存の円を全て削除する
            foreach (Transform child in m_BeatContainer)
            {
                Destroy(child.gameObject);
            }

            m_BeatCircles = new Image[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Beat_{i + 1}");
                go.transform.SetParent(m_BeatContainer, false);

                var rect = go.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(m_CircleSize, m_CircleSize);

                var image = go.AddComponent<Image>();
                image.color = m_InactiveColor;

                m_BeatCircles[i] = image;
            }
        }
    }
}