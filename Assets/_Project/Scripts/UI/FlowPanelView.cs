using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

		[Header("Player slot colors")]
		[SerializeField, Tooltip("P1 の文字色（role 固定。ワールドの P1 と同じ色）")]
		// GameConfig.P1Color（#00f2fe）に合わせる
		private Color m_P1SlotColor = new Color(0f, 242f / 255f, 254f / 255f, 1f);
		[SerializeField, Tooltip("P2 の文字色（role 固定。ワールドの P2 と同じ色）")]
		// GameConfig.P2Color（#ff4444）に合わせる
		private Color m_P2SlotColor = new Color(1f, 68f / 255f, 68f / 255f, 1f);

		[Header("Controller UI チューニング")]
		[Tooltip("左スティックを最大に傾けたときのスライダー変化速度（chips/秒）")]
		[SerializeField] private float m_SliderSpeed = 8f;

		// ─────────────────────────────────────────────────────────────
		// 定数（コントローラー UI 用）
		// ─────────────────────────────────────────────────────────────

		/// <summary>スティック入力のデッドゾーン（これ未満は無入力と見なす）</summary>
		private const float m_StickDeadZone = 0.3f;

		/// <summary>バフボタン選択移動のクールダウン（秒）。連続入力チカチカ防止用</summary>
		private const float m_NavCooldown = 0.2f;

		// ─────────────────────────────────────────────────────────────
		// 既存 UI 参照（変更なし）
		// ─────────────────────────────────────────────────────────────

		private IGameState m_State;

		private Slider   m_ExchangeSlider;
		private TMP_Text m_ExchangeAmountText;
		private Button   m_ExchangeConfirmButton;
		private Button   m_HighRiskButton;
		private Button   m_LowRiskButton;
		private Button   m_SkipBuffButton;

		private TMP_Text    m_P1Name, m_P1Money, m_P1Chips;
		private TMP_Text    m_P2Name, m_P2Money;
		private Image[]     m_NormalBeats;
		private Image       m_FinalBeat;
		private TMP_Text    m_ExecuteText;
		private TMP_Text    m_ReadyText, m_CountdownText;
		private RectTransform m_TimeBarFill;
		private TMP_Text    m_RoundText;

		private Image    m_PrepareTimebar;
		private TMP_Text m_PrepareTimeText;

		private int       m_RoundCount;
		private Coroutine m_CountdownCo;
		private Coroutine m_ExecuteFlashCo;
		private Coroutine m_RoundOverHideCo;
		private Coroutine m_PrepareCountdownCo;
		private Vector2   m_TimeBarFillFullSize;

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 用フィールド（追加）
		// ─────────────────────────────────────────────────────────────

		/// <summary>左スティック + 十字キーを統合した Vector2 アクション</summary>
		private InputAction m_NavigateAction;

		/// <summary>B ボタン（buttonEast）による決定アクション</summary>
		private InputAction m_ConfirmUiAction;

		/// <summary>
		/// バフ選択ボタン配列。
		/// [0]=HighRiskButton / [1]=LowRiskButton / [2]=SkipBuffButton の順。
		/// FindFlowControls 後に構築する。
		/// </summary>
		private Button[] m_BuffButtons;

		/// <summary>バフ選択フェーズで現在フォーカスしているボタンのインデックス</summary>
		private int m_SelectedBuffIndex;

		/// <summary>バフ選択移動のクールダウン残り時間（秒）</summary>
		private float m_NavCooldownRemaining;

		// ─────────────────────────────────────────────────────────────
		// ライフサイクル
		// ─────────────────────────────────────────────────────────────

		private void Awake()
		{
			BuildUiActions();
			RegisterUiCallbacks();
		}

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
			WireButtons();

			// FindFlowControls でボタン参照が揃ってからバッファを構築する
			m_BuffButtons = new[] { m_HighRiskButton, m_LowRiskButton, m_SkipBuffButton };

			m_State.OnPhaseChanged   += HandlePhase;
			m_State.OnPlayersChanged += HandlePlayersChanged;
			m_State.OnPlayersChanged += UpdateExchangeRange;
			m_State.OnBeatChanged    += HandleBeatChanged;

			HandlePhase(m_State.Phase);
			HandlePlayersChanged();
			UpdateExchangeRange();
			UpdateBeatVisual();

			EnableUiActions();
		}

		private void OnDestroy()
		{
			if (m_State == null) return;
			m_State.OnPhaseChanged   -= HandlePhase;
			m_State.OnPlayersChanged -= HandlePlayersChanged;
			m_State.OnPlayersChanged -= UpdateExchangeRange;
			m_State.OnBeatChanged    -= HandleBeatChanged;

			DisableUiActions();
			DisposeUiActions();
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：InputAction 構築
		// ─────────────────────────────────────────────────────────────

		private void BuildUiActions()
		{
			// 左スティックと十字キーの両方を 1 つの Vector2 Action に統合する
			m_NavigateAction = new InputAction("UINavigate", InputActionType.Value, expectedControlType: "Vector2");
			m_NavigateAction.AddBinding("<Gamepad>/leftStick");
			m_NavigateAction.AddCompositeBinding("Dpad")
				.With("Up",    "<Gamepad>/dpad/up")
				.With("Down",  "<Gamepad>/dpad/down")
				.With("Left",  "<Gamepad>/dpad/left")
				.With("Right", "<Gamepad>/dpad/right");

			// B ボタン（buttonEast）で決定する
			// Xbox = B / Switch Pro = A（右側ボタン）
			m_ConfirmUiAction = new InputAction("UIConfirm", InputActionType.Button);
			m_ConfirmUiAction.AddBinding("<Gamepad>/buttonEast");
		}

		private void RegisterUiCallbacks()
		{
			// B ボタン押下時に現在フェーズに応じた決定処理を行う
			m_ConfirmUiAction.performed += _ => OnConfirmUi();
		}

		private void EnableUiActions()
		{
			m_NavigateAction.Enable();
			m_ConfirmUiAction.Enable();
		}

		private void DisableUiActions()
		{
			m_NavigateAction?.Disable();
			m_ConfirmUiAction?.Disable();
		}

		private void DisposeUiActions()
		{
			m_NavigateAction?.Dispose();
			m_ConfirmUiAction?.Dispose();
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：毎フレーム処理
		// ─────────────────────────────────────────────────────────────

		private void Update()
		{
			if (m_State == null) return;

			m_NavCooldownRemaining -= Time.deltaTime;

			Vector2 nav = m_NavigateAction.ReadValue<Vector2>();

			switch (m_State.Phase)
			{
				case EGamePhase.Exchange:
					HandleSliderInput(nav.x);
					break;

				case EGamePhase.BuffSelection:
					HandleBuffNavigation(nav.x);
					break;
			}
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：換金スライダー操作
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// 左スティック左右 / 十字キー左右でスライダー値を連続増減する。
		/// スティックの傾き量に比例して変化速度が変わる。
		/// 決定済み（ConfirmButton が非アクティブ）の場合は操作不可。
		/// </summary>
		private void HandleSliderInput(float axisX)
		{
			if (m_ExchangeSlider == null) return;
			if (m_ExchangeConfirmButton != null && !m_ExchangeConfirmButton.interactable) return;
			if (Mathf.Abs(axisX) < m_StickDeadZone) return;

			float delta = axisX * m_SliderSpeed * Time.deltaTime;
			m_ExchangeSlider.value = Mathf.Clamp(
				m_ExchangeSlider.value + delta,
				m_ExchangeSlider.minValue,
				m_ExchangeSlider.maxValue
			);
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：バフ選択ナビゲーション
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// 左スティック左右 / 十字キー左右でバフボタンの選択カーソルを移動する。
		/// クールダウンで連続入力時のチカチカを防止する。
		/// </summary>
		private void HandleBuffNavigation(float axisX)
		{
			if (m_NavCooldownRemaining > 0f) return;
			if (Mathf.Abs(axisX) < m_StickDeadZone) return;

			int direction = axisX > 0 ? 1 : -1;
			int next = Mathf.Clamp(m_SelectedBuffIndex + direction, 0, m_BuffButtons.Length - 1);
			if (next == m_SelectedBuffIndex) return;

			m_SelectedBuffIndex    = next;
			m_NavCooldownRemaining = m_NavCooldown;
			FocusBuffButton(m_SelectedBuffIndex);
		}

		/// <summary>
		/// 指定インデックスのバフボタンに EventSystem のフォーカスを移す。
		/// Unity の Button の選択時ハイライトが自動で適用される。
		/// </summary>
		private void FocusBuffButton(int index)
		{
			if (m_BuffButtons == null) return;
			if (index < 0 || index >= m_BuffButtons.Length) return;

			var button = m_BuffButtons[index];
			if (button == null) return;

			if (EventSystem.current != null)
				EventSystem.current.SetSelectedGameObject(button.gameObject);
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：B ボタン決定
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// B ボタン押下時に現在のフェーズに応じた決定処理を行う。
		/// </summary>
		private void OnConfirmUi()
		{
			if (m_State == null) return;

			switch (m_State.Phase)
			{
				case EGamePhase.Exchange:
					ConfirmExchange();
					break;

				case EGamePhase.BuffSelection:
					ConfirmBuffSelection();
					break;
			}
		}

		/// <summary>換金量を確定して ExchangeConfirmButton を押す。決定済みの場合は無視。</summary>
		private void ConfirmExchange()
		{
			if (m_ExchangeConfirmButton == null) return;
			if (!m_ExchangeConfirmButton.interactable) return;
			m_ExchangeConfirmButton.onClick.Invoke();
		}

		/// <summary>現在フォーカス中のバフボタンを押す。非アクティブの場合は無視。</summary>
		private void ConfirmBuffSelection()
		{
			if (m_BuffButtons == null) return;
			if (m_SelectedBuffIndex < 0 || m_SelectedBuffIndex >= m_BuffButtons.Length) return;

			var button = m_BuffButtons[m_SelectedBuffIndex];
			if (button == null) return;
			if (!button.interactable) return;

			button.onClick.Invoke();
		}

		// ─────────────────────────────────────────────────────────────
		// 既存メソッド群（変更なし）
		// ─────────────────────────────────────────────────────────────

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

		private void FindStageControls()
		{
			if (m_MainGameStage == null) return;

			m_P1Name  = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P1/Name");
			m_P1Money = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P1/Money");
			m_P1Chips = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P1/Chips");
			m_P2Name  = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P2/Name");
			m_P2Money = FindByPath<TMP_Text>(m_MainGameStage, "PlayerStatus/P2/Money");

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
					if (m_ExchangeAmountText != null)
						m_ExchangeAmountText.text = $"{(int)v} chips (¥{(int)v * 100})";
				});

			if (m_ExchangeConfirmButton != null)
				m_ExchangeConfirmButton.onClick.AddListener(() =>
				{
					int amount = m_ExchangeSlider != null ? (int)m_ExchangeSlider.value : 0;
					m_State.SubmitExchange(amount);
					m_ExchangeConfirmButton.interactable = false;
				});

			if (m_HighRiskButton != null) m_HighRiskButton.onClick.AddListener(() => SubmitBuff(BuffIds.HighRisk));
			if (m_LowRiskButton != null)  m_LowRiskButton.onClick.AddListener(() => SubmitBuff(BuffIds.LowRisk));
			if (m_SkipBuffButton != null) m_SkipBuffButton.onClick.AddListener(() => SubmitBuff(null));
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
			SetActive(m_ExchangePanel,  phase == EGamePhase.Exchange);
			SetActive(m_BuffPanel,      phase == EGamePhase.BuffSelection);
			SetActive(m_GameOverPanel,  phase == EGamePhase.GameOver);

			// 決着パネルは固定秒数だけ表示して自動で隠す
			if (phase == EGamePhase.RoundOver)
				ShowRoundOverThenHide();
			else
				HideRoundOverPanel();

			bool stageVisible = phase == EGamePhase.Countdown || phase == EGamePhase.Battle;
			SetActive(m_MainGameStage, stageVisible);

			if (phase == EGamePhase.Exchange || phase == EGamePhase.BuffSelection)
				StartPrepareCountdown();
			else
				StopPrepareCountdown();

			if (phase == EGamePhase.Exchange)
			{
				if (m_ExchangeConfirmButton != null) m_ExchangeConfirmButton.interactable = true;
				UpdateExchangeRange();
			}

			if (phase == EGamePhase.BuffSelection)
			{
				int chips = m_State.Me?.Chips ?? 0;
				if (m_HighRiskButton != null) m_HighRiskButton.interactable = chips >= 15;
				if (m_LowRiskButton != null)  m_LowRiskButton.interactable  = chips >= 5;
				if (m_SkipBuffButton != null) m_SkipBuffButton.interactable = true;

				// バフ選択フェーズ開始時に先頭ボタンにフォーカスを移す（コントローラー用）
				m_SelectedBuffIndex    = 0;
				m_NavCooldownRemaining = 0f;
				FocusBuffButton(m_SelectedBuffIndex);
			}

			if (phase == EGamePhase.Countdown)
			{
				m_RoundCount++;
				UpdateRoundText();
				StartCountdown();
			}
			else
			{
				StopCountdown();
			}

			if (phase == EGamePhase.Battle)
				UpdateBeatVisual();

			if (phase == EGamePhase.GameOver)
			{
				m_RoundCount = 0;
				StartCoroutine(GoToResultSceneAfterDelay(5f));
			}
		}

		private void HandlePlayersChanged()
		{
			if (m_State == null) return;

			var me       = m_State.Me;
			var opponent = m_State.Opponent;

			ApplyPlayerSlot(me,       m_P1Name, m_P1Money, m_P1Chips, isSelf: true);
			ApplyPlayerSlot(opponent, m_P2Name, m_P2Money, null,      isSelf: false);
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
				chipsText.text = isSelf ? dto.Chips.ToString() : "??";
				chipsText.color = slotColor;
			}
		}

		private void HandleBeatChanged()
		{
			UpdateBeatVisual();
			UpdateTimeBar();

			if (m_State.CurrentBeat == 4)
				FlashExecuteText();
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

		private void UpdateExchangeRange()
		{
			if (m_ExchangeSlider == null) return;
			var me = m_State.Me;
			int max = me != null ? Mathf.Min(100, me.Money / 100) : 0;
			m_ExchangeSlider.minValue = 0;
			m_ExchangeSlider.maxValue = max;
			if (m_ExchangeSlider.value > max) m_ExchangeSlider.value = max;
			if (m_ExchangeAmountText != null)
				m_ExchangeAmountText.text = $"{(int)m_ExchangeSlider.value} chips (¥{(int)m_ExchangeSlider.value * 100})";
		}

		private static void SetActive(GameObject go, bool active)
		{
			if (go != null && go.activeSelf != active) go.SetActive(active);
		}
	}
}