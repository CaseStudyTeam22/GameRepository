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
