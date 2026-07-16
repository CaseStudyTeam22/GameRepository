/**
 * 核心配置 - Proto V10
 */
module.exports = {
    GRID_SIZE: 8,
    BEAT_INTERVAL: 375,
    GAME_DURATION: 150,

    // ターン制限定数
    TURN_MAX: 20,

    // 定力消耗：技能不消耗定力，定力只会因被攻击而减少
    COST: {
        'move': 0,
        'push': 0,
        'attack': 0,
        'defense': 0,
        'skill': 0,
        'rest': 0
    },

    // 筹码消耗：三档 power=1/2/3 对应 [小注, 加注, 梭哈]
    // defense / rest 不分档，取第一位
    CHIP_COST_BY_POWER: {
        'move': [1, 3, 5],
        'push': [3, 5, 9],
        'attack': [3, 5, 9],
        'defense': [2, 2, 2],
        'skill': [3, 5, 9],
        'rest': [6, 6, 6]
    },

    // 动作效果
    EFFECTS: {
        'move': { staminaDmg: 0 },
        'push': { staminaDmg: 0 },
        'attack': { staminaDmg: 1 }, // 小注削 1，大招削 3（× power）
        'defense': { reduction: 0.8 },
        'idle': { staminaRec: 0, chipsRec: 100 },
        'skill': { staminaDmg: 0 }, // ここがキャラによって可変するため、要調整
        'rest': { staminaRec: 1 }
    },

    // スキル効果
    SKILLS: {
        'docter': { heal: 2 },
        // 正直特殊処理が多すぎて値ってより関数にまとめたいな。
        // それこそこの設計を活かすなら値を呼んで適切に処理する関数的なね。

        // 他欲しいプロパティ
        // force action
        // no chip cost
        // no kb
        // no stamina cost
        // area(攻撃範囲)
        // can watch action
        // 
    },

    // キャラのパラメータ許容上限（改ざん対策用）
    LIMITS: {
        MAX_STAMINA_LIMIT: 16,
        SKILL_STAMINA_REC_LIMIT: 3,
        SKILL_CHIP_COST_LIMIT: 1000
    },

    // 【企画調整用】所持チップの上限。入手経路を問わず、これを超えた分は切り捨てる。
    MAX_CHIPS: 35000,

    INITIAL_MONEY: 10000,
    INITIAL_CHIPS: 0,
    INITIAL_STAMINA: 5,
    MAX_STAMINA: 5,

    ITEM_SPAWN_INTERVAL: 2,
    MAX_ITEMS_ON_FIELD: 10,
    CHIP_ITEM_VALUE: 100,
    MONEY_ITEM_VALUE: 500,

    // 債務者（debtor_skill）スキルの発動可能チップ上限。
    // チップがこの値を超えている場合はスキルを発動できない。
    // チップのインフレ調整時はこの値も合わせて見直すこと。
    DEBTOR_CHIP_THRESHOLD: 200,

    // 勝利条件：1点先取（ゲーム終了演出の確認用）
    // ファイナルレイズは、お互いどちらかが2-1になったときに発動する
    MAX_WINS: 1,
    FINAL_RAISE_TRIGGER_SCORE_WINNER: 2,
    FINAL_RAISE_TRIGGER_SCORE_LOSER: 1,

    // ファイナルレイズの提案・応答それぞれの制限時間（ミリ秒）。
    // 無応答はキャンセル扱いで通常の game_over へ進む。
    FINAL_RAISE_TIMEOUT_MS: 20000
};
