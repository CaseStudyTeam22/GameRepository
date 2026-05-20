using UnityEngine;

namespace GamblingAction.Audio
{
    // 拍番号に応じたSEを再生する
    public class BeatSEPlayer : MonoBehaviour
    {
        [Header("SE")]
        [Tooltip("1-3拍目に鳴らすSE")]
        [SerializeField] private AK.Wwise.Event m_BeatSE;
        [Tooltip("BeatsPerBar拍目に鳴らすSE")]
        [SerializeField] private AK.Wwise.Event m_BarSE;

        private IBeatClock m_BeatClock;

        private void Start()
        {
            BeatClock beatClock = FindFirstObjectByType<BeatClock>();

            if (beatClock == null)
            {
                Debug.LogError("[BeatSEPlayer] BeatClockが見つかりません。シーンにBeatManagerが存在するか確認してください。");
                return;
            }

            m_BeatClock = beatClock;
            m_BeatClock.OnBeat += HandleBeat;
        }

        private void OnDestroy()
        {
            Debug.Log("[SEPlayer] OnDestroy");

            if (m_BeatClock == null)
            {
                return;
            }

            m_BeatClock.OnBeat -= HandleBeat;
        }

        // OnBeatから拍番号に応じて再生
        private void HandleBeat(int beat)
        {
            // 4拍目と1～3拍目
            if (beat == m_BeatClock.BeatsPerBar)
            {
                m_BarSE.Post(gameObject);
            }
            else
            {
                m_BeatSE.Post(gameObject);
            }
        }
    }
}