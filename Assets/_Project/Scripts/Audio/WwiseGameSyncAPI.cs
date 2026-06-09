using UnityEngine;

namespace GamblingAction.Audio
{
    /// <summary>
    /// Wwise の State / Switch / RTPC を操作する静的API。
    /// 再生系は WwiseSoundAPI、ゲームシンク系はこのクラスで扱う。
    /// </summary>
    public static class WwiseGameSyncAPI
    {
        private static bool s_IsQuitting = false;

        public static void NotifyApplicationQuitting()
        {
            s_IsQuitting = true;
        }

        public static void ResetForPlayMode()
        {
            s_IsQuitting = false;
        }

        public static void SetState(string stateGroup, string stateName)
        {
            if (s_IsQuitting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(stateGroup))
            {
                Debug.LogWarning($"[WwiseGameSyncAPI] SetState: 無効なStateGroupです。StateName: {stateName}");
                return;
            }

            if (string.IsNullOrWhiteSpace(stateName))
            {
                Debug.LogWarning($"[WwiseGameSyncAPI] SetState: 無効なStateNameです。StateGroup: {stateGroup}");
                return;
            }

            AkUnitySoundEngine.SetState(stateGroup, stateName);
        }

        public static void SetSwitch(string switchGroup, string switchName, GameObject gameObject)
        {
            if (s_IsQuitting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(switchGroup))
            {
                Debug.LogWarning($"[WwiseGameSyncAPI] SetSwitch: 無効なSwitchGroupです。SwitchName: {switchName}");
                return;
            }

            if (string.IsNullOrWhiteSpace(switchName))
            {
                Debug.LogWarning($"[WwiseGameSyncAPI] SetSwitch: 無効なSwitchNameです。SwitchGroup: {switchGroup}");
                return;
            }

            if (gameObject == null)
            {
                Debug.LogWarning($"[WwiseGameSyncAPI] SetSwitch: 無効なGameObjectです。SwitchGroup: {switchGroup}, SwitchName: {switchName}");
                return;
            }

            AkUnitySoundEngine.SetSwitch(switchGroup, switchName, gameObject);
        }

        public static void SetGlobalRTPCValue(string rtpcName, float value)
        {
            if (s_IsQuitting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(rtpcName))
            {
                Debug.LogWarning("[WwiseGameSyncAPI] SetGlobalRTPCValue: 無効なRTPCNameです。");
                return;
            }

            AkUnitySoundEngine.SetRTPCValue(rtpcName, value, null);
        }

        public static void SetRTPCValueForObject(string rtpcName, float value, GameObject gameObject)
        {
            if (s_IsQuitting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(rtpcName))
            {
                string objectName = gameObject != null ? gameObject.name : "null";
                Debug.LogWarning($"[WwiseGameSyncAPI] SetRTPCValueForObject: 無効なRTPCNameです。GameObject: {objectName}");
                return;
            }

            if (gameObject == null)
            {
                Debug.LogWarning($"[WwiseGameSyncAPI] SetRTPCValueForObject: 無効なGameObjectです。RTPCName: {rtpcName}");
                return;
            }

            AkUnitySoundEngine.SetRTPCValue(rtpcName, value, gameObject);
        }
    }
}