using UnityEngine;

namespace GamblingAction.Core
{
    public class CharacterBase
    {
        public string Id { get; set; }

        // 各種補正値はこっち側の定数で持っておいて、start時かなんかに
        // dtoに対して登録が走るみたいな処理の方がいいかなぁ
        // のでキャラの補正をGameState側で加算して上げる形になると思われる。

        // そのためにキャラ選択イベントを作成し、そのデータを送って初期のdtoと
        // するような構築をする必要あり。

        public int Stamina { get; set; }
        public int MaxStamina { get; set; }

        // スタミナに対する補正値を管理するコンテナ
        public StaminaContainer StaminaModifier { get; private set; } = new StaminaContainer();
    }
}