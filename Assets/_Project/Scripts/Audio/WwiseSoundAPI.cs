using System.Collections.Generic;
using UnityEngine;

namespace GamblingAction.Audio
{
    /// <summary>
    /// Wwiseのサウンド再生・停止を一元管理するAPI。
    /// 他担当者がWwiseの知識なしにサウンドを扱える窓口を提供する。
    /// </summary>
    /// <remarks>
    /// 呼び出し側の責務：
    /// - AK.Wwise.Event を SerializeField で保持する
    /// - Instance の null チェックを行う
    /// - OnDestroy で StopAll(this.gameObject) を呼ぶ
    /// - 個別停止が必要な場合は Play の戻り値（playingID）を保持する
    /// </remarks>
    [DisallowMultipleComponent]
    public class WwiseSoundAPI : MonoBehaviour
    {
        /// <summary>シングルトンインスタンス</summary>
        public static WwiseSoundAPI Instance { get; private set; }

        /// <summary>GameObjectに紐づくPlayingIDのリスト</summary>
        private Dictionary<GameObject, List<uint>> m_PlayingIDs;

        // -------------------------------------------------------
        // Unity ライフサイクル
        // -------------------------------------------------------

        private void Awake()
        {
            // 二重生成防止
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            m_PlayingIDs = new Dictionary<GameObject, List<uint>>();
        }

        // -------------------------------------------------------
        // 公開API
        // -------------------------------------------------------

        /// <summary>
        /// サウンドを再生する。
        /// </summary>
        /// <param name="wwiseEvent">再生するWwiseイベント</param>
        /// <param name="gameObject">再生に紐づけるGameObject</param>
        /// <returns>再生インスタンスのID。再生失敗時は 0 を返す</returns>
        public uint Play(AK.Wwise.Event wwiseEvent, GameObject gameObject)
        {
            var playingID = wwiseEvent.Post(gameObject);

            if (playingID == 0)
            {
                Debug.LogWarning($"[WwiseSoundAPI] 再生失敗。Event: {wwiseEvent.Name}, GameObject: {gameObject.name}");
                return 0;
            }

            AddPlayingID(gameObject, playingID);
            return playingID;
        }

        /// <summary>
        /// 指定したPlayingIDのサウンドを停止する。
        /// </summary>
        /// <param name="playingID">停止するサウンドのPlayingID</param>
        /// <param name="gameObject">PlayingIDに紐づくGameObject</param>
        /// <param name="transitionDuration">フェードアウト時間（ミリ秒）。デフォルトは即時停止</param>
        public void Stop(uint playingID, GameObject gameObject, int transitionDuration = 0)
        {
            if (playingID == 0)
            {
                Debug.LogWarning($"[WwiseSoundAPI] 無効なPlayingIDです。GameObject: {gameObject.name}");
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

        /// <summary>
        /// 指定したGameObjectに紐づく全サウンドを停止する。
        /// </summary>
        /// <param name="gameObject">停止対象のGameObject</param>
        /// <param name="transitionDuration">フェードアウト時間（ミリ秒）。デフォルトは即時停止</param>
        public void StopAll(GameObject gameObject, int transitionDuration = 0)
        {
            if (!m_PlayingIDs.ContainsKey(gameObject))
            {
                Debug.LogWarning($"[WwiseSoundAPI] 未登録のGameObjectです。GameObject: {gameObject.name}");
                return;
            }

            foreach (var playingID in m_PlayingIDs[gameObject])
            {
                AkUnitySoundEngine.StopPlayingID(playingID, transitionDuration, AkCurveInterpolation.AkCurveInterpolation_Linear);
            }

            m_PlayingIDs.Remove(gameObject);
        }

        // -------------------------------------------------------
        // 非公開メソッド
        // -------------------------------------------------------

        /// <summary>
        /// PlayingIDをDictionaryに追加する。
        /// GameObjectのキーが存在しない場合は新規作成する。
        /// </summary>
        private void AddPlayingID(GameObject gameObject, uint playingID)
        {
            if (!m_PlayingIDs.ContainsKey(gameObject))
            {
                m_PlayingIDs[gameObject] = new List<uint>();
            }

            m_PlayingIDs[gameObject].Add(playingID);
        }

        /// <summary>
        /// PlayingIDをDictionaryから除去する。
        /// Listが空になった場合はキーごと削除する。
        /// </summary>
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