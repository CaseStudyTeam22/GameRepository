using UnityEngine;
using GamblingAction.Domain;

namespace GamblingAction.Audio
{
    // フェーズ変化に応じて BGM の再生・停止を管理する
    public class BGMPlayer : MonoBehaviour
    {
        [Tooltip("BGM->Boot")]
        [SerializeField] private AK.Wwise.Event m_BGMEvent;
        [SerializeField] private AK.Wwise.Event m_SeekEvent;

        private const string k_StateGroup = "Gameplay_State";
        private const string k_StateSilence = "Silence";

        private const uint k_MusicSyncCallbackFlags =
            (uint)AkCallbackType.AK_MusicSyncUserCue |
            (uint)AkCallbackType.AK_MusicSyncBar |
            (uint)AkCallbackType.AK_EnableGetMusicPlayPosition |
            (uint)AkCallbackType.AK_MusicPlayStarted;

        private IGameState m_State;
        private IBeatClock m_BeatClock;
        private BGMSyncController m_BgmSyncController;

        // 再生中の PlayingID。0u = 未再生
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
                Debug.LogError("[BGMPlayer] BeatClockが見つかりません。シーンにBeatClockが存在するか確認してください。");
                return;
            }

            m_BeatClock = beatClock;

            if (m_SeekEvent == null)
            {
                m_SeekEvent = m_BGMEvent;
                Debug.LogWarning("[BGMPlayer] m_SeekEvent が未設定のため m_BGMEvent を使用します。");
            }

            m_BgmSyncController = GetComponent<BGMSyncController>();
            if (m_BgmSyncController == null)
            {
                m_BgmSyncController = gameObject.AddComponent<BGMSyncController>();
            }

            m_BgmSyncController.Initialize(m_BeatClock, m_State, gameObject, m_SeekEvent);

            m_State.OnPhaseChanged += HandlePhaseChanged;
            m_BeatClock.OnBeat += HandleBeat;
        }

        private void OnDestroy()
        {
            if (m_State != null)
            {
                m_State.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (m_BeatClock != null)
            {
                m_BeatClock.OnBeat -= HandleBeat;
            }

            if (m_BgmSyncController != null)
            {
                m_BgmSyncController.StopSync();
            }
        }

        // IGameState.OnPhaseChanged から呼ばれる
        private void HandlePhaseChanged(EGamePhase phase)
        {
            if (phase == EGamePhase.Battle)
            {
                if (m_PlayingID != 0u)
                {
                    return;
                }

                m_IsWaitingForBeat1 = true;
                Debug.Log("[BGMPlayer] Battle開始。CurrentBeat == 1 を待機中");
                return;
            }

            m_IsWaitingForBeat1 = false;
            m_PlayingID = 0u;
            WwiseGameSyncAPI.SetState(k_StateGroup, k_StateSilence);

            if (phase == EGamePhase.RoundOver || phase == EGamePhase.GameOver)
            {
                Debug.Log("[BGMPlayer] BGM停止");
            }
        }

        // IBeatClock.OnBeat から呼ばれる
        private void HandleBeat(int beat)
        {
            if (m_BgmSyncController != null)
            {
                m_BgmSyncController.HandleBeat(beat);
            }

            if (!m_IsWaitingForBeat1 || beat != 1)
            {
                return;
            }

            if (WwiseSoundAPI.Instance == null)
            {
                Debug.LogWarning("[BGMPlayer] WwiseSoundAPI.Instance が null です。");
                return;
            }

            if (m_BGMEvent == null)
            {
                Debug.LogWarning("[BGMPlayer] m_BGMEvent が未設定です。");
                return;
            }

            uint playingId = WwiseSoundAPI.Instance.PlayTracked(
                m_BGMEvent,
                gameObject,
                k_MusicSyncCallbackFlags,
                HandleMusicSyncCallback);

            if (playingId == 0u)
            {
                Debug.LogWarning("[BGMPlayer] BGM再生に失敗しました。");
                return;
            }

            m_PlayingID = playingId;
            m_IsWaitingForBeat1 = false;

            if (m_BgmSyncController != null)
            {
                m_BgmSyncController.StartSync(m_PlayingID);
            }

            Debug.Log("[BGMPlayer] BGM再生命令を発行");
        }

        private void HandleMusicSyncCallback(object cookie, AkCallbackType type, AkCallbackInfo info)
        {
            if (m_BgmSyncController == null)
            {
                return;
            }

            if (info is AkMusicSyncCallbackInfo musicSyncInfo)
            {
                m_BgmSyncController.HandleMusicSyncCallback(type, musicSyncInfo);
            }
        }
    }
}