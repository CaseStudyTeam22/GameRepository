using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace GamblingAction.Input
{
	// プレイヤーの入力を処理するモジュール（PlayerInput / Input System 版）。
	//
	// 【なぜ PlayerInput 方式か】
	//   InputAction を直接 new する方式だと、接続中の全 Gamepad・全インスタンスに
	//   ブロードキャストされ、Multiplayer Play Mode で両インスタンスが同じパッド入力を
	//   受け取ってしまう（両キャラが同じ動きをする）。
	//   PlayerInput は Control Scheme（Gamepad / Keyboard&Mouse）を手がかりに
	//   デバイスをプレイヤーへ排他的にペアリングするため、MPPM でも分離が効く。
	//
	// 【前提】
	//   同じ GameObject に PlayerInput コンポーネントが付いており、
	//   Actions に InputSystem_Actions、Behavior が「Invoke C# Events」、
	//   Default Map が「Player」に設定されていること。
	//
	// 【Action 割り当て（InputSystem_Actions の Player マップ）】
	//   Move       : 左スティック / WASD          … 方向
	//   Push       : LT / Q                        … 突進
	//   Attack     : LB / W                        … スキル（攻撃）
	//   Defense    : RT / E                        … 防御
	//   Rest       : R                             … 回復（コントローラー割当なし）
	//   Confirm    : buttonNorth(Y) / 左クリック   … 決定
	//   Cancel     : Escape                        … キャンセル（コントローラー割当なし）
	//   PowerCycle : buttonWest(X/□/Y)            … 強さ変更（1→2→3→1）
	//   Point      : マウス位置                    … 方向解決用
	//   Scroll     : マウスホイール                … 強さ増減
	public class InputModule : MonoBehaviour
	{
		// ─────────────────────────────────────────────────────────────
		// 定数
		// ─────────────────────────────────────────────────────────────

		// 左スティックのデッドゾーン（これ未満は無入力と見なす）
		private const float m_StickDeadZone = 0.5f;

		// 左スティック方向変更のクールダウン（秒）。チカチカ防止用
		private const float m_StickDirChangeInterval = 0.15f;

		// ─────────────────────────────────────────────────────────────
		// シリアライズフィールド
		// ─────────────────────────────────────────────────────────────

		[FormerlySerializedAs("worldCamera")]
		[SerializeField] private Camera m_WorldCamera;

		// 同じ GameObject に付いている PlayerInput。未設定なら自動取得する。
		[SerializeField] private PlayerInput m_PlayerInput;

		// ─────────────────────────────────────────────────────────────
		// InputAction 参照（PlayerInput のアセットから取得）
		// ─────────────────────────────────────────────────────────────

		private InputAction m_MoveAction;
		private InputAction m_PushAction;
		private InputAction m_AttackAction;
		private InputAction m_DefenseAction;
		private InputAction m_RestAction;
		private InputAction m_ConfirmAction;
		private InputAction m_CancelAction;
		private InputAction m_PowerCycleAction;
		private InputAction m_PointAction;
		private InputAction m_ScrollAction;

		// ─────────────────────────────────────────────────────────────
		// 内部状態
		// ─────────────────────────────────────────────────────────────

		private IGameState   m_State;
		private IBoardCoords m_Board;
		private Plane        m_GroundPlane = new Plane(Vector3.up, Vector3.zero);

		// 現在アクティブなスキル Action（離し判定用）。null は無効。
		private InputAction m_ActiveSkillAction;

		// 左スティック方向変更クールダウン残り時間（秒）
		private float m_StickDirChangeCooldown;

		private string m_ActiveMode;
		private string m_LastSentDir;
		private int    m_Power = 1;

		// ─────────────────────────────────────────────────────────────
		// ライフサイクル
		// ─────────────────────────────────────────────────────────────

		private void Awake()
		{
			if (m_PlayerInput == null) m_PlayerInput = GetComponent<PlayerInput>();
			if (m_PlayerInput == null)
			{
				Debug.LogError("[Input] PlayerInput コンポーネントが見つかりません");
				return;
			}

			ResolveActions();
		}

		private void Start()
		{
			if (m_WorldCamera == null) m_WorldCamera = Camera.main;
			m_Board = BoardCoordsLocator.Current;
			m_State = GameStateLocator.Current;

			if (m_State == null) Debug.LogError("[Input] GameStateLocator.Current is null");
			if (m_Board == null) Debug.LogError("[Input] BoardCoordsLocator.Current is null");

			if (m_State != null) m_State.OnBeatChanged += HandleBeatChanged;

			// Project-wide actions の警告対策：
			// 全マップを一旦無効化し、Player マップだけを有効にする。
			// これで UI マップ等との二重発火を防ぐ。
			EnablePlayerMapOnly();

			RegisterCallbacks();
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnBeatChanged -= HandleBeatChanged;
			UnregisterCallbacks();
		}

		// ─────────────────────────────────────────────────────────────
		// Action 取得・マップ制御
		// ─────────────────────────────────────────────────────────────

		// PlayerInput のアクションアセットから各 Action を名前で取得する。
		private void ResolveActions()
		{
			m_MoveAction       = m_PlayerInput.actions["Move"];
			m_PushAction       = m_PlayerInput.actions["Push"];
			m_AttackAction     = m_PlayerInput.actions["Attack"];
			m_DefenseAction    = m_PlayerInput.actions["Defense"];
			m_RestAction       = m_PlayerInput.actions["Rest"];
			m_ConfirmAction    = m_PlayerInput.actions["Confirm"];
			m_CancelAction     = m_PlayerInput.actions["Cancel"];
			m_PowerCycleAction = m_PlayerInput.actions["PowerCycle"];
			m_PointAction      = m_PlayerInput.actions["Point"];
			m_ScrollAction     = m_PlayerInput.actions["Scroll"];
		}

		// 全アクションマップを無効化し、Player マップだけを有効にする。
		private void EnablePlayerMapOnly()
		{
			var asset = m_PlayerInput.actions;
			if (asset == null) return;

			foreach (var map in asset.actionMaps)
				map.Disable();

			var playerMap = asset.FindActionMap("Player", throwIfNotFound: false);
			if (playerMap != null) playerMap.Enable();
			else Debug.LogWarning("[Input] Player マップが見つかりません");
		}

		// ─────────────────────────────────────────────────────────────
		// コールバック登録
		// ─────────────────────────────────────────────────────────────

		private void RegisterCallbacks()
		{
			// スキル系（押した瞬間 performed / 離した瞬間 canceled）
			m_PushAction.performed    += OnPushPerformed;
			m_PushAction.canceled     += OnSkillCanceled;
			m_AttackAction.performed  += OnAttackPerformed;
			m_AttackAction.canceled   += OnSkillCanceled;
			m_DefenseAction.performed += OnDefensePerformed;
			m_DefenseAction.canceled  += OnSkillCanceled;
			m_RestAction.performed    += OnRestPerformed;
			m_RestAction.canceled     += OnSkillCanceled;

			// 決定・キャンセル
			m_ConfirmAction.performed += OnConfirmPerformed;
			m_CancelAction.performed  += OnCancelPerformed;

			// 強さ変更（ボタン=サイクル / ホイール=増減）
			m_PowerCycleAction.performed += OnPowerCyclePerformed;
			m_ScrollAction.performed     += OnScrollPerformed;
		}

		private void UnregisterCallbacks()
		{
			if (m_PushAction != null)       { m_PushAction.performed    -= OnPushPerformed;    m_PushAction.canceled    -= OnSkillCanceled; }
			if (m_AttackAction != null)     { m_AttackAction.performed  -= OnAttackPerformed;  m_AttackAction.canceled  -= OnSkillCanceled; }
			if (m_DefenseAction != null)    { m_DefenseAction.performed -= OnDefensePerformed; m_DefenseAction.canceled -= OnSkillCanceled; }
			if (m_RestAction != null)       { m_RestAction.performed    -= OnRestPerformed;    m_RestAction.canceled    -= OnSkillCanceled; }
			if (m_ConfirmAction != null)    m_ConfirmAction.performed    -= OnConfirmPerformed;
			if (m_CancelAction != null)     m_CancelAction.performed     -= OnCancelPerformed;
			if (m_PowerCycleAction != null) m_PowerCycleAction.performed -= OnPowerCyclePerformed;
			if (m_ScrollAction != null)     m_ScrollAction.performed     -= OnScrollPerformed;
		}

		// ─────────────────────────────────────────────────────────────
		// 拍変更ハンドラ
		// ─────────────────────────────────────────────────────────────

		private void HandleBeatChanged()
		{
			if (m_State.CurrentBeat != 1) return;
			ResetIntentState();
		}

		private void ResetIntentState()
		{
			m_ActiveSkillAction      = null;
			m_ActiveMode             = null;
			m_LastSentDir            = null;
			m_Power                  = 1;
			m_StickDirChangeCooldown = 0f;
			LocalIntentBus.Clear();
		}

		// ─────────────────────────────────────────────────────────────
		// 毎フレーム処理（方向の連続追跡のみ）
		// ─────────────────────────────────────────────────────────────

		private void Update()
		{
			if (m_State == null) return;
			if (!CanAcceptInput())
			{
				if (LocalIntentBus.Current.IsActive) LocalIntentBus.Clear();
				return;
			}

			m_StickDirChangeCooldown -= Time.deltaTime;

			// スキルモード中は左スティック or マウスで方向をリアルタイム更新する
			HandleDirectionUpdate();
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

		// ─────────────────────────────────────────────────────────────
		// スキルボタン コールバック
		// ─────────────────────────────────────────────────────────────

		private void OnPushPerformed(InputAction.CallbackContext ctx)    => OnSkillPressed(m_PushAction,    IntentTypes.Push);
		private void OnAttackPerformed(InputAction.CallbackContext ctx)  => OnSkillPressed(m_AttackAction,  IntentTypes.Attack);
		private void OnDefensePerformed(InputAction.CallbackContext ctx) => OnSkillPressed(m_DefenseAction, IntentTypes.Defense);
		private void OnRestPerformed(InputAction.CallbackContext ctx)    => OnSkillPressed(m_RestAction,    IntentTypes.Rest);

		// スキルボタンが押された瞬間の共通処理。
		private void OnSkillPressed(InputAction action, string mode)
		{
			if (!CanAcceptInput()) return;
			if (m_ActiveMode == mode) return; // 同モードの二重押しは無視

			m_ActiveSkillAction = action;
			m_ActiveMode        = mode;
			m_Power             = 1;

			if (mode == IntentTypes.Rest)
			{
				m_LastSentDir = null;
				m_State.SubmitIntent(IntentTypes.Rest, null, m_Power);
				PublishLocal();
				return;
			}

			string dir = ResolveDir();

			if (mode == IntentTypes.Defense && dir == null)
			{
				// 防御は方向なしでも有効
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

		// スキルボタンが離された瞬間の共通処理。
		// 自身が起動した Action のときだけキャンセルする。
		private void OnSkillCanceled(InputAction.CallbackContext ctx)
		{
			if (m_ActiveSkillAction == null) return;
			if (ctx.action != m_ActiveSkillAction) return;
			CancelAll();
		}

		// ─────────────────────────────────────────────────────────────
		// 決定・キャンセル コールバック
		// ─────────────────────────────────────────────────────────────

		// Y ボタン / 左クリックで現在の向きを確定送信する。未選択なら Move。
		private void OnConfirmPerformed(InputAction.CallbackContext ctx)
		{
			if (!CanAcceptInput()) return;

			string dir = ResolveDir();
			if (dir == null) return;

			string type = string.IsNullOrEmpty(m_ActiveMode) || m_ActiveMode == IntentTypes.None
				? IntentTypes.Move
				: m_ActiveMode;
			m_LastSentDir = dir;
			m_State.SubmitIntent(type, dir, m_Power);
			LocalIntentBus.Set(type, dir, m_Power);
		}

		private void OnCancelPerformed(InputAction.CallbackContext ctx)
		{
			if (!CanAcceptInput()) return;
			CancelAll();
		}

		// ─────────────────────────────────────────────────────────────
		// 強さ変更 コールバック
		// ─────────────────────────────────────────────────────────────

		// buttonWest 押下でパワーを 1→2→3→1 とサイクルさせる。
		private void OnPowerCyclePerformed(InputAction.CallbackContext ctx)
		{
			if (!CanAcceptInput()) return;
			if (!HasIntentToCharge()) return;

			int next = m_Power < 3 ? m_Power + 1 : 1;
			SetPowerAndResend(next);
		}

		// マウスホイールでパワーを増減する。
		private void OnScrollPerformed(InputAction.CallbackContext ctx)
		{
			if (!CanAcceptInput()) return;
			if (!HasIntentToCharge()) return;

			float scrollY = ctx.ReadValue<Vector2>().y;
			if (Mathf.Approximately(scrollY, 0f)) return;

			int next = scrollY > 0 ? Mathf.Min(3, m_Power + 1) : Mathf.Max(1, m_Power - 1);
			if (next == m_Power) return;
			SetPowerAndResend(next);
		}

		// ─────────────────────────────────────────────────────────────
		// Update 内：方向の連続追跡
		// ─────────────────────────────────────────────────────────────

		// スキルモード中、左スティック or マウスの向きをリアルタイム更新する。
		// スティックが動いていればスティック優先（クールダウンあり）、
		// 中立ならマウス位置を使う。
		private void HandleDirectionUpdate()
		{
			if (string.IsNullOrEmpty(m_ActiveMode)) return;
			if (m_ActiveMode == IntentTypes.Rest) return;

			string stickDir = ResolveStickDir();

			if (stickDir != null)
			{
				if (m_StickDirChangeCooldown > 0f) return;
				if (stickDir == m_LastSentDir) return;

				m_LastSentDir            = stickDir;
				m_StickDirChangeCooldown = m_StickDirChangeInterval;
			}
			else
			{
				string mouseDir = ResolveMouseDir();
				if (mouseDir == null || mouseDir == m_LastSentDir) return;
				m_LastSentDir = mouseDir;
			}

			m_State.SubmitIntent(m_ActiveMode, m_LastSentDir, m_Power);
			PublishLocal();
		}

		// ─────────────────────────────────────────────────────────────
		// 共通ユーティリティ
		// ─────────────────────────────────────────────────────────────

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

		private void CancelAll()
		{
			m_ActiveSkillAction = null;
			m_ActiveMode        = null;
			m_LastSentDir       = null;
			m_Power             = 1;
			m_State.SubmitIntent(IntentTypes.None, null, 1);
			LocalIntentBus.Clear();
		}

		private void PublishLocal()
		{
			if (string.IsNullOrEmpty(m_ActiveMode))
				LocalIntentBus.Clear();
			else
				LocalIntentBus.Set(m_ActiveMode, m_LastSentDir, m_Power);
		}

		// ─────────────────────────────────────────────────────────────
		// 方向リゾルバ
		// ─────────────────────────────────────────────────────────────

		// スティックが動いていればスティック優先、中立ならマウスから方向を返す。
		private string ResolveDir()
		{
			return ResolveStickDir() ?? ResolveMouseDir();
		}

		// Move Action（左スティック）の傾きから4方向文字列を解決する。
		// デッドゾーン以下なら null。
		private string ResolveStickDir()
		{
			if (m_MoveAction == null) return null;
			Vector2 stick = m_MoveAction.ReadValue<Vector2>();
			if (stick.magnitude < m_StickDeadZone) return null;

			return Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
				? (stick.x > 0 ? Directions.Right : Directions.Left)
				: (stick.y > 0 ? Directions.Up    : Directions.Down);
		}

		// Point Action（マウス位置）をワールド座標に投影し、
		// プレイヤー基準の4方向文字列を解決する。
		private string ResolveMouseDir()
		{
			if (m_WorldCamera == null || m_Board == null) return null;
			if (m_PointAction == null) return null;
			var me = m_State.Me;
			if (me == null) return null;

			Vector2 mousePos = m_PointAction.ReadValue<Vector2>();
			var ray = m_WorldCamera.ScreenPointToRay(mousePos);
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