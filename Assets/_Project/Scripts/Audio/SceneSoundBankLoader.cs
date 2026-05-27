using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamblingAction.Audio
{
    /// <summary>
    /// シーンごとに必要なSoundBankのロード・アンロードを行うコンポーネント。
    /// 各シーンに配置し、Inspector で使用するSoundBank名を登録する。
    /// ロード・アンロードは WwiseBankManager を経由して行う。
    /// </summary>
    public class SceneSoundBankLoader : MonoBehaviour
    {
        // -------------------------------------------------------
        // フィールド
        // -------------------------------------------------------

        /// <summary>
        /// このシーンで使用するSoundBank名の一覧。Inspector で登録する。
        /// </summary>
        [SerializeField] private List<string> m_SoundBankNames = new List<string>();

        /// <summary>
        /// Play終了時のフラグ。
        /// OnApplicationQuit が呼ばれた場合に true になり、OnDestroy でのアンロードをスキップする。
        /// </summary>
        private bool m_IsQuitting = false;

        /// <summary>ログ出力用のシーン名サフィックス。</summary>
        private string SceneLabel => $"(Scene: {SceneManager.GetActiveScene().name})";

        private void Start()
        {
            // WwiseBankManager が存在しない場合はエラーログを出して処理しない
            if (WwiseBankManager.Instance == null)
            {
                Debug.LogError($"[SceneSoundBankLoader] WwiseBankManager のインスタンスが存在しません。SoundBankのロードをスキップします。{SceneLabel}");
                return;
            }

            // 登録されている全SoundBankをロードする
            foreach (string soundBankName in m_SoundBankNames)
            {
                WwiseBankManager.Instance.LoadBank(soundBankName, success =>
                {
                    if (!success)
                    {
                        Debug.LogError($"[SceneSoundBankLoader] SoundBankのロードに失敗しました: {soundBankName} {SceneLabel}");
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
            // Play終了時はアンロード処理をスキップする
            if (m_IsQuitting) return;

            // WwiseBankManager が存在しない場合はエラーログを出して処理しない
            if (WwiseBankManager.Instance == null)
            {
                Debug.LogError($"[SceneSoundBankLoader] WwiseBankManager のインスタンスが存在しません。SoundBankのアンロードをスキップします。{SceneLabel}");
                return;
            }

            // 登録されている全SoundBankをアンロードする
            foreach (string soundBankName in m_SoundBankNames)
            {
                WwiseBankManager.Instance.UnloadBank(soundBankName);
            }
        }
    }
}