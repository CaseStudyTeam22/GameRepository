using UnityEngine;

namespace GamblingAction.Audio
{
    // IBeatClockを購読して拍番号に応じたSEを鳴らすスクリプト
    // 1 3拍目と4拍目で異なるSEを再生する
    public class BeatSEPlayer : MonoBehaviour
    {
        [Header("SE")]
        [Tooltip("1-3拍目に鳴らすSE")]
        [SerializeField] private AK.Wwise.Event m_BeatSE;
        [Tooltip("4拍目（BeatsPerBar拍目）に鳴らすSE")]
        [SerializeField] private AK.Wwise.Event m_BarSE;

        private IBeatClock m_BeatClock;

        private void Awake()
        {
            BeatClock beatClock = FindFirstObjectByType<BeatClock>();

            if (beatClock == null)
            {
                Debug.LogError("[BeatSEPlayer] BeatClockが見つかりません。シーンにBeatManagerが存在するか確認してください。");
                return;
            }

            m_BeatClock = beatClock;
        }

        private void OnEnable()
        {
            if (m_BeatClock != null)
            {
                m_BeatClock.OnBeat += HandleBeat;
            }
        }

        private void OnDisable()
        {
            if (m_BeatClock != null)
            {
                m_BeatClock.OnBeat -= HandleBeat;
            }
        }

        // OnBeatを購読。拍番号に応じてSEを再生する
        private void HandleBeat(int beat)
        {
            if (beat == m_BeatClock.BeatsPerBar)
            {
                // 4拍目（BeatsPerBar拍目）
                m_BarSE.Post(gameObject);
            }
            // else
            // {
            // 	// 1-3拍目
            // 	m_BeatSE.Post(gameObject);
            // }
        }
    }
}