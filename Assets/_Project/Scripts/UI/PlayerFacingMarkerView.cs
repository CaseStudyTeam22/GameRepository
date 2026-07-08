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
                m_VisualRoot.SetActive(visible);
            else
                gameObject.SetActive(visible);
        }

        public void SetDirection(string dir)
        {
            if (m_ArrowTransform == null) return;
            m_ArrowTransform.localRotation = Quaternion.Euler(0f, 0f, ToZAngle(dir));
        }

        private static float ToZAngle(string dir)
        {
            // 基準スプライトは「盤面の up = 右上」を向いている前提
            return dir switch
            {
                Directions.Up => 0f,
                Directions.Right => -90f,
                Directions.Down => 180f,
                Directions.Left => 90f,
                _ => 0f
            };
        }
    }
}