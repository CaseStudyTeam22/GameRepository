// バフ量等を管理するためのクラス

// ミッションを達成した時のイベントをそれぞれで発火し、それに応じてバフを加える？

namespace GamblingAction.Gameplay
{

    // このmodifierをList化してそれぞれのバフを管理できるようにしたい。
    // んで項目ごとに変数として保存すればバフの有効化無効化が簡単になる。
    // でattackやmoveなどをplayerDtoに保存すればおけ。



    public class Modifier
    {
        public string Type { get; set; }
        public float RawValue { get; set; } // そのままの値
        public float RatioValue { get; set; } // 乗算用値(%)
    }

    public class ModifierContainer
    {
        // listでなくdictionaryで管理し、keyで指定できるような形に。
        // ただkeyの管理をどうするかかなぁ、指定がが

        public List<Modifier> Modifiers { get; set; }

        public void AddModifier(Modifier modifier)
        {
            Modifiers.Add(modifier);
        }

        // remove、効率いい方法探したいね
        public void RemoveModifier(Modifier modifier)
        {
            Modifiers.Remove(modifier);
        }

        public float GetModifiedValue(string type, float baseValue)
        {
            float modifiedValue = baseValue;
            foreach (var modifier in Modifiers.Where(m => m.Type == type))
            {
                modifiedValue += modifier.RawValue;
                modifiedValue *= (1 + modifier.RatioValue);
            }
            return modifiedValue;
        }
    }
}