using System;

namespace GamblingAction.Audio
{
    // 拍情報の提供窓口
    // 別担当者はこのインターフェースのみを通じて拍情報にアクセスする
    public interface IBeatClock
    {
        // BeatDurationとBeatsPerBarが確定しているか
        bool IsReady { get; }

        // 現在何拍目か (1始まり)
        int CurrentBeat { get; }

        // 1小節の拍数
        int BeatsPerBar { get; }

        // 1拍の長さ (秒)
        float BeatDuration { get; }

        // 1小節の長さ (秒)
        float BarDuration { get; }

        // 拍発火。引数は現在の拍番号
        event Action<int> OnBeat;

        // 小節頭発火
        event Action OnBar;

        // 再生終了発火
        //event Action OnEnd;
    }
}