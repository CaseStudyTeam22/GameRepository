using System;
using System.Collections;
using UnityEngine;

namespace GamblingAction.Audio
{
    // Wwiseのコールバックを受け取り、Actionで外部に通知する
    public class RhythmSignal : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event m_MusicEvent;

        [Header("RTPC")]
        [SerializeField] private string m_RTPCName = "MusicEnergy";

        [Header("音量感度調整 (dB)")]
        [Tooltip("この音量以下なら強度は0になります")]
        [SerializeField] private float m_MinDb = -24f;
        [Tooltip("この音量以上なら強度は1(MAX)になります")]
        [SerializeField] private float m_MaxDb = -2f;

        // 拍ごとに発火。引数は強度(0-1)
        public event Action<float> OnBeat;
        // 小節頭ごとに発火。引数は強度(0-1)
        public event Action<float> OnBar;
        // 再生終了時に発火
        public event Action OnEnd;

        // 0u = 無効なPlayingID
        private uint m_PlayingID = 0u;
        // 現在の音量強度(0-1)
        private float m_CurrentIntensity = 0f;

        // コールバックはオーディオスレッドから呼ばれるため
        // フラグ経由でメインスレッドに伝える
        private volatile bool m_BeatFlag = false;
        private volatile bool m_BarFlag = false;
        private volatile bool m_EndFlag = false;

        void Start()
        {
            StartCoroutine(PostWhenReady());
        }

        void Update()
        {
            if (m_PlayingID == 0u) return;

            UpdateMusicIntensity();

            if (m_BarFlag)
            {
                m_BarFlag = false;
                OnBar?.Invoke(m_CurrentIntensity);
            }
            if (m_BeatFlag)
            {
                m_BeatFlag = false;
                OnBeat?.Invoke(m_CurrentIntensity);
            }
            if (m_EndFlag)
            {
                m_EndFlag = false;
                OnEnd?.Invoke();
            }
        }

        // Wwise RTPC取得
        private void UpdateMusicIntensity()
        {
            // io_rValueType: 0で初期化して渡す。呼び出し後にWwiseが値の種類を書き込む
            int valueType = 0;
            float value = 0f;

            AkUnitySoundEngine.GetRTPCValue(
                m_RTPCName,
                null,
                0u,
                out value,
                ref valueType
            );

            // dB(m_MinDb～m_MaxDb) → 0～1へ変換
            m_CurrentIntensity = Mathf.InverseLerp(m_MinDb, m_MaxDb, value);
        }

        // AkBankのロード完了を待ってからEventをPostするコルーチン
        private IEnumerator PostWhenReady()
        {
            yield return null;

            m_PlayingID = m_MusicEvent.Post(
                gameObject,
                (uint)(
                    AkCallbackType.AK_MusicSyncBeat
                    | AkCallbackType.AK_MusicSyncBar
                    | AkCallbackType.AK_EndOfEvent
                ),
                MusicCallback
            );

            if (m_PlayingID == 0u)
            {
                Debug.LogWarning("[RhythmSignal] Eventのpostに失敗しました。");
            }
        }

        // このメソッドはオーディオスレッドで実行される。
        private void MusicCallback(object cookie, AkCallbackType type, AkCallbackInfo info)
        {
            switch (type)
            {
                case AkCallbackType.AK_MusicSyncBeat:
                    m_BeatFlag = true;
                    break;

                case AkCallbackType.AK_MusicSyncBar:
                    m_BarFlag = true;
                    break;

                case AkCallbackType.AK_EndOfEvent:
                    m_EndFlag = true;
                    break;
            }
        }
    }
}