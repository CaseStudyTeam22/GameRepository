using UnityEngine;

namespace GamblingAction.Audio
{
    // IBeatClockを購読して
    // 1 3拍目に入力を受け付け
    // 4拍目（BeatsPerBar拍目）に登録した動作を実行するテスト用スクリプト
    public class BeatTestObject : MonoBehaviour
    {
        [Header("移動設定")]
        [SerializeField] private float m_MoveDistance = 2f;

        private IBeatClock m_BeatClock;
        private BeatIndicatorUI m_BeatIndicatorUI;
        private Vector3 m_PendingMove = Vector3.zero;
        private bool m_HasPendingAction = false;

        private void Awake()
        {
            // シーン上のBeatClockを検索してIBeatClockとして取得する
            BeatClock beatClock = FindFirstObjectByType<BeatClock>();

            if (beatClock == null)
            {
                Debug.LogError("[BeatTestObject] BeatClockが見つかりません。シーンにBeatManagerが存在するか確認してください。");
                return;
            }

            m_BeatClock = beatClock;

            m_BeatIndicatorUI = FindFirstObjectByType<BeatIndicatorUI>();

            if (m_BeatIndicatorUI == null)
            {
                Debug.LogWarning("[BeatTestObject] BeatIndicatorUIが見つかりません。");
            }
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

        private void Update()
        {
            if (m_BeatClock == null)
            {
                return;
            }

            // 1 3拍目のみ入力を受け付ける
            bool isBetPhase = m_BeatClock.CurrentBeat >= 1
                && m_BeatClock.CurrentBeat < m_BeatClock.BeatsPerBar;

            if (!isBetPhase)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                m_PendingMove = Vector3.right * m_MoveDistance;
                m_HasPendingAction = true;
                m_BeatIndicatorUI?.SetInputColor(m_BeatClock.CurrentBeat);
                Debug.Log($"[BeatTestObject] {m_BeatClock.CurrentBeat}拍目: 右移動を登録");
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                m_PendingMove = Vector3.left * m_MoveDistance;
                m_HasPendingAction = true;
                m_BeatIndicatorUI?.SetInputColor(m_BeatClock.CurrentBeat);
                Debug.Log($"[BeatTestObject] {m_BeatClock.CurrentBeat}拍目: 左移動を登録");
            }
        }

        // OnBeatを購読。BeatsPerBar拍目に登録された動作を実行する
        private void HandleBeat(int beat)
        {
            if (beat != m_BeatClock.BeatsPerBar)
            {
                return;
            }

            if (m_HasPendingAction)
            {
                transform.position += m_PendingMove;
                Debug.Log($"[BeatTestObject] {beat}拍目: {m_PendingMove} に移動を実行");
            }
            else
            {
                Debug.Log($"[BeatTestObject] {beat}拍目: 入力なし");
            }

            // 登録をリセット
            m_PendingMove = Vector3.zero;
            m_HasPendingAction = false;
        }
    }
}