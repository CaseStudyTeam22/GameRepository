using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	public class StatusGauge : MonoBehaviour
	{
		[SerializeField] private Image m_FillImage;

		private float m_CurrentRatio = -1f;
		private Tweener m_Tween;

		/// <summary>
		/// ゲージ値を即時更新する（アニメーションなし）。
		/// NOTE: m_FillImage の Image Type を Filled / Fill Method: Horizontal に設定してください。
		/// </summary>
		public void SetValue(float value, float maxValue)
		{
			if (m_FillImage == null) return;

			float ratio = maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);

			// 値が変わっていなければ Canvas Rebuild をスキップ
			if (Mathf.Approximately(m_CurrentRatio, ratio)) return;
			m_CurrentRatio = ratio;

			m_Tween?.Kill();
			m_FillImage.fillAmount = ratio;
		}

		/// <summary>
		/// ゲージ値をアニメーションして更新する。
		/// </summary>
		public void SetValueAnimated(float value, float maxValue, float duration)
		{
			if (m_FillImage == null) return;

			float ratio = maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);

			if (Mathf.Approximately(m_CurrentRatio, ratio)) return;
			m_CurrentRatio = ratio;

			m_Tween?.Kill();
			m_Tween = DOTween.To(
				() => m_FillImage.fillAmount,
				v => m_FillImage.fillAmount = v,
				ratio,
				duration
			).SetEase(Ease.OutQuad);
		}

		private void OnDestroy()
		{
			m_Tween?.Kill();
		}
	}
}