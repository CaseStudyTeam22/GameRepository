using GamblingAction.Core.Dto;
using TMPro;
using UnityEngine;

namespace GamblingAction.UI
{
    public class CharaStatusPreviewView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text m_NameText;
        [SerializeField] private TMP_Text m_StaminaText;
        [SerializeField] private TMP_Text m_PushText;
        [SerializeField] private TMP_Text m_DefenseText;

        [Header("Gauges")]
        [SerializeField] private StatusGauge m_StaminaGauge;
        [SerializeField] private StatusGauge m_PushGauge;
        [SerializeField] private StatusGauge m_DefenseGauge;

        [Header("Gauge Max Values")]
        [SerializeField] private float m_MaxStaminaView = 10f;
        [SerializeField] private float m_MaxPushView = 7f;
        [SerializeField] private float m_MaxDefenseView = 3f;
        [ContextMenu("Test Status")]
        private void TestStatus()
        {
            SetStatus(new CharaDataMessage
            {
                Name = "Doctor",
                MaxStamina = 5,
                PushPower = 2,
                DefensePower = 3
            });
        }

        public void SetStatus(CharaDataMessage data)
        {
            Debug.Log("★★ SetStatus呼ばれた ★★");
            if (data == null)
            {
                Clear();
                return;
            }

            if (m_NameText != null)
                m_NameText.text = data.Name;

            if (m_StaminaText != null)
                m_StaminaText.text = $"スタミナ {data.MaxStamina}";

            if (m_PushText != null)
                m_PushText.text = $"突進 {data.PushPower}";

            if (m_DefenseText != null)
                m_DefenseText.text = $"防御 {data.DefensePower}";

            if (m_StaminaGauge != null)
                m_StaminaGauge.SetValue(data.MaxStamina, m_MaxStaminaView);

            if (m_PushGauge != null)
                m_PushGauge.SetValue(data.PushPower, m_MaxPushView);

            if (m_DefenseGauge != null)
                m_DefenseGauge.SetValue(data.DefensePower, m_MaxDefenseView);
        }

        public void Clear()
        {
            if (m_NameText != null)
                m_NameText.text = "";

            if (m_StaminaText != null)
                m_StaminaText.text = "スタミナ";

            if (m_PushText != null)
                m_PushText.text = "突進";

            if (m_DefenseText != null)
                m_DefenseText.text = "防御";

            if (m_StaminaGauge != null)
                m_StaminaGauge.SetValue(0, 1);

            if (m_PushGauge != null)
                m_PushGauge.SetValue(0, 1);

            if (m_DefenseGauge != null)
                m_DefenseGauge.SetValue(0, 1);
        }
    }
}