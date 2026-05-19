const Config = require('./config');

/**
 * 核心引擎 - Proto V10 三档 power 版
 */
const Engine = {
    resolveActions: (players, intents, items) => {
        const events = [];
        const nextPlayers = JSON.parse(JSON.stringify(players));
        const ids = Object.keys(nextPlayers);
        if (ids.length < 2) return { players: nextPlayers, items, events };

        const p1Id = ids[0], p2Id = ids[1];
        const p1 = nextPlayers[p1Id], p2 = nextPlayers[p2Id];
        const i1 = intents[p1Id] || { type: 'none' };
        const i2 = intents[p2Id] || { type: 'none' };

        // --- 1. 基础状态初始化 ---
        [p1, p2].forEach(p => {
            const intent = intents[p.id] || { type: 'none' };
            const power = Math.max(1, Math.min(3, intent.power || 1));

            // 筹码消耗：按 power 查表
            const costTable = Config.CHIP_COST_BY_POWER[intent.type];
            const chipCost = costTable ? costTable[power - 1] : 0;
            if (p.chips < chipCost) intent.type = 'none';
            else p.chips -= chipCost;

            const baseMax = p.maxStamina || 5;
            // Buff: 高风险降低定力上限到 4；低风险 rest 额外 +1
            const maxStamina = p.selectedBuff === 'high_risk' ? (baseMax - 1) : baseMax;
            const restRec = Config.EFFECTS.rest.staminaRec + (p.selectedBuff === 'low_risk' ? 1 : 0);
            if (intent.type === 'rest') p.stamina = Math.min(maxStamina, p.stamina + restRec);
            else if (intent.type === 'none') {
                p.stamina = Math.min(maxStamina, p.stamina + Config.EFFECTS.idle.staminaRec);
                p.chips += Config.EFFECTS.idle.chipsRec;
            }
            // 确保当前定力不超上限（选卡后定力需要被 clamp）
            p.stamina = Math.min(p.stamina, maxStamina);

            p.prevX = p.x; p.prevY = p.y;
            p.targetX = p.x; p.targetY = p.y;

            // 计算预想目标：push 自己前进 1 格；move 前进 power 格（遇障碍/越界会在后面限制）
            if (intent.type === 'push') {
                if (intent.dir === 'up') p.targetY--;
                else if (intent.dir === 'down') p.targetY++;
                else if (intent.dir === 'left') p.targetX--;
                else if (intent.dir === 'right') p.targetX++;
            } else if (intent.type === 'move') {
                if (intent.dir === 'up') p.targetY -= power;
                else if (intent.dir === 'down') p.targetY += power;
                else if (intent.dir === 'left') p.targetX -= power;
                else if (intent.dir === 'right') p.targetX += power;
            }
        });

        // move 多格的逐格碰撞：如果路径上有对方占格，停在对方格之前的最后一个空格
        // 起始距离（在位移前计算）
        const startDist = Math.abs(p2.x - p1.x) + Math.abs(p2.y - p1.y);

        [p1, p2].forEach(p => {
            const intent = intents[p.id] || { type: 'none' };
            if (intent.type !== 'move') return;
            const other = p.id === p1Id ? p2 : p1;
            const power = Math.max(1, Math.min(3, intent.power || 1));
            // 从 prev 出发逐格推进，遇到对方（用其 prev 位置判定，避免交错冲突）或越界则停止
            let cx = p.prevX, cy = p.prevY;
            let dx = 0, dy = 0;
            if (intent.dir === 'up') dy = -1;
            else if (intent.dir === 'down') dy = 1;
            else if (intent.dir === 'left') dx = -1;
            else if (intent.dir === 'right') dx = 1;
            for (let step = 0; step < power; step++) {
                const nx = cx + dx, ny = cy + dy;
                if (nx < 0 || nx >= Config.GRID_SIZE || ny < 0 || ny >= Config.GRID_SIZE) break;
                if (nx === other.prevX && ny === other.prevY) break;
                cx = nx; cy = ny;
            }
            p.targetX = cx; p.targetY = cy;
        });

        // --- 2. 优先级判定值 ---
        p1.priority = p1.stamina + (i1.type === 'defense' ? 10 : 0);
        p2.priority = p2.stamina + (i2.type === 'defense' ? 10 : 0);

        // --- 3. 核心冲突判定逻辑 ---
        const isTargetConflict = (p1.targetX === p2.targetX && p1.targetY === p2.targetY);
        const isHeadOn = (p1.targetX === p2.prevX && p1.targetY === p2.prevY && p2.targetX === p1.prevX && p2.targetY === p1.prevY);

        // 拳力：push 的档位 1/2/3 直接对应拳力；非 push 为 0
        const getPF = (intent) => intent.type === 'push' ? Math.max(1, Math.min(3, intent.power || 1)) : 0;

        if (isTargetConflict) {
            const p1Moved = (p1.targetX !== p1.prevX || p1.targetY !== p1.prevY);
            const p2Moved = (p2.targetX !== p2.prevX || p2.targetY !== p2.prevY);

            if (p1Moved && p2Moved) {
                const pf1 = getPF(i1);
                const pf2 = getPF(i2);

                if (pf1 > 0 && pf2 > 0) {
                    if (pf1 === pf2 && Math.abs(p1.priority - p2.priority) <= 1) {
                        p1.x = p1.prevX; p1.y = p1.prevY;
                        p2.x = p2.prevX; p2.y = p2.prevY;
                        const midX = p1.targetX, midY = p1.targetY;
                        events.push({ type: 'clash_explosion', x: midX, y: midY });
                        generateExplosionItems(items, midX, midY);
                        events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: p1.id, dir: i1.dir, x: p1.x, y: p1.y });
                        events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: p2.id, dir: i2.dir, x: p2.x, y: p2.y });
                    } else {
                        const winner = pf1 > pf2 ? p1 : (pf2 > pf1 ? p2 : (p1.priority > p2.priority ? p1 : p2));
                        const loser = winner === p1 ? p2 : p1;
                        const winIntent = winner === p1 ? i1 : i2;
                        winner.x = winner.targetX; winner.y = winner.targetY;
                        loser.x = loser.prevX; loser.y = loser.prevY;
                        const diff = Math.abs(pf1 - pf2);
                        if (winIntent.dir === 'up') loser.y -= diff;
                        else if (winIntent.dir === 'down') loser.y += diff;
                        else if (winIntent.dir === 'left') loser.x -= diff;
                        else if (winIntent.dir === 'right') loser.x += diff;
                        events.push({ type: 'vfx', vfxType: 'bump', targetId: loser.id, text: "PUSHED!" });
                        events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: winner.id, dir: winIntent.dir, x: winner.prevX, y: winner.prevY });
                    }
                } else if (Math.abs(p1.priority - p2.priority) <= 1) {
                    events.push({ type: 'clash_moment', players: [p1Id, p2Id], x: p1.targetX, y: p1.targetY });
                    p1.x = p1.prevX; p1.y = p1.prevY;
                    p2.x = p2.prevX; p2.y = p2.prevY;
                } else {
                    const winner = p1.priority > p2.priority ? p1 : p2;
                    const loser = p1.priority > p2.priority ? p2 : p1;
                    winner.x = winner.targetX; winner.y = winner.targetY;
                    loser.x = loser.prevX; loser.y = loser.prevY;
                    events.push({ type: 'vfx', vfxType: 'bump', targetId: loser.id, text: "BLOCKED" });
                }
            } else if (p1Moved || p2Moved) {
                const mover = p1Moved ? p1 : p2;
                mover.x = mover.prevX; mover.y = mover.prevY;
                events.push({ type: 'vfx', vfxType: 'bump', targetId: mover.id, text: "BLOCKED" });
            }
        }
        else if (isHeadOn) {
            const pf1 = getPF(i1);
            const pf2 = getPF(i2);

            if (pf1 > 0 && pf2 > 0) {
                if (pf1 === pf2 && Math.abs(p1.priority - p2.priority) <= 1) {
                    p1.x = p1.prevX; p1.y = p1.prevY;
                    p2.x = p2.prevX; p2.y = p2.prevY;
                    const midX = (p1.prevX + p2.prevX) / 2, midY = (p1.prevY + p2.prevY) / 2;
                    events.push({ type: 'clash_explosion', x: midX, y: midY });
                    generateExplosionItems(items, midX, midY);
                    events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: p1.id, dir: i1.dir, x: p1.x, y: p1.y });
                    events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: p2.id, dir: i2.dir, x: p2.x, y: p2.y });
                } else {
                    const winner = pf1 > pf2 ? p1 : (pf2 > pf1 ? p2 : (p1.priority > p2.priority ? p1 : p2));
                    const loser = winner === p1 ? p2 : p1;
                    const winIntent = winner === p1 ? i1 : i2;
                    winner.x = winner.targetX; winner.y = winner.targetY;
                    loser.x = loser.prevX; loser.y = loser.prevY;
                    const diff = Math.abs(pf1 - pf2) || 1;
                    if (winIntent.dir === 'up') loser.y -= diff;
                    else if (winIntent.dir === 'down') loser.y += diff;
                    else if (winIntent.dir === 'left') loser.x -= diff;
                    else if (winIntent.dir === 'right') loser.x += diff;
                    events.push({ type: 'vfx', vfxType: 'bump', targetId: loser.id, text: "KICKED!" });
                    events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: winner.id, dir: winIntent.dir, x: winner.prevX, y: winner.prevY });
                }
            } else if (Math.abs(p1.priority - p2.priority) <= 1) {
                events.push({ type: 'clash_moment', players: [p1Id, p2Id], x: p1.targetX, y: p1.targetY });
                p1.x = p1.prevX; p1.y = p1.prevY;
                p2.x = p2.prevX; p2.y = p2.prevY;
            } else {
                const winner = p1.priority > p2.priority ? p1 : p2;
                const loser = p1.priority > p2.priority ? p2 : p1;
                winner.x = winner.targetX; winner.y = winner.targetY;
                loser.x = loser.prevX; loser.y = loser.prevY;
                const winIntent = intents[winner.id];
                if (winIntent.dir === 'up') loser.y--;
                else if (winIntent.dir === 'down') loser.y++;
                else if (winIntent.dir === 'left') loser.x--;
                else if (winIntent.dir === 'right') loser.x++;
                events.push({ type: 'vfx', vfxType: 'bump', targetId: loser.id, text: "KICKED!" });
            }
        }
        else {
            // 无冲突移动
            p1.x = p1.targetX; p1.y = p1.targetY;
            p2.x = p2.targetX; p2.y = p2.targetY;
        }

        // --- 4. 执行动作效果 ---
        [p1, p2].forEach(p => {
            const intent = intents[p.id];
            if (!intent) return;
            const target = p.id === p1Id ? p2 : p1;
            const power = Math.max(1, Math.min(3, intent.power || 1));

            // 严格邻位判定：只有起始相邻，动作才生效
            if (intent.type === 'push' && startDist === 1) {
                events.push({ type: 'vfx', vfxType: 'push_vfx', targetId: p.id, dir: intent.dir, power: intent.power, x: p.prevX, y: p.prevY });
                let finalDist = power;
                // 高风险攻击方：power=3 时推距 +1
                if (p.selectedBuff === 'high_risk' && power === 3) finalDist += 1;
                const tIntent = intents[target.id] || { type: 'none' };
                if (tIntent.type === 'push' && tIntent.dir !== intent.dir) {
                    const tPower = Math.max(1, Math.min(3, tIntent.power || 1));
                    finalDist = Math.max(0, finalDist - tPower);
                }
                if (tIntent.type === 'defense') finalDist = 0;
                // 高风险被击方：30% 概率推距 +1
                if (finalDist > 0 && target.selectedBuff === 'high_risk' && Math.random() < 0.3) finalDist += 1;

                if (intent.dir === 'up') target.y -= finalDist;
                else if (intent.dir === 'down') target.y += finalDist;
                else if (intent.dir === 'left') target.x -= finalDist;
                else if (intent.dir === 'right') target.x += finalDist;
                if (finalDist > 0) events.push({ type: 'pushed', targetId: target.id, dir: intent.dir, dist: finalDist });
            }

            if (intent.type === 'attack' && startDist === 1) {
                events.push({ type: 'vfx', vfxType: 'attack_vfx', targetId: p.id, dir: intent.dir, power: intent.power, x: p.prevX, y: p.prevY });
                let dmg = Config.EFFECTS.attack.staminaDmg * power;
                // 高风险攻击方：power=3 伤害 +1
                if (p.selectedBuff === 'high_risk' && power === 3) dmg += 1;
                const tIntent = intents[target.id] || { type: 'none' };
                if (tIntent.type === 'defense') dmg = Math.floor(dmg * (1 - Config.EFFECTS.defense.reduction));
                // 高风险被击方：30% 概率伤害 +1
                if (dmg > 0 && target.selectedBuff === 'high_risk' && Math.random() < 0.3) dmg += 1;
                target.stamina = Math.max(0, target.stamina - dmg);
                events.push({ type: 'hit', targetId: target.id, damage: dmg });
            }

            if (intent.type === 'rest') events.push({ type: 'vfx', vfxType: 'rest_vfx', targetId: p.id, x: p.x, y: p.y });
        });

        // --- 5. 拾取物品 ---
        [p1, p2].forEach(p => {
            for (let i = items.length - 1; i >= 0; i--) {
                if (p.x === items[i].x && p.y === items[i].y) {
                    if (items[i].type === 'chips') p.chips += Config.CHIP_ITEM_VALUE;
                    else p.money += Config.MONEY_ITEM_VALUE;
                    items.splice(i, 1);
                }
            }
        });

        // 最终安全检查：严禁坐标重叠
        if (p1.x === p2.x && p1.y === p2.y) {
            p1.x = p1.prevX; p1.y = p1.prevY;
            p2.x = p2.prevX; p2.y = p2.prevY;
        }

        return { players: nextPlayers, items, events };
    }
};

function generateExplosionItems(items, midX, midY) {
    const count = 3 + Math.floor(Math.random() * 3);
    for (let i = 0; i < count; i++) {
        const angle = Math.random() * Math.PI * 2;
        const dist = 0.5 + Math.random() * 1.5;
        let tx = Math.round(midX + Math.cos(angle) * dist);
        let ty = Math.round(midY + Math.sin(angle) * dist);
        tx = Math.max(0, Math.min(Config.GRID_SIZE - 1, tx));
        ty = Math.max(0, Math.min(Config.GRID_SIZE - 1, ty));
        items.push({ id: Date.now() + Math.random(), type: Math.random() > 0.3 ? 'chips' : 'money', x: tx, y: ty });
    }
}

module.exports = Engine;
