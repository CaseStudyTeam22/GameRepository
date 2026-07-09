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

            if (maxValue <= 0f)
            {
                m_FillImage.fillAmount = 0f;
                return;
            }

            m_FillImage.fillAmount = Mathf.Clamp01(value / maxValue);
        }
    }
}