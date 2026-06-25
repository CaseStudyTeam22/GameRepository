using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	public class FlowPanelView : MonoBehaviour
	{
		[Header("Existing flow panels")]
		[FormerlySerializedAs("exchangePanel")]
		[SerializeField] private GameObject m_ExchangePanel;
		[FormerlySerializedAs("buffPanel")]
		[SerializeField] private GameObject m_BuffPanel;
		[FormerlySerializedAs("roundOverPanel")]
		[SerializeField] private GameObject m_RoundOverPanel;
		[FormerlySerializedAs("gameOverPanel")]
		[SerializeField] private GameObject m_GameOverPanel;

		[Header("Main game stage (Battle / Countdown)")]
		[FormerlySerializedAs("mainGameStage")]
		[SerializeField] private GameObject m_MainGameStage;

		[Header("Preparing countdown (Exchange / BuffSelection)")]
		[Tooltip("チップ交換 / カード選択フェーズの制限時間パネル。Timebar(Image filled radial) と TimeText を子に持つ")]
		[SerializeField] private GameObject m_PreparingCountdownPanel;

		[Header("Mission Panels")]
		[SerializeField] private GameObject m_MissionPanel;
		[SerializeField] private GameObject m_MissionSelectionPanel;

		[Tooltip("チップ交換 / カード選択の制限時間（秒）。サーバの PREPARE_PHASE_MS に合わせる")]
		[SerializeField] private float m_PrepareSeconds = 20f;

		[Header("Tuning")]
		[FormerlySerializedAs("totalRounds")]
		[SerializeField] private int m_TotalRounds = 3;
		[Tooltip("決着パネル（RoundOver）を表示しておく秒数。経過後に自動で隠す。サーバの次ラウンド開始待ち（3 秒）に合わせる")]
		[SerializeField] private float m_RoundOverDisplaySeconds = 3f;
		[FormerlySerializedAs("executeFlashSeconds")]
		[SerializeField] private float m_ExecuteFlashSeconds = 0.4f;
		[FormerlySerializedAs("beatOnColor")]
		[SerializeField] private Color m_BeatOnColor = new Color(0.94f, 0.62f, 0.15f);
		[FormerlySerializedAs("finalBeatOnColor")]
		[SerializeField] private Color m_FinalBeatOnColor = new Color(0.89f, 0.29f, 0.29f);
		[FormerlySerializedAs("beatOffColor")]
		[SerializeField] private Color m_BeatOffColor = new Color(0.17f, 0.17f, 0.16f, 1f);

		[Header("Command Buttons & Animation Settings")]
		[SerializeField, Tooltip("押し出しコマンド選択ボタン")]
		private Button m_PushButton;
		[SerializeField, Tooltip("攻撃コマンド選択ボタン")]
		private Button m_AttackButton;
		[SerializeField, Tooltip("防御コマンド選択ボタン")]
		private Button m_DefenseButton;
		[SerializeField, Tooltip("スキルコマンド選択ボタン")]
		private Button m_SkillButton;

		[SerializeField, Tooltip("コマンド選択時の背景ハイライト色 (Power 1: 黄)")]
		private Color m_SelectedColorPower1 = new Color(0.9f, 0.9f, 0.1f);
		[SerializeField, Tooltip("コマンド選択時の背景ハイライト色 (Power 2: 橙)")]
		private Color m_SelectedColorPower2 = new Color(0.94f, 0.5f, 0.15f);
		[SerializeField, Tooltip("コマンド選択時の背景ハイライト色 (Power 3: 赤)")]
		private Color m_SelectedColorPower3 = new Color(0.89f, 0.2f, 0.2f);

		// 各コマンドボタンのキャッシュ用配列
		private Button[] m_CommandButtons;
		// 各コマンドボタンの初期位置を保存する配列
		private Vector2[] m_CommandButtonsDefaultPositions;
		// 各コマンドボタンの初期背景色を保存する配列
		private Color[] m_CommandButtonsDefaultColors;
		// 確定アニメーションが現在実行中かどうかを示すフラグ
		private bool m_IsAnimatingCommandSelection;
		// コマンドボタンの並びを制御するレイアウトグループ
		private HorizontalLayoutGroup m_CommandLayoutGroup;
		// コマンドボタンのフェードやスライドなど、実行中の DOTween アニメーションを追跡して確実に停止するためのリスト
		private readonly System.Collections.Generic.List<Tween> m_ActiveTweens = new System.Collections.Generic.List<Tween>();
		// 前回のインテントタイプと強度（Power）を保存するキャッシュ変数（強度変更時の演出判定用）
		private string m_PrevIntentType;
		private int m_PrevPower = 1;

		[Header("Player slot colors")]
		[SerializeField, Tooltip("P1 の文字色（role 固定。ワールドの P1 と同じ色）")]
		// GameConfig.P1Color（#00f2fe）に合わせる
		private Color m_P1SlotColor = new Color(0f, 242f / 255f, 254f / 255f, 1f);
		[SerializeField, Tooltip("P2 の文字色（role 固定。ワールドの P2 と同じ色）")]
		// GameConfig.P2Color（#ff4444）に合わせる
		private Color m_P2SlotColor = new Color(1f, 68f / 255f, 68f / 255f, 1f);

		private IGameState m_State;

		// この回で両替申請したチップ数。精算は両替・カード選択が終わってからまとめて行うため、
		// カード選択ボタンの可否判定は「現チップ + この値」で行う。
		private int m_PendingExchange;

		private Slider m_ExchangeSlider;
		private TMP_Text m_ExchangeAmountText;
		private Button m_ExchangeConfirmButton;
		private Button m_HighRiskButton;
		private Button m_LowRiskButton;
		private Button m_SkipBuffButton;

		private TMP_Text m_MissionText;
		private TMP_Text m_MissionRewardText;
		private Image    m_MissionProgressFill;

		private Button[]   m_MissionOptionButtons;
		private TMP_Text[] m_MissionDescriptionTexts;
		private TMP_Text[] m_MissionRewardTexts;

		private TMP_Text m_P1Name, m_P1Money, m_P1Chips;
		private TMP_Text m_P2Name, m_P2Money, m_P2Chips;
		private Image[] m_NormalBeats;
		private Image m_FinalBeat;
		private TMP_Text m_ExecuteText;
		private TMP_Text m_ReadyText, m_CountdownText;
		private RectTransform m_TimeBarFill;
		private TMP_Text m_RoundText;

		private Image m_PrepareTimebar;
		private TMP_Text m_PrepareTimeText;

		private int m_RoundCount;
		private Coroutine m_CountdownCo;
		private Coroutine m_ExecuteFlashCo;
		private Coroutine m_RoundOverHideCo;
		private Coroutine m_PrepareCountdownCo;
		private Vector2 m_TimeBarFillFullSize;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[FlowPanel] GameStateLocator.Current is null");
				return;
			}

			FindFlowControls();
			FindStageControls();
			FindMissionControls();
			WireButtons();

			// コマンドボタンを配列にまとめ、それぞれの初期位置と背景色をキャッシュする
			InitializeCommandButtonsCache();

			m_State.OnPhaseChanged   += HandlePhase;
			m_State.OnPlayersChanged += HandlePlayersChanged;
			m_State.OnPlayersChanged += UpdateExchangeRange;
			m_State.OnBeatChanged    += HandleBeatChanged;

			// ローカルプレイヤーのインテント（コマンド）選択変更を監視
			LocalIntentBus.OnChanged += HandleLocalIntentChanged;

			HandlePhase(m_State.Phase);
			HandlePlayersChanged();
			UpdateExchangeRange();
			UpdateBeatVisual();
		}

		private void OnDestroy()
		{
			if (m_State == null) return;
			m_State.OnPhaseChanged   -= HandlePhase;
			m_State.OnPlayersChanged -= HandlePlayersChanged;
			m_State.OnPlayersChanged -= UpdateExchangeRange;
			m_State.OnBeatChanged    -= HandleBeatChanged;

			// ローカルプレイヤーのインテント選択の監視を解除
			LocalIntentBus.OnChanged -= HandleLocalIntentChanged;
		}

		private void FindFlowControls()
		{
			m_ExchangeSlider        = FindIn<Slider>(m_ExchangePanel, "ExchangeSlider");
			m_ExchangeAmountText    = FindIn<TMP_Text>(m_ExchangePanel, "ExchangeAmountText");
			m_ExchangeConfirmButton = FindIn<Button>(m_ExchangePanel, "ExchangeConfirmButton");

			m_HighRiskButton        = FindIn<Button>(m_BuffPanel, "HighRiskButton");
			m_LowRiskButton         = FindIn<Button>(m_BuffPanel, "LowRiskButton");
			m_SkipBuffButton        = FindIn<Button>(m_BuffPanel, "SkipBuffButton");

			m_PrepareTimebar  = FindIn<Image>(m_PreparingCountdownPanel, "Timebar");
			m_PrepareTimeText = FindIn<TMP_Text>(m_PreparingCountdownPanel, "TimeText");
		}

		private void FindMissionControls()
		{
			// Mission HUD
			m_MissionText         = FindIn<TMP_Text>(m_MissionPanel, "MissionText");
			m_MissionRewardText   = FindIn<TMP_Text>(m_MissionPanel, "RewardText");
			m_MissionProgressFill = FindIn<Image>(m_MissionPanel, "ProgressFill");

			// Mission Selection
			m_MissionOptionButtons    = new Button[3];
			m_MissionDescriptionTexts = new TMP_Text[3];
			m_MissionRewardTexts      = new TMP_Text[3];

			for (int i = 0; i < 3; i++)
			{
				string path = $"Option{i + 1}";
				m_MissionOptionButtons[i]    = FindByPath<Button>(m_MissionSelectionPanel, $"{path}/Button");
				m_MissionDescriptionTexts[i] = FindByPath<TMP_Text>(m_MissionSelectionPanel, $"{path}/Description");
				m_MissionRewardTexts[i]      = FindByPath<TMP_Text>(m_MissionSelectionPanel, $"{path}/Reward");
			}
		}

		private void FindStageControls()
		{
			if (m_MainGameStage == null) return;

			m_P1Name  = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P1/Name");
			m_P1Money = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P1/Money");
			m_P1Chips = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P1/Chips");
			m_P2Name  = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P2/Name");
			m_P2Money = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P2/Money");
			m_P2Chips = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P2/Chips");

			m_NormalBeats = new[]
			{
				FindByPath<Image>(m_MainGameStage, "Metronome/Layout/NormalBeat_1"),
				FindByPath<Image>(m_MainGameStage, "Metronome/Layout/NormalBeat_2"),
				FindByPath<Image>(m_MainGameStage, "Metronome/Layout/NormalBeat_3"),
			};
			m_FinalBeat   = FindByPath<Image>(m_MainGameStage, "Metronome/Layout/FinalBeat");
			m_ExecuteText = FindByPath<TMP_Text>(m_MainGameStage, "Metronome/ExecuteText");

			m_ReadyText     = FindByPath<TMP_Text>(m_MainGameStage, "ReadyPanel/Ready");
			m_CountdownText = FindByPath<TMP_Text>(m_MainGameStage, "ReadyPanel/Countdown");

			var fill = FindByPath<RectTransform>(m_MainGameStage, "TimeBar/Fill");
			if (fill != null)
			{
				m_TimeBarFill = fill;
				m_TimeBarFillFullSize = fill.sizeDelta;
			}

			m_RoundText = FindByPath<TMP_Text>(m_MainGameStage, "Round/RoundText");

			// バトル用の各コマンド選択ボタン（Push, Attack, Defense, Skill）を再帰検索して取得
			m_PushButton    = FindChild<Button>(m_MainGameStage.transform, "Push");
			m_AttackButton  = FindChild<Button>(m_MainGameStage.transform, "Attack");
			m_DefenseButton = FindChild<Button>(m_MainGameStage.transform, "Defense");
			m_SkillButton   = FindChild<Button>(m_MainGameStage.transform, "Skill");

			if (m_ExecuteText != null) m_ExecuteText.gameObject.SetActive(false);
		}

		private static T FindIn<T>(GameObject root, string name) where T : Component
		{
			if (root == null) return null;
			var t = root.transform.Find(name);
			if (t == null)
			{
				Debug.LogWarning($"[FlowPanel] '{name}' not found under {root.name}");
				return null;
			}
			var c = t.GetComponent<T>();
			if (c == null) Debug.LogWarning($"[FlowPanel] '{name}' has no {typeof(T).Name}");
			return c;
		}

		private static T FindByPath<T>(GameObject root, string path) where T : Component
		{
			if (root == null) return null;
			var t = root.transform.Find(path);
			if (t == null)
			{
				Debug.LogWarning($"[FlowPanel] '{path}' not found under {root.name}");
				return null;
			}
			var c = t.GetComponent<T>();
			if (c == null) Debug.LogWarning($"[FlowPanel] '{path}' has no {typeof(T).Name}");
			return c;
		}

		private void WireButtons()
		{
			if (m_ExchangeSlider != null)
				m_ExchangeSlider.onValueChanged.AddListener(v =>
				{
					int amount = GetExchangeAmount(v);
					if (m_ExchangeAmountText != null)
						m_ExchangeAmountText.text = $"{amount} chips (¥{amount})";
				});

			if (m_ExchangeConfirmButton != null)
				m_ExchangeConfirmButton.onClick.AddListener(() =>
				{
					int amount = m_ExchangeSlider != null ? GetExchangeAmount(m_ExchangeSlider.value) : 0;
					m_PendingExchange = amount;
					m_State.SubmitExchange(amount);
					m_ExchangeConfirmButton.interactable = false;
				});

			if (m_HighRiskButton != null) m_HighRiskButton.onClick.AddListener(() => SubmitBuff(BuffIds.HighRisk));
			if (m_LowRiskButton != null)  m_LowRiskButton.onClick.AddListener(() => SubmitBuff(BuffIds.LowRisk));
			if (m_SkipBuffButton != null) m_SkipBuffButton.onClick.AddListener(() => SubmitBuff(null));

			if (m_MissionOptionButtons != null)
			{
				for (int i = 0; i < m_MissionOptionButtons.Length; i++)
				{
					int index = i;
					if (m_MissionOptionButtons[i] == null) continue;
					m_MissionOptionButtons[i].onClick.AddListener(() =>
					{
						var me = m_State.Me;
						if (me != null && me.AvailableMissions != null && index < me.AvailableMissions.Count)
						{
							m_State.SubmitMission(me.AvailableMissions[index].Id);
							SetActive(m_MissionSelectionPanel, false);
						}
					});
				}
			}
		}

		private void SubmitBuff(string id)
		{
			m_State.SubmitBuff(id);
			if (m_HighRiskButton != null) m_HighRiskButton.interactable = false;
			if (m_LowRiskButton != null)  m_LowRiskButton.interactable = false;
			if (m_SkipBuffButton != null) m_SkipBuffButton.interactable = false;
		}

		private void HandlePhase(EGamePhase phase)
		{
			var me = m_State.Me;
			bool showExchange = (phase == EGamePhase.Exchange) && (me == null || !me.Exchanged);
			SetActive(m_ExchangePanel,  showExchange);
			SetActive(m_GameOverPanel,  phase == EGamePhase.GameOver);

			// ミッションHUDを表示するフェーズの制御（Countdown / Battle 中）
			bool showMissionHUD = (phase == EGamePhase.Countdown || phase == EGamePhase.Battle) && m_State.Me?.Mission != null;
			SetActive(m_MissionPanel, showMissionHUD);
			
			// ミッション表示時はバフパネルを非表示に
			if (showMissionHUD)
			{
				SetActive(m_BuffPanel, false);
				SetActive(m_MissionSelectionPanel, false);
			}

			// 決着パネルは固定秒数だけ表示して自動で隠す（次ラウンドの生成より前に消す）。
			// それ以外のフェーズに入ったら取りこぼし防止で即座に隠す。
			if (phase == EGamePhase.RoundOver)
				ShowRoundOverThenHide();
			else
				HideRoundOverPanel();

			bool stageVisible = phase == EGamePhase.Countdown || phase == EGamePhase.Battle;
			SetActive(m_MainGameStage, stageVisible);

			// チップ交換 / カード選択フェーズだけ制限時間パネルを出してカウントダウンする。
			if (phase == EGamePhase.Exchange || phase == EGamePhase.BuffSelection)
				StartPrepareCountdown();
			else
				StopPrepareCountdown();

			if (phase == EGamePhase.Exchange)
			{
				m_PendingExchange = 0;
				if (m_ExchangeConfirmButton != null) m_ExchangeConfirmButton.interactable = true;
				UpdateExchangeRange();
			}

			if (phase == EGamePhase.BuffSelection)
			{
				int chips = (m_State.Me?.Chips ?? 0) + m_PendingExchange;
				if (m_HighRiskButton != null) m_HighRiskButton.interactable = chips >= 15;
				if (m_LowRiskButton != null)  m_LowRiskButton.interactable  = chips >= 5;
				if (m_SkipBuffButton != null) m_SkipBuffButton.interactable = true;

				// バフ/ミッションパネル表示の更新
				UpdateBuffPanelUI();
				UpdateMissionSelectionUI();
			}

			if (phase == EGamePhase.Countdown)
			{
				m_RoundCount++;
				UpdateRoundText();
				StartCountdown();
				UpdateMissionUI();
				// カウントダウン開始時（ステージ表示時）に、前ラウンドのフェードアウト状態からコマンドボタンを初期状態に戻して表示する
				ResetCommandButtons();
			}
			else
			{
				StopCountdown();
			}

			if (phase == EGamePhase.Battle)
			{
				UpdateBeatVisual();
				UpdateMissionUI();
				// バトル開始時にコマンドボタンの位置・色・アルファ値を初期状態に戻す
				ResetCommandButtons();
			}

			if (phase == EGamePhase.GameOver)
			{
				m_RoundCount = 0;
                StartCoroutine(GoToResultSceneAfterDelay(5f));
            }
		}

		private void HandlePlayersChanged()
		{
			if (m_State == null) return;

			// 左スロット=自分、右スロット=相手で固定（role に依存しない）
			var me       = m_State.Me;
			var opponent = m_State.Opponent;

			ApplyPlayerSlot(me,       m_P1Name, m_P1Money, m_P1Chips, isSelf: true);
			ApplyPlayerSlot(opponent, m_P2Name, m_P2Money, m_P2Chips, isSelf: false);

			UpdateMissionUI();
			UpdateBuffPanelUI();
			UpdateMissionSelectionUI();

			// チップ交換フェーズ中、すでに両替済みの場合は両替パネルを非表示にする
			if (m_State.Phase == EGamePhase.Exchange)
			{
				bool showExchange = me == null || !me.Exchanged;
				SetActive(m_ExchangePanel, showExchange);
			}
		}

		private void UpdateMissionUI()
		{
			var me = m_State.Me;
			bool showMission = (m_State.Phase == EGamePhase.Countdown || m_State.Phase == EGamePhase.Battle) && me?.Mission != null;
			SetActive(m_MissionPanel, showMission);

			if (showMission && me.Mission != null)
			{
				if (m_MissionText != null)
				{
					m_MissionText.text = me.Mission.Description;
					// ミッション達成時は文字色を緑にする
					m_MissionText.color = me.Mission.IsCleared ? new Color(0.18f, 0.9f, 0.3f, 1f) : Color.white;
				}
				if (m_MissionRewardText != null) m_MissionRewardText.text = $"Reward: {me.Mission.RewardType} x{me.Mission.RewardValue}";
				if (m_MissionProgressFill != null)
				{
					float progress = me.Mission.TargetCount > 0 ? (float)me.Mission.CurrentCount / me.Mission.TargetCount : 0f;
					m_MissionProgressFill.fillAmount = Mathf.Clamp01(progress);
				}
			}
		}

		// バフパネルの表示制御（BuffSelection フェーズのみ）
		private void UpdateBuffPanelUI()
		{
			if (m_State == null) return;

			// BuffSelection フェーズ以外ではバフパネルを非表示
			if (m_State.Phase != EGamePhase.BuffSelection)
			{
				SetActive(m_BuffPanel, false);
				return;
			}

			// BuffSelection フェーズ：自分がバフ未選択の時のみ表示
			var me = m_State.Me;
			bool buffSelected = me != null && me.BuffReady;
			bool showBuffPanel = !buffSelected;

			SetActive(m_BuffPanel, showBuffPanel);
		}

		// ミッション選択パネルの表示制御
		private void UpdateMissionSelectionUI()
		{
			var me = m_State.Me;
			// 自分がバフ選択済み、かつミッション未選択の時のみ表示
			bool buffSelected = me != null && me.BuffReady;
			bool showSelection = m_State.Phase == EGamePhase.BuffSelection && 
							 buffSelected &&
								 me != null &&
								 me.AvailableMissions != null &&
								 me.AvailableMissions.Count > 0 &&
								 me.Mission == null;
			SetActive(m_MissionSelectionPanel, showSelection);

			if (showSelection)
			{
				for (int i = 0; i < m_MissionOptionButtons.Length; i++)
				{
					if (m_MissionOptionButtons[i] == null) continue;

					if (i < me.AvailableMissions.Count)
					{
						m_MissionOptionButtons[i].gameObject.SetActive(true);
						var mission = me.AvailableMissions[i];
						if (m_MissionDescriptionTexts != null && i < m_MissionDescriptionTexts.Length && m_MissionDescriptionTexts[i] != null)
							m_MissionDescriptionTexts[i].text = mission.Description;
						if (m_MissionRewardTexts != null && i < m_MissionRewardTexts.Length && m_MissionRewardTexts[i] != null)
							m_MissionRewardTexts[i].text = $"{mission.RewardType} x{mission.RewardValue}";
					}
					else
					{
						m_MissionOptionButtons[i].gameObject.SetActive(false);
					}
				}
			}
		}

		private void ApplyPlayerSlot(PlayerDto dto, TMP_Text nameText, TMP_Text moneyText, TMP_Text chipsText, bool isSelf)
		{
			if (dto == null)
			{
				if (nameText != null)  nameText.text = "-";
				if (moneyText != null) moneyText.text = "-";
				if (chipsText != null) chipsText.text = "-";
				return;
			}

			// 色は role 固定（ワールドのプレイヤー色と一致させる）。スロット位置は self/opponent。
			Color slotColor = dto.Role == "P2" ? m_P2SlotColor : m_P1SlotColor;

			if (nameText != null)
			{
				nameText.text = dto.IsAI ? $"{dto.Role} (AI)" : dto.Role;
				nameText.color = slotColor;
			}
			if (moneyText != null)
			{
				moneyText.text = $"¥{dto.Money:N0}";
				moneyText.color = slotColor;
			}
			if (chipsText != null)
			{
				var me = m_State.Me;
				bool canSeeChips = isSelf || (me != null && me.ScammerActive);
				chipsText.text = canSeeChips ? dto.Chips.ToString() : "??";
				chipsText.color = slotColor;
			}
		}

		private void HandleBeatChanged()
		{
			UpdateBeatVisual();
			UpdateTimeBar();

			if (m_State == null)
			{
				return;
			}

			// 4拍目（実行拍）での処理
			if (m_State.CurrentBeat == 4)
			{
				FlashExecuteText();

				// バトル中であれば選択されているコマンドの確定アニメーションを再生
				if (m_State.Phase == EGamePhase.Battle)
				{
					var activeIntentType = LocalIntentBus.Current.Mode;
					PlayCommandSelectionAnimation(activeIntentType);
				}
			}

			// 1拍目（入力開始拍）での処理
			if (m_State.CurrentBeat == 1)
			{
				// バトル中であればコマンドボタンの表示を初期状態にリセット
				if (m_State.Phase == EGamePhase.Battle)
				{
					ResetCommandButtons();
				}
			}
		}

		private void UpdateBeatVisual()
		{
			bool battle = m_State != null && m_State.Phase == EGamePhase.Battle;
			int beat = battle ? m_State.CurrentBeat : 0;

			if (m_NormalBeats != null)
			{
				for (int i = 0; i < m_NormalBeats.Length; i++)
				{
					if (m_NormalBeats[i] == null) continue;
					bool on = battle && beat == i + 1;
					m_NormalBeats[i].color = on ? m_BeatOnColor : m_BeatOffColor;
				}
			}
			if (m_FinalBeat != null)
				m_FinalBeat.color = (battle && beat == 4) ? m_FinalBeatOnColor : m_BeatOffColor;
		}

		private void UpdateTimeBar()
		{
			if (m_TimeBarFill == null) return;
			if (m_State.Phase != EGamePhase.Battle)
			{
				m_TimeBarFill.localScale = Vector3.one;
				return;
			}

			int total = GamblingAction.Core.GameConfig.GameDurationSec;
			float t = total > 0 ? Mathf.Clamp01((float)m_State.TimeLeft / total) : 0f;
			m_TimeBarFill.localScale = new Vector3(t, 1f, 1f);
		}

		private void FlashExecuteText()
		{
			if (m_ExecuteText == null) return;
			if (m_ExecuteFlashCo != null) StopCoroutine(m_ExecuteFlashCo);
			m_ExecuteFlashCo = StartCoroutine(ExecuteFlash());
		}

		private IEnumerator ExecuteFlash()
		{
			m_ExecuteText.text = "EXECUTE!";
			m_ExecuteText.gameObject.SetActive(true);
			yield return new WaitForSeconds(m_ExecuteFlashSeconds);
			m_ExecuteText.gameObject.SetActive(false);
			m_ExecuteFlashCo = null;
		}

		// 決着パネルを表示し、固定秒数後に自動で隠す。
		private void ShowRoundOverThenHide()
		{
			SetActive(m_RoundOverPanel, true);
			if (m_RoundOverHideCo != null) StopCoroutine(m_RoundOverHideCo);
			m_RoundOverHideCo = StartCoroutine(HideRoundOverAfterDelay());
		}

		private IEnumerator HideRoundOverAfterDelay()
		{
			yield return new WaitForSeconds(m_RoundOverDisplaySeconds);
			SetActive(m_RoundOverPanel, false);
			m_RoundOverHideCo = null;
		}

		private void HideRoundOverPanel()
		{
			if (m_RoundOverHideCo != null)
			{
				StopCoroutine(m_RoundOverHideCo);
				m_RoundOverHideCo = null;
			}
			SetActive(m_RoundOverPanel, false);
		}

		// チップ交換 / カード選択フェーズの制限時間カウントダウンを開始する。
		// サーバ側の制限時間と同じ秒数をクライアントでも独立に数えて表示する（表示専用）。
		private void StartPrepareCountdown()
		{
			if (m_PreparingCountdownPanel == null) return;
			SetActive(m_PreparingCountdownPanel, true);
			if (m_PrepareCountdownCo != null) StopCoroutine(m_PrepareCountdownCo);
			m_PrepareCountdownCo = StartCoroutine(PrepareCountdownSequence());
		}

		private void StopPrepareCountdown()
		{
			if (m_PrepareCountdownCo != null)
			{
				StopCoroutine(m_PrepareCountdownCo);
				m_PrepareCountdownCo = null;
			}
			SetActive(m_PreparingCountdownPanel, false);
		}

		private IEnumerator PrepareCountdownSequence()
		{
			if (m_PrepareSeconds <= 0f)
			{
				if (m_PrepareTimebar != null) m_PrepareTimebar.fillAmount = 0f;
				if (m_PrepareTimeText != null) m_PrepareTimeText.text = "0";
				SetActive(m_PreparingCountdownPanel, false);
				m_PrepareCountdownCo = null;
				yield break;
			}

			float remaining = m_PrepareSeconds;
			while (remaining > 0f)
			{
				if (m_PrepareTimebar != null) m_PrepareTimebar.fillAmount = remaining / m_PrepareSeconds;
				if (m_PrepareTimeText != null) m_PrepareTimeText.text = Mathf.CeilToInt(remaining).ToString();
				remaining -= Time.deltaTime;
				yield return null;
			}
			if (m_PrepareTimebar != null) m_PrepareTimebar.fillAmount = 0f;
			if (m_PrepareTimeText != null) m_PrepareTimeText.text = "0";
			m_PrepareCountdownCo = null;
		}

		private void StartCountdown()
		{
			if (m_ReadyText == null && m_CountdownText == null) return;
			StopCountdown();
			m_CountdownCo = StartCoroutine(CountdownSequence());
		}

		private void StopCountdown()
		{
			if (m_CountdownCo != null)
			{
				StopCoroutine(m_CountdownCo);
				m_CountdownCo = null;
			}
			if (m_ReadyText != null)     m_ReadyText.gameObject.SetActive(false);
			if (m_CountdownText != null) m_CountdownText.gameObject.SetActive(false);
		}

		private IEnumerator CountdownSequence()
		{
			if (m_ReadyText != null)
			{
				m_ReadyText.gameObject.SetActive(true);
				m_ReadyText.text = "READY?";
			}
			if (m_CountdownText != null)
			{
				m_CountdownText.gameObject.SetActive(true);
				m_CountdownText.text = "";
			}

			yield return new WaitForSeconds(0.8f);

			for (int i = 3; i >= 1; i--)
			{
				if (m_CountdownText != null) m_CountdownText.text = i.ToString();
				yield return new WaitForSeconds(0.8f);
			}

			if (m_ReadyText != null) m_ReadyText.text = "";
			if (m_CountdownText != null) m_CountdownText.text = "GO!";
			yield return new WaitForSeconds(0.6f);

			if (m_ReadyText != null)     m_ReadyText.gameObject.SetActive(false);
			if (m_CountdownText != null) m_CountdownText.gameObject.SetActive(false);
			m_CountdownCo = null;
		}

        private IEnumerator GoToResultSceneAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene("ResultScene");
        }

        private void UpdateRoundText()
		{
			if (m_RoundText == null) return;
			int shown = Mathf.Clamp(m_RoundCount, 1, m_TotalRounds);
			m_RoundText.text = $"Round {shown}/{m_TotalRounds}";
		}

		/// <summary>
		/// ローカルプレイヤーのインテント変更イベントを受け取り、選択状態の背景色（倍率による3段階）およびボタンの位置を更新します。
		/// </summary>
		private void HandleLocalIntentChanged()
		{
			// 確定アニメーションが再生中の場合は背景色や位置の更新を行わない
			if (m_IsAnimatingCommandSelection)
			{
				return;
			}

			// 現在選択されているインテントタイプ（Push, Defense など）と倍率（Power）を取得
			var activeIntentType = LocalIntentBus.Current.Mode;
			var power = LocalIntentBus.Current.Power;
			UpdateCommandButtonStates(activeIntentType, power);
		}

		/// <summary>
		/// 指定されたインテントタイプとパワー値に基づいて、コマンドボタンのハイライト色（黄/橙/赤の3段階）およびY座標の位置（選択中は少し上げる）を更新します。
		/// </summary>
		/// <param name="activeIntentType">現在アクティブなインテントの種類</param>
		/// <param name="power">現在の入力倍率（1〜3）</param>
		private void UpdateCommandButtonStates(string activeIntentType, int power)
		{
			if (m_CommandButtons == null)
			{
				return;
			}

			// 選択状態にかかわらず、位置の個別制御が必要になるため
			// レイアウトグループを一時的に無効にします（ResetCommandButtons() 呼び出し時に再度有効化されます）
			if (m_CommandLayoutGroup != null)
			{
				m_CommandLayoutGroup.enabled = false;
			}

			// パワー値に応じたハイライト色を決定
			Color highlightColor = m_SelectedColorPower1;
			if (power == 2)
			{
				highlightColor = m_SelectedColorPower2;
			}
			else if (power >= 3)
			{
				highlightColor = m_SelectedColorPower3;
			}

			for (int i = 0; i < m_CommandButtons.Length; i++)
			{
				var btn = m_CommandButtons[i];
				if (btn == null)
				{
					continue;
				}

				var img = btn.GetComponent<Image>();
				var rect = btn.GetComponent<RectTransform>();

				// ボタンに対応するインテントタイプが選択されているか判定
				bool isSelected = false;
				if (btn == m_PushButton && activeIntentType == IntentTypes.Push)
				{
					isSelected = true;
				}
				else if (btn == m_AttackButton && activeIntentType == IntentTypes.Attack)
				{
					isSelected = true;
				}
				else if (btn == m_DefenseButton && activeIntentType == IntentTypes.Defense)
				{
					isSelected = true;
				}
				else if (btn == m_SkillButton && activeIntentType == IntentTypes.Skill)
				{
					isSelected = true;
				}

				// 色の適用
				if (img != null)
				{
					img.color = isSelected ? highlightColor : m_CommandButtonsDefaultColors[i];
				}

				// 位置の適用（選択されているボタンは少し上にスライド、非選択は元の位置に戻す）
				if (rect != null)
				{
					float targetY = isSelected
						? m_CommandButtonsDefaultPositions[i].y + 20f
						: m_CommandButtonsDefaultPositions[i].y;

					if (isSelected)
					{
						// 同一のコマンド内で強度が変化した際のアニメーション演出
						if (activeIntentType == m_PrevIntentType && power != m_PrevPower)
						{
							rect.DOKill();
							// 強度が変化した場合：下から上がる演出（一度デフォルト位置にスナップさせてから浮かせる）
							rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, m_CommandButtonsDefaultPositions[i].y);
						}
					}

					SlideButtonY(rect, targetY, 0.2f);
				}
			}

			// 前回のインテントと強度を保存
			m_PrevIntentType = activeIntentType;
			m_PrevPower = power;
		}

		private int GetExchangeAmount(float sliderValue)
		{
			var me = m_State.Me;
			if (me == null) return 0;
			int max = Mathf.Min(20000, me.Money);
			if (max <= 0) return 0;

			int maxSteps = Mathf.CeilToInt((float)max / 200f);
			int step = Mathf.RoundToInt(sliderValue);

			if (step >= maxSteps)
			{
				return max; // 最右端は所持金全て（オールイン）
			}
			return step * 200;
		}

		private void UpdateExchangeRange()
		{
			if (m_ExchangeSlider == null) return;
			var me = m_State.Me;
			int max = me != null ? Mathf.Min(20000, me.Money) : 0;
			int maxSteps = Mathf.CeilToInt((float)max / 200f);

			m_ExchangeSlider.minValue = 0;
			m_ExchangeSlider.maxValue = maxSteps;
			m_ExchangeSlider.wholeNumbers = true; // 整数値にスナップさせる

			if (m_ExchangeSlider.value > maxSteps) m_ExchangeSlider.value = maxSteps;

			int currentAmount = GetExchangeAmount(m_ExchangeSlider.value);
			if (m_ExchangeAmountText != null)
				m_ExchangeAmountText.text = $"{currentAmount} chips (¥{currentAmount})";
		}

		/// <summary>
		/// バトルコマンドボタンの初期状態（ anchoredPosition および背景色）をキャッシュします。
		/// </summary>
		private void InitializeCommandButtonsCache()
		{
			// コマンドボタンの親オブジェクトにアタッチされているレイアウトグループを取得
			if (m_PushButton != null && m_PushButton.transform.parent != null)
			{
				m_CommandLayoutGroup = m_PushButton.transform.parent.GetComponent<HorizontalLayoutGroup>();
			}

			// Startのフレームではレイアウトグループ（HorizontalLayoutGroup）による整列計算が完了していない場合があるため、
			// レイアウトを強制的に即時再構築して、正しい整列状態の座標を確定させてキャッシュします
			if (m_CommandLayoutGroup != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(m_CommandLayoutGroup.GetComponent<RectTransform>());
			}

			m_CommandButtons = new Button[]
			{
				m_PushButton,
				m_AttackButton,
				m_DefenseButton,
				m_SkillButton
			};

			m_CommandButtonsDefaultPositions = new Vector2[m_CommandButtons.Length];
			m_CommandButtonsDefaultColors = new Color[m_CommandButtons.Length];

			for (int i = 0; i < m_CommandButtons.Length; i++)
			{
				var btn = m_CommandButtons[i];
				if (btn == null)
				{
					continue;
				}

				var rect = btn.GetComponent<RectTransform>();
				if (rect != null)
				{
					m_CommandButtonsDefaultPositions[i] = rect.anchoredPosition;
				}

				var img = btn.GetComponent<Image>();
				if (img != null)
				{
					// シーン初期状態でボタンが透明化されている場合を考慮し、アルファ値を 1.0f (不透明) に補正してキャッシュ
					var defaultColor = img.color;
					defaultColor.a = 1f;
					m_CommandButtonsDefaultColors[i] = defaultColor;
				}
			}
		}
		/// <summary>
		/// 4拍目（実行拍）になった瞬間に呼び出され、決定したコマンドのボタンを上にスライドフェードアウトし、他をその場でフェードアウトさせます。
		/// </summary>
		/// <param name="activeIntentType">決定されたインテントの種類</param>
		private void PlayCommandSelectionAnimation(string activeIntentType)
		{
			if (m_IsAnimatingCommandSelection || m_CommandButtons == null)
			{
				return;
			}

			m_IsAnimatingCommandSelection = true;

			// アニメーションによる位置の移動を許可するため、レイアウトグループの自動整列を一時的に無効化
			if (m_CommandLayoutGroup != null)
			{
				m_CommandLayoutGroup.enabled = false;
			}

			// 減衰速度を調整（選択されたボタン: 0.8秒、非選択のボタン: 0.5秒）
			float selectedDuration = 0.8f;
			float unselectedDuration = 0.5f;

			for (int i = 0; i < m_CommandButtons.Length; i++)
			{
				var btn = m_CommandButtons[i];
				if (btn == null)
				{
					continue;
				}

				// アニメーション中はクリックを無効化
				btn.interactable = false;

				var rectTransform = btn.GetComponent<RectTransform>();
				// 配下にあるすべての Image (アイコン含む) と TMP_Text (操作案内等含む) を取得
				var images = btn.GetComponentsInChildren<Image>(true);
				var texts = btn.GetComponentsInChildren<TMP_Text>(true);

				// ボタンに対応するインテントタイプが選択されているか判定
				bool isSelected = false;
				if (btn == m_PushButton && activeIntentType == IntentTypes.Push)
				{
					isSelected = true;
				}
				else if (btn == m_AttackButton && activeIntentType == IntentTypes.Attack)
				{
					isSelected = true;
				}
				else if (btn == m_DefenseButton && activeIntentType == IntentTypes.Defense)
				{
					isSelected = true;
				}
				else if (btn == m_SkillButton && activeIntentType == IntentTypes.Skill)
				{
					isSelected = true;
				}

				if (isSelected)
				{
					// 選択されたボタン：上に移動しながらフェードアウト
					if (images != null)
					{
						for (int j = 0; j < images.Length; j++)
						{
							FadeImage(images[j], 0f, selectedDuration);
						}
					}
					if (texts != null)
					{
						for (int j = 0; j < texts.Length; j++)
						{
							FadeText(texts[j], 0f, selectedDuration);
						}
					}
					if (rectTransform != null)
					{
						SlideButtonY(rectTransform, m_CommandButtonsDefaultPositions[i].y + 50f, selectedDuration);
					}
				}
				else
				{
					// 選択されなかったボタン：その場でフェードアウト
					if (images != null)
					{
						for (int j = 0; j < images.Length; j++)
						{
							FadeImage(images[j], 0f, unselectedDuration);
						}
					}
					if (texts != null)
					{
						for (int j = 0; j < texts.Length; j++)
						{
							FadeText(texts[j], 0f, unselectedDuration);
						}
					}
				}
			}
		}

		/// <summary>
		/// 1拍目（入力開始拍）になった瞬間に呼び出され、コマンドボタンの座標・アルファ値・背景色を初期状態にリセットします。
		/// </summary>
		private void ResetCommandButtons()
		{
			// 実行中のすべてのコマンドフェードやスライドのアニメーションを確実に Kill して停止する
			for (int i = 0; i < m_ActiveTweens.Count; i++)
			{
				if (m_ActiveTweens[i] != null && m_ActiveTweens[i].IsActive())
				{
					m_ActiveTweens[i].Kill();
				}
			}
			m_ActiveTweens.Clear();

			if (m_CommandButtons == null)
			{
				return;
			}

			m_IsAnimatingCommandSelection = false;

			m_PrevIntentType = null;
			m_PrevPower = 1;

			for (int i = 0; i < m_CommandButtons.Length; i++)
			{
				var btn = m_CommandButtons[i];
				if (btn == null)
				{
					continue;
				}

				// 位置の復元とアニメーション停止
				var rect = btn.GetComponent<RectTransform>();
				if (rect != null)
				{
					rect.DOKill();
					rect.anchoredPosition = m_CommandButtonsDefaultPositions[i];
				}

				// 配下にあるすべての Image (本体背景およびアイコン) の透明度と色を復元
				var images = btn.GetComponentsInChildren<Image>(true);
				if (images != null)
				{
					for (int j = 0; j < images.Length; j++)
					{
						var img = images[j];
						if (img == null)
						{
							continue;
						}
						img.DOKill();

						// ボタン本体の背景画像（一番最初のImage）はキャッシュされた初期色に戻す
						// 子オブジェクトの画像（アイコンなど）は元のアルファ値 1 に戻す
						if (img == btn.GetComponent<Image>())
						{
							img.color = m_CommandButtonsDefaultColors[i];
						}
						else
						{
							img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
						}
					}
				}

				// 配下にあるすべての TMP_Text の透明度を復元
				var texts = btn.GetComponentsInChildren<TMP_Text>(true);
				if (texts != null)
				{
					for (int j = 0; j < texts.Length; j++)
					{
						var txt = texts[j];
						if (txt == null)
						{
							continue;
						}
						txt.DOKill();
						txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 1f);
					}
				}

				// インタラクティブ状態を復元（Battleフェーズでの入力許可）
				btn.interactable = true;
			}

			// レイアウトグループを再び有効化し、ボタンを元の並びに自動整列させます
			if (m_CommandLayoutGroup != null)
			{
				m_CommandLayoutGroup.enabled = true;
			}
		}

		/// <summary>
		/// 指定した Image のアルファ値を DOTween.To でフェードアニメーションさせます。
		/// </summary>
		/// <param name="image">対象のImageコンポーネント</param>
		/// <param name="targetAlpha">目標のアルファ値</param>
		/// <param name="duration">アニメーションの時間（秒）</param>
		private void FadeImage(Image image, float targetAlpha, float duration)
		{
			if (image == null)
			{
				return;
			}

			// 既存の同対象へのアニメーションがあれば事前に停止
			image.DOKill();

			var t = DOTween.To(() => image.color.a, a =>
			{
				var c = image.color;
				image.color = new Color(c.r, c.g, c.b, a);
			}, targetAlpha, duration)
				.SetTarget(image)
				.SetEase(Ease.OutQuad);

			m_ActiveTweens.Add(t);
		}

		/// <summary>
		/// 指定した TMP_Text のアルファ値を DOTween.To でフェードアニメーションさせます。
		/// </summary>
		/// <param name="text">対象のTMP_Textコンポーネント</param>
		/// <param name="targetAlpha">目標のアルファ値</param>
		/// <param name="duration">アニメーションの時間（秒）</param>
		private void FadeText(TMP_Text text, float targetAlpha, float duration)
		{
			if (text == null)
			{
				return;
			}

			// 既存の同対象へのアニメーションがあれば事前に停止
			text.DOKill();

			var t = DOTween.To(() => text.color.a, a =>
			{
				var c = text.color;
				text.color = new Color(c.r, c.g, c.b, a);
			}, targetAlpha, duration)
				.SetTarget(text)
				.SetEase(Ease.OutQuad);

			m_ActiveTweens.Add(t);
		}

		/// <summary>
		/// 指定した RectTransform の anchoredPosition.y を DOTween.To でアニメーションさせます。
		/// </summary>
		/// <param name="rect">対象のRectTransform</param>
		/// <param name="targetY">目標の anchoredPosition.y</param>
		/// <param name="duration">アニメーションの時間（秒）</param>
		private void SlideButtonY(RectTransform rect, float targetY, float duration)
		{
			if (rect == null)
			{
				return;
			}

			// 既存の同対象へのアニメーションがあれば事前に停止
			rect.DOKill();

			var t = DOTween.To(() => rect.anchoredPosition.y, y =>
			{
				rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
			}, targetY, duration)
				.SetTarget(rect)
				.SetEase(Ease.OutQuad);

			m_ActiveTweens.Add(t);
		}

		private static void SetActive(GameObject go, bool active)
		{
			if (go != null && go.activeSelf != active) go.SetActive(active);
		}

		/// <summary>
		/// 指定した親オブジェクトの配下から、非アクティブオブジェクトも含めて名前で子コンポーネントを再帰的に検索します。
		/// </summary>
		/// <typeparam name="T">検索するコンポーネントの型</typeparam>
		/// <param name="root">検索の起点となるTransform</param>
		/// <param name="name">検索対象のゲームオブジェクト名</param>
		/// <returns>見つかったコンポーネント。見つからない場合は null</returns>
		private static T FindChild<T>(Transform root, string name) where T : Component
		{
			if (root == null)
			{
				return null;
			}

			// 直下の子から名前が一致するものを取得
			var direct = root.Find(name);
			if (direct != null)
			{
				var c = direct.GetComponent<T>();
				if (c != null)
				{
					return c;
				}
			}

			// 見つからない場合は非アクティブオブジェクトも含めて配下全体から再帰的に取得
			var all = root.GetComponentsInChildren<T>(true);
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i].name == name)
				{
					return all[i];
				}
			}

			return null;
		}
	}
}
