using UnityEngine;

namespace GamblingAction.Gameplay
{
    public class PlayerFacingBodyView : MonoBehaviour
    {
        [SerializeField] private GameObject m_VisualRoot;
        [SerializeField] private Transform m_BodyTransform;
        [SerializeField] private SpriteRenderer m_SpriteRenderer;

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
            if (m_BodyTransform == null) return;

            float zAngle = ToZAngle(dir);
            if (isP2View)
            {
                zAngle += 180f;
            }

            m_BodyTransform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        }

        public void SetColor(Color color)
        {
            if (m_SpriteRenderer == null) return;
            m_SpriteRenderer.color = color;
        }

        private static float ToZAngle(string dir)
        {
            // 基準スプライトは盤面上の Up（右上）を向いている前提
            switch (dir)
            {
                case Core.Dto.Directions.Up:
                    return 0f;
                case Core.Dto.Directions.Right:
                    return -90f;
                case Core.Dto.Directions.Down:
                    return 180f;
                case Core.Dto.Directions.Left:
                    return 90f;
                default:
                    return 0f;
            }
        }
    }
}