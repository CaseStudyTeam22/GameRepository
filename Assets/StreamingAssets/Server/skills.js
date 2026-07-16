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
        const charaIndex = player.charaIndex;

        // 成金（charaIndex === 2 / nouveau_skill）は、スキル発動時は本来のpushの行動の2倍のチップを消費
        const isNarikin = skillId === 'nouveau_skill' || charaIndex === 2 || player.charaName === 'NouveauRiche';
        if (isNarikin) {
            if (intent.type === 'skill') {
                 const Config = require('./config');
                 const power = Math.max(1, Math.min(3, intent.power || 1));
                 const pushCosts = (player.chipCosts && player.chipCosts.push) || (player.baseChipCosts && player.baseChipCosts.push) || Config.CHIP_COST_BY_POWER.push;
                 const pushCost = pushCosts[power - 1] !== undefined ? pushCosts[power - 1] : 3;
                 return pushCost * 2;
            }
        }

        // 債務者（charaIndex === 6 / debtor_skill）: チップが閾値を超えている場合はスキル発動不可
        const isDebtor = skillId === 'debtor_skill' || charaIndex === 6 || player.charaName === 'Debtor';
        if (isDebtor && intent.type === 'skill') {
            const Config = require('./config');
            const threshold = Config.DEBTOR_CHIP_THRESHOLD !== undefined ? Config.DEBTOR_CHIP_THRESHOLD : 10;
            if (player.chips > threshold) {
                // コストを現在チップ+999にすることで「チップ不足」扱いにし発動を防ぐ
                return player.chips + 999;
            }
        }

        // その他のキャラでスキル発動時は、C#クライアント（スプシ）から送られてきた設定コストを適用しつつ、バフ・デバフを適用
        if (intent.type === 'skill' && player.skillData) {
            if (player.skillData.chipCost !== undefined) {
                const activeDebuffs = player.activeDebuffs || {};
                const modifiers = player.modifiers || {};

                // 行動時チップ消費量補正
                const actionCostDebuff = activeDebuffs.actionCost || 0;
                const actionCostBuff = modifiers.actionCostBonus || 0;
                const totalActionCostMod = actionCostDebuff + actionCostBuff;

                // スキルチップ消費量補正
                const skillCostDebuff = activeDebuffs.skillCost || 0;
                const skillCostBuff = modifiers.skillCostBonus || 0;
                const totalSkillCostMod = skillCostDebuff + skillCostBuff;

                // 基本コストにスキル軽減補正と行動軽減補正を加算する（下限は0）
                return Math.max(0, player.skillData.chipCost + totalSkillCostMod + totalActionCostMod);
            }
            return baseCost;
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
     * @param {Array} items フィールド上のアイテムリスト (参照渡し)
     */
    onResolve: (player, opponent, intent, events, config, items) => {
        if (intent.type !== 'skill') return;

        const skillId = player.skillData?.id;
        const charaIndex = player.charaIndex;

        // スキルID（推奨）またはインデックスによる分岐
        if (skillId === 'heal_instant' || charaIndex === 1 || player.charaName === 'Doctor') {
            // 医師: 定力回復
            let healAmount = (player.skillData && player.skillData.staminaRec) || 2;
            if (player.modifiers && player.modifiers.charaUniqueBuff) {
                healAmount += 10;
            }
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
            const startDist = Math.abs(opponent.x - player.x) + Math.abs(opponent.y - player.y);
            const power = Math.max(1, Math.min(3, intent.power || 1));
            const pushCost = (config.CHIP_COST_BY_POWER && config.CHIP_COST_BY_POWER['push']) ? config.CHIP_COST_BY_POWER['push'][power - 1] : 3;
            const consumedChips = pushCost * 2;

            // 押し出し判定 (初期位置で隣接しているか、または突進衝突したか)
            const col = getPushCollision(player, opponent, intent);
            const isPushHit = (startDist === 1) || (col !== null);
            if (isPushHit) {
                const dCol = (startDist === 1) ? 0 : col.d_col;
                const pushBonus = (player.modifiers && player.modifiers.pushPowerBonus) || 0;
                let finalDist = power + 2 + pushBonus; // 強化pushなので base: power + 2

                // 高リスク攻撃：power=3 push +1
                if (player.selectedBuff === 'high_risk' && power === 3) {
                    finalDist += 1;
                }

                const tIntent = opponent.intent || { type: 'none' };
                // 相手の押し出しによる相殺
                if (tIntent.type === 'push' && tIntent.dir !== intent.dir) {
                    const tPower = Math.max(1, Math.min(3, tIntent.power || 1));
                    finalDist = Math.max(0, finalDist - tPower);
                }

                // 相手の防御による軽減
                if (tIntent.type === 'defense') {
                    finalDist = Math.max(0, finalDist - 2);
                }

                // ガーディアン: スキル中はノックバック無効
                if (tIntent.type === 'skill' && opponent.skillData?.id === 'guardian_skill') {
                    finalDist = 0;
                    events.push({ type: 'mission_progress', playerId: opponent.id, missionType: 'GuardianSkillDefense', amount: 1 });
                }

                // 高リスク被撃：30% 確率でpush_powerを+1
                if (finalDist > 0 && opponent.selectedBuff === 'high_risk' && Math.random() < 0.3) {
                    finalDist += 1;
                }

                if (finalDist > 0) {
                    if (intent.dir === 'up') opponent.y -= finalDist;
                    else if (intent.dir === 'down') opponent.y += finalDist;
                    else if (intent.dir === 'left') opponent.x -= finalDist;
                    else if (intent.dir === 'right') opponent.x += finalDist;

                    events.push({ type: 'pushed', targetId: opponent.id, dir: intent.dir, dist: finalDist });
                    events.push({ type: 'vfx', vfxType: 'bump', targetId: opponent.id, text: "SUPER PUSH!" });
                }

                // 突進したプレイヤーの座標を、実際に相手を押し出した距離（finalDist）と衝突までの歩数（dCol）に合わせて制限する
                const actualDist = Math.min(power, dCol + finalDist);
                let dx = 0, dy = 0;
                if (intent.dir === 'up') dy = -1;
                else if (intent.dir === 'down') dy = 1;
                else if (intent.dir === 'left') dx = -1;
                else if (intent.dir === 'right') dx = 1;

                player.x = player.prevX + dx * actualDist;
                player.y = player.prevY + dy * actualDist;
            }

            // 消費したチップのばらまき処理 (items に追加)
            if (items) {
                const chipValue = config.CHIP_ITEM_VALUE || 50;
                const numItems = Math.max(1, Math.round(consumedChips / chipValue));

                for (let i = 0; i < numItems; i++) {
                    const angle = Math.random() * Math.PI * 2;
                    const dist = 1.0 + Math.random() * 1.5;
                    let tx = Math.round(player.x + Math.cos(angle) * dist);
                    let ty = Math.round(player.y + Math.sin(angle) * dist);

                    tx = Math.max(0, Math.min(config.GRID_SIZE - 1, tx));
                    ty = Math.max(0, Math.min(config.GRID_SIZE - 1, ty));

                    items.push({
                        id: Date.now() + Math.random() + i,
                        type: 'chips',
                        x: tx,
                        y: ty
                    });
                }
                events.push({ type: 'vfx', vfxType: 'bump', targetId: player.id, text: "GOLDEN LAUNCH!" });
            }
        }
        else if (skillId === 'fighter_skill' || charaIndex === 3 || player.charaName === 'Fighter') {
            // 格闘家キャラ: 自身の一歩前方を中心とした3x3範囲へ、相手のスタミナを大きく削る攻撃
            const dir = intent.dir || (player.role === 'P2' ? 'down' : 'up');
            events.push({ type: 'vfx', vfxType: 'attack_vfx', targetId: player.id, dir: dir, power: 2, x: player.prevX, y: player.prevY });

            // 一歩前方の座標
            let cx = player.x;
            let cy = player.y;
            if (dir === 'up') cy -= 1;
            else if (dir === 'down') cy += 1;
            else if (dir === 'left') cx -= 1;
            else if (dir === 'right') cx += 1;

            const dx = opponent.x - cx;
            const dy = opponent.y - cy;

            // 相手がその一歩前方を中心とした3x3範囲内（かつ自分自身ではない）にいるか判定
            if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1 && (opponent.x !== player.x || opponent.y !== player.y)) {
                // 相手が範囲内にいる場合、スタミナを大きく削る(固定値3、バフ時は13)
                let dmg = 3;
                if (player.modifiers && player.modifiers.charaUniqueBuff) {
                    dmg += 10;
                }
                const oppIntent = opponent.intent || { type: 'none' };
                let finalDmg = dmg;

                if (oppIntent.type === 'defense') {
                    finalDmg = Math.floor(dmg * (1 - config.EFFECTS.defense.reduction));
                }

                // ガーディアン: スキル中はダメージ無効
                if (oppIntent.type === 'skill' && opponent.skillData?.id === 'guardian_skill') {
                    finalDmg = 0;
                    events.push({ type: 'mission_progress', playerId: opponent.id, missionType: 'GuardianSkillDefense', amount: 1 });
                }

                if (finalDmg > 0) {
                    opponent.stamina = Math.max(0, opponent.stamina - finalDmg);
                    events.push({ type: 'hit', targetId: opponent.id, damage: finalDmg });
                    events.push({ type: 'vfx', vfxType: 'bump', targetId: opponent.id, text: "FIGHTER STRIKE!" });
                }
            }
        }
        else if (skillId === 'guardian_skill' || charaIndex === 4 || player.charaName === 'Guardian') {
            // ガーディアン: スキル中に被弾してもスタミナが減らず吹き飛ばされない（無敵防御）
            // 実際の無敵化はengine.jsの push/attack 処理内で isGuardianBlocking() によって行われる。
            // ここでは演出イベントのみ追加する。
            events.push({ type: 'vfx', vfxType: 'defense_vfx', targetId: player.id, x: player.prevX, y: player.prevY });
            events.push({ type: 'vfx', vfxType: 'bump', targetId: player.id, text: 'GUARD!' });
        }
        else if (skillId === 'scammer_skill' || charaIndex === 5 || player.charaName === 'Scammer') {
            // イカサマ: scammerActive フラグを立てる。
            // server.js の set_intent ハンドラがこのフラグを見て、相手の intent を当該 Socket にのみ Emit する。
            if (!player.scammerActive) {
                player.scammerActive = true;
                events.push({ type: 'vfx', vfxType: 'bump', targetId: player.id, text: 'READING...' });
            }
            events.push({ type: 'vfx', vfxType: 'rest_vfx', targetId: player.id });
        }
        else if (skillId === 'debtor_skill' || charaIndex === 6 || player.charaName === 'Debtor') {
            // 債務者: フィールド上の全アイテムを回収 + 次の突進を強化する
            // ※ チップ条件（chips <= DEBTOR_CHIP_THRESHOLD）は onCalculateCost で担保済み
            let chipsGained = 0;
            let moneyGained = 0;

            if (items) {
                const chipValue = config.CHIP_ITEM_VALUE || 50;
                const moneyValue = config.MONEY_ITEM_VALUE || 500;
                let collectedChipsCount = 0;
                for (let i = items.length - 1; i >= 0; i--) {
                    if (items[i].type === 'chips') {
                        chipsGained += chipValue;
                        collectedChipsCount++;
                    } else {
                        moneyGained += moneyValue;
                    }
                    items.splice(i, 1);
                }

                for (let i = 0; i < collectedChipsCount; i++) {
                    events.push({
                        type: 'mission_progress',
                        playerId: player.id,
                        missionType: 'GainChip',
                        amount: 1
                    });
                }
            }

            player.chips += chipsGained;
            player.money += moneyGained;
            // 次に使用する突進を +2 強化する（一時的フラグ）
            player.nextPushBonus = 2;

            events.push({ type: 'vfx', vfxType: 'rest_vfx', targetId: player.id });
            const gainText = chipsGained > 0 ? `COLLECT! +${chipsGained}chips` : 'COLLECT!';
            events.push({ type: 'vfx', vfxType: 'bump', targetId: player.id, text: gainText });
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

function getPushCollision(p, target, intent) {
    if (intent.type !== 'push' && !(intent.type === 'skill' && (p.charaIndex === 2 || p.charaName === 'NouveauRiche'))) {
        return null;
    }
    const power = Math.max(1, Math.min(3, intent.power || 1));
    let dx = 0, dy = 0;
    if (intent.dir === 'up') dy = -1;
    else if (intent.dir === 'down') dy = 1;
    else if (intent.dir === 'left') dx = -1;
    else if (intent.dir === 'right') dx = 1;

    for (let k = 1; k <= power; k++) {
        const tx = p.prevX + dx * k;
        const ty = p.prevY + dy * k;
        if (tx === target.prevX && ty === target.prevY) {
            return {
                d_col: k - 1,
                dx,
                dy
            };
        }
    }
    return null;
}

module.exports = Skills;
