using System.Collections;
using DG.Tweening;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
    /// <summary>
    /// 4拍目に画面中央へ "ACTION!" を大きく表示する演出。
    /// ゼンレスゾーンゼロの追加攻撃 "FIRE" 演出を参考に、
    ///   1. テキストがスケールイン（大→標準）しながらフェードイン
    ///   2. 短時間保持
    ///   3. スケールアウト＋フェードアウト
    /// という 3 フェーズで再生する。
    ///
    /// 【重要】本コンポーネント自身が IGameState.OnBeatChanged を購読するため、
    ///   FlowPanelView 等への変更は不要。
    ///   Unity GameObject は常に active のままとし、CanvasGroup.alpha で表示/非表示を切り替える。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ActionBannerView : MonoBehaviour
    {
        public static ActionBannerView Instance { get; private set; }

        [Header("References")]
        [SerializeField, Tooltip("'ACTION!' テキスト")]
        private TMP_Text m_Label;
        [SerializeField, Tooltip("背景フラッシュ用の半透明 Image（任意）")]
        private Image m_BackgroundFlash;

        [Header("Text")]
        [SerializeField] private string m_BannerText = "ACTION!";
        [SerializeField, Tooltip("フォントサイズ（TMP Canvas ローカル単位）。Canvas のスケールに依存するため環境によって大きく異なる。\nWorld Space Canvas では数万単位になることがある")]
        private float m_FontSize = 18000f;

        [Header("Timing")]
        [SerializeField, Tooltip("スケールイン時間（秒）")]
        private float m_ScaleInDuration = 0.12f;
        [SerializeField, Tooltip("表示保持時間（秒）")]
        private float m_HoldDuration = 0.25f;
        [SerializeField, Tooltip("フェードアウト時間（秒）")]
        private float m_FadeOutDuration = 0.22f;

        [Header("Scale")]
        [SerializeField, Tooltip("出現開始時のスケール（大きいほど迫力）")]
        private float m_StartScale = 1.6f;
        [SerializeField, Tooltip("保持・フェードアウト中の最終スケール")]
        private float m_EndScale = 0.85f;

        [Header("Color")]
        [SerializeField, Tooltip("文字の塗り色")]
        private Color m_TextColor = new Color(1f, 0.92f, 0.2f, 1f); // ゴールド
        [SerializeField, Tooltip("アウトラインの色")]
        private Color m_OutlineColor = new Color(0.15f, 0.05f, 0.0f, 1f); // 濃い茶（見やすいアウトライン）
        [SerializeField, Tooltip("アウトラインの太さ（0〜1）。小さいほど細い。0 で非表示。")]
        [Range(0f, 1f)] private float m_OutlineWidth = 0.06f;
        [SerializeField, Tooltip("背景フラッシュの最大アルファ")]
        [Range(0f, 1f)] private float m_FlashMaxAlpha = 0.35f;

        private CanvasGroup m_CanvasGroup;
        private RectTransform m_LabelRect;
        private IGameState m_State;
        private Coroutine m_PlayCo;

        // ── ライフサイクル ──────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_Label != null)
            {
                m_LabelRect      = m_Label.GetComponent<RectTransform>();
                m_Label.fontSize     = m_FontSize;
                m_Label.color        = m_TextColor;
                m_Label.outlineColor = m_OutlineColor;
                m_Label.outlineWidth = m_OutlineWidth;
            }

            // alpha=0 で非表示。GameObject は active 維持（コルーチンを止めないため）。
            m_CanvasGroup.alpha = 0f;
            if (m_BackgroundFlash != null)
            {
                var fc = m_BackgroundFlash.color;
                fc.a = 0f;
                m_BackgroundFlash.color = fc;
            }
        }

        private void Start()
        {
            m_State = GameStateLocator.Current;
            if (m_State != null)
                m_State.OnBeatChanged += HandleBeatChanged;
            else
                Debug.LogWarning("[ActionBannerView] GameStateLocator.Current is null; ACTION! banner won't fire.");
        }

        private void OnDestroy()
        {
            if (m_State != null) m_State.OnBeatChanged -= HandleBeatChanged;
            if (Instance == this) Instance = null;
        }

        // ── イベントハンドラ ────────────────────────────────────
        private void HandleBeatChanged()
        {
            if (m_State == null) return;
            if (m_State.Phase != EGamePhase.Battle) return;
            if (m_State.CurrentBeat == 4)
                Play();
        }

        // ── 外部からも呼べるエントリーポイント ─────────────────────
        public void Play()
        {
            if (m_PlayCo != null)
            {
                StopCoroutine(m_PlayCo);
                DOTween.Kill(this);
                m_CanvasGroup.alpha = 0f;
                if (m_BackgroundFlash != null) { var fc = m_BackgroundFlash.color; fc.a = 0f; m_BackgroundFlash.color = fc; }
            }
            m_PlayCo = StartCoroutine(PlaySequence());
        }

        // ── 演出コルーチン ──────────────────────────────────────
        private IEnumerator PlaySequence()
        {
            if (m_Label != null) m_Label.text = m_BannerText;

            // ── フェーズ 1: スケールイン ──────────────────────────
            m_CanvasGroup.alpha = 0f;
            if (m_LabelRect != null) m_LabelRect.localScale = Vector3.one * m_StartScale;
            if (m_BackgroundFlash != null)
            {
                var fc = m_BackgroundFlash.color; fc.a = 0f;
                m_BackgroundFlash.color = fc;
            }

            // スケール & アルファを同時アニメーション（DOTween）
            if (m_LabelRect != null)
                m_LabelRect.DOScale(m_EndScale * 1.05f, m_ScaleInDuration).SetEase(Ease.OutExpo);

            DOTween.To(() => m_CanvasGroup.alpha, x => m_CanvasGroup.alpha = x, 1f, m_ScaleInDuration * 0.8f)
                .SetEase(Ease.OutQuad).SetTarget(this);

            if (m_BackgroundFlash != null)
            {
                var flash = m_BackgroundFlash;
                DOTween.To(() => flash.color.a, x => { var c = flash.color; c.a = x; flash.color = c; },
                    m_FlashMaxAlpha, m_ScaleInDuration).SetEase(Ease.OutQuad).SetTarget(this);
            }

            yield return new WaitForSeconds(m_ScaleInDuration);

            // 微小オーバーシュート補正
            if (m_LabelRect != null)
                m_LabelRect.DOScale(m_EndScale, 0.06f).SetEase(Ease.InOutSine);

            // ── フェーズ 2: 保持 ──────────────────────────────────
            yield return new WaitForSeconds(m_HoldDuration);

            // ── フェーズ 3: フェードアウト ────────────────────────
            DOTween.To(() => m_CanvasGroup.alpha, x => m_CanvasGroup.alpha = x, 0f, m_FadeOutDuration)
                .SetEase(Ease.InQuad).SetTarget(this);
            if (m_LabelRect != null)
                m_LabelRect.DOScale(m_EndScale * 0.7f, m_FadeOutDuration).SetEase(Ease.InQuad);
            if (m_BackgroundFlash != null)
            {
                var flash = m_BackgroundFlash;
                DOTween.To(() => flash.color.a, x => { var c = flash.color; c.a = x; flash.color = c; },
                    0f, m_FadeOutDuration * 0.6f).SetEase(Ease.InQuad).SetTarget(this);
            }

            yield return new WaitForSeconds(m_FadeOutDuration);

            m_PlayCo = null;
        }
    }
}
