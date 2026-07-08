using System;
using UnityEngine;
using GamblingAction.Domain;
using GamblingAction.Core;

namespace GamblingAction.Audio
{
    // IGameState の拍情報を IBeatClock として外部に提供する
    // このクラスは拍を推定せず、サーバー定義の拍情報を公開するだけに徹する
    public class BeatClock : MonoBehaviour, IBeatClock
    {
        // サーバー定義の拍情報をそのまま使うため、初期化後は常に利用可能
        public bool IsReady { get; private set; } = false;

        // 現在何拍目か (1始まり)
        public int CurrentBeat { get; private set; } = 1;

        // 1小節の拍数
        public int BeatsPerBar { get; private set; } = GameConfig.BeatsPerCycle;

        // 1拍の長さ (秒)
        public float BeatDuration { get; private set; } = GameConfig.BeatIntervalMs / 1000f;

        // 1小節の長さ (秒)
        public float BarDuration { get; private set; } = (GameConfig.BeatIntervalMs / 1000f) * GameConfig.BeatsPerCycle;

        // 拍発火。引数は現在の拍番号
        public event Action<int> OnBeat;

        // 小節頭発火
        public event Action OnBar;

        private IGameState m_State;

        private void Start()
        {
            m_State = GameStateLocator.Current;

            if (m_State == null)
            {
                Debug.LogError("[BeatClock] GameStateLocator.CurrentがNullです。GameStateが初期化されているか確認してください。");
                return;
            }

            CurrentBeat = m_State.CurrentBeat;
            BeatsPerBar = GameConfig.BeatsPerCycle;
            BeatDuration = GameConfig.BeatIntervalMs / 1000f;
            BarDuration = BeatDuration * BeatsPerBar;
            IsReady = true;

            m_State.OnBeatChanged += HandleBeatChanged;
        }

        private void OnDestroy()
        {
            if (m_State == null)
            {
                return;
            }

            m_State.OnBeatChanged -= HandleBeatChanged;
        }

        // IGameState.OnBeatChanged から呼ばれる
        private void HandleBeatChanged()
        {
            CurrentBeat = m_State.CurrentBeat;

            if (CurrentBeat == 1)
            {
                OnBar?.Invoke();
            }

            OnBeat?.Invoke(CurrentBeat);
        }
    }
}