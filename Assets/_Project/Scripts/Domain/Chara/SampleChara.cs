using System.Data;
using UnityEngine;
using GamblingAction.Core.Dto;

namespace GamblingAction.Domain
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
        // スキルボタンを押し、4拍目に発動する効果
        public override void SkillEffect(PlayerDto targetDto)
        {

            // スタミナを回復
            targetDto.Stamina = Mathf.Min(targetDto.Stamina + 2, MaxStamina); // スタミナを2回復

            // 遅延実行用にidを保存
            PlayerId = targetDto.Id;

            // スキルの効果を記載
            Debug.Log("SampleCharaのスキルが発動しました!");
        }

        public override void DelayedEffect(IGameState state)
        {
            // idから現在のdtoをもらう
            if(state.Players.TryGetValue(PlayerId, out var targetDto))
            {
                // さらにスタミナを回復
                targetDto.Stamina = Mathf.Min(targetDto.Stamina + 1, MaxStamina); // スタミナをさらに1回復
            }

            // 遅延して発動する効果の処理を記載(次ラウンド開始時に発動)
            Debug.Log("SampleCharaの遅延効果が発動しました!");
        }
    }
}