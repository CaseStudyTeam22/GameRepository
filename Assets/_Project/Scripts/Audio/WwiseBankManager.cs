using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamblingAction.Audio
{
    [DisallowMultipleComponent]
    public class WwiseBankManager : MonoBehaviour
    {
        public static WwiseBankManager Instance { get; private set; }

        [Header("使用可能なSoundBankの一覧をInspectorで登録する(登録していないのはエラーログが出る)")]
        [SerializeField]
        private List<string> m_RegisteredBankNames = new List<string>();

        private Dictionary<string, int> m_BankRefCounts = new Dictionary<string, int>();
        private Dictionary<string, uint> m_LoadedBankIds = new Dictionary<string, uint>();
        private Dictionary<string, Coroutine> m_PendingUnloads = new Dictionary<string, Coroutine>();

        private string SceneLabel => $"(Scene: {SceneManager.GetActiveScene().name})";

        private bool m_IsQuitting = false;

        private void OnApplicationQuit()
        {
            m_IsQuitting = true;

            foreach (var pending in m_PendingUnloads.Values)
            {
                if (pending != null)
                {
                    StopCoroutine(pending);
                }
            }

            m_PendingUnloads.Clear();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            m_PendingUnloads.Clear();
            m_BankRefCounts.Clear();
            m_LoadedBankIds.Clear();
        }

        public void LoadBank(string bankName, Action<bool> onCompleted = null)
        {
            if (!IsRegistered(bankName))
            {
                Debug.LogError($"[WwiseBankManager] 未登録のSoundBank名が指定されました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(false);
                return;
            }

            if (m_PendingUnloads.TryGetValue(bankName, out Coroutine pending))
            {
                StopCoroutine(pending);
                m_PendingUnloads.Remove(bankName);
                m_BankRefCounts[bankName] = 1;
                Debug.Log($"[WwiseBankManager] 遅延アンロードをキャンセルしました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(true);
                return;
            }

            if (m_BankRefCounts.TryGetValue(bankName, out int refCount) && refCount > 0)
            {
                m_BankRefCounts[bankName] = refCount + 1;
                Debug.Log($"[WwiseBankManager] 参照カウントを加算しました: {bankName} => {m_BankRefCounts[bankName]} {SceneLabel}");
                onCompleted?.Invoke(true);
                return;
            }

            AKRESULT result = AkUnitySoundEngine.LoadBank(bankName, out uint bankID);

            if (result != AKRESULT.AK_Success)
            {
                Debug.LogError($"[WwiseBankManager] SoundBankのロードに失敗しました: {bankName}, Result: {result} {SceneLabel}");
                onCompleted?.Invoke(false);
                return;
            }

            m_BankRefCounts[bankName] = 1;
            m_LoadedBankIds[bankName] = bankID;

            Debug.Log($"[WwiseBankManager] SoundBankをロードしました: {bankName} (ID: {bankID}) {SceneLabel}");
            onCompleted?.Invoke(true);
        }

        public void UnloadBank(string bankName)
        {
            if (m_IsQuitting)
            {
                return;
            }

            if (!IsRegistered(bankName))
            {
                Debug.LogError($"[WwiseBankManager] 未登録のSoundBank名が指定されました: {bankName} {SceneLabel}");
                return;
            }

            if (!m_BankRefCounts.TryGetValue(bankName, out int refCount) || refCount <= 0)
            {
                Debug.LogWarning($"[WwiseBankManager] 使用中ではないSoundBankです: {bankName} {SceneLabel}");
                return;
            }

            refCount--;
            m_BankRefCounts[bankName] = refCount;

            if (refCount > 0)
            {
                Debug.Log($"[WwiseBankManager] 参照カウントを減算しました: {bankName} => {refCount} {SceneLabel}");
                return;
            }

            if (m_PendingUnloads.ContainsKey(bankName))
            {
                return;
            }

            Coroutine unloadCoroutine = StartCoroutine(UnloadBankNextFrame(bankName));
            m_PendingUnloads[bankName] = unloadCoroutine;
        }

        private IEnumerator UnloadBankNextFrame(string bankName)
        {
            yield return null;

            if (m_IsQuitting)
            {
                m_PendingUnloads.Remove(bankName);
                yield break;
            }

            if (!m_BankRefCounts.TryGetValue(bankName, out int refCount) || refCount > 0)
            {
                m_PendingUnloads.Remove(bankName);
                yield break;
            }

            if (!m_LoadedBankIds.TryGetValue(bankName, out uint bankID))
            {
                Debug.LogWarning($"[WwiseBankManager] bankIDが見つかりません: {bankName} {SceneLabel}");
                m_PendingUnloads.Remove(bankName);
                m_BankRefCounts.Remove(bankName);
                yield break;
            }

            AKRESULT result = AkUnitySoundEngine.UnloadBank(bankID, System.IntPtr.Zero);

            if (result != AKRESULT.AK_Success)
            {
                Debug.LogError($"[WwiseBankManager] SoundBankのアンロードに失敗しました: {bankName}, Result: {result} {SceneLabel}");
                m_PendingUnloads.Remove(bankName);
                yield break;
            }

            m_PendingUnloads.Remove(bankName);
            m_BankRefCounts.Remove(bankName);
            m_LoadedBankIds.Remove(bankName);

            Debug.Log($"[WwiseBankManager] SoundBankをアンロードしました: {bankName} (ID: {bankID}) {SceneLabel}");
        }

        private bool IsRegistered(string bankName)
        {
            return m_RegisteredBankNames.Contains(bankName);
        }
    }
}