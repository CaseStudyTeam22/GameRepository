using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingAction.Audio
{
    // 拍情報の管理と外部への提供
    // IBeatClockの実装クラス
    // Wwiseを直接知らない
    [RequireComponent(typeof(WwiseBeatReceiver))]
    public class BeatClock : MonoBehaviour, IBeatClock
    {

        // 現在何拍目か (1始まり)
        public int CurrentBeat { get; private set; } = 1;

        // 1小節の拍数 (デフォルト4拍子)
        public int BeatsPerBar { get; private set; } = 4;

        // 1拍の長さ (デフォルトBPM120想定)
        public float BeatDuration { get; private set; } = 0.5f;

        // 1小節の長さ (デフォルトBPM120・4拍子想定)
        public float BarDuration { get; private set; } = 2f;


        // 拍発火。引数は現在の拍番号
        public event Action<int> OnBeat;

        // 小節頭発火
        public event Action OnBar;

        // 再生終了発火
        public event Action OnEnd;

        private WwiseBeatReceiver m_Receiver;

        private void Awake()
        {
            m_Receiver = GetComponent<WwiseBeatReceiver>();
        }

        private void Update()
        {
            List<BeatCallbackData> dataList = m_Receiver.DequeueAll();

            foreach (BeatCallbackData data in dataList)
            {
                switch (data.Type)
                {
                    case AkCallbackType.AK_MusicSyncBeat:
                        ProcessBeat(data);
                        break;
                    case AkCallbackType.AK_MusicSyncBar:
                        ProcessBar(data);
                        break;
                    case AkCallbackType.AK_EndOfEvent:
                        ProcessEnd();
                        break;
                }
            }
        }

        // 拍コールバックの処理
        private void ProcessBeat(BeatCallbackData data)
        {
            if (data.BeatDuration > 0f)
            {
                BeatDuration = data.BeatDuration;
            }

            // BeatsPerBarを再計算
            if (data.BeatDuration > 0f && data.BarDuration > 0f)
            {
                BeatsPerBar = Mathf.RoundToInt(data.BarDuration / data.BeatDuration);
            }

            CurrentBeat++;

            // BeatsPerBarを超えた場合はリセット
            if (CurrentBeat > BeatsPerBar)
            {
                CurrentBeat = ((CurrentBeat - 1) % BeatsPerBar) + 1;
            }

            Debug.Log($"[BeatClock] Beat: {CurrentBeat} / {BeatsPerBar}  BeatDuration: {BeatDuration:F3}s");
            OnBeat?.Invoke(CurrentBeat);
        }

        // 小節頭コールバックの処理
        private void ProcessBar(BeatCallbackData data)
        {
            // 全ての値を更新
            if (data.BeatDuration > 0f)
            {
                BeatDuration = data.BeatDuration;
            }
            if (data.BarDuration > 0f)
            {
                BarDuration = data.BarDuration;
            }
            if (data.BeatDuration > 0f && data.BarDuration > 0f)
            {
                BeatsPerBar = Mathf.RoundToInt(data.BarDuration / data.BeatDuration);
            }

            CurrentBeat = 1;

            Debug.Log($"[BeatClock] Bar: BeatsPerBar: {BeatsPerBar}  BarDuration: {BarDuration:F3}s");
            OnBar?.Invoke();
        }

        // 再生終了コールバックの処理
        private void ProcessEnd()
        {
            Debug.Log("[BeatClock] End of Event");
            OnEnd?.Invoke();
        }
    }
}