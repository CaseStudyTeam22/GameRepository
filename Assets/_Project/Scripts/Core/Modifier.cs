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
        // listでなくdictionaryで管理し、keyで指定できるような形に。
        // ただkeyの管理をどうするかかなぁ、指定がが

        public Dictionary<string, Modifier> Modifiers { get; set; }

        // 変更通知用イベント
        public event Action OnChanged;

        virtual public void AddModifier(string tag, Modifier modifier)
        {
            Modifiers[tag] = modifier;
            OnChanged?.Invoke(); // 通知
        }

        // remove、効率いい方法探したいね
        virtual public void RemoveModifier(string tag)
        {
            Modifiers.Remove(tag);
            OnChanged?.Invoke(); // 通知
        }

        public void ClearAllModifiers()
        {
            Modifiers.Clear();
        }

        // 乗算等を行い補正値を出力
        public float GetModifiedValue(float baseValue)
        {
            // 計算用一時変数
            float totalRaw = 0.0f;
            float totalRatio = 0.0f;

            // foreachで探索
            foreach (var modifier in Modifiers.Values)
            {
                totalRaw += modifier.RawValue;
                totalRatio += modifier.RatioValue;
            }
            // 探索した値を元に最終補正値を出力
            // 現在の計算式は (基礎値 + 補正の数値) * (1 + 補正の割合)
            return (baseValue + totalRaw) * (1 + totalRatio);
        }

        // 下は単一版で現状いらないためコメントアウト

        // public float GetModifiedValue(string type, float baseValue)
        // {
        //     if (!Modifiers.ContainsKey(type)) return baseValue;

        //     // ここ全探索して書き換えるように変更の必要あり
        //     var modifier = Modifiers[type];
        //     return baseValue * (1 + modifier.RatioValue) + modifier.RawValue;
        // }
    }
}