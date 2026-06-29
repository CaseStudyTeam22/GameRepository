using System;

namespace GamblingAction.Audio
{
    // サーバー定義の拍情報を外部へ公開する窓口
    public interface IBeatClock
    {
        // 拍情報を利用可能か
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
    }
}