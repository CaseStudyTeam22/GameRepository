using System.Collections.Generic;
using UnityEngine;

/// Wwiseのサウンド再生・停止を一元管理するAPI。
namespace GamblingAction.Audio
{
    /// 呼び出し側の責務：
    /// - AK.Wwise.Event を SerializeField で保持する
    /// - Instance の null チェックを行う
    /// - OnDestroy で StopAll(this.gameObject) を呼ぶ
    /// - 個別停止が必要な場合は Play の戻り値（playingID）を保持する
    [DisallowMultipleComponent]
    public class WwiseSoundAPI : MonoBehaviour
    {
        public static WwiseSoundAPI Instance { get; private set; }

        /// GameObjectに紐づくPlayingIDのリスト
        private Dictionary<GameObject, List<uint>> m_PlayingIDs;

        private bool m_IsQuitting = false;

        private void OnApplicationQuit()
        {
            m_IsQuitting = true;
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            m_PlayingIDs = new Dictionary<GameObject, List<uint>>();
        }

        private void OnDestroy()
        {
            if(Instance == this)
            {
                Instance = null;
            }

            m_PlayingIDs.Clear();
        }

        // 再生インスタンスのID。再生失敗時は 0 を返す
        public uint Play(AK.Wwise.Event wwiseEvent, GameObject gameObject)
        {
            if(wwiseEvent == null)
            {
                string objectName = gameObject != null ? gameObject.name : "null";
                Debug.LogWarning($"[WwiseSoundAPI] 無効なEventが指定されました。GameObject: {objectName}"); 
                return 0;
            }

            if (gameObject == null)
            {
                Debug.LogWarning($"[WwiseSoundAPI] 無効なGameObjectが指定されました。Event: {wwiseEvent.Name}");
                return 0;
            }

            var playingID = wwiseEvent.Post(gameObject);

            if (playingID == 0)
            {
                Debug.LogWarning($"[WwiseSoundAPI] 再生失敗。Event: {wwiseEvent.Name}, GameObject: {gameObject.name}");
                return 0;
            }

            AddPlayingID(gameObject, playingID);
            return playingID;
        }

        public void Stop(uint playingID, GameObject gameObject, int transitionDuration = 0)
        {
            if(m_IsQuitting)
            {
                return;
            }


            if (playingID == 0)
            {
                string objectName = gameObject != null ? gameObject.name : "null";
                Debug.LogWarning($"[WwiseSoundAPI] 無効なEventが指定されました。GameObject: {objectName}");
                return;
            }

            if (!m_PlayingIDs.ContainsKey(gameObject))
            {
                Debug.LogWarning($"[WwiseSoundAPI] 未登録のGameObjectです。GameObject: {gameObject.name}");
                return;
            }

            if (!m_PlayingIDs[gameObject].Contains(playingID))
            {
                Debug.LogWarning($"[WwiseSoundAPI] 未登録のPlayingIDです。PlayingID: {playingID}, GameObject: {gameObject.name}");
                return;
            }

            AkUnitySoundEngine.StopPlayingID(playingID, transitionDuration, AkCurveInterpolation.AkCurveInterpolation_Linear);
            RemovePlayingID(gameObject, playingID);
        }

        public void StopAll(GameObject gameObject, int transitionDuration = 0)
        {
            if(m_IsQuitting)
            {
                return;
            }

            if (!m_PlayingIDs.ContainsKey(gameObject))
            {
                return;
            }

            foreach (var playingID in m_PlayingIDs[gameObject])
            {
                AkUnitySoundEngine.StopPlayingID(
                    playingID,
                    transitionDuration,
                    AkCurveInterpolation.AkCurveInterpolation_Linear);
            }

            m_PlayingIDs.Remove(gameObject);
        }

        /// PlayingIDをDictionaryに追加する。
        /// GameObjectのキーが存在しない場合は新規作成する。
        private void AddPlayingID(GameObject gameObject, uint playingID)
        {
            if (!m_PlayingIDs.ContainsKey(gameObject))
            {
                m_PlayingIDs[gameObject] = new List<uint>();
            }

            m_PlayingIDs[gameObject].Add(playingID);
        }

        /// PlayingIDをDictionaryから除去する。
        /// Listが空になった場合はキーごと削除する。
        private void RemovePlayingID(GameObject gameObject, uint playingID)
        {
            m_PlayingIDs[gameObject].Remove(playingID);

            if (m_PlayingIDs[gameObject].Count == 0)
            {
                m_PlayingIDs.Remove(gameObject);
            }
        }
    }
}