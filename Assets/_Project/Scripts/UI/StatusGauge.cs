using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	public class StatusGauge : MonoBehaviour
	{
		[SerializeField] private Image m_FillImage;

		public void SetValue(float value, float maxValue)
		{
			if (m_FillImage == null) return;

			float ratio = maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);

			// Sliced 画像の伸縮をアンカーのみで制御（マージンは親のAreaオブジェクトが担保するため、オフセットはゼロ）
			m_FillImage.rectTransform.anchorMin = new Vector2(0f, 0f);
			m_FillImage.rectTransform.anchorMax = new Vector2(ratio, 1f);
			m_FillImage.rectTransform.offsetMin = Vector2.zero;
			m_FillImage.rectTransform.offsetMax = Vector2.zero;
		}
	}
}