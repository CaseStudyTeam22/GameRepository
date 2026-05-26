using UnityEngine;
using GamblingAction.Core.Dto;

namespace GamblingAction.Domain
{
    public class CharacterBase
    {
        // 各種補正値はこっち側の定数で持っておいて、start時かなんかに
        // dtoに対して登録が走るみたいな処理の方がいいかなぁ
        // のでキャラの補正をGameState側で加算して上げる形になると思われる。

        // そのためにキャラ選択イベントを作成し、そのデータを送って初期のdtoと
        // するような構築をする必要あり。

        public virtual int StartMoney => 10000;
        public virtual int StartChips => 10;
        public virtual int MaxStamina => 5;
        public virtual int Defense => 5;
        public virtual int Charge => 5;

        public virtual string CharacterName => "サンプルキャラ";

        protected string PlayerId { get; set; }

        // 初期化メソッドなどを用意してdtoを受け取るような形を作りたい

        // まだこの辺のメソッド呼び出せてないので、
        // 4拍目以降にこの関数を呼び出すのとdelayを呼び出すような構造をgamestateやserver.js側に作成の必要あり

        // スキル発動メソッド
        public virtual void SkillEffect(PlayerDto targetDto)
        {
            // スキルの効果を記載
        }

        public virtual void DelayedEffect(IGameState state)
        {
            // 遅延して発動する効果の処理を記載(次ラウンド開始時に発動)
        }
    }
}