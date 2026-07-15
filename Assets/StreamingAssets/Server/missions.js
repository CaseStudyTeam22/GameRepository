const Config = require('./config');

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

const Missions = {
    generateMissions: (player, selectedBuff) => {
        // ハイリスク2回達成かつキャラミッション未達成であれば、キャラ別ミッションを強制提示する。
        // キャラミッション達成済み（charaUniqueBuff === true）の場合は通常のミッション選択に進む。
        if (player.highRiskMissionsCleared >= 2 && !(player.modifiers && player.modifiers.charaUniqueBuff)) {
            let type = 0;
            let description = "";
            let targetCount = 1;
            const charaIndex = player.charaIndex;
            const charaName = player.charaName;
            const isDoc = (charaIndex === 1 || charaName === 'Doctor');
            const isGuard = (charaIndex === 4 || charaIndex === 0 || charaName === 'Guardian');
            const isFight = (charaIndex === 3 || charaName === 'Fighter');
            const isScam = (charaIndex === 5 || charaName === 'Scammer');
            const isNouveau = (charaIndex === 2 || charaName === 'NouveauRiche');
            const isDebt = (charaIndex === 6 || charaName === 'Debtor');

            if (isDoc) {
                type = 10;
                description = "医師：スタミナが最大値の状態でラウンドを終了する。";
                targetCount = 1;
            } else if (isGuard) {
                type = 11;
                description = "守護者：スキルによる防御を3回成功させる。";
                targetCount = 3;
            } else if (isFight) {
                type = 12;
                description = "格闘家：スキルで相手のスタミナを0にする。";
                targetCount = 1;
            } else if (isScam) {
                type = 13;
                description = "イカサマ師：相手と同じ動きを4回行う。";
                targetCount = 4;
            } else if (isNouveau) {
                type = 14;
                description = "成金：スキルを使用して相手を落としラウンドを獲得する。";
                targetCount = 1;
            } else if (isDebt) {
                type = 15;
                description = "債務者：フィールドのチップを10個回収する。";
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
            return [mission, mission, mission];
        }

        if (selectedBuff === 'high_risk') {
            const m1 = {
                id: 'high_risk_1',
                type: 1, // Push
                description: "8回突進を使う",
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
                description: "所持チップを0にする",
                targetCount: 1,
                currentCount: 0,
                rewardType: 'ActionCostBonus',
                rewardValue: -20,
                isCleared: false,
                debuff: { type: 'actionCost', value: 20 }
            } : {
                id: 'high_risk_2',
                type: 3, // Skill
                description: "4回スキルを発動する",
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
                description: "7回防御する",
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
                description: "自分のスタミナを0にする",
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
                description: "所持チップを0にする",
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
                description: "5回突進を使う",
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
                description: "チップを5回拾う",
                targetCount: 5,
                currentCount: 0,
                rewardType: 'Chips',
                rewardValue: 1000,
                isCleared: false
            } : {
                id: 'low_risk_2',
                type: 3, // Skill
                description: "2回スキルを発動する",
                targetCount: 2,
                currentCount: 0,
                rewardType: 'Chips',
                rewardValue: 600,
                isCleared: false
            };
            const m3 = {
                id: 'low_risk_3',
                type: 2, // Defense
                description: "4回防御を発動する",
                targetCount: 4,
                currentCount: 0,
                rewardType: 'Chips',
                rewardValue: 350,
                isCleared: false
            };
            const m4 = {
                id: 'low_risk_4',
                type: 5, // StaminaOpponentZero
                description: "相手のスタミナを0にする",
                targetCount: 1,
                currentCount: 0,
                rewardType: 'Chips',
                rewardValue: 1500,
                isCleared: false
            };
            const m5 = {
                id: 'low_risk_5',
                type: 4, // GainChip
                description: "チップを5回拾う",
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
    },

    updateProgress: (players, resultEvents, appendedEvents) => {
        if (!resultEvents) return;
        resultEvents.forEach(ev => {
            if (ev.missionType && ev.targetId) {
                const p = players[ev.targetId];
                if (p && p.mission && !p.mission.isCleared) {
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
                                // Scammer: ミッション達成時に即座に scammerActive を有効化する
                                if (p.charaIndex === 5 || p.charaName === 'Scammer') {
                                    p.scammerActive = true;
                                }
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
    },

    checkStateDependentMissions: (players, appendedEvents) => {
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
    }
};

module.exports = Missions;
