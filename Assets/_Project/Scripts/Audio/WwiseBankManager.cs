using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamblingAction.Audio
{
    /// <summary>
    /// Wwiseサウンドバンクのロード・アンロードを一元管理するクラス。
    /// DontDestroyOnLoad により永続化され、シングルトンとしてアクセスする。
    /// バンク操作は必ず AkBankManager を経由する。
    /// </summary>
    [DisallowMultipleComponent]
    public class WwiseBankManager : MonoBehaviour
    {
        // -------------------------------------------------------
        // シングルトン
        // -------------------------------------------------------

        /// <summary>唯一のインスタンス。</summary>
        public static WwiseBankManager Instance { get; private set; }

        // -------------------------------------------------------
        // フィールド
        // -------------------------------------------------------

        /// <summary>
        /// 使用可能なバンク名の一覧。Inspector で登録する。
        /// 登録されていないバンク名は操作対象外としてエラーログを出す。
        /// </summary>
        [SerializeField]
        private List<string> m_RegisteredBankNames = new List<string>();

        /// <summary>現在ロード済みのバンク名を管理するセット。</summary>
        private HashSet<string> m_LoadedBanks = new HashSet<string>();

        /// <summary>
        /// 遅延アンロード待ち中のコルーチンを管理する辞書。
        /// キー：バンク名、値：実行中のコルーチン。
        /// </summary>
        private Dictionary<string, Coroutine> m_PendingUnloads = new Dictionary<string, Coroutine>();

        /// <summary>ログ出力用のシーン名サフィックス。</summary>
        private string SceneLabel => $"(Scene: {SceneManager.GetActiveScene().name})";

        private void Awake()
        {
            // 既にインスタンスが存在する場合は自身を破棄して二重生成を防ぐ
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------
        // 公開API
        // -------------------------------------------------------

        /// <summary>
        /// 指定したSoundBankをロードする。
        /// ロード完了時に onCompleted を呼び出す（引数は成否）。
        /// 遅延アンロード待ち中の場合はキャンセルしてロード済み状態を維持する。
        /// </summary>
        /// <param name="bankName">ロードするSoundBank名。Inspector 登録済みである必要がある。</param>
        /// <param name="onCompleted">ロード完了時のコールバック。引数は成否（true: 成功）。</param>
        public void LoadBank(string bankName, Action<bool> onCompleted = null)
        {
            // 未登録バンク名はエラーとして処理しない
            if (!IsRegistered(bankName))
            {
                Debug.LogError($"[WwiseBankManager] 未登録のSoundBank名が指定されました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(false);
                return;
            }

            // 遅延アンロード待ち中であればキャンセルしてロード済み状態を維持する
            if (m_PendingUnloads.TryGetValue(bankName, out Coroutine pending))
            {
                StopCoroutine(pending);
                m_PendingUnloads.Remove(bankName);
                Debug.Log($"[WwiseBankManager] 遅延アンロードをキャンセルしました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(true);
                return;
            }

            // 既にロード済みの場合はスキップする
            if (m_LoadedBanks.Contains(bankName))
            {
                Debug.LogWarning($"[WwiseBankManager] 既にロード済みのSoundBankが指定されました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(true);
                return;
            }

            // AkBankManager 経由でロードする（戻り値はSoundBankID、0 は無効IDとして失敗扱い）
            uint bankID = AkBankManager.LoadBank(bankName, false, false);

            if (bankID != 0)
            {
                m_LoadedBanks.Add(bankName);
                Debug.Log($"[WwiseBankManager] SoundBankのロードに成功しました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[WwiseBankManager] SoundBankのロードに失敗しました: {bankName} {SceneLabel}");
                onCompleted?.Invoke(false);
            }
        }

        /// <summary>
        /// 指定したSoundBankを次のフレームでアンロードする。
        /// ロードされていないSoundBankが指定された場合はエラーログを出して処理しない。
        /// </summary>
        /// <param name="bankName">アンロードするSoundBank名。Inspector 登録済みである必要がある。</param>
        public void UnloadBank(string bankName)
        {
            // 未登録SoundBank名はエラーとして処理しない
            if (!IsRegistered(bankName))
            {
                Debug.LogError($"[WwiseBankManager] 未登録のSoundBank名が指定されました: {bankName} {SceneLabel}");
                return;
            }

            // ロードされていないSoundBankはエラーとして処理しない
            if (!m_LoadedBanks.Contains(bankName))
            {
                Debug.LogError($"[WwiseBankManager] ロードされていないSoundBankのアンロードが指定されました: {bankName} {SceneLabel}");
                return;
            }

            // 既に遅延アンロード待ち中の場合はスキップする
            if (m_PendingUnloads.ContainsKey(bankName))
            {
                Debug.LogWarning($"[WwiseBankManager] 既にアンロード待ち中のSoundBankが指定されました: {bankName} {SceneLabel}");
                return;
            }

            // 次のフレームで実行するよう遅延アンロードを予約する
            Coroutine coroutine = StartCoroutine(UnloadBankNextFrame(bankName));
            m_PendingUnloads.Add(bankName, coroutine);
        }

        // -------------------------------------------------------
        // 内部処理
        // -------------------------------------------------------

        /// <summary>
        /// 1フレーム待機後にSoundBankをアンロードするコルーチン。
        /// </summary>
        private IEnumerator UnloadBankNextFrame(string bankName)
        {
            yield return null;

            // AkBankManager 経由でアンロードする
            AkBankManager.UnloadBank(bankName);
            m_LoadedBanks.Remove(bankName);
            m_PendingUnloads.Remove(bankName);
            Debug.Log($"[WwiseBankManager] SoundBankのアンロードに成功しました: {bankName} {SceneLabel}");
        }

        /// <summary>
        /// 指定したSoundBank名が Inspector に登録済みかどうかを返す。
        /// </summary>
        private bool IsRegistered(string bankName)
        {
            return m_RegisteredBankNames.Contains(bankName);
        }
    }
}