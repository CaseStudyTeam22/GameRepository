using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	/// <summary>
	/// ボタン（Selectable）が選択されたときに、
	/// 拡大アニメーションとふちの発光（Outline）を行うコンポーネント。
	///
	/// 【使い方】
	///   選択時に強調したいボタンの GameObject にアタッチするだけ。
	///   EventSystem.SetSelectedGameObject でフォーカスされると自動で反応する。
	///   （マウスホバーではなく「選択」状態に反応する点に注意）
	///
	/// 【仕組み】
	///   - ISelectHandler   : 選択された瞬間に拡大＋発光 ON
	///   - IDeselectHandler : 選択が外れた瞬間に元へ戻す
	///   Outline コンポーネントが無ければ自動で追加する。
	/// </summary>
	[RequireComponent(typeof(RectTransform))]
	public class ButtonFocusHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
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

		/// <summary>元のスケール（戻すとき用に保持）</summary>
		private Vector3 m_BaseScale;

		/// <summary>現在の目標スケール（選択中は拡大、非選択時は等倍）</summary>
		private Vector3 m_TargetScale;

		/// <summary>選択中かどうか</summary>
		private bool m_IsSelected;

		// ─────────────────────────────────────────────────────────────
		// ライフサイクル
		// ─────────────────────────────────────────────────────────────

		private void Awake()
		{
			m_Rect      = GetComponent<RectTransform>();
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
			// 無効化されたとき強制的に元へ戻す（選択状態が残らないように）
			m_IsSelected      = false;
			m_TargetScale     = m_BaseScale;
			if (m_Rect != null)    m_Rect.localScale = m_BaseScale;
			if (m_Outline != null) m_Outline.enabled = false;
		}

		// ─────────────────────────────────────────────────────────────
		// 選択・選択解除ハンドラ
		// ─────────────────────────────────────────────────────────────

		/// <summary>EventSystem に選択された瞬間に呼ばれる。</summary>
		public void OnSelect(BaseEventData eventData)
		{
			m_IsSelected  = true;
			m_TargetScale = m_BaseScale * m_SelectedScale;
			if (m_Outline != null) m_Outline.enabled = true;
		}

		/// <summary>選択が外れた瞬間に呼ばれる。</summary>
		public void OnDeselect(BaseEventData eventData)
		{
			m_IsSelected  = false;
			m_TargetScale = m_BaseScale;
			if (m_Outline != null) m_Outline.enabled = false;
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

			// 選択中はふちの色をパルス（明滅）させる
			if (m_IsSelected && m_Outline != null && m_PulseSpeed > 0f)
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
