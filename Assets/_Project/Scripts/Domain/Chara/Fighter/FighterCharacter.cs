using UnityEngine;
using GamblingAction.Core.Dto;

namespace GamblingAction.Domain
{
    public class FighterChara : CharacterBase
    {
        [Header("格闘家ステータスデータ")]
        [SerializeField] private FighterStatsData stats;

        public override int StartMoney => stats.GetInt("資金");
        public override int StartChips => stats.GetInt("チップ");
        public override int MaxStamina => stats.GetInt("スタミナ（体幹）");
        public override int Charge => stats.GetInt("突進");
        public override int Defense => stats.GetInt("防御");
        public override string CharacterName => stats.GetString("キャラクター名");

        // スキル発動
        public void SkillEffect(IGameState state, PlayerDto casterDto)
        {
            const int damage = 2;

            PlayerId = casterDto.Id;

            // 入力方向がない場合は発動しない
            string dir = casterDto.Intent != null ? casterDto.Intent.Dir : null;
            if (string.IsNullOrEmpty(dir))
            {
                Debug.Log($"{CharacterName} のスキルは方向が指定されていないため発動しない。");
                return;
            }

            // 前方の縦3横3マスの範囲を決定
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            switch (dir)
            {
                case Directions.Up:
                    minX = casterDto.X - 1;
                    maxX = casterDto.X + 1;
                    minY = casterDto.Y - 3;
                    maxY = casterDto.Y - 1;
                    break;
                case Directions.Down:
                    minX = casterDto.X - 1;
                    maxX = casterDto.X + 1;
                    minY = casterDto.Y + 1;
                    maxY = casterDto.Y + 3;
                    break;
                case Directions.Left:
                    minX = casterDto.X - 3;
                    maxX = casterDto.X - 1;
                    minY = casterDto.Y - 1;
                    maxY = casterDto.Y + 1;
                    break;
                case Directions.Right:
                    minX = casterDto.X + 1;
                    maxX = casterDto.X + 3;
                    minY = casterDto.Y - 1;
                    maxY = casterDto.Y + 1;
                    break;
                default:
                    Debug.Log($"{CharacterName} のスキルは不正な方向({dir})のため発動しない。");
                    return;
            }

            foreach (var kvp in state.Players)
            {
                var p = kvp.Value;
                // 自分には当てない
                if (p.Id == casterDto.Id) continue;

                // 前方の縦3横3マス内にいるか判定
                if (p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY)
                {
                    // スタミナを大きく削る
                    p.Stamina = Mathf.Max(p.Stamina - damage, 0);
                    Debug.Log($"{CharacterName} のスキルが発動！相手 {p.Role} のスタミナを削った！");
                }
            }
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
