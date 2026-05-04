using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityInput = UnityEngine.Input;

namespace GamblingAction.Input
{
	public class InputModule : MonoBehaviour
	{
		[SerializeField] Camera worldCamera;

		IGameState _state;
		IBoardCoords _board;
		Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

		KeyCode? _activeSkillKey;
		string _activeMode;          // "push" | "attack" | "defense" | "rest" | null
		string _lastSentDir;
		int _power = 1;

		void Start()
		{
			if (worldCamera == null) worldCamera = Camera.main;
			_board = BoardCoordsLocator.Current;
			_state = GameStateLocator.Current;

			if (_state == null) Debug.LogError("[Input] GameStateLocator.Current is null");
			if (_board == null) Debug.LogError("[Input] BoardCoordsLocator.Current is null");

			if (_state != null) _state.OnBeatChanged += HandleBeatChanged;
		}

		void OnDestroy()
		{
			if (_state != null) _state.OnBeatChanged -= HandleBeatChanged;
		}

		void HandleBeatChanged()
		{
			if (_state.CurrentBeat != 1) return;
			ResetIntentState();
		}

		void ResetIntentState()
		{
			_activeSkillKey = null;
			_activeMode = null;
			_lastSentDir = null;
			_power = 1;
			LocalIntentBus.Clear();
		}

		void Update()
		{
			if (_state == null) return;
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

		bool CanAcceptInput()
		{
			if (!_state.GameActive || _state.CurrentBeat >= 4) return false;
			var me = _state.Me;
			if (me == null || me.IsAI) return false;
			foreach (var p in _state.Players.Values)
				if (p.Falling) return false;
			return true;
		}

		void HandleSkillKeys()
		{
			TrySetSkillMode(KeyCode.Q, IntentTypes.Push);
			TrySetSkillMode(KeyCode.W, IntentTypes.Attack);
			TrySetSkillMode(KeyCode.E, IntentTypes.Defense);
			TrySetSkillMode(KeyCode.R, IntentTypes.Rest);
		}

		void TrySetSkillMode(KeyCode key, string mode)
		{
			if (!UnityInput.GetKeyDown(key)) return;
			if (_activeSkillKey == key) return;

			_activeSkillKey = key;
			_activeMode = mode;
			_power = 1;

			if (mode == IntentTypes.Rest)
			{
				_lastSentDir = null;
				_state.SubmitIntent(IntentTypes.Rest, null, _power);
				PublishLocal();
				return;
			}

			string dir = ResolveMouseDir();
			if (mode == IntentTypes.Defense && dir == null)
			{
				_lastSentDir = null;
				_state.SubmitIntent(mode, null, _power);
				PublishLocal();
				return;
			}
			if (dir == null)
			{
				PublishLocal();
				return;
			}
			_lastSentDir = dir;
			_state.SubmitIntent(mode, dir, _power);
			PublishLocal();
		}

		void HandleSkillKeyReleased()
		{
			if (!_activeSkillKey.HasValue) return;
			if (UnityInput.GetKeyUp(_activeSkillKey.Value))
				CancelAll();
		}

		void HandleEscape()
		{
			if (UnityInput.GetKeyDown(KeyCode.Escape)) CancelAll();
		}

		void CancelAll()
		{
			_activeSkillKey = null;
			_activeMode = null;
			_lastSentDir = null;
			_power = 1;
			_state.SubmitIntent(IntentTypes.None, null, 1);
			LocalIntentBus.Clear();
		}

		void HandleWheel()
		{
			float scroll = UnityInput.mouseScrollDelta.y;
			if (Mathf.Approximately(scroll, 0f)) return;
			if (!HasIntentToCharge()) return;

			int prev = _power;
			int next = scroll > 0 ? Mathf.Min(3, prev + 1) : Mathf.Max(1, prev - 1);
			if (next == prev) return;
			SetPowerAndResend(next);
		}

		void HandlePowerNumberKeys()
		{
			int pick = 0;
			if (UnityInput.GetKeyDown(KeyCode.Alpha1) || UnityInput.GetKeyDown(KeyCode.Keypad1)) pick = 1;
			else if (UnityInput.GetKeyDown(KeyCode.Alpha2) || UnityInput.GetKeyDown(KeyCode.Keypad2)) pick = 2;
			else if (UnityInput.GetKeyDown(KeyCode.Alpha3) || UnityInput.GetKeyDown(KeyCode.Keypad3)) pick = 3;
			if (pick == 0) return;
			if (!HasIntentToCharge()) return;
			if (pick == _power) return;
			SetPowerAndResend(pick);
		}

		bool HasIntentToCharge()
		{
			if (!string.IsNullOrEmpty(_activeMode) && _activeMode != IntentTypes.None) return true;
			if (!string.IsNullOrEmpty(_lastSentDir)) return true;
			return false;
		}

		void SetPowerAndResend(int newPower)
		{
			_power = newPower;
			string type = !string.IsNullOrEmpty(_activeMode) && _activeMode != IntentTypes.None
				? _activeMode
				: IntentTypes.Move;
			bool needsDir = type != IntentTypes.Rest && type != IntentTypes.Defense;
			if (!needsDir || !string.IsNullOrEmpty(_lastSentDir))
			{
				_state.SubmitIntent(type, _lastSentDir, _power);
				LocalIntentBus.Set(type, _lastSentDir, _power);
			}
		}

		void HandleMouseMove()
		{
			if (string.IsNullOrEmpty(_activeMode)) return;
			if (_activeMode == IntentTypes.Rest) return;

			string dir = ResolveMouseDir();
			if (dir == null || dir == _lastSentDir) return;
			_lastSentDir = dir;
			_state.SubmitIntent(_activeMode, dir, _power);
			PublishLocal();
		}

		void HandleMouseClick()
		{
			if (!UnityInput.GetMouseButtonDown(0)) return;
			string dir = ResolveMouseDir();
			if (dir == null) return;

			string type = string.IsNullOrEmpty(_activeMode) || _activeMode == IntentTypes.None
				? IntentTypes.Move
				: _activeMode;
			_lastSentDir = dir;
			_state.SubmitIntent(type, dir, _power);
			LocalIntentBus.Set(type, dir, _power);
		}

		void PublishLocal()
		{
			if (string.IsNullOrEmpty(_activeMode))
				LocalIntentBus.Clear();
			else
				LocalIntentBus.Set(_activeMode, _lastSentDir, _power);
		}

		string ResolveMouseDir()
		{
			if (worldCamera == null || _board == null) return null;
			var me = _state.Me;
			if (me == null) return null;

			var ray = worldCamera.ScreenPointToRay(UnityInput.mousePosition);
			if (!_groundPlane.Raycast(ray, out float enter)) return null;
			Vector3 hit = ray.GetPoint(enter);

			Vector3 mePos = _board.GridToWorld(me.X, me.Y);
			float dx = hit.x - mePos.x;
			float dz = hit.z - mePos.z;

			if (Mathf.Abs(dx) > Mathf.Abs(dz))
				return dx > 0 ? Directions.Right : Directions.Left;
			return dz > 0 ? Directions.Up : Directions.Down;
		}
	}
}
