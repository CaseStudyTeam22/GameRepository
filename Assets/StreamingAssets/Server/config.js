/**
 * 核心配置 - Proto V10
 */
module.exports = {
    GRID_SIZE: 8,
    BEAT_INTERVAL: 375,
    GAME_DURATION: 150,

    // 定力消耗：技能不消耗定力，定力只会因被攻击而减少
    COST: {
        'move':    0,
        'push':    0,
        'attack':  0,
        'defense': 0,
        'rest':    0
    },

    // 筹码消耗：三档 power=1/2/3 对应 [小注, 加注, 梭哈]
    // defense / rest 不分档，取第一位
    CHIP_COST_BY_POWER: {
        'move':    [1, 3, 5],
        'push':    [3, 5, 9],
        'attack':  [3, 5, 9],
        'defense': [2, 2, 2],
        'rest':    [6, 6, 6]
    },

    // 动作效果
    EFFECTS: {
        'move':    { staminaDmg: 0 },
        'push':    { staminaDmg: 0 },
        'attack':  { staminaDmg: 1 }, // 小注削 1，大招削 3（× power）
        'defense': { reduction: 0.8 },
        'idle':    { staminaRec: 0, chipsRec: 3 },
        'rest':    { staminaRec: 1 }
    },

    INITIAL_MONEY: 10000,
    INITIAL_CHIPS: 10,
    INITIAL_STAMINA: 5,
    MAX_STAMINA: 5,

    ITEM_SPAWN_INTERVAL: 2,
    MAX_ITEMS_ON_FIELD: 10,
    CHIP_ITEM_VALUE: 5,
    MONEY_ITEM_VALUE: 500,

    // ファイナルレイズの提案・応答それぞれの制限時間（ミリ秒）。
    // 無応答はキャンセル扱いで通常の game_over へ進む。
    FINAL_RAISE_TIMEOUT_MS: 20000
};
