using UnityEngine;

namespace GamblingAction.Core
{
    public class SampleChara : CharacterBase
    {
        // 現状ステータスや処理を参照して発火する箇所がないため値の設定等々のみとなります
        // 注意してね

        // キャラごとのステータスをオーバーライドするならする
        public override int StartMoney => 15000; // 例えばこのキャラは初期所持金が多い
        public override int StartChips => 5;     // 例えばこのキャラは初期チップが少ない
        public override int MaxStamina => 7;     // 例えばこのキャラは最大スタミナが多い
        public override string CharacterName => "サンプルキャラ1"; // キャラ名のオーバーライド

        // スキル発動メソッドのオーバーライド
        public override void SkillEffect()
        {
            // スキルの効果を記載
            Debug.Log("SampleCharaのスキルが発動しました!");
        }
    }
}