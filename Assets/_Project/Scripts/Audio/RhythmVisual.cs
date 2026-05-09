using UnityEngine;

namespace GamblingAction.Audio
{
    [RequireComponent(typeof(RhythmSignal))]
    public class RhythmVisual : MonoBehaviour
    {
        [Header("ビート反応")]
        [Tooltip("拍ごとに交互に上がるY軸の高さ")]
        [SerializeField] private float m_BounceHeight = 2f;
        [Tooltip("1拍ごとに回転するY軸角度(度)")]
        [SerializeField] private float m_RotateAngleY = 30f;

        [Header("小節反応")]
        [Tooltip("4拍ごとに移動するX軸の量")]
        [SerializeField] private float m_BarMoveX = 1f;

        private RhythmSignal m_Signal;
        private Vector3 m_OriginPos;

        // 拍ごとにY位置を0と高さで交互に切り替えるフラグ
        private bool m_IsBounceUp = false;
        // 小節ごとに積算するX軸オフセット
        private float m_CurrentOffsetX = 0f;
        // X移動の方向 (1 = 正方向, -1 = 負方向)
        private int m_DirectionX = 1;
        // X移動の折り返し限界
        private const float k_MaxOffsetX = 10f;

        void Awake()
        {
            m_Signal = GetComponent<RhythmSignal>();
            m_OriginPos = transform.position;
        }

        void OnEnable()
        {
            m_Signal.OnBeat += HandleBeat;
            m_Signal.OnBar += HandleBar;
            m_Signal.OnEnd += HandleEnd;
        }

        void OnDisable()
        {
            m_Signal.OnBeat -= HandleBeat;
            m_Signal.OnBar -= HandleBar;
            m_Signal.OnEnd -= HandleEnd;
        }

        // 拍ごと: Y軸を0と高さで交互に切り替え + 回転
        private void HandleBeat(float intensity)
        {
            // Y位置を交互に切り替える
            m_IsBounceUp = !m_IsBounceUp;
            float y = m_IsBounceUp ? m_BounceHeight : 0f;
            transform.position = new Vector3(
                m_OriginPos.x + m_CurrentOffsetX,
                m_OriginPos.y + y,
                m_OriginPos.z
            );

            // Y軸のみを積算して回転する。X・Z軸は常に0に固定する
            float currentY = transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, currentY + m_RotateAngleY, 0f);
        }

        // 小節頭ごと: X軸に移動する。限界に達したら反転する
        private void HandleBar(float intensity)
        {
            m_CurrentOffsetX += m_BarMoveX * m_DirectionX;

            // 限界に達したら方向を反転する
            if (m_CurrentOffsetX >= k_MaxOffsetX)
            {
                m_CurrentOffsetX = k_MaxOffsetX;
                m_DirectionX = -1;
            }
            else if (m_CurrentOffsetX <= -k_MaxOffsetX)
            {
                m_CurrentOffsetX = -k_MaxOffsetX;
                m_DirectionX = 1;
            }

            transform.position = new Vector3(
                m_OriginPos.x + m_CurrentOffsetX,
                transform.position.y,
                m_OriginPos.z
            );
        }

        private void HandleEnd()
        {
            Debug.Log("[RhythmVisual] 再生終了");
        }
    }
}