using UnityEngine;
using UnityEngine.UI;


namespace GamblingAction.UI
{
    public class SuddenDeathPanelGlow : MonoBehaviour
    {
        // 表示対象の Image (未設定なら同一 GameObject の Image を使用)
        [SerializeField] private Image m_TargetImage;
        // 黒 <-> 赤 のパルス速度
        [SerializeField] private float m_PulseSpeed = 1.5f;
        // 発光（赤）色
        [SerializeField] private Color m_OnColor = new Color(1f, 0f, 0f, 1f);
        // 非発光（黒）色
        [SerializeField] private Color m_OffColor = new Color(0f, 0f, 0f, 1f);
        // アウトライン（発光っぽさ）の最大アルファ
        [SerializeField] private float m_OutlineMaxAlpha = 0.9f;
        // アウトラインの距離
        [SerializeField] private Vector2 m_OutlineDistance = new Vector2(6f, 6f);
        // 初期で有効にするか
        [SerializeField] private bool m_StartActive = false;

        private Outline m_Outline;
        private float m_Timer;
        private bool m_IsActive;

        private void Awake()
        {
            // Image を取得
            if (m_TargetImage == null)
                m_TargetImage = GetComponent<Image>();

            // Outline を取得/追加して初期設定
            m_Outline = GetComponent<Outline>();
            if (m_Outline == null)
            {
                // Outline を自動付与しておく（UI 用の簡易な発光表現）
                m_Outline = gameObject.AddComponent<Outline>();
            }
            m_Outline.effectDistance = m_OutlineDistance;
            m_Outline.useGraphicAlpha = true;

            m_Timer = 0f;
            m_IsActive = m_StartActive;
            if (!m_IsActive)
            {
                // 非アクティブ時は確実にオフカラーにしておく
                ApplyColor(0f);
            }
        }

        private void Update()
        {
            if (!m_IsActive || m_TargetImage == null) return;

            m_Timer += Time.deltaTime;
            // サイン波で滑らかに往復（0..1）
            float t = (Mathf.Sin(m_Timer * m_PulseSpeed * Mathf.PI * 2f) * 0.5f) + 0.5f;

            ApplyColor(t);
        }

        // t: 0 (off) .. 1 (on)
        private void ApplyColor(float t)
        {
            if (m_TargetImage != null)
            {
                m_TargetImage.enabled = m_IsActive;
                if (m_IsActive)
                {
                    m_TargetImage.color = Color.Lerp(m_OffColor, m_OnColor, t);
                }
            }

            if (m_Outline != null)
            {
                m_Outline.enabled = m_IsActive;
                if (m_IsActive)
                {
                    var oc = m_OnColor;
                    oc.a = Mathf.Lerp(0f, m_OutlineMaxAlpha, t);
                    m_Outline.effectColor = oc;
                }
            }
        }

        // 外部から発光を開始
        public void StartGlow()
        {
            m_IsActive = true;
            m_Timer = 0f;
        }

        // 外部から発光を停止（色をオフに戻す）
        public void StopGlow()
        {
            m_IsActive = false;
            ApplyColor(0f);
        }
    }
}