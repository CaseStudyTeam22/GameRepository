using System.Collections.Generic;
using Newtonsoft.Json;

namespace GamblingAction.Core.Dto
{
    public class InitMessage
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("players")] public Dictionary<string, PlayerDto> Players;
        [JsonProperty("gridSize")] public int GridSize;
    }

    public class SyncStateMessage
    {
        [JsonProperty("players")] public Dictionary<string, PlayerDto> Players;
    }

    public class BeatMessage
    {
        [JsonProperty("beat")] public int Beat;
        [JsonProperty("timeLeft")] public int TimeLeft;
        [JsonProperty("gameActive")] public bool GameActive;
        [JsonProperty("cycleCount")] public int CycleCount;
    }

    public class RoundOverMessage
    {
        [JsonProperty("winnerRole")] public string WinnerRole;
    }

    public class GameOverMessage
    {
        [JsonProperty("winnerRole")] public string WinnerRole;
    }

    public class WaitingForOthersMessage
    {
        [JsonProperty("waitingFor")] public string WaitingFor;
    }

    public class CharaSelectedMessage
    {
        [JsonProperty("playerId")] public string PlayerId;
        [JsonProperty("index")] public int Index;
    }

    // ファイナルレイズの提案フェーズ開始。敗者（proposerRole）が発起するかを決める。
    public class FinalRaiseOfferMessage
    {
        [JsonProperty("proposerRole")] public string ProposerRole;
        [JsonProperty("responderRole")] public string ResponderRole;
        [JsonProperty("timeoutMs")] public int TimeoutMs;
    }

    // 敗者が発起した後の、勝者の応答待ちフェーズ。
    public class FinalRaisePendingMessage
    {
        [JsonProperty("proposerRole")] public string ProposerRole;
        [JsonProperty("responderRole")] public string ResponderRole;
        [JsonProperty("timeoutMs")] public int TimeoutMs;
    }

    // ファイナルレイズが中断された理由（拒否 / タイムアウト / 切断）。
    public class FinalRaiseCanceledMessage
    {
        [JsonProperty("reason")] public string Reason;
    }

    // イカサマスキル発動中、相手が intent を送信した際にイカサマのみに届く通知。
    public class OpponentIntentRevealedMessage
    {
        [JsonProperty("intent")] public IntentDto Intent;
    }

    public static class ServerEvents
    {
        public const string Init = "init";
        public const string SyncState = "sync_state";
        public const string SyncItems = "sync_items";
        public const string Beat = "beat";
        public const string GameEvents = "game_events";
        public const string StartExchange = "start_exchange";
        public const string StartBuffSelection = "start_buff_selection";
        public const string StartMatchCountdown = "start_match_countdown";
        public const string RoundStart = "round_start";
        public const string RoundOver = "round_over";
        public const string GameOver = "game_over";
        public const string WaitingForOthers = "waiting_for_others";
        public const string PlayerLeft = "player_left";
        public const string CloseAll = "close_all";
        // Lobby で双方準備完了後のカウントダウン開始 / 中断。
        public const string StartCountdown = "start_countdown";
        public const string CountdownCanceled = "countdown_canceled";
        // ラウンドの開始要求。クライアントは盤面・キャラ生成を終えてから round_ready を返す。
        public const string PrepareRound = "prepare_round";
        // 誰かが Lobby でキャラを選択した。相手側の表示同期に使う。
        public const string CharaSelected = "chara_selected";
        // 2 勝が決まった瞬間に敗者へ送る、ファイナルレイズの発起確認。
        public const string FinalRaiseOffer = "final_raise_offer";
        // 敗者が発起したあと、勝者の応答を待つフェーズ。
        public const string FinalRaisePending = "final_raise_pending";
        // 拒否・タイムアウト・切断でファイナルレイズが中断された通知。直後に game_over が来る。
        public const string FinalRaiseCanceled = "final_raise_canceled";
        // 勝者が受諾し、ファイナルレイズ本番ラウンドへ入る通知。
        public const string FinalRaiseStarted = "final_raise_started";
        // イカサマスキル発動中、相手のintentが更新された際にイカサマプレイヤーのみに送信される通知。
        public const string OpponentIntentRevealed = "opponent_intent_revealed";

        // 既に 2 人ぶんの席が埋まっているため入室を断られた通知。直後にサーバから切断される。
        public const string RoomFull = "room_full";
    }
}

/*既存のコードに追加
" public class BeatMessage "
{
		[JsonProperty("beat")]       public int Beat;
		[JsonProperty("timeLeft")]   public int TimeLeft;
		[JsonProperty("gameActive")] public bool GameActive;
		[JsonProperty("beat")]					public int Beat;
		[JsonProperty("timeLeft")]				public int TimeLeft;
		[JsonProperty("gameActive")]			public bool GameActive;
        [JsonProperty("cycleCount")]            public int CycleCount;
        [JsonProperty("barIndex")]				public int BarIndex;
        [JsonProperty("beatSequence")]			public long BeatSequence;
        [JsonProperty("roundId")]				public long RoundId;
        [JsonProperty("beatStartServerMs")]		public long BeatStartServerMs;
        [JsonProperty("nextBoundaryServerMs")]	public long NextBoundaryServerMs;
        [JsonProperty("beatIntervalMs")]		public int BeatIntervalMs;
        [JsonProperty("beatsPerBar")]			public int BeatsPerBar;
 
 */