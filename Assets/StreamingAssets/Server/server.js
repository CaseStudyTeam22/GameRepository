const express = require('express');
const http = require('http');
const { Server } = require('socket.io');
const path = require('path');
const dgram = require('dgram');
const os = require('os');

const Config = require('./config');
const Engine = require('./engine');

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
// 双方準備完了後のカウントダウン中の setTimeout 句柄。取り消し時に clear する。
let lobbyCountdownTimer = null;
// カウントダウンの長さ（ミリ秒）。クライアントの 3-2-1-GO 表示に合わせる。
const LOBBY_COUNTDOWN_MS = 3200;

// チップ交換 / カード選択フェーズの制限時間（ミリ秒）。超過時は自動処理する。
const PREPARE_PHASE_MS = 20000;
// チップ交換 / カード選択の制限時間タイマー句柄。全員完了時 / フェーズ離脱時に clear する。
let exchangeTimer = null;
let buffTimer = null;
// 盤面・キャラ生成の完了後、チップ交換フェーズへ進むまでの待ち時間（ミリ秒）。
// 生成直後すぐ UI を出さず、少し見せてから出すための間。
const ROUND_INTRO_MS = 1500;
let roundIntroTimer = null;

function resetPlayerPos(id) {
    const p = players[id];
    if (!p) return;
    const startPos = p.role === 'P1' ? { x: 1, y: 6 } : { x: 6, y: 1 };
    p.x = startPos.x; p.y = startPos.y;
    p.color = p.role === 'P1' ? '#00f2fe' : '#ff4444';
    p.stamina = Config.INITIAL_STAMINA;
    p.falling = false; p.intent = null; p.chips = Config.INITIAL_CHIPS;
    p.selectedBuff = null; p.buffReady = false;
}

// ラウンドの開始要求。クライアントの盤面・キャラ生成が終わるのを待ってからチップ交換へ進む。
// resetMatch / round_over の直後にここを通し、双方の round_ready を待つ。
function beginRound() {
    for (let id in players) players[id].roundReady = false;
    if (roundIntroTimer) { clearTimeout(roundIntroTimer); roundIntroTimer = null; }
    // 位置を先に初期化して配る。クライアントは再生成時に新しい位置のキャラを出せる。
    for (let id in players) resetPlayerPos(id);
    io.emit('sync_state', { players });
    io.emit('prepare_round');
}

// 双方のクライアントが盤面・キャラ生成を終えたら、少し間を置いてチップ交換フェーズへ進む。
function checkAllRoundReady() {
    const pList = Object.values(players);
    if (pList.length >= 2 && pList.every(pl => pl.roundReady)) {
        if (roundIntroTimer) clearTimeout(roundIntroTimer);
        roundIntroTimer = setTimeout(() => {
            roundIntroTimer = null;
            prepareExchangePhase();
        }, ROUND_INTRO_MS);
    }
}

function prepareExchangePhase() {
    items = []; currentBeat = 0; timeLeft = Config.GAME_DURATION;
    for (let id in players) {
        resetPlayerPos(id);
        players[id].exchanged = false;
        if (players[id].isAI) handleAIExchange(id);
    }
    io.emit('sync_state', { players });
    io.emit('sync_items', items);
    io.emit('start_exchange');

    // 制限時間：超過したら未交換のプレイヤーを自動でチップ交換する。
    if (exchangeTimer) clearTimeout(exchangeTimer);
    exchangeTimer = setTimeout(autoExchangeTimedOut, PREPARE_PHASE_MS);
}

// チップ交換の制限時間超過。未交換のプレイヤーは所持金の 1/3 をチップに替える。
function autoExchangeTimedOut() {
    exchangeTimer = null;
    let changed = false;
    for (let id in players) {
        const p = players[id];
        if (p.exchanged || p.isAI) continue;
        const amount = Math.floor(p.money / 3 / 100);
        const cost = amount * 100;
        p.money -= cost; p.chips += amount; p.exchanged = true;
        changed = true;
    }
    if (changed) io.emit('sync_state', { players });
    checkAllExchanged();
}

function handleAIExchange(id) {
    const p = players[id];
    let ratio = 0.5 + (Math.random() * 0.1 - 0.05);
    const amount = Math.floor((p.money * ratio) / 100);
    const cost = amount * 100;
    setTimeout(() => {
        p.money -= cost; p.chips += amount; p.exchanged = true;
        checkAllExchanged();
    }, 1000 + Math.random() * 1000);
}

setInterval(() => {
    if (!gameActive) return;
    currentBeat = (currentBeat % 4) + 1;
    
    // 时间到，判定平局或结束
    if (timeLeft <= 0) {
        gameActive = false;
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
        if (cycleCount % Config.ITEM_SPAWN_INTERVAL === 0) spawnItem();
        const intents = {};
        for (let id in players) intents[id] = players[id].intent || { type: 'none' };
        
        const result = Engine.resolveActions(players, intents, items);
        players = result.players;
        items = result.items;
        io.emit('game_events', result.events);

        for (let id in players) {
            const p = players[id];
            if (p.x < 0 || p.x >= Config.GRID_SIZE || p.y < 0 || p.y >= Config.GRID_SIZE) {
                if (!p.falling) {
                    p.falling = true; io.emit('sync_state', { players });
                    setTimeout(() => {
                        gameActive = false;
                        const loserId = id;
                        const winnerId = Object.keys(players).find(oid => oid !== loserId);
                        if (winnerId && players[winnerId]) {
                            players[winnerId].score++;
                            if (players[winnerId].score >= 2) io.emit('game_over', { winnerRole: players[winnerId].role });
                            else { io.emit('round_over', { winnerRole: players[winnerId].role }); setTimeout(beginRound, 3000); }
                        }
                    }, 1500);
                }
                break;
            }
        }
        // 同步结算前的状态，以便客户端记录日志（保留一拍 intent）
        io.emit('sync_state', { players });
        if (result.events) io.emit('game_events', result.events);

        // 延迟清除 intent，确保客户端有足够时间在 Beat 4 记录
        setTimeout(() => {
            for (let id in players) players[id].intent = null;
            io.emit('sync_state', { players });
        }, 100);

        io.emit('sync_items', items);
    }
    io.emit('beat', { beat: currentBeat, timeLeft, gameActive });
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
    const canMove  = me.chips >= 1;
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
    const distLeft  = opponent.x;
    const distRight = GS - 1 - opponent.x;
    const distUp    = opponent.y;
    const distDown  = GS - 1 - opponent.y;
    const minEdge = Math.min(distLeft, distRight, distUp, distDown);
    // 对手离某条边的最短距离，决定最佳推出方向
    let killDir = 'left';
    if (distRight === minEdge) killDir = 'right';
    else if (distUp === minEdge) killDir = 'up';
    else if (distDown === minEdge) killDir = 'down';
    else killDir = 'left';
    // 要把对手推向 killDir，AI 需要站在对手的反方向
    const idealSpot = { x: opponent.x, y: opponent.y };
    if (killDir === 'left')  idealSpot.x = opponent.x + 1;
    else if (killDir === 'right') idealSpot.x = opponent.x - 1;
    else if (killDir === 'up')    idealSpot.y = opponent.y + 1;
    else if (killDir === 'down')  idealSpot.y = opponent.y - 1;

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
                decision.type = 'rest';
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
                else decision.type = rand < 0.5 + jitter() ? 'defense' : 'rest';
            } else {
                decision.type = rand < 0.5 + jitter() ? 'defense' : 'rest';
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

io.on('connection', (socket) => {
    const existingPlayers = Object.values(players);
    const hasP1 = existingPlayers.some(p => p.role === 'P1');
    const role = hasP1 ? 'P2' : 'P1';
    
    const isP1 = role === 'P1';
    players[socket.id] = { 
        id: socket.id, role: role, x: isP1 ? 1 : 6, y: isP1 ? 6 : 1, 
        intent: null, ready: false, exchanged: false, score: 0,
        money: Config.INITIAL_MONEY, chips: Config.INITIAL_CHIPS, stamina: Config.INITIAL_STAMINA,
        isAI: false, personality: 'Balanced', color: isP1 ? '#00f2fe' : '#ff4444',
        selectedBuff: null, buffReady: false, inLobby: false, roundReady: false,
        charaIndex: 0
    };
    
    console.log(`[Server] Player joined: ${socket.id} as ${role}`);
    socket.emit('init', { id: socket.id, players, gridSize: Config.GRID_SIZE });
    io.emit('sync_state', { players });

    socket.on('player_ready', (data) => {
        const p = players[socket.id];
        if (p) {
            p.ready = true;
            // 接管模式：将当前玩家标记为 AI
            p.isAI = !!(data && data.isAI);
            if (p.isAI) p.personality = ['Aggressive', 'Balanced', 'Conservative'][Math.floor(Math.random() * 3)];
            
            console.log(`[Server] Player ${p.role} is ready (AI: ${p.isAI})`);

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
            const amount = parseInt(data.amount) || 0;
            const cost = amount * 100;
            if (p.money >= cost) { 
                p.money -= cost; p.chips += amount; p.exchanged = true; 
                io.emit('sync_state', { players });
                checkAllExchanged(); 
            }
        }
    });

    socket.on('buff_selected', (data) => {
        const p = players[socket.id];
        if (!p) return;
        const cost = data.buffId === 'high_risk' ? 15 : (data.buffId === 'low_risk' ? 5 : 0);
        if (p.chips < cost) return;
        p.chips -= cost;
        p.selectedBuff = data.buffId;
        p.buffReady = true;
        io.emit('sync_state', { players });
        checkAllBuffsSelected();
    });

    socket.on('set_intent', (data) => {
        const p = players[socket.id];
        if (gameActive && currentBeat < 4 && p && !p.isAI) p.intent = { type: data.type || 'move', dir: data.dir, power: data.power || 1 };
    });

    socket.on('shutdown', () => { io.emit('close_all'); setTimeout(() => process.exit(0), 1000); });
    socket.on('disconnect', () => { 
        console.log(`[Server] Player left: ${socket.id}`);
        delete players[socket.id]; 
        io.emit('player_left', socket.id); 
        io.emit('sync_state', { players });
    });
});

function checkAllExchanged() {
    const pList = Object.values(players);
    if (pList.length >= 2 && pList.every(pl => pl.exchanged)) {
        // チップ交換フェーズを抜けるので制限時間タイマーを止める。
        if (exchangeTimer) { clearTimeout(exchangeTimer); exchangeTimer = null; }
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
        let pick = null;
        if (p.chips >= 15 && Math.random() < 0.5) pick = 'high_risk';
        else if (p.chips >= 5) pick = 'low_risk';
        if (pick) {
            const c = pick === 'high_risk' ? 15 : 5;
            p.chips -= c;
            p.selectedBuff = pick;
        }
        p.buffReady = true;
        changed = true;
    }
    if (changed) io.emit('sync_state', { players });
    checkAllBuffsSelected();
}

function checkAllBuffsSelected() {
    const pList = Object.values(players);
    if (pList.length < 2) return;

    // 如果所有真人玩家都选好了，让 AI 自动选卡
    if (pList.every(pl => pl.buffReady || pl.isAI)) {
        let changed = false;
        pList.forEach(pl => {
            if (pl.isAI && !pl.buffReady) {
                let pick = null;
                if (pl.chips >= 15 && Math.random() < 0.6) pick = 'high_risk';
                else if (pl.chips >= 5) pick = 'low_risk';
                if (pick) {
                    const c = pick === 'high_risk' ? 15 : 5;
                    pl.chips -= c;
                    pl.selectedBuff = pick;
                }
                pl.buffReady = true;
                changed = true;
            }
        });
        
        if (changed) io.emit('sync_state', { players });

        // 如果全员（包括 AI）都选好了，开始倒计时
        if (pList.every(pl => pl.buffReady)) {
            // カード選択フェーズを抜けるので制限時間タイマーを止める。
            if (buffTimer) { clearTimeout(buffTimer); buffTimer = null; }
            io.emit('start_match_countdown');
            setTimeout(() => { gameActive = true; io.emit('round_start'); }, 3500);
        }
    }
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

function resetMatch() {
    gameActive = false; items = []; currentBeat = 0;
    for (let id in players) { players[id].score = 0; players[id].money = Config.INITIAL_MONEY; resetPlayerPos(id); players[id].exchanged = false; }
    // 直接チップ交換へ進めず、クライアントの盤面・キャラ生成を待ってから進む。
    beginRound();
}
