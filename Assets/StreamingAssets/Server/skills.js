/**
 * キャラクタースキルマネージャー - 各キャラ固有のフック・処理を定義
 */
const Skills = {
    /**
     * 1. 両替（チップ変換）時のフック
     * キャラクターに応じて、変換レートや得られるチップの量を補正します。
     * @param {Object} player プレイヤー情報
     * @param {number} pendingExchange 両替予定チップ数
     * @returns {number} 補正後の両替チップ数
     */
    onExchange: (player, pendingExchange) => {
        const skillId = player.skillData?.id;
        // 格闘家（charaIndex === 3 / fighter_skill）は両替効率が1.2倍になる (パッシブ効果)
        if (skillId === 'fighter_skill' || player.charaName === 'Fighter' || player.charaIndex === 3) {
            return Math.floor(pendingExchange * 1.2);
        }
        return pendingExchange;
    },

    /**
     * 2. アクション時のチップコスト計算フック
     * @param {Object} player プレイヤー情報
     * @param {Object} intent 選択した意図 (type, dir, power)
     * @param {number} baseCost 基本のチップコスト
     * @returns {number} 補正後のチップコスト
     */
    onCalculateCost: (player, intent, baseCost) => {
        const skillId = player.skillData?.id;
        // 両刃（charaIndex === 2 / double_cost_power）は、スキル発動時はチップ消費0、
        // 代わりにそれ以外の行動時の消費チップが2倍になる
        if (skillId === 'double_cost_power' || player.charaName === 'DoubleEdge' || player.charaIndex === 2) {
            if (intent.type === 'skill') {
                return 0;
            } else if (intent.type !== 'none') {
                return baseCost * 2;
            }
        }

        // その他のキャラでスキル発動時は、C#クライアント（スプシ）から送られてきた設定コストを適用
        if (intent.type === 'skill' && player.skillData) {
            return player.skillData.chipCost !== undefined ? player.skillData.chipCost : baseCost;
        }

        return baseCost;
    },

    /**
     * 3. アクション解決（Engine.resolveActions）時のフック
     * 固有スキルアクション（強化pushや回復など）を実行します。
     * @param {Object} player アクションを実行するプレイヤー
     * @param {Object} opponent 相手プレイヤー
     * @param {Object} intent アクション意図 (type, dir, power)
     * @param {Array} events 描画演出用のイベントリスト (追加用)
     * @param {Object} config config.js の参照
     */
    onResolve: (player, opponent, intent, events, config) => {
        if (intent.type !== 'skill') return;

        const skillId = player.skillData?.id;
        const charaIndex = player.charaIndex;

        // スキルID（推奨）またはインデックスによる分岐
        if (skillId === 'heal_instant' || charaIndex === 1 || player.charaName === 'Doctor') {
            // 医師: 定力回復
            const healAmount = (player.skillData && player.skillData.staminaRec) || 2;
            const baseMax = player.maxStamina || config.MAX_STAMINA;
            const maxStamina = player.selectedBuff === 'high_risk' ? (baseMax - 1) : baseMax;

            player.stamina = Math.min(maxStamina, player.stamina + healAmount);

            // 演出用イベントの追加
            events.push({ type: 'vfx', vfxType: 'rest_vfx', targetId: player.id });
            events.push({ type: 'vfx', vfxType: 'bump', targetId: player.id, text: `HEAL +${healAmount}` });
        }
        else if (skillId === 'nouveau_skill' || charaIndex === 2 || player.charaName === 'NouveauRiche') {
            // 成金: 本来のpushの行動の2倍のチップを消費して強化pushを出せる。また、消費したチップはフィールドにばらまかれる
            events.push({ type: 'vfx', vfxType: 'attack_vfx', targetId: player.id, dir: intent.dir, power: 3, x: player.prevX, y: player.prevY });



        }
        else if (skillId === 'fighter_skill' || charaIndex === 3 || player.charaName === 'Fighter') {
            // 格闘家キャラ: 自身を中心として3x3範囲へ、相手のスタミナを大きく削る攻撃
            const dir = intent.dir;
            events.push({ type: 'vfx', vfxType: 'attack_vfx', targetId: player.id, dir: dir, power: 2, x: player.prevX, y: player.prevY });

            const dx = opponent.x - player.x;
            const dy = opponent.y - player.y;

            // 相手が自身を中心とした3x3範囲内（自身を含まない）にいるか判定
            if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1 && (dx !== 0 || dy !== 0)) {
                // 相手が範囲内にいる場合、スタミナを大きく削る（固定値3）
                const dmg = 3;
                const oppIntent = opponent.intent || { type: 'none' };
                let finalDmg = dmg;

                if (oppIntent.type === 'defense') {
                    finalDmg = Math.floor(dmg * (1 - config.EFFECTS.defense.reduction));
                }

                opponent.stamina = Math.max(0, opponent.stamina - finalDmg);
                events.push({ type: 'hit', targetId: opponent.id, damage: finalDmg });
                events.push({ type: 'vfx', vfxType: 'bump', targetId: opponent.id, text: "FIGHTER STRIKE!" });
            }
        }
        else {
            // デフォルトスキル（スプシから送られてきたスキル情報に基づく汎用処理、または何もしない）
            const heal = player.skillData?.staminaRec || 0;
            if (heal > 0) {
                const baseMax = player.maxStamina || config.MAX_STAMINA;
                const maxStamina = player.selectedBuff === 'high_risk' ? (baseMax - 1) : baseMax;
                player.stamina = Math.min(maxStamina, player.stamina + heal);
                events.push({ type: 'vfx', vfxType: 'rest_vfx', targetId: player.id });
                events.push({ type: 'vfx', vfxType: 'bump', targetId: player.id, text: `HEAL +${heal}` });
            }
        }
    }
};

module.exports = Skills;
