using System;
using UnityEngine;
using GamblingAction.Domain;

namespace GamblingAction.Audio
{
    // IGameStateの拍情報をIBeatClockとして外部に提供する
    // Wwiseを直接知らない
    public class BeatClock : MonoBehaviour, IBeatClock
    {
        // BeatDurationとBeatsPerBarが確定しているか
        public bool IsReady { get; private set; } = false;

        // 現在何拍目か (1始まり)
        public int CurrentBeat { get; private set; } = 1;

        // 1小節の拍数 (確定前のデフォルト値)
        public int BeatsPerBar { get; private set; } = 4;

        // 1拍の長さ (確定前のデフォルト値)
        public float BeatDuration { get; private set; } = 0.375f;

        // 1小節の長さ (BeatDuration × BeatsPerBarのデフォルト値)
        public float BarDuration { get; private set; } = 1.5f;

        // 拍発火。引数は現在の拍番号
        public event Action<int> OnBeat;

        // 小節頭発火
        public event Action OnBar;

        // 再生終了発火
        // BGMPlayerがOnPhaseChangedで管理するため現時点では未使用
        //public event Action OnEnd;

        private IGameState m_State;

        // 前回OnBeatChangedが呼ばれた時刻。BeatDuration計測に使用
        private float m_LastBeatTime = 0f;

        // 前回の拍番号。BeatsPerBar推定に使用
        private int m_PreviousBeat = 1;

        // 最初の拍かどうか。最初はBeatDurationが計測できないため
        private bool m_IsFirstBeat = true;

        private void Start()
        {
            m_State = GameStateLocator.Current;

            if (m_State == null)
            {
                Debug.LogError("[BeatClock] GameStateLocator.CurrentがNullです。GameStateが初期化されているか確認してください。");
                return;
            }

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

        // IGameState.OnBeatChangedから呼ばれる
        private void HandleBeatChanged()
        {
            // BeatDurationの計測
            // 最初の拍は前回時刻がないためスキップする
            if (!m_IsFirstBeat)
            {
                float elapsed = Time.unscaledTime - m_LastBeatTime;
                BeatDuration = elapsed;
                BarDuration = BeatDuration * BeatsPerBar;
            }
            m_LastBeatTime = Time.unscaledTime;
            m_IsFirstBeat = false;

            // BeatsPerBarの推定
            // CurrentBeatが1に戻ったとき前回の拍番号がBeatsPerBarになる
            if (m_State.CurrentBeat == 1 && m_PreviousBeat > 1)
            {
                BeatsPerBar = m_PreviousBeat;
                BarDuration = BeatDuration * BeatsPerBar;
                IsReady = true;

                //Debug.Log($"[BeatClock] BeatsPerBar確定: {BeatsPerBar}  BarDuration: {BarDuration:F3}s");
                OnBar?.Invoke();
            }

            // CurrentBeatの更新
            CurrentBeat = m_State.CurrentBeat;
            m_PreviousBeat = CurrentBeat;

            //Debug.Log($"[BeatClock] Beat: {CurrentBeat} / {BeatsPerBar}  BeatDuration: {BeatDuration:F3}s  IsReady: {IsReady}");
            OnBeat?.Invoke(CurrentBeat);
        }
    }
}