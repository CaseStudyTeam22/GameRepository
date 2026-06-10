using UnityEngine;
using GamblingAction.Core.Dto;

namespace GamblingAction.Domain
{
    public class DocterChara : CharacterBase
    {
        [Header("医者ステータスデータ")]
        [SerializeField] private DocterStatsData stats;

        public override int StartMoney => stats.GetInt("資金");
        public override int StartChips => stats.GetInt("チップ");
        public override int MaxStamina => stats.GetInt("スタミナ（体幹）");
        public override int Charge => stats.GetInt("突進");
        public override int Defense => stats.GetInt("防御");
        public override string CharacterName => stats.GetString("キャラクター名");

        // スキル発動
        public void SkillEffect(IGameState state, PlayerDto casterDto)
        {
            // スキルによる回復量の設定（現状は固定値にしています）
            int healAmount = 3;

            // 発動者（キャスター）のスタミナを回復させます。
            // Mathf.Min を使用することで、回復結果が最大値(MaxStamina)を上回らないよう安全に制御します。
            casterDto.Stamina = Mathf.Min(casterDto.Stamina + healAmount, MaxStamina);

            Debug.Log($"{CharacterName} のスキル発動！自身のスタミナを {healAmount} 回復！（現在値: {casterDto.Stamina}）");
        }

        // 遅延効果（次ラウンド開始時）
        public override void DelayedEffect(IGameState state)
        {
            if (state.Players.TryGetValue(PlayerId, out var targetDto))
            {
                // 次ラウンド開始時にスタミナを少し回復
                targetDto.Stamina = Mathf.Min(targetDto.Stamina + 1, MaxStamina);
            }

            Debug.Log($"{CharacterName} の遅延効果が発動！");
        }
        
    }
}
