using GamblingAction.Core.Dto;
using UnityEngine;

namespace GamblingAction.Gameplay
{
    public class PlayerFacingMarkerView : MonoBehaviour
    {
        [SerializeField] private GameObject m_VisualRoot;
        [SerializeField] private Transform m_ArrowTransform;

        public void SetVisible(bool visible)
        {
            if (m_VisualRoot != null)
            {
                m_VisualRoot.SetActive(visible);
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        public void SetDirection(string dir, bool isP2View)
        {
            if (m_ArrowTransform == null) return;

            float zAngle = ToZAngle(dir);
            if (isP2View)
            {
                zAngle += 180f;
            }

            m_ArrowTransform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        }

        private static float ToZAngle(string dir)
        {
            // 基準スプライトは盤面上の Up（右上）を向いている前提
            switch (dir)
            {
                case Directions.Up:
                    return 0f;
                case Directions.Right:
                    return -90f;
                case Directions.Down:
                    return 180f;
                case Directions.Left:
                    return 90f;
                default:
                    return 0f;
            }
        }
    }
}