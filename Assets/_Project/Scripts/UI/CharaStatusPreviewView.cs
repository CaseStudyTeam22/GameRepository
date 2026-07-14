using GamblingAction.Core;
using GamblingAction.Core.Dto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
    public class CharaStatusPreviewView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text m_NameText;
        [SerializeField] private TMP_Text m_StaminaText;
        [SerializeField] private TMP_Text m_PushText;
        [SerializeField] private TMP_Text m_DefenseText;
        [SerializeField] private TMP_Text m_MoneyText;       // 初期所持金
        [SerializeField] private TMP_Text m_PushCostText;    // 突進消費チップ
        [SerializeField] private TMP_Text m_DefenseCostText; // 防御消費チップ
        [SerializeField] private TMP_Text m_SkillCostText;   // スキル消費チップ
        [SerializeField] private TMP_Text m_SkillDescriptionText; // スキル効果説明

        [Header("Gauges")]
        [SerializeField] private StatusGauge m_StaminaGauge;
        [SerializeField] private StatusGauge m_PushGauge;
        [SerializeField] private StatusGauge m_DefenseGauge;
        [SerializeField] private StatusGauge m_MoneyGauge;       // 初期所持金
        [SerializeField] private StatusGauge m_PushCostGauge;    // 突進消費チップ
        [SerializeField] private StatusGauge m_DefenseCostGauge; // 防御消費チップ
        [SerializeField] private StatusGauge m_SkillCostGauge;   // スキル消費チップ

        [Header("Skill Icon")]
        [SerializeField] private Image m_SkillIconImage;
        [SerializeField] private SkillDatabase m_SkillDatabase;

        [Header("Gauge Max Values")]
        [SerializeField] private float m_MaxStaminaView = 10f;
        [SerializeField] private float m_MaxPushView = 7f;
        [SerializeField] private float m_MaxDefenseView = 3f;
        [SerializeField] private float m_MaxMoneyView = 12000f;
        [SerializeField] private float m_MaxPushCostView = 15f;
        [SerializeField] private float m_MaxDefenseCostView = 5f;
        [SerializeField] private float m_MaxSkillCostView = 15f;

        [Header("Animation")]
        [SerializeField] private float m_AnimationDuration = 0.3f;

        [ContextMenu("Test Status")]
        private void TestStatus()
        {
            SetStatus(new CharaDataMessage
            {
                Name = "Doctor",
                MaxStamina = 5,
                PushPower = 2,
                DefensePower = 3,
                InitMoney = 10000,
                PushCost = new[] { 3, 5, 9 },
                DefenseCost = new[] { 2, 2, 2 },
                Skills = new CharaSkillDataMessage { Id = "heal_instant", StaminaRec = 2, ChipCost = 3 },
                SkillDescription = "スタミナを2回復する。\nコスト: 3チップ"
            }, animate: true);
        }

        /// <summary>
        /// キャラクターのステータスを表示する。
        /// </summary>
        /// <param name="data">表示するキャラクターデータ</param>
        /// <param name="animate">true のとき、ゲージをアニメーションして更新する</param>
        public void SetStatus(CharaDataMessage data, bool animate = false)
        {
            if (data == null)
            {
                Clear();
                return;
            }

            // テキスト更新（即時）
            if (m_NameText != null)
                m_NameText.text = data.Name;

            if (m_StaminaText != null)
                m_StaminaText.text = $"スタミナ {data.MaxStamina}";

            if (m_PushText != null)
                m_PushText.text = $"突進 {data.PushPower}";

            if (m_DefenseText != null)
                m_DefenseText.text = $"防御 {data.DefensePower}";

            if (m_MoneyText != null)
                m_MoneyText.text = $"¥{data.InitMoney:N0}";

            if (m_SkillDescriptionText != null)
                m_SkillDescriptionText.text = data.SkillDescription ?? "";

            int pushCost    = data.PushCost    != null && data.PushCost.Length    > 0 ? data.PushCost[0]    : 0;
            int defenseCost = data.DefenseCost != null && data.DefenseCost.Length > 0 ? data.DefenseCost[0] : 0;
            int skillCost   = data.Skills != null ? data.Skills.ChipCost : 0;

            if (m_PushCostText != null)
                m_PushCostText.text = $"突進コスト {pushCost}";

            if (m_DefenseCostText != null)
                m_DefenseCostText.text = $"防御コスト {defenseCost}";

            if (m_SkillCostText != null)
                m_SkillCostText.text = $"スキルコスト {skillCost}";

            // スキルアイコン更新（SkillDatabase が割り当てられている場合のみ）
            if (m_SkillIconImage != null && m_SkillDatabase != null && data.Skills != null)
            {
                var icon = m_SkillDatabase.GetIcon(data.Skills.Id);
                if (icon != null)
                    m_SkillIconImage.sprite = icon;
            }

            // ゲージ更新（アニメーションあり / なし）
            if (animate)
            {
                m_StaminaGauge?.SetValueAnimated(data.MaxStamina, m_MaxStaminaView, m_AnimationDuration);
                m_PushGauge?.SetValueAnimated(data.PushPower, m_MaxPushView, m_AnimationDuration);
                m_DefenseGauge?.SetValueAnimated(data.DefensePower, m_MaxDefenseView, m_AnimationDuration);
                m_MoneyGauge?.SetValueAnimated(data.InitMoney, m_MaxMoneyView, m_AnimationDuration);
                m_PushCostGauge?.SetValueAnimated(pushCost, m_MaxPushCostView, m_AnimationDuration);
                m_DefenseCostGauge?.SetValueAnimated(defenseCost, m_MaxDefenseCostView, m_AnimationDuration);
                m_SkillCostGauge?.SetValueAnimated(skillCost, m_MaxSkillCostView, m_AnimationDuration);
            }
            else
            {
                m_StaminaGauge?.SetValue(data.MaxStamina, m_MaxStaminaView);
                m_PushGauge?.SetValue(data.PushPower, m_MaxPushView);
                m_DefenseGauge?.SetValue(data.DefensePower, m_MaxDefenseView);
                m_MoneyGauge?.SetValue(data.InitMoney, m_MaxMoneyView);
                m_PushCostGauge?.SetValue(pushCost, m_MaxPushCostView);
                m_DefenseCostGauge?.SetValue(defenseCost, m_MaxDefenseCostView);
                m_SkillCostGauge?.SetValue(skillCost, m_MaxSkillCostView);
            }
        }

        public void Clear()
        {
            if (m_NameText != null)            m_NameText.text            = "";
            if (m_StaminaText != null)         m_StaminaText.text         = "スタミナ";
            if (m_PushText != null)            m_PushText.text            = "突進";
            if (m_DefenseText != null)         m_DefenseText.text         = "防御";
            if (m_MoneyText != null)           m_MoneyText.text           = "資金";
            if (m_PushCostText != null)        m_PushCostText.text        = "突進コスト";
            if (m_DefenseCostText != null)     m_DefenseCostText.text     = "防御コスト";
            if (m_SkillCostText != null)       m_SkillCostText.text       = "スキルコスト";
            if (m_SkillDescriptionText != null)m_SkillDescriptionText.text = "";

            m_StaminaGauge?.SetValue(0, 1);
            m_PushGauge?.SetValue(0, 1);
            m_DefenseGauge?.SetValue(0, 1);
            m_MoneyGauge?.SetValue(0, 1);
            m_PushCostGauge?.SetValue(0, 1);
            m_DefenseCostGauge?.SetValue(0, 1);
            m_SkillCostGauge?.SetValue(0, 1);
        }
    }
}