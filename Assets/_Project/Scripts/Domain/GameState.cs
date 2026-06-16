using System;
using System.Collections.Generic;
using System.Linq;
using GamblingAction.Core;
using GamblingAction.Core.Dto;
using GamblingAction.Net;
using UnityEngine;

namespace GamblingAction.Domain
{
	public class GameState : IGameState, IDisposable
	{
		private readonly INetClient m_Net;
		private readonly Dictionary<string, PlayerDto> m_Players = new();
		private List<ItemDto> m_Items = new();
		private int m_SelectedCharaIndex = 0;

		public string MyId { get; private set; }
		public int GridSize { get; private set; } = GamblingAction.Core.GameConfig.GridSize;
		public IReadOnlyDictionary<string, PlayerDto> Players => m_Players;
		public IReadOnlyList<ItemDto> Items => m_Items;
		public int CurrentBeat { get; private set; }
		public int TimeLeft { get; private set; }
		public bool GameActive { get; private set; }
		public EGamePhase Phase { get; private set; } = EGamePhase.Lobby;
		public bool IsConnected { get; private set; }
		public bool IsFinalDuel { get; private set; }

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

		public GameState(INetClient net)
		{
			m_Net = net;
			Subscribe();
		}

		public void SubmitIntent(string type, string dir, int power)
		{
			if (!GameActive || CurrentBeat >= 4) return;
			var me = Me;
			if (me == null || me.IsAI) return;

			// テストで4拍ごとに押し出し力+1
			//m_Players[MyId].PushModifier.AddModifier("test", new Modifier { Type = "push", RawValue = 1.0f, RatioValue = 0.0f});
			//Debug.Log($"[GameState] SubmitIntent: type={type} dir={dir} basePower={power} finalPower={GetCalculatedPower(MyId, type, power)}");

			// スタミナ上限を追加
			//m_Players[MyId].StaminaModifier.AddModifier("test", new Modifier { Type = "stamina", RawValue = 1.0f, RatioValue = 0.0f });

			// ステータスを更新
			RefreshPlayerStats(m_Players[MyId]);
			// 自分の stats 変化を UI に反映する。
			OnPlayersChanged?.Invoke();

			// 補正等を考慮した力を持ってくる
			int finalPower = GetCalculatedPower(MyId, type, power);

			m_Net.Emit(ClientEvents.SetIntent, new SetIntentMessage { Type = type, Dir = dir, Power = finalPower });
		}

		public void SubmitReady(bool isAI)
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

			// ===== TODO: 下記、スプシから読み込むように学校で変更すること =====

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
				charaData.MoveCost = new[] { 3, 6, 9 };
				//charaData.PushCost = new[] { 3, 5, 9 },
				//charaData.AttackCost = new[] { 3, 5, 9 },
				//charaData.DefenseCost = new[] { 2, 2, 2 },
				charaData.SkillCost = new[] { 3, 3, 3 }; // スキルコスト3固定
				charaData.Skills = new CharaSkillDataMessage { Id = "heal_instant", StaminaRec = 2, ChipCost = 3 };
			}
			else if (m_SelectedCharaIndex == 2)
			{
				charaData.Name = "DoubleEdge";
				charaData.MaxStamina = 5;
				charaData.InitMoney = 8000; // 初期資金が少なめ
				charaData.InitChips = 0;
				charaData.PushPower = 0;
				charaData.MoveSpeed = 0;
				// キャラ別に発動コストを変更したい場合はこれをいじってね
				//charaData.MoveCost = new[] { 1, 3, 5 };
				//charaData.PushCost = new[] { 3, 5, 9 },
				//charaData.AttackCost = new[] { 3, 5, 9 },
				//charaData.DefenseCost = new[] { 2, 2, 2 },
				charaData.SkillCost = new[] { 0, 0, 0 }; // スキルコスト0
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
				// キャラ別に発動コストを変更したい場合はこれをいじってね
				//charaData.MoveCost = new[] { 1, 3, 5 };
				//charaData.PushCost = new[] { 3, 5, 9 },
				//charaData.AttackCost = new[] { 3, 5, 9 },
				//charaData.DefenseCost = new[] { 2, 2, 2 },
				charaData.SkillCost = new[] { 3, 3, 3 };
				charaData.Skills = new CharaSkillDataMessage { Id = "fighter_skill", StaminaRec = 0, ChipCost = 3 };
			}

			m_Net.Emit(ClientEvents.PlayerReady, new PlayerReadyMessage 
			{ 
				IsAI = isAI,
				CharaData = charaData
			});
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

		public int GetCalculatedPower(string playerId, string intentType, int basePower)
		{
			if (!m_Players.TryGetValue(playerId, out var player)) return basePower;

			float finalPower = basePower;

			// 行動タイプ別に応じてバフを適応
			// stringで管理してる関係上switchｶﾅｰ
			switch(intentType)
			{
				case IntentTypes.Push:
					finalPower = player.PushModifier.GetModifiedValue(basePower);
					break;
				case IntentTypes.Move:
					finalPower = player.MoveModifier.GetModifiedValue(basePower);
					break;
				// スタミナは一旦まち
			}

			return Mathf.RoundToInt(finalPower);
		}

		private void Subscribe()
		{
			m_Net.OnConnected += () =>
			{
				IsConnected = true;
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

			m_Net.On(ServerEvents.StartCountdown,      () => OnCountdownStart?.Invoke());
			m_Net.On(ServerEvents.CountdownCanceled,   () => OnCountdownCancel?.Invoke());
			m_Net.On(ServerEvents.PrepareRound,        () => OnPrepareRound?.Invoke());
			m_Net.On<CharaSelectedMessage>(ServerEvents.CharaSelected,
				msg => OnCharaSelected?.Invoke(msg.PlayerId, msg.Index));
			m_Net.On(ServerEvents.StartExchange,       () => SetPhase(EGamePhase.Exchange));
			m_Net.On(ServerEvents.StartBuffSelection,  () => SetPhase(EGamePhase.BuffSelection));
			m_Net.On(ServerEvents.StartMatchCountdown, () => SetPhase(EGamePhase.Countdown));
			m_Net.On(ServerEvents.RoundStart,          () => SetPhase(EGamePhase.Battle));
			m_Net.On(ServerEvents.CloseAll,            HandleCloseAll);

			m_Net.On<FinalRaiseOfferMessage>(ServerEvents.FinalRaiseOffer, HandleFinalRaiseOffer);
			m_Net.On<FinalRaisePendingMessage>(ServerEvents.FinalRaisePending, HandleFinalRaisePending);
			m_Net.On<FinalRaiseCanceledMessage>(ServerEvents.FinalRaiseCanceled, HandleFinalRaiseCanceled);
			m_Net.On(ServerEvents.FinalRaiseStarted, HandleFinalRaiseStarted);
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

		private void ReplacePlayers(Dictionary<string, PlayerDto> incoming)
		{
			m_Players.Clear();
			if (incoming == null) return;
			foreach (var kv in incoming)
			{
				var player = kv.Value;
				// modifierはnullならインスタンス化しておく
				player.PushModifier ??= new ModifierContainer {Modifiers = new()};
				player.MoveModifier ??= new ModifierContainer {Modifiers = new()};
				player.StaminaModifier ??= new ModifierContainer {Modifiers = new()};

				m_Players[kv.Key] = kv.Value;
				
				// プレイヤーの統計情報を更新
				RefreshPlayerStats(player);
			}
		}

		// プレイヤーのModifierに基づきステータスを再計算
		private void RefreshPlayerStats(PlayerDto player)
		{
			if (player == null) return;

			if (player.Modifiers != null)
			{
				Debug.Log($"[GameState] PlayerStats Refreshed via Server Modifiers: {player.Id}, " +
				          $"MaxStamina={player.MaxStamina}, " +
				          $"MaxStaminaBonus={player.Modifiers.MaxStaminaBonus}, " +
				          $"MoveSpeedBonus={player.Modifiers.MoveSpeedBonus}, " +
				          $"PushPowerBonus={player.Modifiers.PushPowerBonus}");
			}
			else
			{
				Debug.Log($"[GameState] PlayerStats Refreshed: {player.Id}, MaxStamina={player.MaxStamina}");
			}
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
		}
	}

	// Modifierの拡張メソッド定義
	public static class ModifierDomainExtensions
	{
		public static float GetModifiedValue(this ModifierContainer container, float baseValue)
		{
			if (container == null || container.Modifiers == null) return baseValue;
			float totalRaw = 0.0f;
			float totalRatio = 0.0f;
			foreach (var modifier in container.Modifiers.Values)
			{
				totalRaw += modifier.RawValue;
				totalRatio += modifier.RatioValue;
			}
			return (baseValue + totalRaw) * (1 + totalRatio);
		}

		public static void AddModifier(this ModifierContainer container, string tag, Modifier modifier)
		{
			if (container.Modifiers == null) container.Modifiers = new Dictionary<string, Modifier>();
			container.Modifiers[tag] = modifier;
		}

		public static void RemoveModifier(this ModifierContainer container, string tag)
		{
			container.Modifiers?.Remove(tag);
		}
	}
}
