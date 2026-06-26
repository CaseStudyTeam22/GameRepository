using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamblingAction.Audio
{
    public class SceneSoundBankLoader : MonoBehaviour
    {
        [SerializeField] private List<string> m_SoundBankNames = new List<string>();

        public bool IsLoaded { get; private set; } = false;
        public event Action OnLoaded;

        private bool m_IsQuitting = false;
        private string SceneLabel => $"(Scene: {SceneManager.GetActiveScene().name})";

        private void Start()
        {
            if (WwiseBankManager.Instance == null)
            {
                Debug.LogError($"[SceneSoundBankLoader] WwiseBankManager のインスタンスが存在しません。SoundBankのロードをスキップします。{SceneLabel}");
                return;
            }

            if (m_SoundBankNames.Count == 0)
            {
                IsLoaded = true;
                OnLoaded?.Invoke();
                return;
            }

            int remaining = m_SoundBankNames.Count;
            bool hasFailure = false;

            foreach (string soundBankName in m_SoundBankNames)
            {
                WwiseBankManager.Instance.LoadBank(soundBankName, success =>
                {
                    if (!success)
                    {
                        hasFailure = true;
                        Debug.LogError($"[SceneSoundBankLoader] SoundBankのロードに失敗しました: {soundBankName} {SceneLabel}");
                    }

                    remaining--;

                    if (remaining == 0 && !hasFailure)
                    {
                        IsLoaded = true;
                        OnLoaded?.Invoke();
                    }
                });
            }
        }

        private void OnApplicationQuit()
        {
            m_IsQuitting = true;
        }

        private void OnDestroy()
        {
            if (m_IsQuitting) return;

            if (WwiseBankManager.Instance == null)
            {
                Debug.LogError($"[SceneSoundBankLoader] WwiseBankManager のインスタンスが存在しません。SoundBankのアンロードをスキップします。{SceneLabel}");
                return;
            }

            foreach (string soundBankName in m_SoundBankNames)
            {
                WwiseBankManager.Instance.UnloadBank(soundBankName);
            }
        }
    }
}