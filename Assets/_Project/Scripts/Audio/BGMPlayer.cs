using UnityEngine;
using GamblingAction.Domain;

namespace GamblingAction.Audio
{
    // フェーズ変化に応じてBGMの再生・停止を管理する
    public class BGMPlayer : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event m_BGMEvent;

        // Wwise State の定数
        private const string k_StateGroup = "Gameplay_State";
        private const string k_StateBattle = "Battle";
        private const string k_StateSilence = "Silence";

        private IGameState m_State;
        private IBeatClock m_BeatClock;

        // 再生中のPlayingID。0u = 未再生
        private uint m_PlayingID = 0u;

        // Battle フェーズ移行後、CurrentBeat == 1 を待つフラグ
        private bool m_IsWaitingForBeat1 = false;

        private void Start()
        {
            m_State = GameStateLocator.Current;

            if (m_State == null)
            {
                Debug.LogError("[BGMPlayer] GameStateLocator.CurrentがNullです。GameStateが初期化されているか確認してください。");
                return;
            }

            BeatClock beatClock = FindFirstObjectByType<BeatClock>();

            if (beatClock == null)
            {
                Debug.LogError("[BGMPlayer] BeatClockが見つかりません。シーンにBeatManagerが存在するか確認してください。");
                return;
            }

            m_BeatClock = beatClock;

            m_State.OnPhaseChanged += HandlePhaseChanged;
            m_BeatClock.OnBeat += HandleBeat;
        }

        private void OnDestroy()
        {
            Debug.Log("[BGMPlayer] OnDestroy");

            if (m_State != null)
            {
                m_State.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (m_BeatClock != null)
            {
                m_BeatClock.OnBeat -= HandleBeat;
            }
        }

        // =========================================================
        // イベントハンドラ
        // =========================================================

        // IGameState.OnPhaseChangedから呼ばれる
        private void HandlePhaseChanged(EGamePhase phase)
        {
            if (phase == EGamePhase.Battle)
            {
                // State の切り替えは Post 時に Wwise Event 内で行われる
                m_IsWaitingForBeat1 = true;
                Debug.Log("[BGMPlayer] Battle開始。CurrentBeat == 1 を待機中");
            }
            else if (phase == EGamePhase.RoundOver || phase == EGamePhase.GameOver)
            {
                // State を Silence に切り替える。停止はWwise側のTransitionに委ねる
                AkUnitySoundEngine.SetState(k_StateGroup, k_StateSilence);
                m_IsWaitingForBeat1 = false;
                m_PlayingID = 0u;
                Debug.Log("[BGMPlayer] BGM停止（Transition委譲）");
            }
            else
            {
                // Battle・RoundOver・GameOver 以外のフェーズでは再生しない
                AkUnitySoundEngine.SetState(k_StateGroup, k_StateSilence);
                m_IsWaitingForBeat1 = false;
                m_PlayingID = 0u;
            }
        }

        // IBeatClock.OnBeatから呼ばれる
        private void HandleBeat(int beat)
        {
            // 待機中かつ1拍目のタイミングでBGMを再生する
            if (!m_IsWaitingForBeat1 || beat != 1)
            {
                return;
            }

            m_PlayingID = m_BGMEvent.Post(gameObject);
            m_IsWaitingForBeat1 = false;
            Debug.Log("[BGMPlayer] BGM再生開始（CurrentBeat == 1）");
        }
    }
}