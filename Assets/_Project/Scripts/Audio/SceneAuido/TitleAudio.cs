using UnityEngine;

namespace GamblingAction.Audio
{
    public class TitleAudio : MonoBehaviour
    {
        [SerializeField] private SceneSoundBankLoader m_SoundBankLoader;
        [SerializeField] private AK.Wwise.Event m_TitleStartSE;

        private bool m_IsDestroyed = false;

        private void Awake()
        {
            if (m_SoundBankLoader == null)
            {
                m_SoundBankLoader = FindFirstObjectByType<SceneSoundBankLoader>();
            }
        }

        private void Start()
        {
            if (m_SoundBankLoader == null)
            {
                Debug.LogWarning("[TitleAudio] SceneSoundBankLoader is not assigned.");
                return;
            }

            if (m_SoundBankLoader.IsLoaded)
            {
                PlayStartSe();
                return;
            }

            m_SoundBankLoader.OnLoaded += PlayStartSe;
        }

        private void OnDestroy()
        {
            m_IsDestroyed = true;

            if (m_SoundBankLoader != null)
            {
                m_SoundBankLoader.OnLoaded -= PlayStartSe;
            }

            WwiseSoundAPI.Instance?.StopAll(gameObject);
        }

        private void PlayStartSe()
        {
            if (m_IsDestroyed || WwiseSoundAPI.Instance == null)
            {
                return;
            }

            if (m_TitleStartSE != null)
            {
                WwiseSoundAPI.Instance.Play(m_TitleStartSE, gameObject);
            }
        }
    }
}