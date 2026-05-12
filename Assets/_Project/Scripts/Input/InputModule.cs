using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;
using UnityInput = UnityEngine.Input;

namespace GamblingAction.Input
{
	public class InputModule : MonoBehaviour
	{
		[FormerlySerializedAs("worldCamera")]
		[SerializeField] private Camera m_WorldCamera;

		private IGameState m_State;
		private IBoardCoords m_Board;
		private Plane m_GroundPlane = new Plane(Vector3.up, Vector3.zero);

		private KeyCode? m_ActiveSkillKey;
		private string m_ActiveMode;
		private string m_LastSentDir;
		private int m_Power = 1;

		private void Start()
		{
			if (m_WorldCamera == null) m_WorldCamera = Camera.main;
			m_Board = BoardCoordsLocator.Current;
			m_State = GameStateLocator.Current;

			if (m_State == null) Debug.LogError("[Input] GameStateLocator.Current is null");
			if (m_Board == null) Debug.LogError("[Input] BoardCoordsLocator.Current is null");

			if (m_State != null) m_State.OnBeatChanged += HandleBeatChanged;
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnBeatChanged -= HandleBeatChanged;
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
			HandleWheel();
			HandlePowerNumberKeys();
			HandleMouseMove();
			HandleMouseClick();
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
			TrySetSkillMode(KeyCode.W, IntentTypes.Attack);
			TrySetSkillMode(KeyCode.E, IntentTypes.Defense);
			TrySetSkillMode(KeyCode.R, IntentTypes.Rest);
		}

		private void TrySetSkillMode(KeyCode key, string mode)
		{
			// m_Powerに加えて、バフ込みの値を計算して渡すように変更の必要姓

			if (!UnityInput.GetKeyDown(key)) return;
			if (m_ActiveSkillKey == key) return;

			m_ActiveSkillKey = key;
			m_ActiveMode = mode;
			m_Power = 1;

			if (mode == IntentTypes.Rest)
			{
				m_LastSentDir = null;
				m_State.SubmitIntent(IntentTypes.Rest, null, m_Power);
				PublishLocal();
				return;
			}

			string dir = ResolveMouseDir();
			if (mode == IntentTypes.Defense && dir == null)
			{
				m_LastSentDir = null;
				m_State.SubmitIntent(mode, null, m_Power);
				PublishLocal();
				return;
			}
			if (dir == null)
			{
				PublishLocal();
				return;
			}
			m_LastSentDir = dir;
			m_State.SubmitIntent(mode, dir, m_Power);
			PublishLocal();
		}

		private void HandleSkillKeyReleased()
		{
			if (!m_ActiveSkillKey.HasValue) return;
			if (UnityInput.GetKeyUp(m_ActiveSkillKey.Value))
				CancelAll();
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
			m_State.SubmitIntent(IntentTypes.None, null, 1);
			LocalIntentBus.Clear();
		}

		private void HandleWheel()
		{
			float scroll = UnityInput.mouseScrollDelta.y;
			if (Mathf.Approximately(scroll, 0f)) return;
			if (!HasIntentToCharge()) return;

			int prev = m_Power;
			int next = scroll > 0 ? Mathf.Min(3, prev + 1) : Mathf.Max(1, prev - 1);
			if (next == prev) return;
			SetPowerAndResend(next);
		}

		private void HandlePowerNumberKeys()
		{
			int pick = 0;
			if (UnityInput.GetKeyDown(KeyCode.Alpha1) || UnityInput.GetKeyDown(KeyCode.Keypad1)) pick = 1;
			else if (UnityInput.GetKeyDown(KeyCode.Alpha2) || UnityInput.GetKeyDown(KeyCode.Keypad2)) pick = 2;
			else if (UnityInput.GetKeyDown(KeyCode.Alpha3) || UnityInput.GetKeyDown(KeyCode.Keypad3)) pick = 3;
			if (pick == 0) return;
			if (!HasIntentToCharge()) return;
			if (pick == m_Power) return;
			SetPowerAndResend(pick);
		}

		private bool HasIntentToCharge()
		{
			if (!string.IsNullOrEmpty(m_ActiveMode) && m_ActiveMode != IntentTypes.None) return true;
			if (!string.IsNullOrEmpty(m_LastSentDir)) return true;
			return false;
		}

		private void SetPowerAndResend(int newPower)
		{
			m_Power = newPower;
			string type = !string.IsNullOrEmpty(m_ActiveMode) && m_ActiveMode != IntentTypes.None
				? m_ActiveMode
				: IntentTypes.Move;
			bool needsDir = type != IntentTypes.Rest && type != IntentTypes.Defense;
			if (!needsDir || !string.IsNullOrEmpty(m_LastSentDir))
			{
				m_State.SubmitIntent(type, m_LastSentDir, m_Power);
				LocalIntentBus.Set(type, m_LastSentDir, m_Power);
			}
		}

		private void HandleMouseMove()
		{
			if (string.IsNullOrEmpty(m_ActiveMode)) return;
			if (m_ActiveMode == IntentTypes.Rest) return;

			string dir = ResolveMouseDir();
			if (dir == null || dir == m_LastSentDir) return;
			m_LastSentDir = dir;
			m_State.SubmitIntent(m_ActiveMode, dir, m_Power);
			PublishLocal();
		}

		private void HandleMouseClick()
		{
			if (!UnityInput.GetMouseButtonDown(0)) return;
			string dir = ResolveMouseDir();
			if (dir == null) return;

			string type = string.IsNullOrEmpty(m_ActiveMode) || m_ActiveMode == IntentTypes.None
				? IntentTypes.Move
				: m_ActiveMode;
			m_LastSentDir = dir;
			m_State.SubmitIntent(type, dir, m_Power);
			LocalIntentBus.Set(type, dir, m_Power);
		}

		private void PublishLocal()
		{
			if (string.IsNullOrEmpty(m_ActiveMode))
				LocalIntentBus.Clear();
			else
				LocalIntentBus.Set(m_ActiveMode, m_LastSentDir, m_Power);
		}

		private string ResolveMouseDir()
		{
			if (m_WorldCamera == null || m_Board == null) return null;
			var me = m_State.Me;
			if (me == null) return null;

			var ray = m_WorldCamera.ScreenPointToRay(UnityInput.mousePosition);
			if (!m_GroundPlane.Raycast(ray, out float enter)) return null;
			Vector3 hit = ray.GetPoint(enter);

			Vector3 mePos = m_Board.GridToWorld(me.X, me.Y);
			float dx = hit.x - mePos.x;
			float dz = hit.z - mePos.z;

			if (Mathf.Abs(dx) > Mathf.Abs(dz))
				return dx > 0 ? Directions.Right : Directions.Left;
			return dz > 0 ? Directions.Up : Directions.Down;
		}
	}
}
