using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GamblingAction.UI
{
    public class CharacterSelectItem : MonoBehaviour
    {
        [SerializeField]
        private Image m_Icon;

        [SerializeField]
        private TMP_Text m_CharacterName;

        [SerializeField]
        private TMP_Text m_Description;

        public void Setup(CharacterSelectData data)
        {
            m_Icon.sprite = data.m_Icon;
            m_CharacterName.text = data.m_CharacterName;
            m_Description.text = data.m_Description;
        }
    }
}
