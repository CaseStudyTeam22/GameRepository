using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GamblingAction.Core;
using GamblingAction.Core.Dto;
using GamblingAction.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace GamblingAction.Domain
{
    public class GameState : IGameState, IDisposable
    {
        private static readonly string[] CharacterSheetUrls = new string[]
        {
            "https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/export?format=csv&gid=0",         // Normal
			"https://docs.google.com/spreadsheets/d/1UnmLUayVJn8xHJbpH6IwUFvAO5Mps5EUMAGYah3tdBY/export?format=csv&gid=573545404", // Doctor
			"https://docs.google.com/spreadsheets/d/1UnmLUayVJn8xHJbpH6IwUFvAO5Mps5EUMAGYah3tdBY/export?format=csv&gid=484412955", // NouveauRiche
			"https://docs.google.com/spreadsheets/d/1UnmLUayVJn8xHJbpH6IwUFvAO5Mps5EUMAGYah3tdBY/export?format=csv&gid=369696735", // Fighter
			"https://docs.google.com/spreadsheets/d/1UnmLUayVJn8xHJbpH6IwUFvAO5Mps5EUMAGYah3tdBY/export?format=csv&gid=34508704", // Guardian
			"https://docs.google.com/spreadsheets/d/1UnmLUayVJn8xHJbpH6IwUFvAO5Mps5EUMAGYah3tdBY/export?format=csv&gid=2137123423", // Scammer
			"https://docs.google.com/spreadsheets/d/1UnmLUayVJn8xHJbpH6IwUFvAO5Mps5EUMAGYah3tdBY/export?format=csv&gid=795014052"  // Debtor
		};

        private readonly INetClient m_Net;
        private readonly Dictionary<string, PlayerDto> m_Players = new();
        private List<ItemDto> m_Items = new();
        private int m_SelectedCharaIndex = 0;

        // この端末を一意に識別するトークン。アプリ起動中は変わらない。
        // 接続のたびにサーバへ送り、再接続時に元の席（P1/P2・スコア等）を復元させる。
        private readonly string m_Token = System.Guid.NewGuid().ToString("N");

        public string MyId { get; private set; }
        public int GridSize { get; private set; } = GamblingAction.Core.GameConfig.GridSize;
        public IReadOnlyDictionary<string, PlayerDto> Players => m_Players;
        public IReadOnlyList<ItemDto> Items => m_Items;
        public int CurrentBeat { get; private set; }
        public int TimeLeft { get; private set; }
        public bool GameActive { get; private set; }
        public int CycleCount { get; private set; }
        public EGamePhase Phase { get; private set; } = EGamePhase.Lobby;
        public bool IsConnected { get; private set; }
        public bool IsFinalDuel { get; private set; }
        public bool SuddenDeathAlreadyStarted { get; private set; }

        public PlayerDto Me =>
            MyId != null && m_Players.TryGetValue(MyId, out var p) ? p : null;

        public PlayerDto Opponent =>
            m_Players.Values.FirstOrDefault(p => p.Id != MyId);

        public event Action OnStateInitialized;
        public event Action OnPlayersChanged;
        public event Action OnItemsChanged;
        public event Action OnBeatChanged;
        public event Action<EventDto[]> OnGameEvents;
        public event Action<EGamePhase> OnPhaseChanged;
        public event Action<string> OnRoundOver;
        public event Action<string> OnGameOver;
        public event Action<string> OnPlayerLeft;
        public event Action<string> OnWaitingForOthers;
        public event Action OnCountdownStart;
        public event Action OnCountdownCancel;
        public event Action OnPrepareRound;
        public event Action<string, int> OnCharaSelected;
        public event Action<bool> OnConnectionChanged;
        public event Action<FinalRaiseOfferMessage> OnFinalRaiseOffer;
        public event Action<FinalRaisePendingMessage> OnFinalRaisePending;
        public event Action<FinalRaiseCanceledMessage> OnFinalRaiseCanceled;
        public event Action OnFinalRaiseStarted;
        /// イカサマスキル発動中に相手の intent が更新された際、イカサマプレイヤーのみに通知されるイベント。
        /// UI表示は未実装（骨格のみ）。
        public event Action<OpponentIntentRevealedMessage> OnOpponentIntentRevealed;

        public event Action OnSuddenDeathStarted;

        public GameState(INetClient net)
        {
            Debug.Log("[GameState] Created instance: " + this.GetHashCode());
            m_Net = net;
            Subscribe();
        }

        public void SubmitIntent(string type, string dir, int power)
        {
            if (!GameActive || CurrentBeat >= 4) return;
            var me = Me;
            if (me == null || me.IsAI) return;

            // ステータスを更新
            RefreshPlayerStats(m_Players[MyId]);
            // 自分の stats 変化を UI に反映する。
            OnPlayersChanged?.Invoke();

            m_Net.Emit(ClientEvents.SetIntent, new SetIntentMessage { Type = type, Dir = dir, Power = power });
        }

        public async void SubmitReady(bool isAI)
        {
            var charaData = new CharaDataMessage
            {
                Name = "Normal",
                MaxStamina = 5,
                InitMoney = 10000,
                InitChips = 0,
                PushPower = 0,
                MoveSpeed = 0,
                MoveCost = new[] { 1, 3, 5 },
                PushCost = new[] { 3, 5, 9 },
                AttackCost = new[] { 3, 5, 9 },
                DefenseCost = new[] { 2, 2, 2 },
                SkillCost = new[] { 3, 5, 9 },
                Skills = new CharaSkillDataMessage { Id = "", StaminaRec = 0, ChipCost = 0 }
            };

            // デフォルト値の設定
            if (m_SelectedCharaIndex == 1)
            {
                charaData.Name = "Doctor";
                charaData.MaxStamina = 5;
                charaData.InitMoney = 10000;
                charaData.InitChips = 0;
                charaData.PushPower = 0;
                charaData.MoveSpeed = 0;
                // キャラ別に発動コストを変更したい場合はこれをいじってね
                // クライアント側の挙動はちゃんと確認できたけど、サーバー側でちゃんと動いてるかはわからんね。多分大丈夫だけど。
                //charaData.MoveCost = new[] { 3, 6, 9 }; // 全キャラ共通にするため無効化
                //charaData.PushCost = new[] { 3, 5, 9 },
                //charaData.AttackCost = new[] { 3, 5, 9 },
                //charaData.DefenseCost = new[] { 2, 2, 2 },
                charaData.SkillCost = new[] { 3, 3, 3 }; // スキルコスト3固定
                charaData.Skills = new CharaSkillDataMessage { Id = "heal_instant", StaminaRec = 2, ChipCost = 3 };
            }
            else if (m_SelectedCharaIndex == 2)
            {
                charaData.Name = "NouveauRiche";
                charaData.MaxStamina = 5;
                charaData.InitMoney = 8000;
                charaData.InitChips = 0;
                charaData.PushPower = 0;
                charaData.MoveSpeed = 0;
                charaData.SkillCost = new[] { 0, 0, 0 };
                charaData.Skills = new CharaSkillDataMessage { Id = "double_cost_power", StaminaRec = 0, ChipCost = 0 };
            }
            else if (m_SelectedCharaIndex == 3)
            {
                charaData.Name = "Fighter";
                charaData.MaxStamina = 5;
                charaData.InitMoney = 10000;
                charaData.InitChips = 0;
                charaData.PushPower = 0;
                charaData.MoveSpeed = 0;
                charaData.SkillCost = new[] { 3, 3, 3 };
                charaData.Skills = new CharaSkillDataMessage { Id = "fighter_skill", StaminaRec = 0, ChipCost = 3 };
            }
            else if (m_SelectedCharaIndex == 4)
            {
                // ガーディアン: スキル発動中はノックバックとダメージを完全無効化する無敵防御キャラ。
                // 最大定力が高いが定力回復手段がないため、防御タイミングが鍵になる。
                charaData.Name = "Guardian";
                charaData.MaxStamina = 7;
                charaData.InitMoney = 10000;
                charaData.InitChips = 3;
                charaData.PushPower = 0;
                charaData.MoveSpeed = 0;
                charaData.SkillCost = new[] { 4, 4, 4 };
                charaData.Skills = new CharaSkillDataMessage { Id = "guardian_skill", StaminaRec = 0, ChipCost = 4 };
            }
            else if (m_SelectedCharaIndex == 5)
            {
                // イカサマ: 初回スキル発動のコストが高いが、以降相手のインテントをリアルタイムで碟き見ることができる。
                // 初期資金少し多め。
                charaData.Name = "Scammer";
                charaData.MaxStamina = 5;
                charaData.InitMoney = 12000;
                charaData.InitChips = 0;
                charaData.PushPower = 0;
                charaData.MoveSpeed = 0;
                charaData.SkillCost = new[] { 15, 15, 15 }; // 初回発動コストが重い
                charaData.Skills = new CharaSkillDataMessage { Id = "scammer_skill", StaminaRec = 0, ChipCost = 15 };
            }
            else if (m_SelectedCharaIndex == 6)
            {
                // 債務者: チップが少ないときのみスキル発動可能。
                // フィールド上のアイテムを一括回収し、次の突進を強化する。
                // 初期資金が少ないので経済管理が重要になる。
                charaData.Name = "Debtor";
                charaData.MaxStamina = 5;
                charaData.InitMoney = 6000;
                charaData.InitChips = 0;
                charaData.PushPower = 1; // 突進力が少し高め
                charaData.MoveSpeed = 0;
                charaData.SkillCost = new[] { 2, 2, 2 }; // スキルコストは低い
                charaData.Skills = new CharaSkillDataMessage { Id = "debtor_skill", StaminaRec = 0, ChipCost = 2 };
            }

            // CSV読み込みによる上書き
            // コストのスケーリングはとりあえず読み取ったものから1x,2x,3xする
            // 足りない項目はたす
            // 英字はなにかと、読み取る順番はcsvのカンマ区切りでないとだめかどうか？
            if (m_SelectedCharaIndex >= 0 && m_SelectedCharaIndex < CharacterSheetUrls.Length)
            {
                string url = CharacterSheetUrls[m_SelectedCharaIndex];
                var csvData = await LoadCSVFromUrlAsync(url);
                if (csvData != null)
                {
                    int maxStamina;
                    int initMoney;
                    int initChips;
                    int pushPower;
                    int defPower;

                    // キャラクター名
                    if (csvData.TryGetValue("キャラクター名", out var nameVals) && nameVals.Length > 0)
                        charaData.Name = nameVals[0];
                    else if (csvData.TryGetValue("Name", out nameVals) && nameVals.Length > 0)
                        charaData.Name = nameVals[0];

                    // スタミナ（体幹）
                    if (csvData.TryGetValue("スタミナ（体幹）", out var maxStaminaVals) && maxStaminaVals.Length > 0)
                        if (int.TryParse(maxStaminaVals[0], out maxStamina)) charaData.MaxStamina = maxStamina;
                        else if (csvData.TryGetValue("MaxStamina", out maxStaminaVals) && maxStaminaVals.Length > 0)
                            if (int.TryParse(maxStaminaVals[0], out maxStamina)) charaData.MaxStamina = maxStamina;

                    // 資金
                    if (csvData.TryGetValue("資金", out var initMoneyVals) && initMoneyVals.Length > 0)
                        if (int.TryParse(initMoneyVals[0], out initMoney)) charaData.InitMoney = initMoney;
                        else if (csvData.TryGetValue("InitMoney", out initMoneyVals) && initMoneyVals.Length > 0)
                            if (int.TryParse(initMoneyVals[0], out initMoney)) charaData.InitMoney = initMoney;

                    // チップ
                    if (csvData.TryGetValue("チップ", out var initChipsVals) && initChipsVals.Length > 0)
                        if (int.TryParse(initChipsVals[0], out initChips)) charaData.InitChips = initChips;
                        else if (csvData.TryGetValue("InitChips", out initChipsVals) && initChipsVals.Length > 0)
                            if (int.TryParse(initChipsVals[0], out initChips)) charaData.InitChips = initChips;

                    // 突進 (power, chip)
                    if (csvData.TryGetValue("突進", out var pushVals))
                    {
                        if (pushVals.Length > 0 && int.TryParse(pushVals[0], out pushPower))
                            charaData.PushPower = pushPower;
                        if (pushVals.Length > 1)
                            charaData.PushCost = ParseIntArray(pushVals[1], charaData.PushCost);
                    }

                    // 防御 (power, chip)
                    if (csvData.TryGetValue("防御", out var defenseVals))
                    {
                        if (defenseVals.Length > 0 && int.TryParse(defenseVals[0], out defPower))
                            charaData.DefensePower = defPower;
                        if (defenseVals.Length > 1)
                            charaData.DefenseCost = ParseIntArray(defenseVals[1], charaData.DefenseCost);
                    }

                    // スキル (power(不要), chip)
                    if (csvData.TryGetValue("スキル", out var skillVals))
                    {
                        if (skillVals.Length > 1)
                        {
                            charaData.SkillCost = ParseIntArray(skillVals[1], charaData.SkillCost);
                            charaData.Skills.ChipCost = charaData.SkillCost[0];
                        }
                    }

                    // スキルIDの個別指定がもしCSV側にあれば読み込む (A列: SkillId など)
                    if (csvData.TryGetValue("SkillId", out var skillIdVals) && skillIdVals.Length > 0)
                        charaData.Skills.Id = skillIdVals[0];
                }
            }

            m_Net.Emit(ClientEvents.PlayerReady, new PlayerReadyMessage
            {
                IsAI = isAI,
                CharaData = charaData
            });
        }

        private async Task<Dictionary<string, string[]>> LoadCSVFromUrlAsync(string url)
        {
            if (string.IsNullOrEmpty(url) || url.Contains("YOUR_SPREADSHEET_ID"))
            {
                return null;
            }

            try
            {
                using var req = UnityWebRequest.Get(url);
                var operation = req.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"CSV読み込み失敗: {req.error} (URL: {url})");
                    return null;
                }

                var dict = new Dictionary<string, string[]>();
                var lines = req.downloadHandler.text.Split('\n');
                foreach (var line in lines)
                {
                    var cols = line.Split(',');
                    if (cols.Length < 1) continue;

                    string key = cols[0].Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    var vals = new List<string>();
                    for (int i = 1; i < cols.Length; i++)
                    {
                        string cVal = cols[i].Trim();
                        if (cVal.StartsWith("\"") && cVal.EndsWith("\""))
                        {
                            cVal = cVal.Substring(1, cVal.Length - 2).Trim();
                        }
                        if (string.IsNullOrEmpty(cVal)) continue; // カンマの連続による空要素を除外
                        vals.Add(cVal);
                    }
                    dict[key] = vals.ToArray();
                }

                return dict;
            }
            catch (Exception ex)
            {
                Debug.LogError($"CSV読み込み中に例外が発生しました: {ex.Message} (URL: {url})");
                return null;
            }
        }

        private int[] ParseIntArray(string val, int[] defaultValue)
        {
            if (string.IsNullOrEmpty(val)) return defaultValue;
            var parts = val.Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return defaultValue;
            var result = new List<int>();
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out var parsed))
                {
                    result.Add(parsed);
                }
            }

            // 1つしか数値がない場合、2、3段階目はそれぞれ2倍、3倍にする
            if (result.Count == 1)
            {
                int baseVal = result[0];
                return new[] { baseVal, baseVal * 2, baseVal * 3 };
            }

            return result.Count > 0 ? result.ToArray() : defaultValue;
        }

        public void SubmitUnready()
        {
            m_Net.Emit(ClientEvents.PlayerUnready, new { });
        }

        public void SubmitEnterLobby()
        {
            m_Net.Emit(ClientEvents.EnterLobby, new { });
        }

        public void SubmitExchange(int amount)
        {
            m_Net.Emit(ClientEvents.ExchangeChips, new ExchangeChipsMessage { Amount = amount });
        }

        public void SubmitBuff(string buffId)
        {
            m_Net.Emit(ClientEvents.BuffSelected, new BuffSelectedMessage { BuffId = buffId });
        }

        public void SubmitMission(string missionId)
        {
            m_Net.Emit(ClientEvents.MissionSelected, new MissionSelectedMessage { MissionId = missionId });
        }

        public void SubmitRoundReady()
        {
            m_Net.Emit(ClientEvents.RoundReady, new { });
        }

        public void SubmitSelectChara(int index)
        {
            m_SelectedCharaIndex = index;
            m_Net.Emit(ClientEvents.SelectChara, new SelectCharaMessage { Index = index });
        }

        public void SubmitFinalRaisePropose(bool accept)
        {
            m_Net.Emit(ClientEvents.FinalRaisePropose, new FinalRaiseProposeMessage { Accept = accept });
        }

        public void SubmitFinalRaiseRespond(bool accept)
        {
            m_Net.Emit(ClientEvents.FinalRaiseRespond, new FinalRaiseRespondMessage { Accept = accept });
        }


        public void NotifySuddenDeathRequested()
        {
            // サーバーへリクエスト送信
            m_Net.Emit("request_sudden_death", null);
        }

        private void Subscribe()
        {
            m_Net.OnConnected += () =>
            {
                IsConnected = true;
                // 接続が確立するたびに端末トークンを送る。
                // サーバはこれを見て新規入室か再接続かを判定する。
                m_Net.Emit(ClientEvents.Identify, new IdentifyMessage { Token = m_Token });
                OnConnectionChanged?.Invoke(true);
            };
            m_Net.OnDisconnected += () =>
            {
                IsConnected = false;
                OnConnectionChanged?.Invoke(false);
            };

            m_Net.On<InitMessage>(ServerEvents.Init, HandleInit);
            m_Net.On<SyncStateMessage>(ServerEvents.SyncState, HandleSyncState);
            m_Net.On<ItemDto[]>(ServerEvents.SyncItems, HandleSyncItems);
            m_Net.On<BeatMessage>(ServerEvents.Beat, HandleBeat);
            m_Net.On<EventDto[]>(ServerEvents.GameEvents, HandleGameEvents);
            m_Net.On<RoundOverMessage>(ServerEvents.RoundOver, HandleRoundOver);
            m_Net.On<GameOverMessage>(ServerEvents.GameOver, HandleGameOver);
            m_Net.On<WaitingForOthersMessage>(ServerEvents.WaitingForOthers, HandleWaitingForOthers);
            m_Net.On<string>(ServerEvents.PlayerLeft, HandlePlayerLeft);

            m_Net.On(ServerEvents.StartCountdown, () => OnCountdownStart?.Invoke());
            m_Net.On(ServerEvents.CountdownCanceled, () => OnCountdownCancel?.Invoke());
            m_Net.On(ServerEvents.PrepareRound, () => OnPrepareRound?.Invoke());
            m_Net.On<CharaSelectedMessage>(ServerEvents.CharaSelected,
                msg => OnCharaSelected?.Invoke(msg.PlayerId, msg.Index));
            m_Net.On(ServerEvents.StartExchange, () => SetPhase(EGamePhase.Exchange));
            m_Net.On(ServerEvents.StartBuffSelection, () => SetPhase(EGamePhase.BuffSelection));
            m_Net.On(ServerEvents.StartMatchCountdown, () => SetPhase(EGamePhase.Countdown));
            m_Net.On(ServerEvents.RoundStart, () => SetPhase(EGamePhase.Battle));
            m_Net.On(ServerEvents.CloseAll, HandleCloseAll);
            m_Net.On(ServerEvents.RoomFull, HandleRoomFull);

            m_Net.On<FinalRaiseOfferMessage>(ServerEvents.FinalRaiseOffer, HandleFinalRaiseOffer);
            m_Net.On<FinalRaisePendingMessage>(ServerEvents.FinalRaisePending, HandleFinalRaisePending);
            m_Net.On<FinalRaiseCanceledMessage>(ServerEvents.FinalRaiseCanceled, HandleFinalRaiseCanceled);
            m_Net.On(ServerEvents.FinalRaiseStarted, HandleFinalRaiseStarted);
            // イカサマスキル発動中、相手のintentが更新された際の通知（イカサマのソケットにのみ送信される）。
            m_Net.On<OpponentIntentRevealedMessage>(ServerEvents.OpponentIntentRevealed,
                msg => OnOpponentIntentRevealed?.Invoke(msg));

            m_Net.On("sudden_death_started", () => { Debug.Log("[GameState] sudden_death_started received"); RaiseSuddenDeathStarted(); });

        }

        private void HandleInit(InitMessage msg)
        {
            MyId = msg.Id;
            GridSize = msg.GridSize;
            ReplacePlayers(msg.Players);
            Debug.Log($"[GameState] init: id={MyId} players={m_Players.Count} grid={GridSize}");
            OnStateInitialized?.Invoke();
            OnPlayersChanged?.Invoke();
        }

        private void HandleSyncState(SyncStateMessage msg)
        {
            ReplacePlayers(msg.Players);
            OnPlayersChanged?.Invoke();
        }

        private void HandleSyncItems(ItemDto[] items)
        {
            m_Items = items != null ? new List<ItemDto>(items) : new List<ItemDto>();
            OnItemsChanged?.Invoke();
        }

        private void HandleBeat(BeatMessage msg)
        {
            CurrentBeat = msg.Beat;
            TimeLeft = msg.TimeLeft;
            GameActive = msg.GameActive;
            CycleCount = msg.CycleCount;

            OnBeatChanged?.Invoke();
        }

        private void HandleGameEvents(EventDto[] events)
        {
            if (events == null || events.Length == 0) return;

            foreach (var ev in events)
            {
                // ミッション達成イベントを検知してログ出力
                if (ev.Type == EventTypes.Vfx && ev.VfxType == VfxTypes.Bump && ev.Text != null && ev.Text.Contains("MISSION CLEAR"))
                {
                    var player = m_Players.Values.FirstOrDefault(p => p.Id == ev.TargetId);
                    string role = player != null ? player.Role : "Unknown";
                    Debug.Log($"<color=yellow>[Mission]</color> <b>Player {role} がミッションを達成しました！</b> ({ev.Text})");
                }
            }

            OnGameEvents?.Invoke(events);
        }

        private void HandleRoundOver(RoundOverMessage msg)
        {
            SetPhase(EGamePhase.RoundOver);
            OnRoundOver?.Invoke(msg.WinnerRole);
        }

        private void HandleGameOver(GameOverMessage msg)
        {
            // 試合終了時はファイナルレイズ状態を必ずリセット（中断 / 完走どちらの経路でも）。
            IsFinalDuel = false;
            SetPhase(EGamePhase.GameOver);
            OnGameOver?.Invoke(msg.WinnerRole);
        }

        private void HandleFinalRaiseOffer(FinalRaiseOfferMessage msg)
        {
            OnFinalRaiseOffer?.Invoke(msg);
        }

        private void HandleFinalRaisePending(FinalRaisePendingMessage msg)
        {
            OnFinalRaisePending?.Invoke(msg);
        }

        private void HandleFinalRaiseCanceled(FinalRaiseCanceledMessage msg)
        {
            OnFinalRaiseCanceled?.Invoke(msg);
        }

        private void HandleFinalRaiseStarted()
        {
            IsFinalDuel = true;
            OnFinalRaiseStarted?.Invoke();
        }

        private void HandleWaitingForOthers(WaitingForOthersMessage msg)
        {
            OnWaitingForOthers?.Invoke(msg.WaitingFor);
        }

        private void HandlePlayerLeft(string id)
        {
            if (m_Players.Remove(id))
                OnPlayersChanged?.Invoke();
            OnPlayerLeft?.Invoke(id);
        }

        private void HandleCloseAll()
        {
            Debug.LogWarning("[GameState] Server requested close_all");
            m_Net.Disconnect();
        }

        // 既に 2 人で埋まっているため入室を断られた。正常な 2 人対戦では起きない。
        private void HandleRoomFull()
        {
            Debug.LogWarning("[GameState] 入室を断られました（既に 2 人で対戦中）");
        }

        private void ReplacePlayers(Dictionary<string, PlayerDto> incoming)
        {
            m_Players.Clear();
            if (incoming == null) return;
            foreach (var kv in incoming)
            {
                var player = kv.Value;


                m_Players[kv.Key] = kv.Value;

                // プレイヤーの統計情報を更新
                RefreshPlayerStats(player);
            }
        }

        // プレイヤーのModifierに基づきステータスを再計算
        private void RefreshPlayerStats(PlayerDto player)
        {
            if (player == null) return;

            Debug.Log($"[GameState] PlayerStats Refreshed: {player.Id}, " +
                      $"CurrentMaxStamina={player.CurrentMaxStamina} (Base={player.MaxStamina}), " +
                      $"CurrentPushPower={player.CurrentPushPower}, " +
                      $"CurrentDefensePower={player.CurrentDefensePower}");
        }

        private void SetPhase(EGamePhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            Debug.Log($"[GameState] phase → {phase}");
            OnPhaseChanged?.Invoke(phase);
        }

        public void Dispose()
        {
            m_Net.Off(ServerEvents.Init);
            m_Net.Off(ServerEvents.SyncState);
            m_Net.Off(ServerEvents.SyncItems);
            m_Net.Off(ServerEvents.Beat);
            m_Net.Off(ServerEvents.GameEvents);
            m_Net.Off(ServerEvents.RoundOver);
            m_Net.Off(ServerEvents.GameOver);
            m_Net.Off(ServerEvents.WaitingForOthers);
            m_Net.Off(ServerEvents.PlayerLeft);
            m_Net.Off(ServerEvents.StartCountdown);
            m_Net.Off(ServerEvents.CountdownCanceled);
            m_Net.Off(ServerEvents.PrepareRound);
            m_Net.Off(ServerEvents.CharaSelected);
            m_Net.Off(ServerEvents.StartExchange);
            m_Net.Off(ServerEvents.StartBuffSelection);
            m_Net.Off(ServerEvents.StartMatchCountdown);
            m_Net.Off(ServerEvents.RoundStart);
            m_Net.Off(ServerEvents.CloseAll);
            m_Net.Off(ServerEvents.FinalRaiseOffer);
            m_Net.Off(ServerEvents.FinalRaisePending);
            m_Net.Off(ServerEvents.FinalRaiseCanceled);
            m_Net.Off(ServerEvents.FinalRaiseStarted);
            m_Net.Off(ServerEvents.OpponentIntentRevealed);
        }
        public void RaiseSuddenDeathStarted()
        {
            SuddenDeathAlreadyStarted = true;
            OnSuddenDeathStarted?.Invoke();
        }

    }




}


/*既存のコードに追加
        public int CycleCount { get; private set; }
        CycleCountの下に追加
        public int CurrentBarIndex { get; private set; }
        public int CurrentAbsoluteBeat { get; private set; }
        public int NextBeat { get; private set; }
        public int NextBarIndex { get; private set; }
        public int NextAbsoluteBeat { get; private set; }
        public long BeatSequence { get; private set; }
        public long RoundId { get; private set; }
        public long BeatStartServerMs { get; private set; }
        public long NextBoundaryServerMs { get; private set; }
        public int BeatIntervalMs { get; private set; }
        public int BeatsPerBar { get; private set; } = 4; 

private void HandleInit(InitMessage msg)
{
        CurrentBeat = 0;
        CurrentBarIndex = 0;
        CurrentAbsoluteBeat = 0;
        NextBeat = 0;
        NextBarIndex = 0;
        NextAbsoluteBeat = 0;
        BeatSequence = 0;
        RoundId = 0;
        BeatStartServerMs = 0;
        NextBoundaryServerMs = 0;
        BeatIntervalMs = 0;
        BeatsPerBar = 4;
        
        MyId = msg.Id;


private void HandleBeat(BeatMessage msg)
{
			CurrentBeat = msg.Beat;
			TimeLeft = msg.TimeLeft;
			GameActive = msg.GameActive;

			CurrentBarIndex = msg.BarIndex;
			BeatSequence = msg.BeatSequence;
			RoundId = msg.RoundId;
			BeatStartServerMs = msg.BeatStartServerMs;
			NextBoundaryServerMs = msg.NextBoundaryServerMs;
			BeatIntervalMs = msg.BeatIntervalMs;
			BeatsPerBar = msg.BeatsPerBar > 0 ? msg.BeatsPerBar : 4;

			CurrentAbsoluteBeat = ((CurrentBarIndex - 1) * BeatsPerBar) + CurrentBeat;

			if (CurrentBeat >= BeatsPerBar)
			{
				NextBeat = 1;
				NextBarIndex = CurrentBarIndex + 1;
			}
			else
			{
				NextBeat = CurrentBeat + 1;
				NextBarIndex = CurrentBarIndex;
			}

			NextAbsoluteBeat = ((NextBarIndex - 1) * BeatsPerBar) + NextBeat;

			 OnBeatChanged?.Invoke();
		}

private void HandleGameOver(GameOverMessage msg)
{
    RoundId = 0;
    SetPhase(EGamePhase.GameOver);

 */