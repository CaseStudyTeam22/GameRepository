using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingAction.Audio
{
    // オーディオスレッドからメインスレッドへ
    // コールバック情報を安全に受け渡すためのデータ構造
    public struct BeatCallbackData
    {
        public AkCallbackType Type;
        public float BeatDuration;
        public float BarDuration;
    }

    // Wwiseコールバックの受信とQueueへの格納
    // Wwiseの知識はこのクラスにのみ存在する
    public class WwiseBeatReceiver : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event m_BGMEvent;

        // コールバックデータの格納先
        private readonly Queue<BeatCallbackData> m_CallbackQueue = new Queue<BeatCallbackData>();

        // Queueへのアクセスを同期するロックオブジェクト
        // オーディオスレッドとメインスレッドの両方からアクセスされるため必須
        private readonly object m_QueueLock = new object();

        private void Start()
        {
            StartCoroutine(PostWhenReady());
        }

        // AkBankのロード完了を待ってからEventをPostするコルーチン
        private IEnumerator PostWhenReady()
        {
            yield return null;

            m_BGMEvent.Post(
                gameObject,
                (uint)(
                    AkCallbackType.AK_MusicSyncBeat
                    | AkCallbackType.AK_MusicSyncBar
                    | AkCallbackType.AK_EndOfEvent
                ),
                MusicCallback
            );
        }

        // このメソッドはオーディオスレッドで実行される
        private void MusicCallback(object cookie, AkCallbackType type, AkCallbackInfo info)
        {
            var musicInfo = info as AkMusicSyncCallbackInfo;

            var data = new BeatCallbackData
            {
                Type = type,
                BeatDuration = musicInfo != null ? musicInfo.segmentInfo_fBeatDuration : 0f,
                BarDuration = musicInfo != null ? musicInfo.segmentInfo_fBarDuration : 0f,
            };

            lock (m_QueueLock)
            {
                m_CallbackQueue.Enqueue(data);
            }
        }

        // メインスレッドから呼ばれる
        // Queueからデータを全て取り出してリストで返す
        public List<BeatCallbackData> DequeueAll()
        {
            var result = new List<BeatCallbackData>();

            lock (m_QueueLock)
            {
                while (m_CallbackQueue.Count > 0)
                {
                    result.Add(m_CallbackQueue.Dequeue());
                }
            }

            return result;
        }
    }
}