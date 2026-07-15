const express = require('express');
const http = require('http');
const { Server } = require('socket.io');
const path = require('path');
const dgram = require('dgram');
const os = require('os');

const Config = require('./config');
const Engine = require('./engine');
const Skills = require('./skills');

const app = express();
const server = http.createServer(app);
// DEV ONLY: pingTimeout を 5 分まで延長。MPPM の Virtual Player が
// フォーカスを失うとメインスレッドが止まり SocketIO の heartbeat も停止する。
// デフォルト 20 秒だと開発中に頻繁にキックされるため一時的に緩和している。
// 正式リリース前に pingTimeout: 20000（デフォルト値）に戻し、
// クライアント側（SocketIONetClient）に切断時の自動再接続を実装すること。
const io = new Server(server, {
    pingTimeout: 300000,
    pingInterval: 25000
});

app.use(express.static(path.join(__dirname, 'public')));

let players = {};
let items = [];
let currentBeat = 0;
let cycleCount = 0;
let timeLeft = Config.GAME_DURATION;
let gameActive = false;
let beatSequence = 0;
let roundId = 0;
let beatStartServerMs = 0;

// 双方準備完了後のカウントダウン中の setTimeout 句柄。取り消し時に clear する。
let lobbyCountdownTimer = null;
// チップ交換 / カード選択の制限時間タイマー句柄。全員完了時 / フェーズ離脱時に clear する。
let exchangeTimer = null;
let buffTimer = null;
let missionTimer = null;
let roundIntroTimer = null;

// カウントダウンの長さ（ミリ秒）。
const LOBBY_COUNTDOWN_MS = 3200;
// チップ交換 / カード選択フェーズの制限時間（ミリ秒）。
const PREPARE_PHASE_MS = 20000;
// ミッション選択フェーズの制限時間（ミリ秒）
const MISSION_PHASE_MS = 15000;
// 盤面・キャラ生成の完了後、チップ交換フェーズへ進むまでの待ち時間（ミリ秒）。
const ROUND_INTRO_MS = 1500;

// ファイナルレイズ進行中フラグ。true の間は次の round_over で勝者が直接全勝（game_over）。
let isFinalDuel = false;
// サドンデス進行中フラグ。ファイナルレイズと同じ決勝局だが、
// こちらは両替・カード選択・ミッション選択を飛ばして即開戦する点が異なる。
let isSuddenDeath = false;
// 提案者（直前ラウンドの敗者）・応答者（直前ラウンドの勝者）の socket id を保持する。
// 切断や resetMatch でクリアする。
let finalRaiseProposerId = null;
let finalRaiseResponderId = null;
// 提案・応答それぞれの制限時間タイマー句柄。
let finalRaiseOfferTimer = null;
let finalRaisePendingTimer = null;
// 優勢側を記録する変数
let finalRaiseFavoredRole = null;
// ファイナルレイズ時のターンカウント
let finalRaiseTurnCount = 0;

function isGuardianBlocking(player, intents) {
    if (!player || !intents) return false;
    const intent = intents[player.id] || {};
    return intent.type === 'skill' && player.skillData?.id === 'guardian_skill';
}

function updatePlayerCurrentStats(p) {
    if (!p) return;
    p.modifiers = p.modifiers || { maxStaminaBonus: 0, pushPowerBonus: 0, defenseReductionBonus: 0.0, chipCostMultiplier: 1.0, defenseBonus: 0 };
    p.activeDebuffs = p.activeDebuffs || {};

    const baseMax = p.maxStamina || Config.MAX_STAMINA;
    const maxStamina = p.selectedBuff === 'high_risk' ? (baseMax - 1) : baseMax;
    const bonus = p.modifiers.maxStaminaBonus || 0;
    const debuffStamina = p.activeDebuffs.maxStamina || 0;
    p.currentMaxStamina = maxStamina + bonus + debuffStamina;

    const basePush = p.basePushPower || 0;
    const pushBonus = p.modifiers.pushPowerBonus || 0;
    const debuffPush = p.activeDebuffs.pushPower || 0;
    p.currentPushPower = basePush + pushBonus + debuffPush;

    const baseDef = p.baseDefensePower || 0;
    const defBonus = p.modifiers.defenseReductionBonus ? Math.round(p.modifiers.defenseReductionBonus * 10) : 0;
    const debuffDef = p.activeDebuffs.defenseReduction ? Math.round(p.activeDebuffs.defenseReduction * 10) : 0;
    p.currentDefensePower = baseDef + defBonus + debuffDef;
}

// sync_state 送信時に自動的にステータスを更新するラッパー
const originalEmit = io.emit;
io.emit = function (event, ...args) {
    if (event === 'sync_state') {
        for (let id in players) {
            updatePlayerCurrentStats(players[id]);
            const pl = players[id];

            pl.activeDebuffs = pl.activeDebuffs || {};
            pl.modifiers = pl.modifiers || {};

            // 行動チップ消費量の調整
            const actionCostDebuff = pl.activeDebuffs.actionCost || 0;
            const actionCostBuff = pl.modifiers.actionCostBonus || 0;
            const totalActionCostMod = actionCostDebuff + actionCostBuff;

            // スキルチップ消費量の調整
            const skillCostDebuff = pl.activeDebuffs.skillCost || 0;
            const skillCostBuff = pl.modifiers.skillCostBonus || 0;
            const totalSkillCostMod = skillCostDebuff + skillCostBuff;

            const baseCosts = pl.baseChipCosts || Config.CHIP_COST_BY_POWER;
            const isDebtorUniqueFR = isFinalDuel && pl.modifiers.charaUniqueBuff && (pl.charaIndex === 6 || pl.charaName === 'Debtor');
            if (isDebtorUniqueFR) {
                pl.chipCosts = {
                    move: [0, 0, 0],
                    push: [0, 0, 0],
                    attack: [0, 0, 0],
                    defense: [0, 0, 0],
                    skill: [0, 0, 0],
                    rest: [0, 0, 0]
                };
            } else {
                pl.chipCosts = {
                    move: baseCosts.move.map(c => Math.max(0, c + totalActionCostMod)),
                    push: baseCosts.push.map(c => Math.max(0, c + totalActionCostMod)),
                    attack: baseCosts.attack.map(c => Math.max(0, c + totalActionCostMod)),
                    defense: baseCosts.defense.map(c => Math.max(0, c + totalActionCostMod)),
                    skill: baseCosts.skill.map(c => Math.max(0, c + totalSkillCostMod + totalActionCostMod)),
                    rest: baseCosts.rest.map(c => Math.max(0, c + totalActionCostMod))
                };
            }

            // 配信直前に所持チップを上限まで丸める
            if (pl.chips > Config.MAX_CHIPS) pl.chips = Config.MAX_CHIPS;
        }
    }
    return originalEmit.apply(io, [event, ...args]);
};

function resetPlayerPos(id) {
    const p = players[id];
    if (!p) return;
    const startPos = p.role === 'P1' ? { x: 1, y: 6 } : { x: 6, y: 1 };
    p.x = startPos.x; p.y = startPos.y;
    p.color = p.role === 'P1' ? '#00f2fe' : '#ff4444';
    // クランプ・バリデーションされた maxStamina と modifiers のボーナスを加味して定力をリセット
    updatePlayerCurrentStats(p);
    p.stamina = p.currentMaxStamina;
    p.falling = false; p.intent = null;
    p.selectedBuff = null; p.buffReady = false; p.pendingExchange = 0;
    // 債務者の次回突進強化はラウンド間で持ち越さない
    p.nextPushBonus = 0;
    // scammerActive はユニークバフがある場合のみラウンド間で持続する。通常はリセットする。
    if (!p.modifiers || !p.modifiers.charaUniqueBuff) {
        p.scammerActive = false;
    }

    if (!isFinalDuel) {
        p.chips = p.initChips !== undefined ? p.initChips : Config.INITIAL_CHIPS;
    }
}

// ラウンドの開始要求。クライアントの盤面・キャラ生成が終わるのを待ってからチップ交換へ進む。
// resetMatch / round_over の直後にここを通し、双方の round_ready を待つ。
function beginRound() {
    roundId++;
    beatSequence = 0;
    beatStartServerMs = 0
    for (let id in players) players[id].roundReady = false;
    if (roundIntroTimer) { clearTimeout(roundIntroTimer); roundIntroTimer = null; }
    // 位置を先に初期化して配る。クライアントは再生成時に新しい位置のキャラを出せる。
    for (let id in players) resetPlayerPos(id);
    io.emit('sync_state', { players });
    io.emit('prepare_round');
}

// 双方のクライアントが盤面・キャラ生成を終えたら、少し間を置いてチップ交換フェーズへ進む。
// サドンデス中は両替・カード選択・ミッション選択を飛ばして即開戦する。
function checkAllRoundReady() {
    const pList = Object.values(players);
    if (pList.length >= 2 && pList.every(pl => pl.roundReady)) {
        if (roundIntroTimer) clearTimeout(roundIntroTimer);
        roundIntroTimer = setTimeout(() => {
            roundIntroTimer = null;
            if (isSuddenDeath) prepareSuddenDeathBattle();
            else prepareExchangePhase();
        }, ROUND_INTRO_MS);
    }
}

// サドンデス用の開戦準備。両替・カード選択・ミッション選択のフェーズを飛ばし、
// 対戦に必要な初期化だけ行って即カウントダウンへ進む。所持金のチップ変換は
// startSuddenDeath で済ませてあるため、ここでは行わない。
function prepareSuddenDeathBattle() {
    items = []; currentBeat = 0; timeLeft = Config.GAME_DURATION; cycleCount = 0;
    // resetPlayerPos は isFinalDuel が立っている間チップを保持するため、変換済みのチップは消えない。
    for (let id in players) resetPlayerPos(id);
    io.emit('sync_state', { players });
    io.emit('sync_items', items);
    startBattleCountdown();
}

function isNouveauRiche(player) {
    if (!player) return false;
    if (player.modifiers && player.modifiers.charaUniqueBuff) return false;
    return player.charaIndex === 2 || player.charaName === 'NouveauRiche' || (player.skillData && player.skillData.id === 'double_cost_power');
}

function checkNouveauRicheAutoExchange() {
    const pList = Object.values(players);
    if (pList.length < 2) return;

    const p1 = pList[0];
    const p2 = pList[1];

    const isN1 = isNouveauRiche(p1);
    const isN2 = isNouveauRiche(p2);

    if (isN1 && isN2) {
        // 両者が成金の場合は現在所持金額の50%をそれぞれがかけてチップとする
        if (p1.pendingExchange === 0) {
            p1.pendingExchange = Math.floor(p1.money * 0.5);
            p1.exchanged = true;
            console.log(`[Server] NouveauRiche vs NouveauRiche: ${p1.role} auto-exchanged ${p1.pendingExchange}`);
        }
        if (p2.pendingExchange === 0) {
            p2.pendingExchange = Math.floor(p2.money * 0.5);
            p2.exchanged = true;
            console.log(`[Server] NouveauRiche vs NouveauRiche: ${p2.role} auto-exchanged ${p2.pendingExchange}`);
        }

    } else if (isN1 && !isN2) {
        // P1が成金、P2が通常キャラ
        if (p2.exchanged) {
            const A_opp = p2.pendingExchange || 0;
            const M_nr = p1.money;
            if (M_nr >= A_opp) {
                // 相手キャラがかけた量の倍の金額になる(倍額が無理な場合は同額)
                const targetAmount = M_nr >= 2 * A_opp ? 2 * A_opp : A_opp;
                p1.pendingExchange = targetAmount;
                p1.exchanged = true;
                console.log(`[Server] NouveauRiche auto-exchange applied to ${p1.role}: ${p1.pendingExchange} (based on ${p2.role}'s ${A_opp})`);
            } else {
                // それでも相手と同じ金額が変換できない場合は自身で換金できる金額を選べる
                if (p1.exchanged && p1.pendingExchange === 0) {
                    p1.exchanged = false;
                    console.log(`[Server] NouveauRiche ${p1.role} cannot afford ${A_opp}. Unlocking manual exchange.`);
                    io.emit('sync_state', { players });

                    if (p1.isAI) {
                        setTimeout(() => {
                            if (!players[p1.id] || players[p1.id].exchanged) return;
                            const amount = Math.floor(p1.money * 0.5);
                            p1.pendingExchange = amount;
                            p1.exchanged = true;
                            console.log(`[Server] NouveauRiche AI ${p1.role} selected manual exchange: ${p1.pendingExchange}`);
                            checkAllExchanged();
                        }, 1000);
                    }
                }
            }
        }
    } else if (!isN1 && isN2) {
        // P2が成金、P1が通常キャラ
        if (p1.exchanged) {
            const A_opp = p1.pendingExchange || 0;
            const M_nr = p2.money;
            if (M_nr >= A_opp) {
                const targetAmount = M_nr >= 2 * A_opp ? 2 * A_opp : A_opp;
                p2.pendingExchange = targetAmount;
                p2.exchanged = true;
                console.log(`[Server] NouveauRiche auto-exchange applied to ${p2.role}: ${p2.pendingExchange} (based on ${p1.role}'s ${A_opp})`);
            } else {
                if (p2.exchanged && p2.pendingExchange === 0) {
                    p2.exchanged = false;
                    console.log(`[Server] NouveauRiche ${p2.role} cannot afford ${A_opp}. Unlocking manual exchange.`);
                    io.emit('sync_state', { players });

                    if (p2.isAI) {
                        setTimeout(() => {
                            if (!players[p2.id] || players[p2.id].exchanged) return;
                            const amount = Math.floor(p2.money * 0.5);
                            p2.pendingExchange = amount;
                            p2.exchanged = true;
                            console.log(`[Server] NouveauRiche AI ${p2.role} selected manual exchange: ${p2.pendingExchange}`);
                            checkAllExchanged();
                        }, 1000);
                    }
                }
            }
        }
    }
}

function prepareExchangePhase() {
    items = []; currentBeat = 0; timeLeft = Config.GAME_DURATION; cycleCount = 0; beatSequence = 0; beatStartServerMs = 0;
    for (let id in players) {
        resetPlayerPos(id);
        players[id].exchanged = false;

        // 成金の場合、最初は相手の出方を待つため exchanged = true にしておく
        if (isNouveauRiche(players[id])) {
            players[id].exchanged = true;
            players[id].pendingExchange = 0;
        }

        // 通常AIキャラのみ即座に両替を開始する
        if (players[id].isAI && !isNouveauRiche(players[id])) {
            handleAIExchange(id);
        }
    }

    // 両者が成金の場合などの即時両替をチェックする
    checkNouveauRicheAutoExchange();

    io.emit('sync_state', { players });
    io.emit('sync_items', items);
    io.emit('start_exchange');

    // 制限時間：超過したら未交換のプレイヤーを自動でチップ交換する。
    if (exchangeTimer) clearTimeout(exchangeTimer);
    exchangeTimer = setTimeout(autoExchangeTimedOut, PREPARE_PHASE_MS);

    // 両者成金など、この時点で全員の両替が確定している場合は即座に移行フェーズをトリガーする
    checkAllExchanged();
}

// チップ交換の制限時間超過。未交換のプレイヤーは所持金の 1/3 をチップに替える。
function autoExchangeTimedOut() {
    exchangeTimer = null;
    let changed = false;
    for (let id in players) {
        const p = players[id];
        if (p.exchanged || p.isAI) continue;
        const amount = Math.floor(p.money / 3);
        const cost = amount;
        p.money -= cost; p.chips += amount; p.exchanged = true;
        changed = true;
    }
    if (changed) io.emit('sync_state', { players });
    checkAllExchanged();
}

function handleAIExchange(id) {
    const p = players[id];
    let ratio = 0.5 + (Math.random() * 0.1 - 0.05);
    const amount = Math.floor(p.money * ratio);
    setTimeout(() => {
        // AI も精算は後でまとめて行うため、選択内容だけ記録する。
        p.pendingExchange = amount;
        p.exchanged = true;
        checkAllExchanged();
    }, 1000 + Math.random() * 1000);
}

setInterval(() => {
    if (!gameActive) return;
    currentBeat = (currentBeat % 4) + 1;
    beatSequence++;
    beatStartServerMs = getCurrentServerTimeMs();

    // ターンの上限数に達したら、引き分け処理
    if (cycleCount >= Config.TURN_MAX) {
        gameActive = false;

        // カウントのリセット
        cycleCount = 0;

        io.emit('round_over', { winnerRole: 'TIME UP - DRAW' });
        // 次ラウンドもゲートを通す：盤面・キャラ生成を待ってからチップ交換へ。
        setTimeout(beginRound, 3000);

        return;
    }

    // AI 决策 (第 2 或 3 拍提交)
    for (let id in players) {
        if (players[id].isAI && !players[id].intent) {
            const decisionBeat = Math.random() > 0.5 ? 2 : 3;
            if (currentBeat === decisionBeat) handleAIDecision(id);
        }
    }

    if (currentBeat === 4) {
        cycleCount++;

        // ファイナルレイズ進行中はターンカウントを増やし、20ターン経過しても決着がつかなければ優勢側を勝者とする。
        if (isFinalDuel) {
            finalRaiseTurnCount++;

            // 20ターン経過しても決着がつかなかった場合の処理。
            if (finalRaiseTurnCount >= Config.TURN_MAX) {
                // サドンデスは 2-2 の互角から始まり優勢側が存在しないため、時間切れは引き分けにする。
                const winner = finalRaiseFavoredRole
                    ? Object.values(players).find(p => p.role === finalRaiseFavoredRole)
                    : null;

                if (winner) {
                    // ファイナルレイズ：優勢側を勝者とする。
                    winner.score = Config.MAX_WINS;
                    handleRoundConcluded(
                        winner.id,
                        Object.keys(players).find(id => id !== winner.id)
                    );
                } else {
                    // サドンデス：勝敗つかず時間切れ。引き分けで次ラウンドへ。
                    gameActive = false;
                    finalRaiseTurnCount = 0;
                    io.emit('round_over', { winnerRole: 'TIME UP - DRAW' });
                    setTimeout(beginRound, 3000);
                }

                return;
            }
        }

        if (cycleCount % Config.ITEM_SPAWN_INTERVAL === 0) spawnItem();

        // --- 防御的プログラミング: intent の構造を保証する ---
        const intents = {};
        for (let id in players) {
            const p = players[id];
            if (p && p.intent) {
                intents[id] = {
                    type: p.intent.type || 'none',
                    dir: p.intent.dir || 'up',
                    power: p.intent.power || 1
                };
            } else {
                intents[id] = { type: 'none', dir: 'up', power: 1 };
            }
        }

        const result = Engine.resolveActions(players, intents, items, isFinalDuel);
        players = result.players;
        items = result.items;

        // ミッション進捗の処理
        if (result.events) {
            // Guardian のスキル防御成功チェック
            result.events.forEach(ev => {
                if (ev.type === 'hit' || ev.type === 'pushed') {
                    const targetId = ev.targetId;
                    const targetPlayer = players[targetId];
                    if (targetPlayer && targetPlayer.charaIndex === 0 && isGuardianBlocking(targetPlayer, intents)) {
                        result.events.push({ type: 'mission_progress', playerId: targetId, missionType: 'GuardianSkillDefense', amount: 1 });
                    }
                }
            });

            // Fighter のスキルによる相手スタミナ 0 化チェック
            result.events.forEach(ev => {
                if (ev.type === 'hit') {
                    const targetId = ev.targetId;
                    const targetPlayer = players[targetId];
                    const fighterPlayer = Object.values(players).find(pl => pl.charaIndex === 3);
                    if (fighterPlayer && targetPlayer && targetPlayer.stamina === 0 && targetId !== fighterPlayer.id) {
                        const fIntent = intents[fighterPlayer.id];
                        if (fIntent && fIntent.type === 'skill') {
                            result.events.push({ type: 'mission_progress', playerId: fighterPlayer.id, missionType: 'FighterSkillKill', amount: 1 });
                        }
                    }
                }
            });

            // Scammer の同じ動きチェック
            const pList = Object.values(players);
            if (pList.length === 2) {
                const p1 = pList[0];
                const p2 = pList[1];
                const i1 = intents[p1.id] || { type: 'none' };
                const i2 = intents[p2.id] || { type: 'none' };
                if (i1.type === i2.type && i1.type !== 'none') {
                    result.events.push({ type: 'mission_progress', playerId: p1.id, missionType: 'SameAction', amount: 1 });
                    result.events.push({ type: 'mission_progress', playerId: p2.id, missionType: 'SameAction', amount: 1 });
                }
            }

            const appendedEvents = [];
            result.events.forEach(ev => {
                if (ev.type === 'mission_progress') {
                    const p = players[ev.playerId];
                    if (p && p.mission && typeof p.mission === 'object' && !p.mission.isCleared) {
                        const mTypeMap = {
                            'Move': 0,
                            'Push': 1,
                            'Defense': 2,
                            'GainChip': 4,
                            'Skill': 3,
                            'GuardianSkillDefense': 11,
                            'FighterSkillKill': 12,
                            'SameAction': 13
                        };
                        let targetType = mTypeMap[ev.missionType];
                        if (ev.missionType === 'GainChip' && p.mission.type === 15) {
                            targetType = 15;
                        }

                        if (targetType !== undefined && p.mission.type === targetType) {
                            p.mission.currentCount += ev.amount;
                            console.log(`[Mission Progress] ${p.role}: ${p.mission.currentCount} / ${p.mission.targetCount} (${ev.missionType})`);

                            if (p.mission.currentCount >= p.mission.targetCount) {
                                p.mission.currentCount = p.mission.targetCount;
                                p.mission.isCleared = true;

                                const rType = p.mission.rewardType || 'Chips';
                                const rVal = p.mission.rewardValue || 0;

                                // デバフの即時解除
                                if (p.mission.debuff) {
                                    delete p.activeDebuffs[p.mission.debuff.type];
                                    console.log(`[Server] Mission cleared! Reverted debuff for ${p.role}: ${p.mission.debuff.type}`);
                                }

                                if (rType === 'Chips') {
                                    p.chips += rVal;
                                } else if (rType === 'MaxStaminaBonus') {
                                    p.modifiers.maxStaminaBonus = (p.modifiers.maxStaminaBonus || 0) + rVal;
                                    p.stamina += rVal;
                                } else if (rType === 'PushPowerBonus') {
                                    p.modifiers.pushPowerBonus = (p.modifiers.pushPowerBonus || 0) + rVal;
                                } else if (rType === 'DefenseBonus') {
                                    p.modifiers.defenseReductionBonus = (p.modifiers.defenseReductionBonus || 0) + rVal * 0.1;
                                } else if (rType === 'ActionCostBonus') {
                                    p.modifiers.actionCostBonus = (p.modifiers.actionCostBonus || 0) + rVal;
                                } else if (rType === 'SkillCostBonus') {
                                    p.modifiers.skillCostBonus = (p.modifiers.skillCostBonus || 0) + rVal;
                                } else if (rType === 'CharaUnique') {
                                    p.modifiers.charaUniqueBuff = true;
                                    console.log(`[Server] [Chara Unique Buff] Player ${p.role} activated character unique buff!`);
                                }

                                if (p.mission.debuff) {
                                    p.highRiskMissionsCleared = (p.highRiskMissionsCleared || 0) + 1;
                                }

                                console.log(`[Mission CLEARED] ${p.role} completed mission. Reward: ${rType} x${rVal}`);
                                appendedEvents.push({ type: 'vfx', vfxType: 'bump', targetId: p.id, text: "MISSION CLEAR!" });
                            }
                        }
                    }
                }
            });

            // 状態依存ミッション（スタミナ0、チップ0）のチェック
            for (let id in players) {
                const p = players[id];
                const opponent = Object.values(players).find(pl => pl.id !== id);
                if (p && p.mission && !p.mission.isCleared) {
                    if (p.mission.type === 5 && opponent && opponent.stamina === 0) {
                        p.mission.currentCount = 1;
                        p.mission.isCleared = true;
                        p.chips += p.mission.rewardValue || 0;
                        console.log(`[Mission CLEARED] ${p.role} reduced opponent stamina to 0. Reward: Chips x${p.mission.rewardValue}`);
                        appendedEvents.push({ type: 'vfx', vfxType: 'bump', targetId: p.id, text: "MISSION CLEAR!" });
                    }
                    if (p.mission.type === 6 && p.stamina === 0) {
                        p.mission.currentCount = 1;
                        p.mission.isCleared = true;
                        
                        // デバフの即時解除
                        if (p.mission.debuff) {
                            delete p.activeDebuffs[p.mission.debuff.type];
                        }
                        
                        const rType = p.mission.rewardType;
                        const rVal = p.mission.rewardValue || 0;
                        if (rType === 'MaxStaminaBonus') {
                            p.modifiers.maxStaminaBonus = (p.modifiers.maxStaminaBonus || 0) + rVal;
                            p.stamina += rVal;
                        }
                        p.highRiskMissionsCleared = (p.highRiskMissionsCleared || 0) + 1;
                        console.log(`[Mission CLEARED] ${p.role} reduced self stamina to 0. Reward: ${rType} x${rVal}`);
                        appendedEvents.push({ type: 'vfx', vfxType: 'bump', targetId: p.id, text: "MISSION CLEAR!" });
                    }
                    if (p.mission.type === 7 && p.chips === 0) {
                        p.mission.currentCount = 1;
                        p.mission.isCleared = true;
                        
                        // デバフの即時解除
                        if (p.mission.debuff) {
                            delete p.activeDebuffs[p.mission.debuff.type];
                        }
                        
                        const rType = p.mission.rewardType;
                        const rVal = p.mission.rewardValue || 0;
                        if (rType === 'ActionCostBonus') {
                            p.modifiers.actionCostBonus = (p.modifiers.actionCostBonus || 0) + rVal;
                        }
                        p.highRiskMissionsCleared = (p.highRiskMissionsCleared || 0) + 1;
                        console.log(`[Mission CLEARED] ${p.role} reduced chips to 0. Reward: ${rType} x${rVal}`);
                        appendedEvents.push({ type: 'vfx', vfxType: 'bump', targetId: p.id, text: "MISSION CLEAR!" });
                    }
                }
            }

            if (appendedEvents.length > 0) result.events = result.events.concat(appendedEvents);
            io.emit('game_events', result.events);
        }

        for (let id in players) {
            const p = players[id];
            if (p && (p.x < 0 || p.x >= Config.GRID_SIZE || p.y < 0 || p.y >= Config.GRID_SIZE)) {
                if (!p.falling) {
                    p.falling = true;
                    io.emit('sync_state', { players });

                    // 1500ms 後の判定判定時にプレイヤーがまだ存在するか再確認する（防御的プログラミング）
                    setTimeout(() => {
                        if (!players[id]) return; // 判定前に切断された場合は処理を中断

                        gameActive = false;
                        const loserId = id;
                        const winnerId = Object.keys(players).find(oid => oid !== loserId);

                        if (winnerId && players[winnerId]) {
                            players[winnerId].score++;
                            handleRoundConcluded(winnerId, loserId);
                            io.emit('sync_state', { players });
                        }
                    }, 1500);
                }
                break;
            }
        }
        // 同步结算前的状态，以便客户端记录日志（保留一拍 intent）
        io.emit('sync_state', { players });
        // すでに上で一括送信済みのため、個別の演出送信は不要
        // if (result.events) io.emit('game_events', result.events);

        // 延迟清除 intent，确保客户端有足够时间在 Beat 4 记录
        setTimeout(() => {
            for (let id in players) players[id].intent = null;
            io.emit('sync_state', { players });
        }, 100);

        io.emit('sync_items', items);
    }
    const beatsPerBar = 4;
    const barIndex = getBarIndexFromSequence(beatSequence - 1, beatsPerBar);
    const nextBoundaryServerMs = beatStartServerMs + Config.BEAT_INTERVAL;
    io.emit('beat', { beat: currentBeat, timeLeft, gameActive, cycleCount, barIndex, beatSequence, roundId, beatStartServerMs, nextBoundaryServerMs, beatIntervalMs: Config.BEAT_INTERVAL, beatsPerBar });
}, Config.BEAT_INTERVAL);

// --- AI Brain: 普通难度，目标是推对手下平台 ---
function handleAIDecision(id) {
    const me = players[id];
    const opponent = Object.values(players).find(p => p.id !== id);
    if (!opponent) return;

    let decision = { type: 'none', dir: null, power: 1 };
    const dx = opponent.x - me.x;
    const dy = opponent.y - me.y;
    const dist = Math.abs(dx) + Math.abs(dy);
    const rand = Math.random();
    // 每个 AI 独立的"个性扰动"（每次决策随机，让双方不做同步决策）
    const jitter = () => (Math.random() - 0.5) * 0.3;

    // --- 资源判定 ---
    const canAllIn = me.chips >= 9;
    const canRaise = me.chips >= 5;
    const canSmall = me.chips >= 3;
    const canMove = me.chips >= 1;
    const staminaAdvantage = me.stamina - opponent.stamina;

    // --- 附近道具扫描：找离我最近的道具 ---
    let nearestItem = null, itemDist = Infinity;
    for (const it of items) {
        const d = Math.abs(it.x - me.x) + Math.abs(it.y - me.y);
        if (d < itemDist) { itemDist = d; nearestItem = it; }
    }
    const itemDirFor = (it) => {
        const idx = it.x - me.x, idy = it.y - me.y;
        if (Math.abs(idx) >= Math.abs(idy) && idx !== 0) return idx > 0 ? 'right' : 'left';
        if (idy !== 0) return idy > 0 ? 'down' : 'up';
        return null;
    };
    // 是否值得脱离对手去捡道具：筹码不够且道具触手可及
    const shouldGrabItem = nearestItem && !canSmall && itemDist <= 3 && itemDist <= dist + 1;

    // --- 对手边缘分析：哪一侧离平台边界最近，就是最理想的推出方向 ---
    const GS = Config.GRID_SIZE;
    const distLeft = opponent.x;
    const distRight = GS - 1 - opponent.x;
    const distUp = opponent.y;
    const distDown = GS - 1 - opponent.y;
    const minEdge = Math.min(distLeft, distRight, distUp, distDown);
    // 对手离某条边的最短距离，决定最佳推出方向
    let killDir = 'left';
    if (distRight === minEdge) killDir = 'right';
    else if (distUp === minEdge) killDir = 'up';
    else if (distDown === minEdge) killDir = 'down';
    else killDir = 'left';
    // 要把对手推向 killDir，AI 需要站在对手的反方向
    const idealSpot = { x: opponent.x, y: opponent.y };
    if (killDir === 'left') idealSpot.x = opponent.x + 1;
    else if (killDir === 'right') idealSpot.x = opponent.x - 1;
    else if (killDir === 'up') idealSpot.y = opponent.y + 1;
    else if (killDir === 'down') idealSpot.y = opponent.y - 1;

    // --- 1. 紧贴对手（dist === 1）---
    if (dist === 1) {
        // 当前能推的方向（从我推向对手的反向即"把对手往外推"的方向）
        const pushDir = (dx === 1) ? 'right' : (dx === -1 ? 'left' : (dy === 1 ? 'down' : 'up'));
        const pushingTowardsKill = (pushDir === killDir);
        const opponentOnEdge = (minEdge === 0);

        // 筹码不足 + 附近有道具 → 脱离去捡
        if (shouldGrabItem && canMove) {
            const d = itemDirFor(nearestItem);
            if (d) { decision.type = 'move'; decision.dir = d; decision.power = 1; }
            else { decision.type = 'none'; }
        }
        // 低定力优先自保
        else if (me.stamina <= 1) {
            // 对手定力也低且我还能打，直接拼一把 push
            if (opponent.stamina <= 1 && canSmall && pushingTowardsKill) {
                decision.type = 'push'; decision.dir = pushDir;
                decision.power = canAllIn ? 3 : (canRaise ? 2 : 1);
            } else if (rand < 0.55 + jitter()) {
                decision.type = 'skill'; // 仮でrest->skillへ
            } else {
                decision.type = 'defense';
            }
        }
        // 对手定力高于我且我没在优势 → 先用 attack 削
        else if (opponent.stamina >= 3 && staminaAdvantage <= 0 && canSmall && rand < 0.5 + jitter()) {
            decision.type = 'attack';
            decision.power = canAllIn ? 3 : (canRaise ? 2 : 1);
        }
        // 推方向不对：要么走位到正确面，要么先 attack 消耗对手
        else if (!pushingTowardsKill && !opponentOnEdge && canMove && rand < 0.6 + jitter()) {
            // 尝试绕到 idealSpot
            const sdx = idealSpot.x - me.x, sdy = idealSpot.y - me.y;
            if (Math.abs(sdx) >= Math.abs(sdy) && sdx !== 0) decision.dir = sdx > 0 ? 'right' : 'left';
            else if (sdy !== 0) decision.dir = sdy > 0 ? 'down' : 'up';
            else decision.dir = pushDir;
            decision.type = 'move';
            decision.power = 1;
        }
        // 默认：就地 push
        else if (canSmall && rand < 0.85 + jitter()) {
            decision.type = 'push'; decision.dir = pushDir;
            if (opponentOnEdge && canAllIn) decision.power = 3;
            else if (pushingTowardsKill && canAllIn && rand < 0.5 + jitter()) decision.power = 3;
            else if (canRaise && rand < 0.6 + jitter()) decision.power = 2;
            else decision.power = 1;
        } else if (canSmall) {
            decision.type = 'attack';
            decision.power = canRaise ? 2 : 1;
        } else {
            // 筹码不够：附近有道具去捡，否则防守
            if (nearestItem && canMove) {
                const d = itemDirFor(nearestItem);
                if (d) { decision.type = 'move'; decision.dir = d; decision.power = 1; }
                else decision.type = rand < 0.5 + jitter() ? 'defense' : 'skill'; // 仮でrest->skillへ
            } else {
                decision.type = rand < 0.5 + jitter() ? 'defense' : 'skill';
            }
        }
    }
    // --- 2. 距离 2 且同一直线（能直接 push 梭哈推到）---
    else if (dist === 2 && (dx === 0 || dy === 0)) {
        const pushDir = dx !== 0 ? (dx > 0 ? 'right' : 'left') : (dy > 0 ? 'down' : 'up');
        const pushingTowardsKill = (pushDir === killDir);
        // power=2 刚好推 2 格 = 把对手推一格过去且我跟进
        if (canRaise && (pushingTowardsKill || minEdge <= 1)) {
            decision.type = 'push'; decision.dir = pushDir;
            decision.power = canAllIn ? 3 : 2;
        } else if (canMove) {
            decision.type = 'move'; decision.dir = pushDir; decision.power = 1;
        } else {
            decision.type = 'none';
        }
    }
    // --- 3. 远距离：接近或攒钱 ---
    else {
        // 筹码少且附近有道具：优先去捡
        if (!canSmall && nearestItem && itemDist <= dist + 2 && canMove) {
            const d = itemDirFor(nearestItem);
            if (d) { decision.type = 'move'; decision.dir = d; decision.power = 1; }
            else decision.type = 'none';
        }
        // 筹码很少且离得远：idle 攒钱
        else if (!canSmall && dist > 2) {
            decision.type = 'none';
        }
        // 定力低且有距离：rest 回复
        else if (me.stamina <= 2 && dist >= 3 && rand < 0.6 + jitter()) {
            decision.type = canSmall ? 'rest' : 'none';
        }
        // 附近有道具且顺路（绕道不太远）：捡了再走
        else if (nearestItem && itemDist <= 2 && canMove && rand < 0.5 + jitter()) {
            const d = itemDirFor(nearestItem);
            if (d) { decision.type = 'move'; decision.dir = d; decision.power = 1; }
            else decision.type = 'none';
        }
        // 其他情况：往理想位置走（绕到能推下的一侧）
        else if (canMove) {
            const sdx = idealSpot.x - me.x, sdy = idealSpot.y - me.y;
            const sAbsX = Math.abs(sdx), sAbsY = Math.abs(sdy);
            let moveDir;
            if (sAbsX === 0 && sAbsY === 0) {
                // 已在 idealSpot 上（罕见），朝对手靠近
                moveDir = dx !== 0 ? (dx > 0 ? 'right' : 'left') : (dy > 0 ? 'down' : 'up');
            } else if (sAbsX >= sAbsY && sdx !== 0) {
                moveDir = sdx > 0 ? 'right' : 'left';
            } else {
                moveDir = sdy > 0 ? 'down' : 'up';
            }
            decision.type = 'move';
            decision.dir = moveDir;
            // 筹码富余时偶尔用 power=2 快速接近
            decision.power = (canRaise && dist >= 4 && rand < 0.4 + jitter()) ? 2 : 1;
        } else {
            decision.type = 'none';
        }
    }

    // 安全校验：选了需要筹码的动作但没钱 → 降档直到买得起，不行就 idle
    const costTable = Config.CHIP_COST_BY_POWER[decision.type];
    if (costTable) {
        let p = Math.max(1, Math.min(3, decision.power || 1));
        while (p > 1 && me.chips < costTable[p - 1]) p--;
        if (me.chips < costTable[p - 1]) decision.type = 'none';
        else decision.power = p;
    }

    me.intent = decision;
    io.emit('sync_state', { players });
}

function spawnItem() {
    if (items.length >= Config.MAX_ITEMS_ON_FIELD) return;
    const x = Math.floor(Math.random() * Config.GRID_SIZE);
    const y = Math.floor(Math.random() * Config.GRID_SIZE);
    if (Object.values(players).some(p => p.x === x && p.y === y) || items.some(it => it.x === x && it.y === y)) return;
    items.push({ id: Date.now() + Math.random(), type: Math.random() > 0.3 ? 'chips' : 'money', x, y });
}

setInterval(() => { if (gameActive && timeLeft > 0) timeLeft--; }, 1000);

// 一度入室したクライアントを端末ごとに一意に識別するためのトークン → socket.id の対応表。
// クライアントは接続のたびに同じトークンを送る（端末を起動している間は変わらない）。
// socket.id は再接続のたびに変わるため、トークンを使って「同じ人の再接続」を判定する。
const tokenToSocketId = {};
// 切断後、同じトークンで戻ってくるのを待つ猶予タイマー（トークン → タイマー句柄）。
// 猶予内に戻れば席を保持し、戻らなければ正式に退室扱いにする。
const disconnectGraceTimers = {};
// 別端末同士の対戦中、一瞬の回線の揺れで切断→再接続が起きても席を失わないだけの猶予。
const RECONNECT_GRACE_MS = 10000;

// 切断したプレイヤーを正式に退室させる。猶予内に再接続がなかった場合に呼ぶ。
function finalizePlayerLeave(socketId) {
    const p = players[socketId];
    if (!p) return;
    console.log(`[Server] Player left (grace expired): ${socketId}`);
    if (socketId === finalRaiseProposerId || socketId === finalRaiseResponderId) {
        cancelFinalRaise('disconnect');
    }
    if (p.token && tokenToSocketId[p.token] === socketId) delete tokenToSocketId[p.token];
    delete players[socketId];
    io.emit('player_left', socketId);
    io.emit('sync_state', { players });
}

io.on('connection', (socket) => {
    // 席の割り当ては接続直後ではなく identify の受信後に行う。
    // 同じトークンなら再接続として元の席を復元し、初めてのトークンなら
    // 空き席へ新規入室、満席なら入室を断る。
    socket.on('identify', (data) => {
        const token = data && data.token ? String(data.token) : null;

        // 既に同じトークンの席があれば「再接続」。新しい socket.id へ席を移し替える。
        const prevSocketId = token ? tokenToSocketId[token] : null;
        if (prevSocketId && players[prevSocketId]) {
            // 退室待ちの猶予タイマーが動いていれば取り消す（無事戻ってきたため）。
            if (disconnectGraceTimers[token]) {
                clearTimeout(disconnectGraceTimers[token]);
                delete disconnectGraceTimers[token];
            }

            const player = players[prevSocketId];
            delete players[prevSocketId];
            player.id = socket.id;
            players[socket.id] = player;
            tokenToSocketId[token] = socket.id;

            // ファイナルレイズの当事者だった場合は socket.id 参照も移し替える。
            if (finalRaiseProposerId === prevSocketId) finalRaiseProposerId = socket.id;
            if (finalRaiseResponderId === prevSocketId) finalRaiseResponderId = socket.id;

            console.log(`[Server] Player reconnected: ${prevSocketId} -> ${socket.id} as ${player.role}`);
            socket.emit('init', { id: socket.id, players, gridSize: Config.GRID_SIZE });
            io.emit('sync_state', { players });
            return;
        }

        // 新規入室。空いている役職を割り当てる。
        const existingPlayers = Object.values(players);
        const hasP1 = existingPlayers.some(p => p.role === 'P1');
        const hasP2 = existingPlayers.some(p => p.role === 'P2');

        // P1・P2 の両方が埋まっているのに別端末が新規入室しようとした場合は断る。
        if (hasP1 && hasP2) {
            console.log(`[Server] Connection refused (room full): ${socket.id}`);
            socket.emit('room_full');
            socket.disconnect(true);
            return;
        }

        const role = hasP1 ? 'P2' : 'P1';
        const isP1 = role === 'P1';
        players[socket.id] = {
            id: socket.id, token: token, role: role, x: isP1 ? 1 : 6, y: isP1 ? 6 : 1,
            intent: null, ready: false, exchanged: false, score: 0,
            money: Config.INITIAL_MONEY, chips: Config.INITIAL_CHIPS, stamina: Config.INITIAL_STAMINA,
            isAI: false, personality: 'Balanced', color: isP1 ? '#00f2fe' : '#ff4444',
            selectedBuff: null, buffReady: false, pendingExchange: 0, inLobby: false, roundReady: false,
            charaIndex: 0,
            charaName: 'Normal',
            maxStamina: Config.MAX_STAMINA,
            currentMaxStamina: Config.MAX_STAMINA,
            currentPushPower: 0,
            currentDefensePower: 0,
            highRiskMissionsCleared: 0,
            activeDebuffs: {},
            initMoney: Config.INITIAL_MONEY,
            initChips: Config.INITIAL_CHIPS,
            basePushPower: 0,
            baseMoveSpeed: 0,
            baseDefensePower: 0,
            chipCosts: JSON.parse(JSON.stringify(Config.CHIP_COST_BY_POWER)),
            baseChipCosts: JSON.parse(JSON.stringify(Config.CHIP_COST_BY_POWER)),
            skillData: null,
            modifiers: {
                maxStaminaBonus: 0,
                pushPowerBonus: 0,
                moveSpeedBonus: 0,
                chipCostMultiplier: 1.0,
                defenseReductionBonus: 0.0
            },
            // イカサマのスキル発動フラグ（trueの間、相手のintentをSocket個別に送信する）
            scammerActive: false,
            // 債務者の次回突進強化（onResolveでセットされ、push実行時にengine.jsが読んでリセットする）
            nextPushBonus: 0
        };
        if (token) tokenToSocketId[token] = socket.id;

        console.log(`[Server] Player joined: ${socket.id} as ${role}`);
        socket.emit('init', { id: socket.id, players, gridSize: Config.GRID_SIZE });
        io.emit('sync_state', { players });
    });

    socket.on('player_ready', (data) => {
        const p = players[socket.id];
        if (p) {
            p.ready = true;
            // 接管模式：将当前玩家标记为 AI
            p.isAI = !!(data && data.isAI);
            if (p.isAI) p.personality = ['Aggressive', 'Balanced', 'Conservative'][Math.floor(Math.random() * 3)];

            // --- クライアントからアップロードされたキャラクターデータをクランプして保持する ---
            if (data && data.charaData) {
                const chara = data.charaData;

                // 定力上限のクランプ
                const maxStaminaLimit = (Config.LIMITS && Config.LIMITS.MAX_STAMINA_LIMIT) || 8;
                p.maxStamina = Math.min(maxStaminaLimit, Math.max(3, parseInt(chara.maxStamina) || 5));

                // 初期資金・初期チップ・プッシュ力・移動速度のバリデーションとクランプ
                const moneyLimit = 20000;
                const chipsLimit = 1000;
                p.initMoney = Math.min(moneyLimit, Math.max(100, parseInt(chara.initMoney) || Config.INITIAL_MONEY));
                p.initChips = Math.min(chipsLimit, Math.max(0, parseInt(chara.initChips) || Config.INITIAL_CHIPS));

                const pushLimit = 3;
                const speedLimit = 3;
                p.basePushPower = Math.min(pushLimit, Math.max(-2, parseInt(chara.pushPower) || 0));
                p.baseMoveSpeed = Math.min(speedLimit, Math.max(-2, parseInt(chara.moveSpeed) || 0));
                p.baseDefensePower = Math.min(pushLimit, Math.max(-2, parseInt(chara.defensePower) || 0));

                // 各アクションごとのチップ消費コストテーブルのクランプ
                const costLimit = (Config.LIMITS && Config.LIMITS.SKILL_CHIP_COST_LIMIT) || 15;
                const parseCosts = (costArr, defaultCosts) => {
                    if (Array.isArray(costArr) && costArr.length === 3) {
                        return costArr.map(c => Math.min(costLimit, Math.max(0, parseInt(c) || 0)));
                    }
                    return JSON.parse(JSON.stringify(defaultCosts));
                };

                p.chipCosts = {
                    move: parseCosts(chara.moveCost, Config.CHIP_COST_BY_POWER.move),
                    push: parseCosts(chara.pushCost, Config.CHIP_COST_BY_POWER.push),
                    attack: parseCosts(chara.attackCost, Config.CHIP_COST_BY_POWER.attack),
                    defense: parseCosts(chara.defenseCost, Config.CHIP_COST_BY_POWER.defense),
                    skill: parseCosts(chara.skillCost, Config.CHIP_COST_BY_POWER.skill),
                    rest: parseCosts(chara.restCost, Config.CHIP_COST_BY_POWER.rest)
                };
                p.baseChipCosts = JSON.parse(JSON.stringify(p.chipCosts));

                // スキルパラメータのクランプ
                const recLimit = (Config.LIMITS && Config.LIMITS.SKILL_STAMINA_REC_LIMIT) || 3;
                p.skillData = {
                    id: chara.skills?.id || null,
                    staminaRec: Math.min(recLimit, parseInt(chara.skills?.staminaRec) || 0),
                    chipCost: Math.min(costLimit, parseInt(chara.skills?.chipCost) || 0)
                };
                p.charaName = chara.name || 'Unknown';
            }

            console.log(`[Server] Player ${p.role} is ready (AI: ${p.isAI}, Chara: ${p.charaName}, Stamina: ${p.maxStamina})`);

            const pList = Object.values(players);
            if (pList.length >= 2 && pList.every(pl => pl.ready)) {
                // すぐに対局へ進めず、カウントダウンを挟む。途中で取り消せるようにする。
                // 重複した ready 通知では既存のカウントダウンを再作成しない。
                if (!lobbyCountdownTimer) {
                    io.emit('start_countdown');
                    lobbyCountdownTimer = setTimeout(() => {
                        lobbyCountdownTimer = null;
                        resetMatch();
                    }, LOBBY_COUNTDOWN_MS);
                }
            } else {
                socket.emit('waiting_for_others', { waitingFor: p.role === 'P1' ? 'P2' : 'P1' });
            }
            io.emit('sync_state', { players });
        }
    });

    socket.on('player_unready', () => {
        const p = players[socket.id];
        // 対局開始前のみ準備を取り消せる。
        if (p && !gameActive) {
            p.ready = false;
            p.isAI = false;
            console.log(`[Server] Player ${p.role} canceled ready`);
            // カウントダウン中なら中断し、対局へ進めない。
            if (lobbyCountdownTimer) {
                clearTimeout(lobbyCountdownTimer);
                lobbyCountdownTimer = null;
                io.emit('countdown_canceled');
            }
            io.emit('sync_state', { players });
        }
    });

    // クライアントが Lobby シーンに入ったことを通知する。相手の Portrait 滑り込みの起点になる。
    socket.on('enter_lobby', () => {
        const p = players[socket.id];
        if (p) {
            p.inLobby = true;
            io.emit('sync_state', { players });
        }
    });

    // Lobby でのキャラ選択。対局開始前に変更でき、全員へ広播して相手側の表示を同期する。
    socket.on('select_chara', (data) => {
        const p = players[socket.id];
        if (p && !gameActive) {
            const index = parseInt(data && data.index) || 0;
            p.charaIndex = index;
            io.emit('chara_selected', { playerId: socket.id, index });
        }
    });

    // クライアントが盤面・キャラ生成を終え、ラウンドを始められる状態になったことを通知する。
    socket.on('round_ready', () => {
        const p = players[socket.id];
        if (p) {
            p.roundReady = true;
            checkAllRoundReady();
        }
    });

    socket.on('exchange_chips', (data) => {
        const p = players[socket.id];
        if (p && !gameActive && !p.isAI) {
            // 成金かつ相手が未決定の場合、早期の手動申請は無視する
            if (isNouveauRiche(p)) {
                const opponent = Object.values(players).find(pl => pl.id !== socket.id);
                if (opponent && !opponent.exchanged) {
                    return;
                }
            }
            const amount = parseInt(data.amount) || 0;
            const cost = amount;
            // この時点では所持金・チップを動かさず、選択内容だけ記録する。
            // 実際の精算は両替・カード選択が全員終わってからまとめて行う。
            if (p.money >= cost) {
                p.pendingExchange = amount;
                p.exchanged = true;
                checkAllExchanged();
            }
        }
    });

    socket.on('buff_selected', (data) => {
        const p = players[socket.id];
        if (!p) return;
        const cost = data.buffId === 'high_risk' ? 15 : (data.buffId === 'low_risk' ? 5 : 0);
        // 両替後に手元に来る予定のチップで購入可否を判定する。
        // ここでもチップは減らさず、選択内容だけ記録する。
        const expectedChips = p.chips + (p.pendingExchange || 0);
        if (expectedChips < cost) return;
        p.selectedBuff = data.buffId;
        p.buffReady = true;
        // リスク決定時にミッションを生成する
        p.availableMissions = generateMissions(p, p.selectedBuff);
        io.emit('sync_state', { players });
        checkAllBuffsSelected();
    });

    socket.on('mission_selected', (data) => {
        const p = players[socket.id];
        if (!p || !p.availableMissions) return;
        const mission = p.availableMissions.find(m => m.id === data.missionId);
        if (mission) {
            p.mission = JSON.parse(JSON.stringify(mission));
            
            // ハイリスクデバフの即時適用
            p.activeDebuffs = p.activeDebuffs || {};
            if (p.mission.debuff) {
                p.activeDebuffs[p.mission.debuff.type] = p.mission.debuff.value;
                console.log(`[Server] Applied debuff immediately to ${p.role}: ${p.mission.debuff.type} = ${p.mission.debuff.value}`);
            }

            console.log(`[Server] Player ${p.role} selected mission: ${p.mission.description}`);
            io.emit('sync_state', { players });
            checkAllMissionsSelected();
        }
    });

    socket.on('set_intent', (data) => {
        const p = players[socket.id];
        if (gameActive && currentBeat < 4 && p && !p.isAI) {
            p.intent = { type: data.type || 'move', dir: data.dir, power: data.power || 1 };

            // イカサマ（scammer_skill）が有効な対戦相手がいれば、このプレイヤーの intent を
            // その対戦相手の Socket にのみ個別送信する（全体 broadcast は行わない）。
            for (const otherId in players) {
                if (otherId !== socket.id && players[otherId].scammerActive) {
                    const scammerSocket = io.sockets.sockets.get(otherId);
                    if (scammerSocket) {
                        scammerSocket.emit('opponent_intent_revealed', { intent: p.intent });
                    }
                }
            }
        }
    });

    // 敗者がファイナルレイズを発起するか決定する。accept=true で勝者の応答待ちへ。
    socket.on('final_raise_propose', (data) => {
        if (socket.id !== finalRaiseProposerId) return;
        if (!finalRaiseOfferTimer) return;
        clearTimeout(finalRaiseOfferTimer);
        finalRaiseOfferTimer = null;
        const accept = !!(data && data.accept);
        if (accept) beginFinalRaisePending();
        else cancelFinalRaise('declined');
    });

    // 勝者がファイナルレイズを受諾するか決定する。accept=true で本番ラウンドへ。
    socket.on('final_raise_respond', (data) => {
        if (socket.id !== finalRaiseResponderId) return;
        if (!finalRaisePendingTimer) return;
        clearTimeout(finalRaisePendingTimer);
        finalRaisePendingTimer = null;
        const accept = !!(data && data.accept);
        if (accept) startFinalDuel();
        else cancelFinalRaise('declined');
    });
    socket.on('request_sudden_death', () => {
        console.log("[Server] Sudden Death Requested");
        startSuddenDeath();
    });
    socket.on('shutdown', () => { io.emit('close_all'); setTimeout(() => process.exit(0), 1000); });
    socket.on('disconnect', () => {
        const p = players[socket.id];
        // identify 前に切れた接続など、席を持たない場合は何もしない。
        if (!p) return;

        // すぐには退室扱いにせず、同じトークンで戻ってくるのを猶予時間だけ待つ。
        // 別端末対戦中の一瞬の回線の揺れで席を失わないようにするため。
        // トークンが無い（古いクライアント等）場合は猶予なしで即退室扱いにする。
        if (p.token) {
            console.log(`[Server] Player disconnected, waiting for reconnect: ${socket.id}`);
            if (disconnectGraceTimers[p.token]) clearTimeout(disconnectGraceTimers[p.token]);
            disconnectGraceTimers[p.token] = setTimeout(() => {
                delete disconnectGraceTimers[p.token];
                finalizePlayerLeave(socket.id);
            }, RECONNECT_GRACE_MS);
        } else {
            finalizePlayerLeave(socket.id);
        }
    });
});

function checkAllExchanged() {
    // 成金の自動両替判定を行う
    checkNouveauRicheAutoExchange();

    const pList = Object.values(players);
    if (pList.length >= 2 && pList.every(pl => pl.exchanged)) {
        // チップ交換フェーズを抜けるので制限時間タイマーを止める。
        if (exchangeTimer) { clearTimeout(exchangeTimer); exchangeTimer = null; }

        // チップ交換分反映
        settleAllChoices();

        // 各プレイヤーのミッション状態を初期化（生成はリスク選択確定時に行う）
        pList.forEach(p => {
            p.availableMissions = [];
            p.mission = null;
        });

        io.emit('start_buff_selection');
        // カード選択フェーズの制限時間。超過したら未選択のプレイヤーを自動で選ぶ。
        if (buffTimer) clearTimeout(buffTimer);
        buffTimer = setTimeout(autoBuffTimedOut, PREPARE_PHASE_MS);
        // 如果全是 AI，或者需要 AI 自动选卡，触发检查
        setTimeout(checkAllBuffsSelected, 1500);
    }
}

// カード選択の制限時間超過。未選択のプレイヤーは購入可能な範囲でランダムに選ぶ。
function autoBuffTimedOut() {
    buffTimer = null;
    let changed = false;
    for (let id in players) {
        const p = players[id];
        if (p.buffReady || p.isAI) continue;
        // 両替後の予定チップで購入可否を判定し、ここでは記録のみ。
        // チップの増減は精算でまとめて行う。
        const expectedChips = p.chips + (p.pendingExchange || 0);
        let pick = null;
        if (expectedChips >= 15 && Math.random() < 0.5) pick = 'high_risk';
        else if (expectedChips >= 5) pick = 'low_risk';
        if (pick) p.selectedBuff = pick;
        p.buffReady = true;
        // リスク決定時にミッションを生成する
        p.availableMissions = generateMissions(p, p.selectedBuff);
        changed = true;
    }
    if (changed) io.emit('sync_state', { players });
    checkAllBuffsSelected();
}

function checkAllBuffsSelected() {
    const pList = Object.values(players);
    if (pList.length < 2) return;

    let changed = false;
    // 如果所有真人玩家都选好了，让 AI 自动选卡
    if (pList.every(pl => pl.buffReady || pl.isAI)) {
        pList.forEach(pl => {
            if (pl.isAI && !pl.buffReady) {
                // AI も両替後の予定チップで購入可否を判定し、ここでは記録のみ。
                const expectedChips = pl.chips + (pl.pendingExchange || 0);
                let pick = null;
                if (expectedChips >= 15 && Math.random() < 0.6) pick = 'high_risk';
                else if (expectedChips >= 5) pick = 'low_risk';
                if (pick) pl.selectedBuff = pick;
                pl.buffReady = true;
                // リスク決定時にミッションを生成する
                pl.availableMissions = generateMissions(pl, pl.selectedBuff);
                changed = true;
            }
        });

        if (changed) io.emit('sync_state', { players });

        // 如果全员（包括 AI）都选好了，开始等待ミッション選択
        if (pList.every(pl => pl.buffReady)) {
            console.log('[Server] All players selected buffs. Waiting for mission selections...');

            // AIがランダムにミッション選択
            let changed = false;
            pList.forEach(pl => {
                if (pl.isAI && !pl.mission) {
                    if (selectRandomMissionForAI(pl)) {
                        console.log(`[Server] AI Player ${pl.role} auto-selected mission: ${pl.mission.description}`);
                        changed = true;
                    }
                }
            });

            if (changed) io.emit('sync_state', { players });

            // ミッション選択フェーズの制限時間タイマーを設定
            if (missionTimer) clearTimeout(missionTimer);
            missionTimer = setTimeout(autoMissionTimedOut, MISSION_PHASE_MS);

            // 全員がミッション選択完了したか確認
            setTimeout(checkAllMissionsSelected, 1500);
        }
    }
}

// AIがミッションをランダムに選択（将来的に重み付けする可能性を考慮して関数化）
function selectRandomMissionForAI(player) {
    if (player.availableMissions && player.availableMissions.length > 0) {
        const randomIndex = Math.floor(Math.random() * player.availableMissions.length);
        player.mission = JSON.parse(JSON.stringify(player.availableMissions[randomIndex]));
        return true;
    }
    return false;
}

function checkAllMissionsSelected() {
    const pList = Object.values(players);
    if (pList.length < 2) return; // プレイヤーが揃っていない場合は開始しない

    // 全員がミッション選択済み（またはAI）か確認
    if (pList.every(pl => pl.mission !== null && pl.mission !== undefined)) {
        // 全員がミッション選択完了
        if (missionTimer) { clearTimeout(missionTimer); missionTimer = null; }
        console.log('[Server] All players selected missions. Starting match countdown...');

        // カード選択フェーズを抜けるので制限時間タイマーを止める。
        if (buffTimer) { clearTimeout(buffTimer); buffTimer = null; }
        startBattleCountdown();
    }
}

// 開戦カウントダウンを流し、一定時間後に対戦を開始する。
function startBattleCountdown() {
    io.emit('start_match_countdown');
    setTimeout(() => { gameActive = true; io.emit('round_start'); }, 3500);
}

// ミッション選択の制限時間超過
function autoMissionTimedOut() {
    missionTimer = null;
    let changed = false;

    for (let id in players) {
        const p = players[id];
        // ミッション未選択のプレイヤーに最初の候補を自動割当
        if (!p.mission && p.availableMissions && p.availableMissions.length > 0) {
            p.mission = JSON.parse(JSON.stringify(p.availableMissions[0]));
            
            // ハイリスクデバフの即時適用
            p.activeDebuffs = p.activeDebuffs || {};
            if (p.mission.debuff) {
                p.activeDebuffs[p.mission.debuff.type] = p.mission.debuff.value;
            }

            console.log(`[Server] Auto-assigned mission to Player ${p.role}: ${p.mission.description}`);
            changed = true;
        }
    }

    if (changed) io.emit('sync_state', { players });
    checkAllMissionsSelected();
}

// 両替とカード選択が全員終わった後、まとめて所持金・チップを精算する。
// ここで初めて値を変えて一度だけ sync_state を送るので、
// クライアント側の所持金・チップ表示は最後に一括で動く。
function settleAllChoices() {
    for (let id in players) {
        const p = players[id];
        const amount = p.pendingExchange || 0;
        // チップ変換時のスキル効果フック
        const finalAmount = Skills.onExchange(p, amount);
        if (amount > 0) {
            p.money -= amount;
            p.chips += finalAmount;
        }
        const buffCost = p.selectedBuff === 'high_risk' ? 15 : (p.selectedBuff === 'low_risk' ? 5 : 0);
        if (buffCost > 0) p.chips -= buffCost;
        p.pendingExchange = 0;
    }
    io.emit('sync_state', { players });
}

// IPv6 ワイルドカード '::' でリッスン。Node は IPv4-mapped IPv6 経由で
// IPv4 接続も同じソケットで受けるため、127.0.0.1 / ::1 / LAN IPv4 / IPv6
// いずれのアドレスでもクライアントから接続できる。
server.listen(3000, '::', () => {
    console.log(`[GamblingAction Server] Running on port 3000 (IPv4/IPv6 dual-stack)...`);
    startLanBroadcast();
});

// ---------------------------------------------------------------------
// LAN 自動発見：同一ブロードキャストドメイン上のクライアントに自機 IP を
// UDP で周期的に通知する。クライアント側（LanDiscovery.cs）がこのパケットを
// 受信して接続先 URL を決定する。
// ---------------------------------------------------------------------

// パケット識別用の MAGIC 文字列。クライアントと一致させること。
const LAN_DISCOVERY_MAGIC = 'GAMBLINGACTION|7f3a4d9e';
// 通知先 UDP ポート。TCP 3000 と衝突しないよう別ポートを使う。
const LAN_DISCOVERY_PORT = 38900;
// 通知間隔（ミリ秒）。
const LAN_BROADCAST_INTERVAL_MS = 1000;

// 自機の LAN 上の IPv4 アドレスを返す。複数 NIC があれば最初の非ループバック v4 を採用する。
function pickLanIPv4() {
    const ifaces = os.networkInterfaces();
    // 1次フィルター: 明らかに仮想と思われるアダプターを除外して探索
    for (const name of Object.keys(ifaces)) {
        const lowerName = name.toLowerCase();
        if (lowerName.includes('virtual') ||
            lowerName.includes('vbox') ||
            lowerName.includes('vmware') ||
            lowerName.includes('docker') ||
            lowerName.includes('wsl') ||
            lowerName.includes('vethernet') ||
            lowerName.includes('loopback')) {
            continue;
        }
        for (const info of ifaces[name] || []) {
            if (info.family === 'IPv4' && !info.internal) {
                return info.address;
            }
        }
    }
    // フォールバック: 見つからなければ名前制限なしで再探索
    for (const name of Object.keys(ifaces)) {
        for (const info of ifaces[name] || []) {
            if (info.family === 'IPv4' && !info.internal) {
                return info.address;
            }
        }
    }
    return '127.0.0.1';
}
function startLanBroadcast() {
    const sock = dgram.createSocket({ type: 'udp4', reuseAddr: true });
    sock.on('error', (err) => {
        console.warn('[LanDiscovery] broadcast socket error:', err.message);
        sock.close();
    });
    sock.bind(() => {
        try { sock.setBroadcast(true); } catch (_) { /* OS によっては失敗するが致命ではない */ }
        const send = () => {
            const ip = pickLanIPv4();
            // 形式: <MAGIC>|<IPv4>|<TCP_PORT>|<PID>
            // PID は自プロセス発信分の自己受信を除外するためにクライアント側で照合する。
            const payload = `${LAN_DISCOVERY_MAGIC}|${ip}|3000|${process.pid}`;
            const buf = Buffer.from(payload, 'utf8');
            sock.send(buf, 0, buf.length, LAN_DISCOVERY_PORT, '255.255.255.255', (err) => {
                if (err) console.warn('[LanDiscovery] broadcast send error:', err.message);
            });
        };
        send();
        setInterval(send, LAN_BROADCAST_INTERVAL_MS);
        console.log(`[LanDiscovery] broadcasting on UDP ${LAN_DISCOVERY_PORT} every ${LAN_BROADCAST_INTERVAL_MS}ms`);
    });
}

// 1 ラウンドの決着がついた直後に呼ばれる。
// ファイナルレイズ進行中なら即 game_over。
// 通常ラウンドで2-1または1-2になった瞬間に提案フェーズへ入り、それ以外は次ラウンドへ。
function handleRoundConcluded(winnerId, loserId) {
    const winner = players[winnerId];
    const loser = players[loserId];

    if (!winner || !loser) return;

    // --- ラウンド終了時のミッション最終判定および永続デバフ化処理 ---
    for (let id in players) {
        const p = players[id];
        if (p.mission) {
            // キャラ別ミッションの達成チェック (Doctor: スタミナ最大値でラウンド終了)
            if (p.mission.type === 10 && p.stamina === p.currentMaxStamina) {
                p.mission.isCleared = true;
                p.modifiers.charaUniqueBuff = true;
                console.log(`[Server] Doctor Unique Mission Cleared!`);
            }
            // キャラ別ミッションの達成チェック (NouveauRiche: スキル使用して相手を落として勝利)
            if (p.mission.type === 14 && winnerId === p.id) {
                const intent = p.intent || {};
                if (intent.type === 'skill') {
                    p.mission.isCleared = true;
                    p.modifiers.charaUniqueBuff = true;
                    console.log(`[Server] NouveauRiche Unique Mission Cleared!`);
                }
            }

            if (p.mission.isCleared) {
                // クリア成功：デバフ解除
                p.activeDebuffs = {};
                if (p.mission.isCharaUnique) {
                    p.highRiskMissionsCleared = 0; // 完了したのでリセット
                }
            } else {
                // クリア失敗：デバフを永続化（modifiers に蓄積）
                if (p.mission.debuff) {
                    const db = p.mission.debuff;
                    if (db.type === 'maxStamina') p.modifiers.maxStaminaBonus = (p.modifiers.maxStaminaBonus || 0) + db.value;
                    else if (db.type === 'pushPower') p.modifiers.pushPowerBonus = (p.modifiers.pushPowerBonus || 0) + db.value;
                    else if (db.type === 'defenseReduction') p.modifiers.defenseReductionBonus = (p.modifiers.defenseReductionBonus || 0) + db.value;
                    else if (db.type === 'actionCost') p.modifiers.actionCostBonus = (p.modifiers.actionCostBonus || 0) + db.value;
                    else if (db.type === 'skillCost') p.modifiers.skillCostBonus = (p.modifiers.skillCostBonus || 0) + db.value;
                    console.log(`[Server] Mission FAILED. Debuff is now PERMANENT for ${p.role}: ${db.type} = ${db.value}`);
                }
                p.activeDebuffs = {};
            }
        }
    }

    // ファイナルレイズの勝者は即全勝扱いで試合終了。通常戦の途中でファイナルレイズに入ることがあるため、ここでスコアを最大値まで上げる。
    if (isFinalDuel) {
        winner.score = Config.MAX_WINS;
    }

    // 勝者のスコアが最大値に達したら試合終了。ファイナルレイズの勝者はここで全勝扱いになる。
    if (winner.score >= Config.MAX_WINS) {
        // 試合終了。勝者の役職を通知してからリセットする。
        io.emit('game_over', { winnerRole: winner.role });
        // 試合終了に伴い、Lobby に戻ったときに前回状態が残らないよう全てリセットする。
        resetMatchState();
        io.emit('sync_state', { players });
        return;
    }

    const playerList = Object.values(players);

    // ラウンド終了時、チップを持ち金に戻す
    for (let id in players) {
        const p = players[id];
        p.money += p.chips;  // チップをお金にキャッシュバック
        p.chips = 0;
    }

    // 通常戦の途中で 2-1 または 1-2 になったら、ファイナルレイズの提案フェーズへ
    if (playerList.length === 2) {
        const player1 = playerList[0];
        const player2 = playerList[1];

        const scoreDifference = Math.abs(player1.score - player2.score);

        // 2-1 または 1-2 のスコアになったとき、負けてるプレイヤー側にファイナルレイズの提案権を与える。
        if (scoreDifference === 1 && (player1.score === 2 || player2.score === 2)) {
            // 提案者を決定する
            const proposer = player1.score < player2.score ? player1 : player2;
            // 応答者を決定する
            const responder = player1.score > player2.score ? player1 : player2;

            io.emit('round_over', { winnerRole: winner.role });
            setTimeout(() => startFinalRaiseOffer(responder.id, proposer.id), 3000);
            return;
        }
    }
    const pList = Object.values(players);
    const p1 = pList.find(p => p.role === 'P1');
    const p2 = pList.find(p => p.role === 'P2');

    if (p1 && p2 && p1.score === 2 && p2.score === 2) {
        console.log("[Server] Sudden Death triggered!");
        startSuddenDeath();
        return;
    }
    io.emit('round_over', { winnerRole: winner.role });
    setTimeout(beginRound, 3000);
}

// 敗者（loser）が「ファイナルレイズを発起するか」を決めるフェーズを開始する。
// 制限時間内に応答がなければ拒否扱いで通常の game_over に流す。
function startFinalRaiseOffer(winnerId, loserId) {
    finalRaiseProposerId = loserId;
    finalRaiseResponderId = winnerId;

    const winner = players[winnerId];
    const loser = players[loserId];
    io.emit('final_raise_offer', {
        proposerRole: loser ? loser.role : null,
        responderRole: winner ? winner.role : null,
        timeoutMs: Config.FINAL_RAISE_TIMEOUT_MS
    });

    if (finalRaiseOfferTimer) clearTimeout(finalRaiseOfferTimer);
    finalRaiseOfferTimer = setTimeout(() => {
        finalRaiseOfferTimer = null;
        // 時間切れは「発起しない」扱い。通常の決着へ。
        cancelFinalRaise('timeout');
    }, Config.FINAL_RAISE_TIMEOUT_MS);
}

// 勝者の応答（受諾 / 拒否）を待つフェーズへ進む。
function beginFinalRaisePending() {
    const winner = players[finalRaiseResponderId];
    const loser = players[finalRaiseProposerId];
    io.emit('final_raise_pending', {
        proposerRole: loser ? loser.role : null,
        responderRole: winner ? winner.role : null,
        timeoutMs: Config.FINAL_RAISE_TIMEOUT_MS
    });

    if (finalRaisePendingTimer) clearTimeout(finalRaisePendingTimer);
    finalRaisePendingTimer = setTimeout(() => {
        finalRaisePendingTimer = null;
        cancelFinalRaise('timeout');
    }, Config.FINAL_RAISE_TIMEOUT_MS);
}

// ファイナルレイズの中断（拒否・タイムアウト・切断）。通常戦 へ流す。
function cancelFinalRaise(reason) {
    if (finalRaiseOfferTimer) { clearTimeout(finalRaiseOfferTimer); finalRaiseOfferTimer = null; }
    if (finalRaisePendingTimer) { clearTimeout(finalRaisePendingTimer); finalRaisePendingTimer = null; }
    const winnerId = finalRaiseResponderId;
    finalRaiseProposerId = null;
    finalRaiseResponderId = null;
    isFinalDuel = false;

    const winner = winnerId ? players[winnerId] : null;
    io.emit('final_raise_canceled', { reason });

    // 通常戦続行
    setTimeout(beginRound, 3000);
}

// サドンデスを開始する。両替・カード選択・ミッション選択は行わず、
// 所持金をすべてチップへ変換したうえで即開戦する決勝局。
function startSuddenDeath() {
    // 決勝局として扱うため、勝敗確定時に勝者を全勝にする isFinalDuel も立てる。
    isFinalDuel = true;
    isSuddenDeath = true;
    finalRaiseTurnCount = 0;

    // 所持金をすべてチップへ変換する（1:1に修正）。
    for (let id in players) {
        const p = players[id];
        p.chips += p.money;
        p.money = 0;
    }

    io.emit('sync_state', { players });
    io.emit('sudden_death_started');

    // 盤面・キャラ生成を待ってから開戦へ進む。準備フェーズは beginRound 側で飛ばす。
    setTimeout(beginRound, 2000);
}

// 勝者が受諾した。ファイナルレイズ本番ラウンドを開始する。
function startFinalDuel() {
    if (finalRaiseOfferTimer) { clearTimeout(finalRaiseOfferTimer); finalRaiseOfferTimer = null; }
    if (finalRaisePendingTimer) { clearTimeout(finalRaisePendingTimer); finalRaisePendingTimer = null; }
    finalRaiseProposerId = null;
    finalRaiseResponderId = null;
    isFinalDuel = true;
    finalRaiseTurnCount = 0;

    io.emit('final_raise_started');
    // 通常ラウンドと同じ準備フローに合わせるため、少し間を置いてから beginRound へ。
    setTimeout(beginRound, 3000);

    const playerList = Object.values(players);

    if (playerList.length !== 2) {
        return;
    }

    const player1 = playerList[0];
    const player2 = playerList[1];

    // 優勢側を記録
    const favored = player1.score > player2.score ? player1 : player2;
    finalRaiseFavoredRole = favored.role;

    // 劣勢側
    const underdog = player1.score < player2.score ? player1 : player2;

    // 優勢側へバフを付与

    // 劣勢側の所持金を全てチップ化
    underdog.chips += underdog.money;
    underdog.money = 0;
    io.emit('sync_state', { players });
}

// 試合中の数値・進行状態を初期化する（プレイヤー数値、ファイナルレイズ、対局フラグ）。
// Lobby 関連フラグ（ready / inLobby / isAI / roundReady / buffReady）もここで初期化する。
// 試合終了直後（game_over）と、新しい対局を始める前（resetMatch）から共通で呼ぶ。
function resetMatchState(isMatchStart = false) {
    gameActive = false; items = []; currentBeat = 0; beatSequence = 0; beatStartServerMs = 0;

    // 全ての進行管理タイマーをリセット
    if (finalRaiseOfferTimer) { clearTimeout(finalRaiseOfferTimer); finalRaiseOfferTimer = null; }
    if (finalRaisePendingTimer) { clearTimeout(finalRaisePendingTimer); finalRaisePendingTimer = null; }
    if (lobbyCountdownTimer) { clearTimeout(lobbyCountdownTimer); lobbyCountdownTimer = null; }
    if (exchangeTimer) { clearTimeout(exchangeTimer); exchangeTimer = null; }
    if (buffTimer) { clearTimeout(buffTimer); buffTimer = null; }
    if (missionTimer) { clearTimeout(missionTimer); missionTimer = null; }
    if (roundIntroTimer) { clearTimeout(roundIntroTimer); roundIntroTimer = null; }

    cycleCount = 0;
    timeLeft = Config.GAME_DURATION;

    isFinalDuel = false;
    isSuddenDeath = false;
    finalRaiseProposerId = null;
    finalRaiseResponderId = null;
    finalRaiseFavoredRole = null;
    finalRaiseTurnCount = 0;
    if (lobbyCountdownTimer) { clearTimeout(lobbyCountdownTimer); lobbyCountdownTimer = null; }

    for (let id in players) {
        const p = players[id];
        p.score = 0;

        if (isMatchStart) {
            p.money = p.initMoney !== undefined ? p.initMoney : Config.INITIAL_MONEY;
            p.chips = p.initChips !== undefined ? p.initChips : Config.INITIAL_CHIPS;
        } else {
            p.money = Config.INITIAL_MONEY;
            p.chips = Config.INITIAL_CHIPS;
            p.charaIndex = 0;
            p.charaName = 'Normal';
            p.maxStamina = Config.MAX_STAMINA;
            p.basePushPower = 0;
            p.baseMoveSpeed = 0;
            p.chipCosts = JSON.parse(JSON.stringify(Config.CHIP_COST_BY_POWER));
            p.baseChipCosts = JSON.parse(JSON.stringify(Config.CHIP_COST_BY_POWER));
            p.skillData = null;
            p.initMoney = Config.INITIAL_MONEY;
            p.initChips = Config.INITIAL_CHIPS;
        }

        p.mission = null;
        p.highRiskMissionsCleared = 0;
        p.activeDebuffs = {};
        p.exchanged = false; p.selectedBuff = null; p.buffReady = false;
        p.roundReady = false; p.intent = null;
        // Lobby 表示用フラグも初期化。ResultScene を抜けて Lobby に戻ったとき、
        // 前回の ready / 入室状態が残らないようにする。
        p.ready = false; p.isAI = false; p.inLobby = false;
        // スキル関連とステータス補正（Modifiers）の初期化
        p.modifiers = {
            maxStaminaBonus: 0,
            pushPowerBonus: 0,
            moveSpeedBonus: 0,
            chipCostMultiplier: 1.0,
            defenseReductionBonus: 0.0
        };

        // キャラクター固有の一時フラグ初期化
        p.scammerActive = false;
        p.nextPushBonus = 0;
        resetPlayerPos(id);
    }
}

function resetMatch() {
    resetMatchState(true);
    // 直接チップ交換へ進めず、クライアントの盤面・キャラ生成を待ってから進む。
    beginRound();
}

function generateMissions(player, selectedBuff) {
    // もしハイリスク2回達成していれば、キャラ別ミッションを強制提示する。
    if (player.highRiskMissionsCleared >= 2) {
        let type = 0;
        let description = "";
        let targetCount = 1;
        const isDoc = (player.charaIndex === 1 || player.charaName === 'Doctor');
        const isGuard = (player.charaIndex === 4 || player.charaIndex === 0 || player.charaName === 'Guardian');
        const isFight = (player.charaIndex === 3 || player.charaName === 'Fighter');
        const isScam = (player.charaIndex === 5 || player.charaName === 'Scammer');
        const isNouveau = (player.charaIndex === 2 || player.charaName === 'NouveauRiche');
        const isDebt = (player.charaIndex === 6 || player.charaName === 'Debtor');

        if (isDoc) {
            type = 10;
            description = "医師：スタミナが最大値の状態でラウンドを終了する。 (報酬: スキル回復量+10)";
            targetCount = 1;
        } else if (isGuard) {
            type = 11;
            description = "守護者：スキルによる防御を3回成功させる。 (報酬: 防御成功時に相手のスタミナ3削る)";
            targetCount = 3;
        } else if (isFight) {
            type = 12;
            description = "格闘家：スキルで相手のスタミナを0にする。 (報酬: スキルの攻撃力+10)";
            targetCount = 1;
        } else if (isScam) {
            type = 13;
            description = "イカサマ師：相手と同じ動きを4回行う。 (報酬: スキル効果がゲーム中永続)";
            targetCount = 4;
        } else if (isNouveau) {
            type = 14;
            description = "成金：スキルを使用して相手を落としラウンドを獲得する。 (報酬: 手動両替機能解放)";
            targetCount = 1;
        } else if (isDebt) {
            type = 15;
            description = "債務者：フィールドのチップを10個回収する。 (報酬: FR時消費0&常時強化突進)";
            targetCount = 10;
        } else {
            type = 0;
            description = "キャラ別ミッション (未定義)";
            targetCount = 1;
        }

        const mission = {
            id: `chara_mission_${player.role}`,
            type: type,
            description: description,
            targetCount: targetCount,
            currentCount: 0,
            rewardType: 'CharaUnique',
            rewardValue: 1,
            isCleared: false,
            isCharaUnique: true
        };
        // 3つの選択肢すべてに同じキャラ別ミッションを入れる
        return [mission, mission, mission];
    }

    if (selectedBuff === 'high_risk') {
        const m1 = {
            id: 'high_risk_1',
            type: 1, // Push
            description: "8回突進を使う (リスク: 突進力-2 / 報酬: 突進力+2)",
            targetCount: 8,
            currentCount: 0,
            rewardType: 'PushPowerBonus',
            rewardValue: 2,
            isCleared: false,
            debuff: { type: 'pushPower', value: -2 }
        };
        const isExcluded = (player.charaIndex === 4 || player.charaIndex === 5);
        const m2 = isExcluded ? {
            id: 'high_risk_2_alt',
            type: 7, // ChipsZero
            description: "所持チップを0にする (リスク: 全行動チップ消費量+20 / 報酬: 全行動消費量-20)",
            targetCount: 1,
            currentCount: 0,
            rewardType: 'ActionCostBonus',
            rewardValue: -20,
            isCleared: false,
            debuff: { type: 'actionCost', value: 20 }
        } : {
            id: 'high_risk_2',
            type: 3, // Skill
            description: "4回スキルを発動する (リスク: スキル消費+100 / 報酬: スキル消費-50)",
            targetCount: 4,
            currentCount: 0,
            rewardType: 'SkillCostBonus',
            rewardValue: -50,
            isCleared: false,
            debuff: { type: 'skillCost', value: 100 }
        };
        const m3 = {
            id: 'high_risk_3',
            type: 2, // Defense
            description: "7回防御する (リスク: 防御力-1 / 報酬: 防御力+1)",
            targetCount: 7,
            currentCount: 0,
            rewardType: 'DefenseBonus',
            rewardValue: 1,
            isCleared: false,
            debuff: { type: 'defenseReduction', value: -0.1 }
        };
        const m4 = {
            id: 'high_risk_4',
            type: 6, // StaminaSelfZero
            description: "自分のスタミナを0にする (リスク: 最大スタミナ-2 / 報酬: 最大スタミナ+2)",
            targetCount: 1,
            currentCount: 0,
            rewardType: 'MaxStaminaBonus',
            rewardValue: 2,
            isCleared: false,
            debuff: { type: 'maxStamina', value: -2 }
        };
        const m5 = {
            id: 'high_risk_5',
            type: 7, // ChipsZero
            description: "所持チップを0にする (リスク: 全行動チップ消費量+20 / 報酬: 全行動消費量-20)",
            targetCount: 1,
            currentCount: 0,
            rewardType: 'ActionCostBonus',
            rewardValue: -20,
            isCleared: false,
            debuff: { type: 'actionCost', value: 20 }
        };

        const list = [m1, m2, m3, m4, m5];
        const shuffled = list.sort(() => 0.5 - Math.random());
        return shuffled.slice(0, 3);
    } else {
        const m1 = {
            id: 'low_risk_1',
            type: 1, // Push
            description: "5回突進を使う (報酬: 1200chip)",
            targetCount: 5,
            currentCount: 0,
            rewardType: 'Chips',
            rewardValue: 1200,
            isCleared: false
        };
        const isExcluded = (player.charaIndex === 4 || player.charaIndex === 5);
        const m2 = isExcluded ? {
            id: 'low_risk_2_alt',
            type: 4, // GainChip
            description: "チップを5回拾う (報酬: 1000chip)",
            targetCount: 5,
            currentCount: 0,
            rewardType: 'Chips',
            rewardValue: 1000,
            isCleared: false
        } : {
            id: 'low_risk_2',
            type: 3, // Skill
            description: "2回スキルを発動する (報酬: 600chip)",
            targetCount: 2,
            currentCount: 0,
            rewardType: 'Chips',
            rewardValue: 600,
            isCleared: false
        };
        const m3 = {
            id: 'low_risk_3',
            type: 2, // Defense
            description: "4回防御を発動する (報酬: 350chip)",
            targetCount: 4,
            currentCount: 0,
            rewardType: 'Chips',
            rewardValue: 350,
            isCleared: false
        };
        const m4 = {
            id: 'low_risk_4',
            type: 5, // StaminaOpponentZero
            description: "相手のスタミナを0にする (報酬: 1500chip)",
            targetCount: 1,
            currentCount: 0,
            rewardType: 'Chips',
            rewardValue: 1500,
            isCleared: false
        };
        const m5 = {
            id: 'low_risk_5',
            type: 4, // GainChip
            description: "チップを5回拾う (報酬: 1000chip)",
            targetCount: 5,
            currentCount: 0,
            rewardType: 'Chips',
            rewardValue: 1000,
            isCleared: false
        };

        const list = [m1, m2, m3, m4, m5];
        const shuffled = list.sort(() => 0.5 - Math.random());
        return shuffled.slice(0, 3);
    }
}

function getCurrentServerTimeMs() { return Date.now(); }

function getBarIndexFromSequence(sequence, beatsPerBar) { return Math.floor(sequence / beatsPerBar) + 1; }

// --- グローバル・エラーハンドラ ---
// 予期せぬクラッシュを防ぎ、エラー内容をコンソールに出力してサーバーを延命させる
process.on('uncaughtException', (err) => {
    console.error('[Warning] 処理中に例外が発生しました（進行維持）:', err);
    // gameActive = false; // 開発中は止めずにログのみ出す
});

process.on('unhandledRejection', (reason, promise) => {
    console.error('[Warning] 未処理の Promise 拒否:', reason);
});
