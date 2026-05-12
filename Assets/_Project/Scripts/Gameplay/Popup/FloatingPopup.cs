using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GamblingAction.Gameplay.PopupFx
{
	/// <summary>
	/// TMP ベースの汎用 popup 実装（数字 / 状態文字を共用）。
	/// 水平方向はアンカーに追従、垂直方向は上昇 tween で独立制御。
	/// </summary>
	public class FloatingPopup : MonoBehaviour, IPopupView
	{
		[SerializeField] private TMP_Text m_Label;
		[SerializeField] private CanvasGroup m_Group;
		[SerializeField] private Transform m_BillboardTarget;

		[Header("Animation")]
		[SerializeField] private float m_PopScale = 1.4f;
		[SerializeField] private float m_PopDuration = 0.18f;
		[SerializeField] private float m_RiseHeight = 1.0f;
		[SerializeField] private float m_RiseDuration = 0.9f;
		[SerializeField] private float m_FadeDelay = 0.35f;

		private Camera m_Cam;
		private Sequence m_Seq;
		private Action<IPopupView> m_OnFinished;

		private Transform m_Anchor;
		private Vector3 m_AnchorOffset;
		private float m_RiseY;          // 上昇分の Y オフセット（DOTween で 0 → m_RiseHeight）
		private bool m_Active;

		public GameObject GameObject => gameObject;

		public void Play(string text, Color color, Transform anchor, Vector3 anchorOffset, Action<IPopupView> onFinished)
		{
			m_OnFinished   = onFinished;
			m_Anchor       = anchor;
			m_AnchorOffset = anchorOffset;
			m_RiseY        = 0f;
			m_Active       = true;
			m_Cam          = Camera.main;

			if (m_Label != null)
			{
				m_Label.text  = text;
				m_Label.color = color;
			}
			if (m_Group != null) m_Group.alpha = 1f;
			transform.localScale = Vector3.zero;

			// 初期位置を即時セット（次フレーム LateUpdate を待たない）
			ApplyFollowPosition();

			m_Seq?.Kill();
			m_Seq = DOTween.Sequence();
			m_Seq.Append(transform.DOScale(m_PopScale, m_PopDuration).SetEase(Ease.OutBack));
			m_Seq.Append(transform.DOScale(1f, m_PopDuration * 0.6f).SetEase(Ease.OutQuad));
			// 上昇分は m_RiseY を tween（位置反映は LateUpdate でアンカーと合成）
			m_Seq.Join(DOTween.To(() => m_RiseY, v => m_RiseY = v, m_RiseHeight, m_RiseDuration).SetEase(Ease.OutQuad));
			if (m_Group != null)
			{
				var grp = m_Group;
				var fadeDur = m_RiseDuration - m_FadeDelay;
				m_Seq.Insert(m_FadeDelay, DOTween.To(() => grp.alpha, v => grp.alpha = v, 0f, fadeDur));
			}
			m_Seq.OnComplete(() =>
			{
				m_Active = false;
				m_OnFinished?.Invoke(this);
			});
		}

		private void LateUpdate()
		{
			if (m_Active) ApplyFollowPosition();

			if (m_BillboardTarget != null)
			{
				if (m_Cam == null) m_Cam = Camera.main;
				if (m_Cam != null) m_BillboardTarget.rotation = m_Cam.transform.rotation;
			}
		}

		private void ApplyFollowPosition()
		{
			if (m_Anchor == null) return;
			var basePos = m_Anchor.position + m_AnchorOffset;
			basePos.y += m_RiseY;
			transform.position = basePos;
		}

		private void OnDestroy()
		{
			m_Seq?.Kill();
		}
	}
}
