using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityInput = UnityEngine.Input;

namespace GamblingAction.Input
{
	public class InputModule : MonoBehaviour
	{
		[FormerlySerializedAs("worldCamera")]
		[SerializeField] private Camera m_WorldCamera;

#if UNITY_EDITOR
		// デバッグ時のみデフォルト値はtrueとして、コードやエディタから設定可能にする
		[Header("【debug】長押ししなくてもコマンドを受け付けるか")]
		[SerializeField] private bool m_KeepActionOnRelease = true;
#else
		// ビルド後は強制的にfalse（長押し必須）
		private const bool m_KeepActionOnRelease = false;
#endif

		private IGameState m_State;
		private IBoardCoords m_Board;
		private Plane m_GroundPlane = new Plane(Vector3.up, Vector3.zero);

		private KeyCode? m_ActiveSkillKey;
		private string m_ActiveMode;
		private string m_LastSentDir;
		private int m_Power = 1;

		private InputAction m_ConfirmAction;
		private InputAction m_GamepadNavAction;
		private float m_GamepadNavCooldown;
		private const float k_GamepadNavCooldownTime = 0.2f;

		private int m_GamepadHoverX = -1;
		private int m_GamepadHoverY = -1;

		private void Start()
		{
			if (m_WorldCamera == null) m_WorldCamera = Camera.main;
			m_Board = BoardCoordsLocator.Current;
			m_State = GameStateLocator.Current;

			if (m_State == null) Debug.LogError("[Input] GameStateLocator.Current is null");
			if (m_Board == null) Debug.LogError("[Input] BoardCoordsLocator.Current is null");

			if (m_State != null) m_State.OnBeatChanged += HandleBeatChanged;

			// InputAction 構築
			m_ConfirmAction = new InputAction("ConfirmIntent", InputActionType.Button);
			m_ConfirmAction.AddBinding("<Gamepad>/buttonSouth");
			m_ConfirmAction.AddBinding("<Mouse>/leftButton");
			m_ConfirmAction.performed += OnConfirmActionPerformed;
			m_ConfirmAction.Enable();

			m_GamepadNavAction = new InputAction("GamepadNav", InputActionType.Value, expectedControlType: "Vector2");
			m_GamepadNavAction.AddBinding("<Gamepad>/leftStick");
			m_GamepadNavAction.AddCompositeBinding("Dpad")
				.With("Up",    "<Gamepad>/dpad/up")
				.With("Down",  "<Gamepad>/dpad/down")
				.With("Left",  "<Gamepad>/dpad/left")
				.With("Right", "<Gamepad>/dpad/right");
			m_GamepadNavAction.Enable();
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnBeatChanged -= HandleBeatChanged;

			m_ConfirmAction?.Disable();
			m_ConfirmAction?.Dispose();
			m_GamepadNavAction?.Disable();
			m_GamepadNavAction?.Dispose();
		}

		private void HandleBeatChanged()
		{
			if (m_State.CurrentBeat != 1) return;
			ResetIntentState();
		}

		private void ResetIntentState()
		{
			m_ActiveSkillKey = null;
			m_ActiveMode = null;
			m_LastSentDir = null;
			m_Power = 1;
			m_GamepadHoverX = -1;
			m_GamepadHoverY = -1;
			LocalIntentBus.Clear();
		}

		private void Update()
		{
			if (m_State == null) return;
			if (!CanAcceptInput())
			{
				if (LocalIntentBus.Current.IsActive) LocalIntentBus.Clear();
				return;
			}

			HandleSkillKeys();
			HandleSkillKeyReleased();
			HandleEscape();
			HandleHover();
		}

		private bool CanAcceptInput()
		{
			if (!m_State.GameActive || m_State.CurrentBeat >= 4) return false;
			var me = m_State.Me;
			if (me == null || me.IsAI) return false;
			foreach (var p in m_State.Players.Values)
				if (p.Falling) return false;
			return true;
		}

		private void HandleSkillKeys()
		{
			TrySetSkillMode(KeyCode.Q, IntentTypes.Push);
			// attack廃止につきコメントアウト
			// TrySetSkillMode(KeyCode.W, IntentTypes.Attack);
			TrySetSkillMode(KeyCode.E, IntentTypes.Defense);
			TrySetSkillMode(KeyCode.R, IntentTypes.Skill);
		}

		private void TrySetSkillMode(KeyCode key, string mode)
		{
			if (!UnityInput.GetKeyDown(key)) return;
			if (m_ActiveSkillKey == key) return;

			m_ActiveSkillKey = key;
			m_ActiveMode = mode;
			m_Power = 1;

			if (mode == IntentTypes.Skill)
			{
				m_LastSentDir = null;
				m_State.SubmitIntent(IntentTypes.Skill, null, m_Power);
				LocalIntentBus.Set(IntentTypes.Skill, null, m_Power, -1, -1, -1, -1, true);
				return;
			}

			if (mode == IntentTypes.Defense)
			{
				m_LastSentDir = null;
				m_State.SubmitIntent(IntentTypes.Defense, null, m_Power);
				LocalIntentBus.Set(IntentTypes.Defense, null, m_Power, -1, -1, -1, -1, true);
				return;
			}

			if (mode == IntentTypes.Push)
			{
				m_LastSentDir = null;
				m_GamepadHoverX = -1;
				m_GamepadHoverY = -1;
				LocalIntentBus.Set(IntentTypes.Push, null, m_Power, -1, -1, -1, -1, false);
				return;
			}
		}

		private void HandleSkillKeyReleased()
		{
			if (!m_ActiveSkillKey.HasValue) return;
			if (UnityInput.GetKeyUp(m_ActiveSkillKey.Value))
			{
				if (m_KeepActionOnRelease) return;

				// コマンドをキャンセルする(押しっぱの状態でないと4拍目に受け付けない)
				CancelAll();
			}
		}

		private void HandleEscape()
		{
			if (UnityInput.GetKeyDown(KeyCode.Escape)) CancelAll();
		}

		private void CancelAll()
		{
			m_ActiveSkillKey = null;
			m_ActiveMode = null;
			m_LastSentDir = null;
			m_Power = 1;
			m_GamepadHoverX = -1;
			m_GamepadHoverY = -1;
			m_State.SubmitIntent(IntentTypes.None, null, 1);
			LocalIntentBus.Clear();
		}

		private void OnConfirmActionPerformed(InputAction.CallbackContext context)
		{
			Debug.Log($"[Input] OnConfirmAction: device={context.control.device.name}, activeMode={m_ActiveMode}");
			if (m_State == null) { Debug.LogWarning("[Input] m_State is null"); return; }
			if (!CanAcceptInput()) { Debug.LogWarning($"[Input] CanAcceptInput is false (GameActive={m_State.GameActive}, Beat={m_State.CurrentBeat})"); return; }
			if (string.IsNullOrEmpty(m_ActiveMode) || m_ActiveMode != IntentTypes.Push) return;
			if (LocalIntentBus.Current.IsConfirmed) { Debug.Log("[Input] Already confirmed"); return; }

			var me = m_State.Me;
			if (me == null) { Debug.LogWarning("[Input] Local player Me is null"); return; }

			int targetX = -1;
			int targetY = -1;

			if (context.control.device is Mouse)
			{
				if (ResolveMouseGrid(out int gx, out int gy))
				{
					targetX = gx;
					targetY = gy;
					Debug.Log($"[Input] Mouse target resolved: {gx}, {gy}");
				}
				else
				{
					Debug.LogWarning("[Input] Mouse grid could not be resolved");
				}
			}
			else
			{
				if (m_GamepadHoverX >= 0 && m_GamepadHoverY >= 0)
				{
					targetX = m_GamepadHoverX;
					targetY = m_GamepadHoverY;
					Debug.Log($"[Input] Gamepad target resolved: {targetX}, {targetY}");
				}
			}

			if (targetX >= 0 && targetY >= 0)
			{
				ClampToReachable(me.X, me.Y, targetX, targetY, out int clampedX, out int clampedY);

				int dx = clampedX - me.X;
				int dy = clampedY - me.Y;
				string dir = null;
				int power = 1;

				if (dx != 0)
				{
					dir = dx > 0 ? Directions.Right : Directions.Left;
					power = Mathf.Abs(dx);
				}
				else if (dy != 0)
				{
					dir = dy > 0 ? Directions.Down : Directions.Up;
					power = Mathf.Abs(dy);
				}

				if (dir != null)
				{
					m_LastSentDir = dir;
					m_Power = power;
					Debug.Log($"[Input] Submitting push intent: dir={dir}, power={power}, target={clampedX},{clampedY}");
					m_State.SubmitIntent(m_ActiveMode, dir, power);
					LocalIntentBus.Set(m_ActiveMode, dir, power, clampedX, clampedY, clampedX, clampedY, true);
				}
				else
				{
					Debug.LogWarning("[Input] Calculated direction is null");
				}
			}
		}

		private void HandleHover()
		{
			if (string.IsNullOrEmpty(m_ActiveMode) || m_ActiveMode != IntentTypes.Push) return;
			if (LocalIntentBus.Current.IsConfirmed) return;

			var me = m_State.Me;
			if (me == null) return;

			// ゲームパッド入力を処理
			Vector2 nav = m_GamepadNavAction.ReadValue<Vector2>();
			bool hasGamepadInput = nav.magnitude > 0.5f;

			if (m_GamepadNavCooldown > 0f)
			{
				m_GamepadNavCooldown -= Time.deltaTime;
			}

			if (hasGamepadInput && m_GamepadNavCooldown <= 0f)
			{
				if (m_GamepadHoverX < 0 || m_GamepadHoverY < 0)
				{
					m_GamepadHoverX = me.X;
					m_GamepadHoverY = me.Y;
				}

				if (Mathf.Abs(nav.x) > Mathf.Abs(nav.y))
				{
					m_GamepadHoverX = Mathf.Clamp(m_GamepadHoverX + (nav.x > 0 ? 1 : -1), 0, m_Board.GridSize - 1);
				}
				else
				{
					m_GamepadHoverY = Mathf.Clamp(m_GamepadHoverY + (nav.y > 0 ? 1 : -1), 0, m_Board.GridSize - 1);
				}

				m_GamepadNavCooldown = k_GamepadNavCooldownTime;
			}

			// マウス移動があればホバーをマウスに切り替え、なければゲームパッドのホバーを使用
			bool isMouseActive = UnityInput.mousePresent && (Mathf.Abs(UnityInput.GetAxis("Mouse X")) > 0.01f || Mathf.Abs(UnityInput.GetAxis("Mouse Y")) > 0.01f);

			int hoverX = -1;
			int hoverY = -1;

			if (isMouseActive || (!hasGamepadInput && m_GamepadHoverX < 0))
			{
				if (ResolveMouseGrid(out int gx, out int gy))
				{
					hoverX = gx;
					hoverY = gy;
					m_GamepadHoverX = gx;
					m_GamepadHoverY = gy;
				}
			}
			else
			{
				hoverX = m_GamepadHoverX;
				hoverY = m_GamepadHoverY;
			}

			if (hoverX >= 0 && hoverY >= 0)
			{
				ClampToReachable(me.X, me.Y, hoverX, hoverY, out int clampedX, out int clampedY);

				int dx = clampedX - me.X;
				int dy = clampedY - me.Y;
				string dir = null;
				int power = 1;

				if (dx != 0)
				{
					dir = dx > 0 ? Directions.Right : Directions.Left;
					power = Mathf.Abs(dx);
				}
				else if (dy != 0)
				{
					dir = dy > 0 ? Directions.Down : Directions.Up;
					power = Mathf.Abs(dy);
				}

				LocalIntentBus.Set(m_ActiveMode, dir, power, clampedX, clampedY, clampedX, clampedY, false);
			}
		}

		private bool ResolveMouseGrid(out int gx, out int gy)
		{
			gx = -1;
			gy = -1;
			if (m_WorldCamera == null || m_Board == null) return false;
			var ray = m_WorldCamera.ScreenPointToRay(UnityInput.mousePosition);
			if (!m_GroundPlane.Raycast(ray, out float enter)) return false;
			Vector3 hit = ray.GetPoint(enter);
			return m_Board.TryWorldToGrid(hit, out gx, out gy);
		}

		private void ClampToReachable(int myX, int myY, int targetX, int targetY, out int clampedX, out int clampedY)
		{
			clampedX = myX;
			clampedY = myY;

			int dx = targetX - myX;
			int dy = targetY - myY;

			if (Mathf.Abs(dx) >= Mathf.Abs(dy))
			{
				int sign = dx > 0 ? 1 : -1;
				int dist = Mathf.Min(3, Mathf.Abs(dx));
				clampedX = myX + sign * dist;
			}
			else
			{
				int sign = dy > 0 ? 1 : -1;
				int dist = Mathf.Min(3, Mathf.Abs(dy));
				clampedY = myY + sign * dist;
			}
		}
	}
}
