using DG.Tweening;
using GamblingAction.Domain;
using Unity.Cinemachine;
using UnityEngine;

namespace GamblingAction.Gameplay.CameraFx
{
    /// <summary>
    /// 4拍目に Cinemachine カメラの Lens（FOV / OrthoSize）を
    /// 「少し引いてから素早く戻す」ズーム演出で実行。
    ///
    /// P1 / P2 どちらの視点カメラにも対応するため、
    /// m_TargetCameras に複数の CinemachineCamera を設定できる。
    /// 各カメラに対して独立した Tween Sequence を走らせるため、
    /// 一方が途中でキャンセルされてももう一方に影響しない。
    ///
    /// Cinemachine の Impulse（CameraDirector が担当する画面揺れ）は
    /// カメラの Transposer/Aim オフセットに作用し、Lens への変更とは
    /// 完全に独立しているため競合しない。
    /// </summary>
    public class CameraZoomEffect : MonoBehaviour
    {
        public static CameraZoomEffect Instance { get; private set; }

        [Header("対象カメラ（P1・P2 両方を設定可）")]
        [SerializeField, Tooltip("ズーム演出を適用する CinemachineCamera のリスト。P1/P2 両方指定してください")]
        private CinemachineCamera[] m_TargetCameras = System.Array.Empty<CinemachineCamera>();

        [Header("ズーム設定（Perspective: FOV / Orthographic: OrthoSize）")]
        [SerializeField, Tooltip("引き量（正の値で引く。元の値に加算される）\n小さいほど控えめな演出になる")]
        private float m_PullAmount = 3f;
        [SerializeField, Tooltip("引く時間（秒）\n短いほど鋭いキック感")]
        private float m_PullDuration = 0.10f;
        [SerializeField, Tooltip("元に戻る時間（秒）\n大きいほどゆっくり戻る")]
        private float m_ReturnDuration = 0.60f;
        [SerializeField, Tooltip("引き始めのイージング")]
        private Ease m_PullEase = Ease.OutQuad;
        [SerializeField, Tooltip("戻りのイージング\nOutSine: 滑らか / OutBack: 少し弾む / OutElastic: 強く弾む")]
        private Ease m_ReturnEase = Ease.OutSine;

        [Header("タイミング")]
        [SerializeField, Tooltip("引き始めるまでの遅延（秒）。ACTION! テキスト演出と合わせて微調整する")]
        private float m_StartDelay = 0f;

        private IGameState m_State;

        // カメラ数に合わせた配列。Start 後に初期化。
        private float[]    m_BaseLensValues;
        private bool[]     m_IsOrtho;
        private Sequence[] m_ZoomSeqs;

        // ── ライフサイクル ──────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            InitCameraData();

            m_State = GameStateLocator.Current;
            if (m_State != null)
                m_State.OnBeatChanged += HandleBeatChanged;
            else
                Debug.LogWarning("[CameraZoomEffect] GameStateLocator.Current is null; zoom effect won't fire.");
        }

        private void OnDestroy()
        {
            KillAll();
            if (m_State != null) m_State.OnBeatChanged -= HandleBeatChanged;
            if (Instance == this) Instance = null;
        }

        // ── カメラデータ初期化 ───────────────────────────────────
        // Start のタイミングで取得することで、CameraViewpointSelector による
        // Priority 設定（Start 内で行われる）より後に Lens のベース値を読む。
        private void InitCameraData()
        {
            int n = m_TargetCameras != null ? m_TargetCameras.Length : 0;
            m_BaseLensValues = new float[n];
            m_IsOrtho        = new bool[n];
            m_ZoomSeqs       = new Sequence[n];

            for (int i = 0; i < n; i++)
            {
                var cam = m_TargetCameras[i];
                if (cam == null) continue;
                m_IsOrtho[i] = cam.Lens.Orthographic;
                m_BaseLensValues[i] = m_IsOrtho[i]
                    ? cam.Lens.OrthographicSize
                    : cam.Lens.FieldOfView;
            }
        }

        // ── イベントハンドラ ────────────────────────────────────
        private void HandleBeatChanged()
        {
            if (m_State == null) return;
            if (m_State.Phase != EGamePhase.Battle) return;
            if (m_State.CurrentBeat == 4)
                PlayZoom();
        }

        // ── 外部からも呼べるエントリーポイント ────────────────────
        public void PlayZoom()
        {
            if (m_TargetCameras == null || m_TargetCameras.Length == 0) return;

            for (int i = 0; i < m_TargetCameras.Length; i++)
            {
                var cam = m_TargetCameras[i];
                if (cam == null) continue;
                PlayZoomForCamera(i, cam);
            }
        }

        // ── 個別カメラへのズーム演出 ──────────────────────────────
        private void PlayZoomForCamera(int index, CinemachineCamera cam)
        {
            // 前の演出を強制終了してレンズ値をベースラインに戻す
            if (m_ZoomSeqs[index] != null && m_ZoomSeqs[index].IsActive())
            {
                m_ZoomSeqs[index].Kill();
                ApplyLens(index, cam, m_BaseLensValues[index]);
            }

            float baseVal   = m_BaseLensValues[index];
            float pulledVal = baseVal + m_PullAmount;

            var seq = DOTween.Sequence();

            if (m_StartDelay > 0f)
                seq.AppendInterval(m_StartDelay);

            // カメラを引く
            seq.Append(
                DOTween.To(
                    () => GetCurrentLens(index, cam),
                    v  => ApplyLens(index, cam, v),
                    pulledVal,
                    m_PullDuration
                ).SetEase(m_PullEase)
            );

            // 元に戻す
            seq.Append(
                DOTween.To(
                    () => GetCurrentLens(index, cam),
                    v  => ApplyLens(index, cam, v),
                    baseVal,
                    m_ReturnDuration
                ).SetEase(m_ReturnEase)
            );

            m_ZoomSeqs[index] = seq;
            seq.Play();
        }

        // ── Lens ヘルパー ─────────────────────────────────────
        private float GetCurrentLens(int index, CinemachineCamera cam)
        {
            if (cam == null) return m_BaseLensValues[index];
            return m_IsOrtho[index]
                ? cam.Lens.OrthographicSize
                : cam.Lens.FieldOfView;
        }

        private void ApplyLens(int index, CinemachineCamera cam, float value)
        {
            if (cam == null) return;
            var lens = cam.Lens;
            if (m_IsOrtho[index])
                lens.OrthographicSize = value;
            else
                lens.FieldOfView = value;
            cam.Lens = lens;
        }

        private void KillAll()
        {
            if (m_ZoomSeqs == null) return;
            for (int i = 0; i < m_ZoomSeqs.Length; i++)
                m_ZoomSeqs[i]?.Kill();
        }
    }
}
