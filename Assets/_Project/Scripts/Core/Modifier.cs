// バフ量等を管理するためのクラス

// ミッションを達成した時のイベントをそれぞれで発火し、それに応じてバフを加える？

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GamblingAction.Core
{

    // このmodifierをList化してそれぞれのバフを管理できるようにしたい。
    // んで項目ごとに変数として保存すればバフの有効化無効化が簡単になる。
    // でattackやmoveなどをplayerDtoに保存すればおけ。



    public class Modifier
    {
        public string Type { get; set; }
        public float RawValue { get; set; } // そのままの値
        public float RatioValue { get; set; } // 乗算用値(1.0=100%↑)
    }

    public class ModifierContainer
    {
        public Dictionary<string, Modifier> Modifiers { get; set; } = new();
    }
}