using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;
using UnityInput = UnityEngine.Input;
using UnityEngine.InputSystem;

namespace GamblingAction.Input
{
    /// <summary>
    /// プレイヤーの入力を処理するモジュール（Unity Input System 版）。
    ///
    /// 【コントローラーボタン割り当て（設計書準拠）】
    ///
    ///   番号  ボタン              機能
    ///   ──────────────────────────────────────────────
    ///   ①    左スティック        行動方向選択
    ///   ③    A（buttonSouth）   行動の強さ変更（1→2→3→1 サイクル）
    ///   ⑤    Y（buttonNorth）   決定
    ///   ⑦    LB（leftShoulder） スキル（攻撃）
    ///   ⑨    LT（leftTrigger）  突進
    ///   ⑩    RT（rightTrigger） 防御
    ///
    ///   B（buttonEast）はキャンセルとして引き続き使用。
    ///   RB・右スティック縦軸 はコントローラー割り当てから除外。
    ///   回復（R）はキーボードのみ対応。
    ///
    /// 【キーボード割り当て（変更なし）】
    ///   Q=突進 / W=スキル(攻撃) / E=防御 / R=回復
    ///   1・2・3 = パワー直接指定
    ///   マウスホイール = パワー増減
    ///   左クリック = 決定
    ///   Escape = キャンセル
    /// </summary>
    public class InputModule : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        // 定数
        // ─────────────────────────────────────────────────────────────

        /// <summary>スティック／トリガーのデッドゾーン（これ未満は無入力と見なす）</summary>
        private const float m_StickDeadZone = 0.5f;

        /// <summary>左スティック方向変更のクールダウン（秒）</summary>
        private const float m_StickDirChangeInterval = 0.15f;

        /// <summary>パワーの最小値</summary>
        private const int m_PowerMin = 1;

        /// <summary>パワーの最大値</summary>
        private const int m_PowerMax = 3;

        // ─────────────────────────────────────────────────────────────
        // シリアライズフィールド
        // ─────────────────────────────────────────────────────────────

        [FormerlySerializedAs("worldCamera")]
        [SerializeField] private Camera m_WorldCamera;

        // ─────────────────────────────────────────────────────────────
        // InputAction フィールド
        // ─────────────────────────────────────────────────────────────

        // ── スキル系 ─────────────────────────────────────────────────
        private InputAction m_PushAction;    // Q / LT  ⑨突進
        private InputAction m_AttackAction;  // W / LB  ⑦スキル（攻撃）
        private InputAction m_DefenseAction; // E / RT  ⑩防御
        private InputAction m_RestAction;    // R のみ（コントローラー割り当てなし）

        // ── 決定・キャンセル ─────────────────────────────────────────
        private InputAction m_ConfirmAction; // 左クリック / Y（buttonNorth） ⑤決定
        private InputAction m_CancelAction;  // Escape      / B（buttonEast）  キャンセル

        // ── パワー変更 ───────────────────────────────────────────────
        private InputAction m_PowerCycleAction; // A（buttonSouth） ③行動の強さ変更（サイクル）
        private InputAction m_ScrollAction;     // マウスホイール（キーボード用パワー増減）
        private InputAction m_Power1Action;     // 1 / Numpad1
        private InputAction m_Power2Action;     // 2 / Numpad2
        private InputAction m_Power3Action;     // 3 / Numpad3

        // ── スティック・マウス ───────────────────────────────────────
        private InputAction m_MoveStickAction;     // 左スティック ①行動方向選択
        private InputAction m_MousePositionAction; // マウス座標（連続読み取り）

        // ─────────────────────────────────────────────────────────────
        // 内部状態
        // ─────────────────────────────────────────────────────────────

        private IGameState m_State;
        private IBoardCoords m_Board;
        private Plane m_GroundPlane = new Plane(Vector3.up, Vector3.zero);

        /// <summary>現在押下中のスキル InputAction（離し判定用）</summary>
        private InputAction m_ActiveSkillAction;

        private string m_ActiveMode;
        private string m_LastSentDir;
        private int m_Power = m_PowerMin;

        private float m_StickDirChangeCooldown;

        // ─────────────────────────────────────────────────────────────
        // ライフサイクル
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildActions();
            RegisterCallbacks();
        }

        private void Start()
        {
            if (m_WorldCamera == null) m_WorldCamera = Camera.main;
            m_Board = BoardCoordsLocator.Current;
            m_State = GameStateLocator.Current;

            if (m_State == null) Debug.LogError("[Input] GameStateLocator.Current is null");
            if (m_Board == null) Debug.LogError("[Input] BoardCoordsLocator.Current is null");

            if (m_State != null) m_State.OnBeatChanged += HandleBeatChanged;

            EnableActions();
        }

        private void OnDestroy()
        {
            if (m_State != null) m_State.OnBeatChanged -= HandleBeatChanged;
            DisableActions();
            DisposeActions();
        }

        // ─────────────────────────────────────────────────────────────
        // InputAction 構築
        // ─────────────────────────────────────────────────────────────

        private void BuildActions()
        {
            // ── ⑨突進 / Q ──────────────────────────────────────────────
            m_PushAction = new InputAction("Push", InputActionType.Button);
            m_PushAction.AddBinding("<Keyboard>/q");
            m_PushAction.AddBinding("<Gamepad>/leftTrigger"); // LT(Xbox) / ZL(Switch Pro)

            // ── ⑦スキル（攻撃）/ W ─────────────────────────────────────
            m_AttackAction = new InputAction("Attack", InputActionType.Button);
            m_AttackAction.AddBinding("<Keyboard>/w");
            m_AttackAction.AddBinding("<Gamepad>/leftShoulder"); // LB(Xbox) / L(Switch Pro)

            // ── ⑩防御 / E ──────────────────────────────────────────────
            m_DefenseAction = new InputAction("Defense", InputActionType.Button);
            m_DefenseAction.AddBinding("<Keyboard>/e");
            m_DefenseAction.AddBinding("<Gamepad>/rightTrigger"); // RT(Xbox) / ZR(Switch Pro)

            // ── 回復 / R（キーボードのみ）──────────────────────────────
            // 設計書にコントローラー割り当てなし
            m_RestAction = new InputAction("Rest", InputActionType.Button);
            m_RestAction.AddBinding("<Keyboard>/r");

            // ── ⑤決定 / 左クリック / Y ─────────────────────────────────
            // Y ボタン = buttonNorth（Xbox Y / Switch Pro X に相当）
            m_ConfirmAction = new InputAction("Confirm", InputActionType.Button);
            m_ConfirmAction.AddBinding("<Mouse>/leftButton");
            m_ConfirmAction.AddBinding("<Gamepad>/buttonNorth"); // Y(Xbox) / X(Switch Pro)

            // ── キャンセル / Escape / B ─────────────────────────────────
            // 設計書記載なしだが UX 維持のため存続
            m_CancelAction = new InputAction("Cancel", InputActionType.Button);
            m_CancelAction.AddBinding("<Keyboard>/escape");
            m_CancelAction.AddBinding("<Gamepad>/buttonEast"); // B(Xbox) / A(Switch Pro)

            // ── ③行動の強さ変更 / A ─────────────────────────────────────
            // 押すたびに 1→2→3→1 とサイクルする
            m_PowerCycleAction = new InputAction("PowerCycle", InputActionType.Button);
            m_PowerCycleAction.AddBinding("<Gamepad>/buttonSouth"); // A(Xbox) / B(Switch Pro)

            // ── マウスホイール（パワー増減・キーボード用）───────────────
            m_ScrollAction = new InputAction("Scroll", InputActionType.Value, expectedControlType: "Vector2");
            m_ScrollAction.AddBinding("<Mouse>/scroll");

            // ── パワー数字キー（キーボード用）──────────────────────────
            m_Power1Action = new InputAction("Power1", InputActionType.Button);
            m_Power1Action.AddBinding("<Keyboard>/1");
            m_Power1Action.AddBinding("<Keyboard>/numpad1");

            m_Power2Action = new InputAction("Power2", InputActionType.Button);
            m_Power2Action.AddBinding("<Keyboard>/2");
            m_Power2Action.AddBinding("<Keyboard>/numpad2");

            m_Power3Action = new InputAction("Power3", InputActionType.Button);
            m_Power3Action.AddBinding("<Keyboard>/3");
            m_Power3Action.AddBinding("<Keyboard>/numpad3");

            // ── ①行動方向選択（左スティック）──────────────────────────
            m_MoveStickAction = new InputAction("MoveStick", InputActionType.Value, expectedControlType: "Vector2");
            m_MoveStickAction.AddBinding("<Gamepad>/leftStick");

            // ── マウス座標 ──────────────────────────────────────────────
            m_MousePositionAction = new InputAction("MousePosition", InputActionType.Value, expectedControlType: "Vector2");
            m_MousePositionAction.AddBinding("<Mouse>/position");
        }

        /// <summary>
        /// コールバックを登録する。
        /// ボタン系は performed / canceled で押し離しを検出。
        /// スティック・マウス位置は Update の ReadValue&lt;&gt;() で連続追跡。
        /// </summary>
        private void RegisterCallbacks()
        {
            // ── スキル系 ────────────────────────────────────────────────
            m_PushAction.performed += _ => OnSkillPressed(m_PushAction, IntentTypes.Push);
            m_PushAction.canceled += _ => OnSkillReleased(m_PushAction, IntentTypes.Push);

            m_AttackAction.performed += _ => OnSkillPressed(m_AttackAction, IntentTypes.Attack);
            m_AttackAction.canceled += _ => OnSkillReleased(m_AttackAction, IntentTypes.Attack);

            m_DefenseAction.performed += _ => OnSkillPressed(m_DefenseAction, IntentTypes.Defense);
            m_DefenseAction.canceled += _ => OnSkillReleased(m_DefenseAction, IntentTypes.Defense);

            m_RestAction.performed += _ => OnSkillPressed(m_RestAction, IntentTypes.Rest);
            m_RestAction.canceled += _ => OnSkillReleased(m_RestAction, IntentTypes.Rest);

            // ── ⑤決定・キャンセル ──────────────────────────────────────
            m_ConfirmAction.performed += _ => OnConfirm();
            m_CancelAction.performed += _ => { if (CanAcceptInput()) CancelAll(); };

            // ── ③行動の強さ変更（A ボタン：サイクル）──────────────────
            m_PowerCycleAction.performed += _ => OnPowerCycle();

            // ── パワー増減・数字キー（キーボード用）────────────────────
            m_ScrollAction.performed += ctx => OnScroll(ctx.ReadValue<Vector2>().y);
            m_Power1Action.performed += _ => OnPowerKey(1);
            m_Power2Action.performed += _ => OnPowerKey(2);
            m_Power3Action.performed += _ => OnPowerKey(3);
        }

        private void EnableActions()
        {
            m_PushAction.Enable();
            m_AttackAction.Enable();
            m_DefenseAction.Enable();
            m_RestAction.Enable();
            m_ConfirmAction.Enable();
            m_CancelAction.Enable();
            m_PowerCycleAction.Enable();
            m_ScrollAction.Enable();
            m_Power1Action.Enable();
            m_Power2Action.Enable();
            m_Power3Action.Enable();
            m_MoveStickAction.Enable();
            m_MousePositionAction.Enable();
        }

        private void DisableActions()
        {
            m_PushAction?.Disable();
            m_AttackAction?.Disable();
            m_DefenseAction?.Disable();
            m_RestAction?.Disable();
            m_ConfirmAction?.Disable();
            m_CancelAction?.Disable();
            m_PowerCycleAction?.Disable();
            m_ScrollAction?.Disable();
            m_Power1Action?.Disable();
            m_Power2Action?.Disable();
            m_Power3Action?.Disable();
            m_MoveStickAction?.Disable();
            m_MousePositionAction?.Disable();
        }

        private void DisposeActions()
        {
            m_PushAction?.Dispose();
            m_AttackAction?.Dispose();
            m_DefenseAction?.Dispose();
            m_RestAction?.Dispose();
            m_ConfirmAction?.Dispose();
            m_CancelAction?.Dispose();
            m_PowerCycleAction?.Dispose();
            m_ScrollAction?.Dispose();
            m_Power1Action?.Dispose();
            m_Power2Action?.Dispose();
            m_Power3Action?.Dispose();
            m_MoveStickAction?.Dispose();
            m_MousePositionAction?.Dispose();
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
            m_ActiveSkillAction = null;
            m_ActiveMode = null;
            m_LastSentDir = null;
            m_Power = m_PowerMin;
            m_StickDirChangeCooldown = 0f;
            LocalIntentBus.Clear();
        }

        // ─────────────────────────────────────────────────────────────
        // 毎フレーム処理（連続値のみ）
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

            // 左スティック or マウスで方向をリアルタイム更新する
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
        // スキルボタン共通ハンドラ
        // ─────────────────────────────────────────────────────────────

        /// <summary>スキルボタンが押された瞬間に呼ばれる（キーボード・コントローラー共通）</summary>
        private void OnSkillPressed(InputAction action, string mode)
        {
            if (!CanAcceptInput()) return;
            if (m_ActiveMode == mode) return; // 同モード二重押しは無視

            m_ActiveSkillAction = action;
            m_ActiveMode = mode;
            m_Power = m_PowerMin;

            if (mode == IntentTypes.Rest)
            {
                m_LastSentDir = null;
                m_State.SubmitIntent(IntentTypes.Rest, null, m_Power);
                PublishLocal();
                return;
            }

            // 押した時点の向きを解決してインテントを送信する
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

        /// <summary>スキルボタンが離された瞬間に呼ばれる。自身が起動したアクションのみ処理する</summary>
        private void OnSkillReleased(InputAction action, string mode)
        {
            if (m_ActiveSkillAction != action) return;
            if (m_ActiveMode != mode) return;
            CancelAll();
        }

        // ─────────────────────────────────────────────────────────────
        // ⑤決定ハンドラ
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Y ボタン or 左クリックで現在の向きを確定送信する。
        /// スキルモードが未選択の場合は Move として送信する。
        /// </summary>
        private void OnConfirm()
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

        // ─────────────────────────────────────────────────────────────
        // ③行動の強さ変更ハンドラ
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// A ボタン押下でパワーを 1→2→3→1 とサイクルさせる。
        /// インテントがアクティブであれば即座に再送信する。
        /// </summary>
        private void OnPowerCycle()
        {
            if (!CanAcceptInput()) return;
            if (!HasIntentToCharge()) return;

            // 1→2→3→1 のサイクル
            int next = m_Power < m_PowerMax ? m_Power + 1 : m_PowerMin;
            SetPowerAndResend(next);
        }

        /// <summary>マウスホイールでパワーを増減する（キーボード用）</summary>
        private void OnScroll(float scrollY)
        {
            if (!CanAcceptInput()) return;
            if (!HasIntentToCharge()) return;
            if (Mathf.Approximately(scrollY, 0f)) return;

            int next = scrollY > 0
                ? Mathf.Min(m_PowerMax, m_Power + 1)
                : Mathf.Max(m_PowerMin, m_Power - 1);
            if (next == m_Power) return;
            SetPowerAndResend(next);
        }

        /// <summary>数字キーでパワーを直接指定する（キーボード用）</summary>
        private void OnPowerKey(int power)
        {
            if (!CanAcceptInput()) return;
            if (!HasIntentToCharge()) return;
            if (power == m_Power) return;
            SetPowerAndResend(power);
        }

        // ─────────────────────────────────────────────────────────────
        // Update 内：方向の連続追跡
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// スキルモードがアクティブな間、左スティック or マウスの向きをリアルタイム更新する。
        /// スティックが動いていればスティック優先（クールダウンあり）、
        /// スティックが中立の場合はマウス位置を使用する。
        /// </summary>
        private void HandleDirectionUpdate()
        {
            if (string.IsNullOrEmpty(m_ActiveMode)) return;
            if (m_ActiveMode == IntentTypes.Rest) return;

            string stickDir = ResolveStickDir();

            if (stickDir != null)
            {
                // スティック入力あり：クールダウン中はスキップ
                if (m_StickDirChangeCooldown > 0f) return;
                if (stickDir == m_LastSentDir) return;

                m_LastSentDir = stickDir;
                m_StickDirChangeCooldown = m_StickDirChangeInterval;
            }
            else
            {
                // スティックが中立：マウス位置で更新
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
            m_ActiveMode = null;
            m_LastSentDir = null;
            m_Power = m_PowerMin;
            m_State.SubmitIntent(IntentTypes.None, null, m_PowerMin);
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

        /// <summary>スティックが動いていればスティック、中立ならマウスから方向を返す</summary>
        private string ResolveDir()
        {
            return ResolveStickDir() ?? ResolveMouseDir();
        }

        /// <summary>
        /// 左スティックの傾きから4方向文字列を解決する。
        /// デッドゾーン以下の場合は null を返す。
        /// </summary>
        private string ResolveStickDir()
        {
            Vector2 stick = m_MoveStickAction.ReadValue<Vector2>();
            if (stick.magnitude < m_StickDeadZone) return null;

            return Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                ? (stick.x > 0 ? Directions.Right : Directions.Left)
                : (stick.y > 0 ? Directions.Up : Directions.Down);
        }

        /// <summary>
        /// マウス位置をワールド座標に投影し、プレイヤー基準の4方向文字列を解決する。
        /// 投影に失敗した場合は null を返す。
        /// </summary>
        private string ResolveMouseDir()
        {
            if (m_WorldCamera == null || m_Board == null) return null;
            var me = m_State.Me;
            if (me == null) return null;

            Vector2 mousePos = m_MousePositionAction.ReadValue<Vector2>();
            var ray = m_WorldCamera.ScreenPointToRay(mousePos);
            if (!m_GroundPlane.Raycast(ray, out float enter)) return null;
            Vector3 hit = ray.GetPoint(enter);

            Vector3 mePos = m_Board.GridToWorld(me.X, me.Y);
            float dx = hit.x - mePos.x;
            float dz = hit.z - mePos.z;

            return Mathf.Abs(dx) > Mathf.Abs(dz)
                ? (dx > 0 ? Directions.Right : Directions.Left)
                : (dz > 0 ? Directions.Up : Directions.Down);
        }
    }
}