using System.Collections;
using System.Linq;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	public class FlowPanelView : MonoBehaviour
	{
		[Header("Existing flow panels")]
		[SerializeField] GameObject lobbyPanel;
		[SerializeField] GameObject exchangePanel;
		[SerializeField] GameObject buffPanel;
		[SerializeField] GameObject roundOverPanel;
		[SerializeField] GameObject gameOverPanel;

		[Header("Main game stage (Battle / Countdown)")]
		[SerializeField] GameObject mainGameStage;

		[Header("Tuning")]
		[SerializeField] int totalRounds = 3;
		[SerializeField] float executeFlashSeconds = 0.4f;
		[SerializeField] Color beatOnColor = new Color(0.94f, 0.62f, 0.15f);
		[SerializeField] Color finalBeatOnColor = new Color(0.89f, 0.29f, 0.29f);
		[SerializeField] Color beatOffColor = new Color(0.17f, 0.17f, 0.16f, 1f);

		IGameState _state;

		Button _readyButton;
		Toggle _readyAsAIToggle;
		Slider _exchangeSlider;
		TMP_Text _exchangeAmountText;
		Button _exchangeConfirmButton;
		Button _highRiskButton;
		Button _lowRiskButton;
		Button _skipBuffButton;

		TMP_Text _p1Name, _p1Money, _p1Chips;
		TMP_Text _p2Name, _p2Money;
		Image[] _normalBeats;
		Image _finalBeat;
		TMP_Text _executeText;
		TMP_Text _readyText, _countdownText;
		RectTransform _timeBarFill;
		TMP_Text _roundText;

		int _roundCount;
		Coroutine _countdownCo;
		Coroutine _executeFlashCo;
		Vector2 _timeBarFillFullSize;

		void Start()
		{
			_state = GameStateLocator.Current;
			if (_state == null)
			{
				Debug.LogError("[FlowPanel] GameStateLocator.Current is null");
				return;
			}

			FindFlowControls();
			FindStageControls();
			WireButtons();

			_state.OnPhaseChanged   += HandlePhase;
			_state.OnPlayersChanged += HandlePlayersChanged;
			_state.OnPlayersChanged += UpdateExchangeRange;
			_state.OnBeatChanged    += HandleBeatChanged;

			HandlePhase(_state.Phase);
			HandlePlayersChanged();
			UpdateExchangeRange();
			UpdateBeatVisual();
		}

		void OnDestroy()
		{
			if (_state == null) return;
			_state.OnPhaseChanged   -= HandlePhase;
			_state.OnPlayersChanged -= HandlePlayersChanged;
			_state.OnPlayersChanged -= UpdateExchangeRange;
			_state.OnBeatChanged    -= HandleBeatChanged;
		}

		void FindFlowControls()
		{
			_readyButton           = FindIn<Button>(lobbyPanel,    "ReadyButton");
			_readyAsAIToggle       = FindIn<Toggle>(lobbyPanel,    "ReadyAsAIToggle");

			_exchangeSlider        = FindIn<Slider>(exchangePanel, "ExchangeSlider");
			_exchangeAmountText    = FindIn<TMP_Text>(exchangePanel, "ExchangeAmountText");
			_exchangeConfirmButton = FindIn<Button>(exchangePanel, "ExchangeConfirmButton");

			_highRiskButton        = FindIn<Button>(buffPanel, "HighRiskButton");
			_lowRiskButton         = FindIn<Button>(buffPanel, "LowRiskButton");
			_skipBuffButton        = FindIn<Button>(buffPanel, "SkipBuffButton");
		}

		void FindStageControls()
		{
			if (mainGameStage == null) return;

			_p1Name  = FindByPath<TMP_Text>(mainGameStage, "PlayerStatus/P1/Name");
			_p1Money = FindByPath<TMP_Text>(mainGameStage, "PlayerStatus/P1/Money");
			_p1Chips = FindByPath<TMP_Text>(mainGameStage, "PlayerStatus/P1/Chips");
			_p2Name  = FindByPath<TMP_Text>(mainGameStage, "PlayerStatus/P2/Name");
			_p2Money = FindByPath<TMP_Text>(mainGameStage, "PlayerStatus/P2/Money");

			_normalBeats = new[]
			{
				FindByPath<Image>(mainGameStage, "Metronome/Layout/NormalBeat_1"),
				FindByPath<Image>(mainGameStage, "Metronome/Layout/NormalBeat_2"),
				FindByPath<Image>(mainGameStage, "Metronome/Layout/NormalBeat_3"),
			};
			_finalBeat   = FindByPath<Image>(mainGameStage, "Metronome/Layout/FinalBeat");
			_executeText = FindByPath<TMP_Text>(mainGameStage, "Metronome/ExecuteText");

			_readyText     = FindByPath<TMP_Text>(mainGameStage, "ReadyPanel/Ready");
			_countdownText = FindByPath<TMP_Text>(mainGameStage, "ReadyPanel/Countdown");

			var fill = FindByPath<RectTransform>(mainGameStage, "TimeBar/Fill");
			if (fill != null)
			{
				_timeBarFill = fill;
				_timeBarFillFullSize = fill.sizeDelta;
			}

			_roundText = FindByPath<TMP_Text>(mainGameStage, "Round/RoundText");

			if (_executeText != null) _executeText.gameObject.SetActive(false);
		}

		static T FindIn<T>(GameObject root, string name) where T : Component
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

		static T FindByPath<T>(GameObject root, string path) where T : Component
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

		void WireButtons()
		{
			if (_readyButton != null)
				_readyButton.onClick.AddListener(() =>
				{
					bool isAI = _readyAsAIToggle != null && _readyAsAIToggle.isOn;
					_state.SubmitReady(isAI);
					_readyButton.interactable = false;
				});

			if (_exchangeSlider != null)
				_exchangeSlider.onValueChanged.AddListener(v =>
				{
					if (_exchangeAmountText != null)
						_exchangeAmountText.text = $"{(int)v} chips (¥{(int)v * 100})";
				});

			if (_exchangeConfirmButton != null)
				_exchangeConfirmButton.onClick.AddListener(() =>
				{
					int amount = _exchangeSlider != null ? (int)_exchangeSlider.value : 0;
					_state.SubmitExchange(amount);
					_exchangeConfirmButton.interactable = false;
				});

			if (_highRiskButton != null) _highRiskButton.onClick.AddListener(() => SubmitBuff(BuffIds.HighRisk));
			if (_lowRiskButton != null)  _lowRiskButton.onClick.AddListener(() => SubmitBuff(BuffIds.LowRisk));
			if (_skipBuffButton != null) _skipBuffButton.onClick.AddListener(() => SubmitBuff(null));
		}

		void SubmitBuff(string id)
		{
			_state.SubmitBuff(id);
			if (_highRiskButton != null) _highRiskButton.interactable = false;
			if (_lowRiskButton != null)  _lowRiskButton.interactable = false;
			if (_skipBuffButton != null) _skipBuffButton.interactable = false;
		}

		void HandlePhase(GamePhase phase)
		{
			SetActive(lobbyPanel,     phase == GamePhase.Lobby);
			SetActive(exchangePanel,  phase == GamePhase.Exchange);
			SetActive(buffPanel,      phase == GamePhase.BuffSelection);
			SetActive(roundOverPanel, phase == GamePhase.RoundOver);
			SetActive(gameOverPanel,  phase == GamePhase.GameOver);

			bool stageVisible = phase == GamePhase.Countdown || phase == GamePhase.Battle;
			SetActive(mainGameStage, stageVisible);

			if (phase == GamePhase.Lobby && _readyButton != null)
				_readyButton.interactable = true;

			if (phase == GamePhase.Exchange)
			{
				if (_exchangeConfirmButton != null) _exchangeConfirmButton.interactable = true;
				UpdateExchangeRange();
			}

			if (phase == GamePhase.BuffSelection)
			{
				int chips = _state.Me?.Chips ?? 0;
				if (_highRiskButton != null) _highRiskButton.interactable = chips >= 15;
				if (_lowRiskButton != null)  _lowRiskButton.interactable  = chips >= 5;
				if (_skipBuffButton != null) _skipBuffButton.interactable = true;
			}

			if (phase == GamePhase.Countdown)
			{
				_roundCount++;
				UpdateRoundText();
				StartCountdown();
			}
			else
			{
				StopCountdown();
			}

			if (phase == GamePhase.Battle)
			{
				UpdateBeatVisual();
			}

			if (phase == GamePhase.GameOver)
			{
				_roundCount = 0;
			}
		}

		void HandlePlayersChanged()
		{
			if (_state == null) return;

			var p1 = _state.Players.Values.FirstOrDefault(p => p.Role == "P1");
			var p2 = _state.Players.Values.FirstOrDefault(p => p.Role == "P2");

			ApplyPlayerSlot(p1, _p1Name, _p1Money, _p1Chips, hideChipsIfOpponent: false);
			ApplyPlayerSlot(p2, _p2Name, _p2Money, null,     hideChipsIfOpponent: true);
		}

		void ApplyPlayerSlot(PlayerDto dto, TMP_Text nameText, TMP_Text moneyText, TMP_Text chipsText, bool hideChipsIfOpponent)
		{
			if (dto == null)
			{
				if (nameText != null)  nameText.text = "-";
				if (moneyText != null) moneyText.text = "-";
				if (chipsText != null) chipsText.text = "-";
				return;
			}

			if (nameText != null)
			{
				string label = dto.IsAI ? $"{dto.Role} (AI)" : dto.Role;
				nameText.text = label;
			}
			if (moneyText != null) moneyText.text = $"¥{dto.Money:N0}";
			if (chipsText != null)
			{
				bool isMe = dto.Id == _state.MyId;
				if (hideChipsIfOpponent && !isMe) chipsText.text = "??";
				else chipsText.text = dto.Chips.ToString();
			}
		}

		void HandleBeatChanged()
		{
			UpdateBeatVisual();
			UpdateTimeBar();

			if (_state.CurrentBeat == 4)
				FlashExecuteText();
		}

		void UpdateBeatVisual()
		{
			bool battle = _state != null && _state.Phase == GamePhase.Battle;
			int beat = battle ? _state.CurrentBeat : 0;

			if (_normalBeats != null)
			{
				for (int i = 0; i < _normalBeats.Length; i++)
				{
					if (_normalBeats[i] == null) continue;
					bool on = battle && beat == i + 1;
					_normalBeats[i].color = on ? beatOnColor : beatOffColor;
				}
			}
			if (_finalBeat != null)
				_finalBeat.color = (battle && beat == 4) ? finalBeatOnColor : beatOffColor;
		}

		void UpdateTimeBar()
		{
			if (_timeBarFill == null) return;
			if (_state.Phase != GamePhase.Battle)
			{
				_timeBarFill.localScale = Vector3.one;
				return;
			}

			int total = GamblingAction.Core.GameConfig.GameDurationSec;
			float t = total > 0 ? Mathf.Clamp01((float)_state.TimeLeft / total) : 0f;
			_timeBarFill.localScale = new Vector3(t, 1f, 1f);
		}

		void FlashExecuteText()
		{
			if (_executeText == null) return;
			if (_executeFlashCo != null) StopCoroutine(_executeFlashCo);
			_executeFlashCo = StartCoroutine(ExecuteFlash());
		}

		IEnumerator ExecuteFlash()
		{
			_executeText.text = "EXECUTE!";
			_executeText.gameObject.SetActive(true);
			yield return new WaitForSeconds(executeFlashSeconds);
			_executeText.gameObject.SetActive(false);
			_executeFlashCo = null;
		}

		void StartCountdown()
		{
			if (_readyText == null && _countdownText == null) return;
			StopCountdown();
			_countdownCo = StartCoroutine(CountdownSequence());
		}

		void StopCountdown()
		{
			if (_countdownCo != null)
			{
				StopCoroutine(_countdownCo);
				_countdownCo = null;
			}
			if (_readyText != null)     _readyText.gameObject.SetActive(false);
			if (_countdownText != null) _countdownText.gameObject.SetActive(false);
		}

		IEnumerator CountdownSequence()
		{
			if (_readyText != null)
			{
				_readyText.gameObject.SetActive(true);
				_readyText.text = "READY?";
			}
			if (_countdownText != null)
			{
				_countdownText.gameObject.SetActive(true);
				_countdownText.text = "";
			}

			yield return new WaitForSeconds(0.8f);

			for (int i = 3; i >= 1; i--)
			{
				if (_countdownText != null) _countdownText.text = i.ToString();
				yield return new WaitForSeconds(0.8f);
			}

			if (_readyText != null) _readyText.text = "";
			if (_countdownText != null) _countdownText.text = "GO!";
			yield return new WaitForSeconds(0.6f);

			if (_readyText != null)     _readyText.gameObject.SetActive(false);
			if (_countdownText != null) _countdownText.gameObject.SetActive(false);
			_countdownCo = null;
		}

		void UpdateRoundText()
		{
			if (_roundText == null) return;
			int shown = Mathf.Clamp(_roundCount, 1, totalRounds);
			_roundText.text = $"Round {shown}/{totalRounds}";
		}

		void UpdateExchangeRange()
		{
			if (_exchangeSlider == null) return;
			var me = _state.Me;
			int max = me != null ? Mathf.Min(100, me.Money / 100) : 0;
			_exchangeSlider.minValue = 0;
			_exchangeSlider.maxValue = max;
			if (_exchangeSlider.value > max) _exchangeSlider.value = max;
			if (_exchangeAmountText != null)
				_exchangeAmountText.text = $"{(int)_exchangeSlider.value} chips (¥{(int)_exchangeSlider.value * 100})";
		}

		static void SetActive(GameObject go, bool active)
		{
			if (go != null && go.activeSelf != active) go.SetActive(active);
		}
	}
}
