using Newtonsoft.Json;
using System.Collections.Generic;

namespace GamblingAction.Core.Dto
{
	public class PlayerDto
	{
		[JsonProperty("id")]          public string Id;
		[JsonProperty("role")]        public string Role;
		[JsonProperty("x")]           public int X;
		[JsonProperty("y")]           public int Y;
		[JsonProperty("intent")]      public IntentDto Intent;
		[JsonProperty("ready")]       public bool Ready;
		[JsonProperty("inLobby")]     public bool InLobby;
		[JsonProperty("exchanged")]   public bool Exchanged;
		[JsonProperty("pendingExchange")] public int PendingExchange;
		[JsonProperty("score")]       public int Score;
		[JsonProperty("money")]       public int Money;
		[JsonProperty("chips")]       public int Chips;
		[JsonProperty("isAI")]        public bool IsAI;
		[JsonProperty("personality")] public string Personality;
		[JsonProperty("color")]       public string Color;
		[JsonProperty("selectedBuff")] public string SelectedBuff;
		[JsonProperty("buffReady")]   public bool BuffReady;
		[JsonProperty("falling")]     public bool Falling;

		[JsonProperty("characterId")] public string CharacterId; // キャラクター識別ID

		[JsonProperty("mission")] public MissionDto Mission; // 現在のミッション
		[JsonProperty("availableMissions")] public List<MissionDto> AvailableMissions; // 選択可能なミッション候補
		[JsonProperty("modifiers")]   public PlayerModifiersDto Modifiers;
		[JsonProperty("scammerActive")] public bool ScammerActive;
		[JsonProperty("stamina")]     public int Stamina;
		[JsonProperty("maxStamina")]  public int MaxStamina; // GameConfigからこっちに移行かな
		[JsonProperty("charaIndex")]  public int CharaIndex;

		[JsonProperty("skillData")]  public SkillDataDto SkillData;

		// --- サーバー側でバフや補正値を適用した後の最終的な現在能力値 ---
		[JsonProperty("currentMaxStamina")]   public int CurrentMaxStamina;   // バフ・ボーナス適用後の最大スタミナ。UI等でメモリ数を決める際はこちらを直接参照してください。
		[JsonProperty("currentPushPower")]    public int CurrentPushPower;    // バフ・ボーナス適用後の突進力。
		[JsonProperty("currentDefensePower")] public int CurrentDefensePower; // バフ・ボーナス適用後の防御力。
	}

	public class SkillDataDto
	{
		[JsonProperty("id")]         public string Id;
		[JsonProperty("staminaRec")] public int StaminaRec;
		[JsonProperty("chipCost")]   public int ChipCost;
	}

	public class PlayerModifiersDto
	{
		[JsonProperty("maxStaminaBonus")]       public int MaxStaminaBonus;
		[JsonProperty("pushPowerBonus")]        public int PushPowerBonus;
		[JsonProperty("moveSpeedBonus")]        public int MoveSpeedBonus;
		[JsonProperty("chipCostMultiplier")]    public float ChipCostMultiplier;
		[JsonProperty("defenseReductionBonus")] public float DefenseReductionBonus;
	}

	// キャラクターの初期データの定義
	// とりあえず選択キャラに応じてここからデータを引っ張りサーバー側に渡すような処理の構築が必要。
	public class InitCharacterDto
	{
		[JsonProperty("characterId")] public string CharacterId; // 管理id
		[JsonProperty("name")]        public string Name; // 表示名
		[JsonProperty("description")] public string Description;
		[JsonProperty("initMoney")]    public int InitMoney; // 初期所持金
		[JsonProperty("initChips")]    public int InitChips; // 初期チップ数
		[JsonProperty("initStamina")]  public int InitStamina; // 初期最大スタミナ
	}

	// キャラごとのスキル値管理
	public class CharacterSkillDto
	{
		[JsonProperty("characterId")] public string CharacterId; // 管理id
		[JsonProperty("skill")]      public string Skill;
		[JsonProperty("power")]      public int Power; // 強さ
		[JsonProperty("cost")]       public int Cost; // スタミナコスト
		[JsonProperty("target")]     public string Target; // 対象 (self, enemy, all)
		[JsonProperty("damage")] 	public int Damage; // ダメージ量 (攻撃スキルの場合)
		[JsonProperty("heal")] 		public int Heal; // 回復量 (回復スキルの場合)
		[JsonProperty("forceAction")] public string ForceAction; // スキルを発動した際に強制行動 (攻撃ならattack, 防御ならdefendなど)

	}

	public class IntentDto
	{
		[JsonProperty("type")]  public string Type;
		[JsonProperty("dir")]   public string Dir;
		[JsonProperty("power")] public int Power;
	}


	public enum EMissionType
	{
		Move,
		Push,
		//Attack,
		Defend,
		//Heal,
		GainCoin,
		GainChip
	}

	public class MissionDto
	{
		[JsonProperty("id")]           public string Id;
		[JsonProperty("type")]         public EMissionType Type;
		[JsonProperty("description")]  public string Description;
		[JsonProperty("targetCount")]  public int TargetCount;
		[JsonProperty("currentCount")] public int CurrentCount;
		[JsonProperty("rewardType")]   public string RewardType;
		[JsonProperty("rewardValue")]  public int RewardValue;
		[JsonProperty("isCleared")]    public bool IsCleared;
	}
}
