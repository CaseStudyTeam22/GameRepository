using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	/// <summary>
	/// ボタン（Selectable）が「選択」または「マウスホバー」されたときに、
	/// 拡大アニメーションとふちの発光（Outline）を行うコンポーネント。
	///
	/// 【使い方】
	///   強調したいボタンの GameObject にアタッチするだけ。
	///   - コントローラー：EventSystem.SetSelectedGameObject でフォーカスされると反応（OnSelect）
	///   - マウス：カーソルを乗せると反応（OnPointerEnter）
	///
	/// 【仕組み】
	///   - ISelectHandler / IDeselectHandler   : コントローラー選択の ON/OFF
	///   - IPointerEnterHandler / IPointerExitHandler : マウスホバーの ON/OFF
	///   選択状態 or ホバー状態のどちらかが立っていれば強調表示する。
	///   Outline コンポーネントが無ければ自動で追加する。
	/// </summary>
	[RequireComponent(typeof(RectTransform))]
	public class ButtonFocusHighlight : MonoBehaviour,
		ISelectHandler, IDeselectHandler,
		IPointerEnterHandler, IPointerExitHandler
	{
		// ─────────────────────────────────────────────────────────────
		// シリアライズフィールド
		// ─────────────────────────────────────────────────────────────

		[Header("拡大")]
		[Tooltip("選択時の拡大率（1.1 = 10% 拡大）")]
		[SerializeField] private float m_SelectedScale = 1.1f;

		[Tooltip("拡大・縮小アニメーションの速さ（大きいほど速い）")]
		[SerializeField] private float m_ScaleSpeed = 12f;

		[Header("ふち発光（Outline）")]
		[Tooltip("発光させる色")]
		[SerializeField] private Color m_GlowColor = new Color(1f, 0.85f, 0.2f, 1f);

		[Tooltip("ふちの太さ（ピクセル）")]
		[SerializeField] private Vector2 m_GlowDistance = new Vector2(4f, 4f);

		[Tooltip("発光のパルス（明滅）速度。0 で明滅なし")]
		[SerializeField] private float m_PulseSpeed = 4f;

		// ─────────────────────────────────────────────────────────────
		// 内部状態
		// ─────────────────────────────────────────────────────────────

		private RectTransform m_Rect;
		private Outline       m_Outline;
		private Button        m_Button;

		/// <summary>元のスケール（戻すとき用に保持）</summary>
		private Vector3 m_BaseScale;

		/// <summary>現在の目標スケール（強調中は拡大、非強調時は等倍）</summary>
		private Vector3 m_TargetScale;

		/// <summary>コントローラーで選択中か</summary>
		private bool m_IsSelected;

		/// <summary>マウスがホバー中か</summary>
		private bool m_IsHovered;

		/// <summary>選択 or ホバーのどちらかが立っていれば強調表示する。</summary>
		private bool IsHighlighted => m_IsSelected || m_IsHovered;

        /// <summary>選択しているリスクの表示フラグ</summary>
        private bool m_LastNotifiedHighlighted;

        public event System.Action<ButtonFocusHighlight, bool> HighlightChanged;

        // ─────────────────────────────────────────────────────────────
        // ライフサイクル
        // ─────────────────────────────────────────────────────────────

        private void Awake()
		{
			m_Rect      = GetComponent<RectTransform>();
			m_Button    = GetComponent<Button>();
			m_BaseScale = m_Rect.localScale;
			m_TargetScale = m_BaseScale;

			// Outline が無ければ追加する。発光制御のため最初は無効化しておく。
			m_Outline = GetComponent<Outline>();
			if (m_Outline == null) m_Outline = gameObject.AddComponent<Outline>();

			m_Outline.effectColor    = m_GlowColor;
			m_Outline.effectDistance = m_GlowDistance;
			m_Outline.enabled        = false;
		}

		private void OnDisable()
		{
			// 無効化されたとき強制的に元へ戻す（強調状態が残らないように）
			m_IsSelected   = false;
			m_IsHovered    = false;
			m_TargetScale  = m_BaseScale;
            NotifyHighlightChanged(false);
            if (m_Rect != null)    m_Rect.localScale = m_BaseScale;
			if (m_Outline != null) m_Outline.enabled = false;
		}

		// ─────────────────────────────────────────────────────────────
		// 選択・選択解除ハンドラ（コントローラー）
		// ─────────────────────────────────────────────────────────────

		/// <summary>EventSystem に選択された瞬間に呼ばれる。</summary>
		public void OnSelect(BaseEventData eventData)
		{
			m_IsSelected = true;
			RefreshHighlight();
		}

		/// <summary>選択が外れた瞬間に呼ばれる。</summary>
		public void OnDeselect(BaseEventData eventData)
		{
			m_IsSelected = false;
			RefreshHighlight();
		}

		// ─────────────────────────────────────────────────────────────
		// ホバーハンドラ（マウス）
		// ─────────────────────────────────────────────────────────────

		/// <summary>マウスカーソルが乗った瞬間に呼ばれる。</summary>
		public void OnPointerEnter(PointerEventData eventData)
		{
			m_IsHovered = true;
			RefreshHighlight();
		}

		/// <summary>マウスカーソルが外れた瞬間に呼ばれる。</summary>
		public void OnPointerExit(PointerEventData eventData)
		{
			m_IsHovered = false;
			RefreshHighlight();
		}

		// ─────────────────────────────────────────────────────────────
		// 強調表示の更新
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// 選択・ホバー状態に応じて拡大目標と発光の ON/OFF を更新する。
		/// 押せない（interactable == false）ボタンは強調しない。
		/// </summary>
		private void RefreshHighlight()
		{
			bool active = IsHighlighted && (m_Button == null || m_Button.interactable);

			m_TargetScale = active ? m_BaseScale * m_SelectedScale : m_BaseScale;
			if (m_Outline != null) m_Outline.enabled = active;

            NotifyHighlightChanged(active);
        }


        /// <summary>
        /// ふちが出ているかを通知する
        /// </summary>
        private void NotifyHighlightChanged(bool highlighted)
        {
            if (m_LastNotifiedHighlighted == highlighted) return;

            m_LastNotifiedHighlighted = highlighted;
            HighlightChanged?.Invoke(this, highlighted);
        }
        
		// ─────────────────────────────────────────────────────────────
        // 毎フレーム処理
        // ─────────────────────────────────────────────────────────────

        private void Update()
		{
			// 拡大・縮小をなめらかに補間する
			m_Rect.localScale = Vector3.Lerp(
				m_Rect.localScale,
				m_TargetScale,
				m_ScaleSpeed * Time.deltaTime
			);

			// 強調中はふちの色をパルス（明滅）させる
			bool active = IsHighlighted && (m_Button == null || m_Button.interactable);
			if (active && m_Outline != null && m_PulseSpeed > 0f)
			{
				// 0.5〜1.0 の範囲でアルファを揺らす
				float alpha = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * m_PulseSpeed));
				Color c = m_GlowColor;
				c.a = alpha;
				m_Outline.effectColor = c;
			}
		}
	}
}