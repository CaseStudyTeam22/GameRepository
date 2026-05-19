using UnityEngine;

namespace GamblingAction.Core
{
    public class CharacterBase : MonoBehaviour
    {
        // 各種補正値はこっち側の定数で持っておいて、start時かなんかに
        // dtoに対して登録が走るみたいな処理の方がいいかなぁ
        // のでキャラの補正をGameState側で加算して上げる形になると思われる。

        // そのためにキャラ選択イベントを作成し、そのデータを送って初期のdtoと
        // するような構築をする必要あり。

        public virtual int StartMoney => 10000;
        public virtual int StartChips => 10;
        public virtual int MaxStamina => 5;

        public virtual string CharacterName => "サンプルキャラ";

        // スキル発動メソッド
        public virtual void SkillEffect()
        {
            // スキルの効果を記載
        }
    }
}