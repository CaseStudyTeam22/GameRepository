using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	public class StatusGauge : MonoBehaviour
	{
		[SerializeField] private Image m_FillImage;

		private float m_CurrentRatio = -1f;

		/// <summary>
		/// ゲージ値を設定する。値が変わっていない場合は Canvas Rebuild をスキップする。
		/// NOTE: m_FillImage の Image Type を Filled / Fill Method: Horizontal に設定してください。
		/// </summary>
		public void SetValue(float value, float maxValue)
		{
			if (m_FillImage == null) return;

			float ratio = maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);

			// 値が変わっていなければ Canvas Rebuild をスキップ
			if (Mathf.Approximately(m_CurrentRatio, ratio)) return;
			m_CurrentRatio = ratio;

			m_FillImage.fillAmount = ratio;
		}
	}
}