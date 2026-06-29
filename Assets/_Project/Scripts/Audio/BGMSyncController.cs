using System.Collections.Generic;
using UnityEngine;
using GamblingAction.Domain;

namespace GamblingAction.Audio
{
    /// <summary>
    /// 第1段階用の BGM 同期観測 + Seek 補正クラス。
    /// サーバー拍を基準にした近似再生位置と、実際の再生位置との差分を観測し、
    /// Cue 未取得時は expectedMs、Cue 取得後は Cue 基準位置へ Seek する。
    /// </summary>
    public class BGMSyncController : MonoBehaviour
    {
        private struct UserCueSnapshot
        {
            public string CueName;
            public int MeasureIndex;
            public int BeatInBar;
            public int ReceivedActualPositionMs;
            public float ReceivedTimeSec;
            public int ServerBeatAtReceive;
        }

        [Header("Debug")]
        [SerializeField] private bool m_EnableDebugLog = true;
        [SerializeField] private float m_DebugLogIntervalSec = 0.25f;

        [Header("Seek Correction")]
        [SerializeField] private float m_WarmupDurationSec = 0.5f;
        [SerializeField] private int m_MinObservationCountBeforeSeek = 2;
        [SerializeField] private float m_SeekThresholdMs = 120f;
        [SerializeField] private float m_SeekCooldownSec = 0.75f;
        [SerializeField] private int m_MaxSeekCountPerPlay = 5;
        [SerializeField] private int m_MaxCueHistoryCount = 8;

        [SerializeField] private float m_PostSeekSettleDurationSec = 0.12f;
        [SerializeField] private float m_LargeDriftThresholdMs = 300f;
        [SerializeField] private int m_MaxSeekCountPerBarWindow = 2;

        private IBeatClock m_BeatClock;
        private IGameState m_GameState;
        private GameObject m_Owner;
        private AK.Wwise.Event m_SeekEvent;

        private uint m_PlayingId = 0u;
        private bool m_IsSyncActive = false;

        private int m_LastReceivedBeat = 1;
        private float m_LastBeatReceivedTimeSec = 0f;

        private float m_BeatDurationSec = 0.375f;
        private int m_BeatsPerBar = 4;
        private float m_BarDurationSec = 1.5f;

        private float m_LastDebugLogTimeSec = 0f;
        private float m_LastSeekSkipLogTimeSec = -999f;

        private float m_SyncStartTimeSec = 0f;
        private int m_ObservationCount = 0;
        private float m_LastSeekTimeSec = -999f;
        private int m_SeekCount = 0;
        private float m_PreviousDriftMs = 0f;
        private int m_LastKnownTimeLeft = -1;

        private bool m_HasPendingSeek = false;
        private float m_PendingSeekDriftMs = 0f;
        private int m_PendingSeekExpectedMs = 0;
        private int m_PendingSeekActualMs = 0;
        private int m_PendingAlignBeatInBar = 0;
        private int m_PendingExecuteWindowNextBeatInBar = 0;
        private int m_PendingExecutePreBeatIndex = 0;
        private int m_PendingCreatedServerBeat = 0;
        private int m_PendingSeekTargetMs = 0;

        private bool m_HasLastBeatCue = false;
        private string m_LastBeatCueName = string.Empty;
        private int m_LastBeatCueMeasure = 0;
        private int m_LastBeatCueBeatInBar = 0;
        private string m_LastPreBeatName = string.Empty;
        private int m_PreBeatCountSinceLastBeat = 0;

        private float m_LastSeekAppliedTimeSec = -999f;
        private int m_SeekCountInCurrentBarWindow = 0;

        private readonly List<UserCueSnapshot> m_RecentUserCues = new List<UserCueSnapshot>();

        public float LastDriftMs { get; private set; } = 0f;
        public int LastExpectedPositionMs { get; private set; } = 0;
        public int LastActualPositionMs { get; private set; } = 0;

        public void Initialize(IBeatClock beatClock, IGameState gameState, GameObject owner, AK.Wwise.Event seekEvent)
        {
            m_BeatClock = beatClock;
            m_GameState = gameState;
            m_Owner = owner;
            m_SeekEvent = seekEvent;

            if (m_BeatClock != null)
            {
                m_BeatDurationSec = m_BeatClock.BeatDuration;
                m_BeatsPerBar = m_BeatClock.BeatsPerBar;
                m_BarDurationSec = m_BeatClock.BarDuration;
            }
        }

        public void StartSync(uint playingId)
        {
            if (m_BeatClock == null || m_GameState == null)
            {
                Debug.LogWarning("[BGMSyncController] 初期化不足です。");
                return;
            }

            if (playingId == 0u)
            {
                Debug.LogWarning("[BGMSyncController] 無効な playingID です。");
                return;
            }

            m_PlayingId = playingId;
            m_IsSyncActive = true;

            m_BeatDurationSec = m_BeatClock.BeatDuration;
            m_BeatsPerBar = m_BeatClock.BeatsPerBar;
            m_BarDurationSec = m_BeatClock.BarDuration;

            m_LastReceivedBeat = m_BeatClock.CurrentBeat;
            m_LastBeatReceivedTimeSec = Time.unscaledTime;
            m_LastDebugLogTimeSec = 0f;
            m_LastSeekSkipLogTimeSec = -999f;

            m_SyncStartTimeSec = Time.unscaledTime;
            m_ObservationCount = 0;
            m_LastSeekTimeSec = -999f;
            m_SeekCount = 0;
            m_PreviousDriftMs = 0f;
            m_LastKnownTimeLeft = m_GameState.TimeLeft;

            m_HasPendingSeek = false;
            m_PendingSeekDriftMs = 0f;
            m_PendingSeekExpectedMs = 0;
            m_PendingSeekActualMs = 0;
            m_PendingAlignBeatInBar = 0;
            m_PendingExecuteWindowNextBeatInBar = 0;
            m_PendingCreatedServerBeat = 0;
            m_PendingSeekTargetMs = 0;

            m_HasLastBeatCue = false;
            m_LastBeatCueName = string.Empty;
            m_LastBeatCueMeasure = 0;
            m_LastBeatCueBeatInBar = 0;
            m_LastPreBeatName = string.Empty;
            m_PreBeatCountSinceLastBeat = 0;

            m_RecentUserCues.Clear();

            LastDriftMs = 0f;
            LastExpectedPositionMs = 0;
            LastActualPositionMs = 0;
        }

        public void StopSync()
        {
            m_IsSyncActive = false;
            m_PlayingId = 0u;

            m_ObservationCount = 0;
            m_SeekCount = 0;
            m_PreviousDriftMs = 0f;
            m_LastKnownTimeLeft = -1;

            m_HasPendingSeek = false;
            m_PendingSeekDriftMs = 0f;
            m_PendingSeekExpectedMs = 0;
            m_PendingSeekActualMs = 0;
            m_PendingAlignBeatInBar = 0;
            m_PendingExecuteWindowNextBeatInBar = 0;
            m_PendingCreatedServerBeat = 0;
            m_PendingSeekTargetMs = 0;

            m_HasLastBeatCue = false;
            m_LastBeatCueName = string.Empty;
            m_LastBeatCueMeasure = 0;
            m_LastBeatCueBeatInBar = 0;
            m_LastPreBeatName = string.Empty;
            m_PreBeatCountSinceLastBeat = 0;

            m_RecentUserCues.Clear();

            LastDriftMs = 0f;
            LastExpectedPositionMs = 0;
            LastActualPositionMs = 0;
        }

        public void HandleBeat(int beat)
        {
            if (!m_IsSyncActive)
            {
                return;
            }

            m_LastReceivedBeat = beat;
            m_LastBeatReceivedTimeSec = Time.unscaledTime;
        }

        public void HandleMusicSyncCallback(AkCallbackType type, AkMusicSyncCallbackInfo info)
        {
            if (!m_IsSyncActive || info == null)
            {
                return;
            }

            if (type != AkCallbackType.AK_MusicSyncUserCue && info.musicSyncType != AkCallbackType.AK_MusicSyncUserCue)
            {
                return;
            }

            if (string.IsNullOrEmpty(info.userCueName))
            {
                return;
            }

            if (info.userCueName.StartsWith("Beat"))
            {
                HandleBeatCue(info);
                return;
            }

            if (info.userCueName.StartsWith("PreBeat"))
            {
                HandlePreBeatCue(info);
            }
        }

        private void HandleBeatCue(AkMusicSyncCallbackInfo info)
        {
            if (!TryParseBeatCueLocation(info.userCueName, out int measureIndex, out int beatInBar))
            {
                if (m_EnableDebugLog)
                {
                    Debug.Log($"[BGMSyncController] BeatCue名を解釈できません。cue={info.userCueName}");
                }
                return;
            }

            int actualPositionMs = NormalizePositionMs(info.segmentInfo_iCurrentPosition);

            var snapshot = new UserCueSnapshot
            {
                CueName = info.userCueName,
                MeasureIndex = measureIndex,
                BeatInBar = beatInBar,
                ReceivedActualPositionMs = actualPositionMs,
                ReceivedTimeSec = Time.unscaledTime,
                ServerBeatAtReceive = m_LastReceivedBeat
            };

            m_RecentUserCues.Add(snapshot);

            if (m_RecentUserCues.Count > m_MaxCueHistoryCount)
            {
                m_RecentUserCues.RemoveAt(0);
            }

            bool hadLastBeatCue = m_HasLastBeatCue;
            int previousMeasure = m_LastBeatCueMeasure;

            m_HasLastBeatCue = true;
            m_LastBeatCueName = snapshot.CueName;
            m_LastBeatCueMeasure = snapshot.MeasureIndex;
            m_LastBeatCueBeatInBar = snapshot.BeatInBar;
            m_PreBeatCountSinceLastBeat = 0;

            if (hadLastBeatCue && measureIndex != previousMeasure)
            {
                m_SeekCountInCurrentBarWindow = 0;
            }

            if (m_HasPendingSeek && beatInBar == m_PendingExecuteWindowNextBeatInBar)
            {
                InvalidatePendingSeek("windowExpiredByBeatCue");
            }

            if (m_EnableDebugLog)
            {
                Debug.Log(
                    $"[BGMSyncController] BeatCue cue={snapshot.CueName} measure={snapshot.MeasureIndex} beat={snapshot.BeatInBar} " +
                    $"actual={snapshot.ReceivedActualPositionMs}ms serverBeat={snapshot.ServerBeatAtReceive}");
            }
        }

        private void HandlePreBeatCue(AkMusicSyncCallbackInfo info)
        {
            m_LastPreBeatName = info.userCueName;

            if (!TryParsePreBeatCueNumber(info.userCueName, out int preBeatNumber))
            {
                if (m_EnableDebugLog)
                {
                    Debug.Log($"[BGMSyncController] PreBeatCue名を解釈できません。cue={info.userCueName}");
                }
                return;
            }

            if (!m_HasLastBeatCue)
            {
                if (m_EnableDebugLog)
                {
                    Debug.Log($"[BGMSyncController] PreBeatCue cue={info.userCueName} を受信しましたが、直前のBeatCueがありません。");
                }
                return;
            }

            m_PreBeatCountSinceLastBeat = Mathf.Min(m_PreBeatCountSinceLastBeat + 1, 2);

            int nextBeatInBar = GetNextBeatInBar(m_LastBeatCueBeatInBar);

            if (m_EnableDebugLog)
            {
                Debug.Log(
                    $"[BGMSyncController] PreBeatCue cue={info.userCueName} number={preBeatNumber} " +
                    $"lastBeat={m_LastBeatCueName} nextBeat={nextBeatInBar} preCount={m_PreBeatCountSinceLastBeat}");
            }

            TryExecutePendingSeekFromPreBeat(info.userCueName, nextBeatInBar, m_PreBeatCountSinceLastBeat);
        }

        private void Update()
        {
            if (!m_IsSyncActive || m_PlayingId == 0u)
            {
                return;
            }

            if (m_GameState == null || !m_GameState.GameActive || m_GameState.TimeLeft <= 0)
            {
                return;
            }

            if (m_LastKnownTimeLeft >= 0 && m_GameState.TimeLeft > m_LastKnownTimeLeft)
            {
                m_ObservationCount = 0;
                m_SeekCount = 0;
                m_SeekCountInCurrentBarWindow = 0;
                m_PreviousDriftMs = 0f;
                ClearPendingSeek();
                m_RecentUserCues.Clear();
            }

            m_LastKnownTimeLeft = m_GameState.TimeLeft;

            if (Time.unscaledTime - m_LastSeekAppliedTimeSec < m_PostSeekSettleDurationSec)
            {
                return;
            }

            int expectedMs = CalculateExpectedPositionMs();

            if (!TryGetActualPositionMs(out int actualMs))
            {
                return;
            }

            int barDurationMs = Mathf.RoundToInt(m_BarDurationSec * 1000f);
            float driftMs = CalculateWrappedDriftMs(expectedMs, actualMs, barDurationMs);

            LastExpectedPositionMs = expectedMs;
            LastActualPositionMs = actualMs;
            LastDriftMs = driftMs;
            m_ObservationCount++;

            if (m_EnableDebugLog && Time.unscaledTime - m_LastDebugLogTimeSec >= m_DebugLogIntervalSec)
            {
                Debug.Log($"[BGMSyncController] beat={m_LastReceivedBeat} expected={expectedMs}ms actual={actualMs}ms drift={driftMs:F1}ms");
                m_LastDebugLogTimeSec = Time.unscaledTime;
            }

            TryReserveSeekCorrection(expectedMs, actualMs, driftMs);

            m_PreviousDriftMs = driftMs;
        }

        private int CalculateExpectedPositionMs()
        {
            float elapsedSinceBeatSec = Time.unscaledTime - m_LastBeatReceivedTimeSec;
            elapsedSinceBeatSec = Mathf.Max(0f, elapsedSinceBeatSec);

            int beatDurationMs = Mathf.RoundToInt(m_BeatDurationSec * 1000f);
            int barDurationMs = Mathf.RoundToInt(m_BarDurationSec * 1000f);

            int beatStartOffsetMs = (m_LastReceivedBeat - 1) * beatDurationMs;
            int elapsedMs = Mathf.RoundToInt(elapsedSinceBeatSec * 1000f);

            int expectedMs = beatStartOffsetMs + elapsedMs;

            if (barDurationMs > 0)
            {
                expectedMs %= barDurationMs;
                if (expectedMs < 0)
                {
                    expectedMs += barDurationMs;
                }
            }

            return expectedMs;
        }

        private bool TryGetActualPositionMs(out int actualMs)
        {
            actualMs = 0;

            AKRESULT result = AkUnitySoundEngine.GetSourcePlayPosition(m_PlayingId, out actualMs);

            if (result != AKRESULT.AK_Success)
            {
                return false;
            }

            actualMs = NormalizePositionMs(actualMs);
            return true;
        }

        private float CalculateWrappedDriftMs(int expectedMs, int actualMs, int loopLengthMs)
        {
            if (loopLengthMs <= 0)
            {
                return expectedMs - actualMs;
            }

            int raw = expectedMs - actualMs;
            int half = loopLengthMs / 2;

            if (raw > half)
            {
                raw -= loopLengthMs;
            }
            else if (raw < -half)
            {
                raw += loopLengthMs;
            }

            return raw;
        }

        private void TryReserveSeekCorrection(int expectedMs, int actualMs, float driftMs)
        {
            if (m_SeekEvent == null || m_Owner == null)
            {
                LogSeekSkip("seekEventOrOwnerMissing", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_HasPendingSeek)
            {
                LogSeekSkip("seek予約中", expectedMs, actualMs, driftMs);
                return;
            }

            if (!m_HasLastBeatCue)
            {
                LogSeekSkip("lastBeatCue未確定", expectedMs, actualMs, driftMs);
                return;
            }

            if (Time.unscaledTime - m_SyncStartTimeSec < m_WarmupDurationSec)
            {
                LogSeekSkip("warmup", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_ObservationCount < m_MinObservationCountBeforeSeek)
            {
                LogSeekSkip("observation不足", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_SeekCountInCurrentBarWindow >= m_MaxSeekCountPerBarWindow)
            {
                LogSeekSkip("bar内seek回数上限", expectedMs, actualMs, driftMs);
                return;
            }

            if (Time.unscaledTime - m_LastSeekTimeSec < m_SeekCooldownSec)
            {
                LogSeekSkip("cooldown中", expectedMs, actualMs, driftMs);
                return;
            }

            if (Mathf.Abs(driftMs) < m_SeekThresholdMs)
            {
                LogSeekSkip("drift閾値未満", expectedMs, actualMs, driftMs);
                return;
            }

            if (Mathf.Abs(m_PreviousDriftMs) < m_SeekThresholdMs)
            {
                LogSeekSkip("前回drift閾値未満", expectedMs, actualMs, driftMs);
                return;
            }

            if (Mathf.Sign(m_PreviousDriftMs) != Mathf.Sign(driftMs))
            {
                LogSeekSkip("drift符号反転", expectedMs, actualMs, driftMs);
                return;
            }

            int currentBeat = Mathf.Clamp(m_LastReceivedBeat, 1, m_BeatsPerBar);
            int beatDifference = Mathf.Abs(m_LastBeatCueBeatInBar - currentBeat);

            if (beatDifference > 1 && beatDifference < m_BeatsPerBar - 1)
            {
                LogSeekSkip("serverBeatとBeatCue区間の乖離が大きい", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_PreBeatCountSinceLastBeat >= 2)
            {
                LogSeekSkip("現在区間のPreBeat消費済み", expectedMs, actualMs, driftMs);
                return;
            }

            int alignBeatInBar = driftMs < 0f ? currentBeat : GetNextBeatInBar(currentBeat);
            int executeWindowNextBeatInBar = GetNextBeatInBar(m_LastBeatCueBeatInBar);
            int executePreBeatIndex = Mathf.Abs(driftMs) >= m_LargeDriftThresholdMs ? 1 : 2;
            int seekTargetMs = GetBeatStartPositionMs(alignBeatInBar);

            if (executePreBeatIndex <= m_PreBeatCountSinceLastBeat)
            {
                LogSeekSkip("予約時点で対象PreBeat通過済み", expectedMs, actualMs, driftMs);
                return;
            }

            m_HasPendingSeek = true;
            m_PendingSeekDriftMs = driftMs;
            m_PendingSeekExpectedMs = expectedMs;
            m_PendingSeekActualMs = actualMs;
            m_PendingAlignBeatInBar = alignBeatInBar;
            m_PendingExecuteWindowNextBeatInBar = executeWindowNextBeatInBar;
            m_PendingExecutePreBeatIndex = executePreBeatIndex;
            m_PendingCreatedServerBeat = currentBeat;
            m_PendingSeekTargetMs = seekTargetMs;

            Debug.Log(
                $"[BGMSyncController] Seek予約 alignBeat={m_PendingAlignBeatInBar} executeNextBeat={m_PendingExecuteWindowNextBeatInBar} " +
                $"executePreBeat={m_PendingExecutePreBeatIndex} targetMs={m_PendingSeekTargetMs}ms expected={expectedMs}ms actual={actualMs}ms " +
                $"drift={driftMs:F1}ms lastBeatCue={m_LastBeatCueName} preCount={m_PreBeatCountSinceLastBeat}");
        }

        private bool TryParseCueIndex(string cueName, out int cueIndex)
        {
            cueIndex = 0;

            if (string.IsNullOrEmpty(cueName))
            {
                return false;
            }

            if (!cueName.StartsWith("Beat"))
            {
                return false;
            }

            return int.TryParse(cueName.Substring(4), out cueIndex) && cueIndex >= 1;
        }

        private void TryExecutePendingSeek()
        {
            if (!m_HasPendingSeek)
            {
                return;
            }

            if (m_SeekEvent == null || m_Owner == null)
            {
                m_HasPendingSeek = false;
                return;
            }

            if (m_PlayingId == 0u)
            {
                m_HasPendingSeek = false;
                return;
            }

            if (m_SeekCount >= m_MaxSeekCountPerPlay)
            {
                m_HasPendingSeek = false;
                return;
            }

            if (Time.unscaledTime - m_LastSeekTimeSec < m_SeekCooldownSec)
            {
                return;
            }

            const int seekTargetMs = 0;

            bool hasCue = TryGetLatestUserCue(out UserCueSnapshot lastCue);

            AKRESULT result = AkUnitySoundEngine.SeekOnEvent(m_SeekEvent.Id, m_Owner, seekTargetMs, false, m_PlayingId);

            if (result != AKRESULT.AK_Success)
            {
                Debug.LogWarning(
                    $"[BGMSyncController] Seek失敗 result={result} target={seekTargetMs}ms " +
                    $"reservedExpected={m_PendingSeekExpectedMs}ms reservedActual={m_PendingSeekActualMs}ms reservedDrift={m_PendingSeekDriftMs:F1}ms");
                return;
            }

            m_LastSeekTimeSec = Time.unscaledTime;
            m_SeekCount++;
            m_ObservationCount = 0;
            m_PreviousDriftMs = 0f;
            m_HasPendingSeek = false;
            m_RecentUserCues.Clear();

            if (hasCue)
            {
                Debug.Log(
                    $"[BGMSyncController] Seek実行 target=0ms reservedExpected={m_PendingSeekExpectedMs}ms " +
                    $"reservedActual={m_PendingSeekActualMs}ms reservedDrift={m_PendingSeekDriftMs:F1}ms " +
                    $"lastCue={lastCue.CueName} cueIndex={lastCue.BeatInBar} cueActual={lastCue.ReceivedActualPositionMs}ms seekCount={m_SeekCount}");
            }
            else
            {
                Debug.Log(
                    $"[BGMSyncController] Seek実行 target=0ms reservedExpected={m_PendingSeekExpectedMs}ms " +
                    $"reservedActual={m_PendingSeekActualMs}ms reservedDrift={m_PendingSeekDriftMs:F1}ms " +
                    $"lastCue=none seekCount={m_SeekCount}");
            }

            m_PendingSeekDriftMs = 0f;
            m_PendingSeekExpectedMs = 0;
            m_PendingSeekActualMs = 0;
        }

        private int NormalizePositionMs(int positionMs)
        {
            int barDurationMs = Mathf.RoundToInt(m_BarDurationSec * 1000f);

            if (barDurationMs <= 0)
            {
                return positionMs;
            }

            positionMs %= barDurationMs;

            if (positionMs < 0)
            {
                positionMs += barDurationMs;
            }

            return positionMs;
        }

        private bool TryGetLatestUserCue(out UserCueSnapshot snapshot)
        {
            if (m_RecentUserCues.Count <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = m_RecentUserCues[m_RecentUserCues.Count - 1];
            return true;
        }

        private void TryExecutePendingSeekFromPreBeat(string preBeatCueName, int nextBeatInBar, int preBeatCount)
        {
            if (!m_HasPendingSeek)
            {
                return;
            }

            if (nextBeatInBar != m_PendingExecuteWindowNextBeatInBar)
            {
                return;
            }

            if (preBeatCount != m_PendingExecutePreBeatIndex)
            {
                if (preBeatCount > m_PendingExecutePreBeatIndex)
                {
                    InvalidatePendingSeek("preBeatWindowExpired");
                }
                return;
            }

            if (m_SeekEvent == null || m_Owner == null)
            {
                InvalidatePendingSeek("seekEventOrOwnerMissingAtExecute");
                return;
            }

            if (m_PlayingId == 0u)
            {
                InvalidatePendingSeek("playingIdInvalidAtExecute");
                return;
            }

            if (m_SeekCountInCurrentBarWindow >= m_MaxSeekCountPerBarWindow)
            {
                InvalidatePendingSeek("bar内seek回数上限AtExecute");
                return;
            }

            if (Time.unscaledTime - m_LastSeekTimeSec < m_SeekCooldownSec)
            {
                InvalidatePendingSeek("cooldownMiss");
                return;
            }

            if (m_GameState == null || !m_GameState.GameActive || m_GameState.TimeLeft <= 0)
            {
                InvalidatePendingSeek("gameInactiveAtExecute");
                return;
            }

            if (!TryGetActualPositionMs(out int actualMsNow))
            {
                InvalidatePendingSeek("actualPosition取得失敗");
                return;
            }

            int expectedMsNow = CalculateExpectedPositionMs();
            int barDurationMs = Mathf.RoundToInt(m_BarDurationSec * 1000f);
            float driftMsNow = CalculateWrappedDriftMs(expectedMsNow, actualMsNow, barDurationMs);

            if (Mathf.Abs(driftMsNow) < m_SeekThresholdMs)
            {
                InvalidatePendingSeek("driftRecovered");
                return;
            }

            if (Mathf.Sign(driftMsNow) != Mathf.Sign(m_PendingSeekDriftMs))
            {
                InvalidatePendingSeek("driftSignChanged");
                return;
            }

            AKRESULT result = AkUnitySoundEngine.SeekOnEvent(m_SeekEvent.Id, m_Owner, m_PendingSeekTargetMs, false, m_PlayingId);

            if (result != AKRESULT.AK_Success)
            {
                Debug.LogWarning(
                    $"[BGMSyncController] Seek失敗 result={result} preCue={preBeatCueName} " +
                    $"alignBeat={m_PendingAlignBeatInBar} executeNextBeat={m_PendingExecuteWindowNextBeatInBar} " +
                    $"executePreBeat={m_PendingExecutePreBeatIndex} targetMs={m_PendingSeekTargetMs}ms " +
                    $"expectedNow={expectedMsNow}ms actualNow={actualMsNow}ms driftNow={driftMsNow:F1}ms");
                InvalidatePendingSeek("seekCallFailed");
                return;
            }

            m_LastSeekTimeSec = Time.unscaledTime;
            m_LastSeekAppliedTimeSec = Time.unscaledTime;
            m_SeekCount++;
            m_SeekCountInCurrentBarWindow++;
            m_ObservationCount = 0;
            m_PreviousDriftMs = 0f;

            Debug.Log(
                $"[BGMSyncController] Seek実行 preCue={preBeatCueName} lastBeatCue={m_LastBeatCueName} " +
                $"alignBeat={m_PendingAlignBeatInBar} executeNextBeat={m_PendingExecuteWindowNextBeatInBar} " +
                $"executePreBeat={m_PendingExecutePreBeatIndex} targetMs={m_PendingSeekTargetMs}ms " +
                $"expectedNow={expectedMsNow}ms actualNow={actualMsNow}ms driftNow={driftMsNow:F1}ms seekCount={m_SeekCount}");

            ClearPendingSeek();
        }

        private bool TryParseBeatCueLocation(string cueName, out int measureIndex, out int beatInBar)
        {
            measureIndex = 0;
            beatInBar = 0;

            if (string.IsNullOrEmpty(cueName))
            {
                return false;
            }

            if (!cueName.StartsWith("Beat"))
            {
                return false;
            }

            string suffix = cueName.Substring(4);

            if (suffix.Length < 2)
            {
                return false;
            }

            string beatPart = suffix.Substring(suffix.Length - 1, 1);
            string measurePart = suffix.Substring(0, suffix.Length - 1);

            if (!int.TryParse(measurePart, out measureIndex))
            {
                return false;
            }

            if (!int.TryParse(beatPart, out beatInBar))
            {
                return false;
            }

            if (measureIndex < 1)
            {
                return false;
            }

            if (beatInBar < 1 || beatInBar > m_BeatsPerBar)
            {
                return false;
            }

            return true;
        }

        private bool TryParsePreBeatCueNumber(string cueName, out int preBeatNumber)
        {
            preBeatNumber = 0;

            if (string.IsNullOrEmpty(cueName))
            {
                return false;
            }

            if (!cueName.StartsWith("PreBeat"))
            {
                return false;
            }

            string suffix = cueName.Substring(7);
            return int.TryParse(suffix, out preBeatNumber) && preBeatNumber >= 1;
        }

        private int GetNextBeatInBar(int beatInBar)
        {
            int clampedBeat = Mathf.Clamp(beatInBar, 1, m_BeatsPerBar);
            return (clampedBeat % m_BeatsPerBar) + 1;
        }

        private int GetBeatStartPositionMs(int beatInBar)
        {
            int beatDurationMs = Mathf.RoundToInt(m_BeatDurationSec * 1000f);
            int targetMs = (beatInBar - 1) * beatDurationMs;
            return NormalizePositionMs(targetMs);
        }

        private void InvalidatePendingSeek(string reason)
        {
            if (m_EnableDebugLog)
            {
                Debug.Log(
                    $"[BGMSyncController] Seek失効 reason={reason} alignBeat={m_PendingAlignBeatInBar} " +
                    $"executeNextBeat={m_PendingExecuteWindowNextBeatInBar} targetMs={m_PendingSeekTargetMs}ms " +
                    $"lastBeatCue={(m_HasLastBeatCue ? m_LastBeatCueName : "none")} lastPreBeat={m_LastPreBeatName}");
            }

            ClearPendingSeek();
        }

        private void ClearPendingSeek()
        {
            m_HasPendingSeek = false;
            m_PendingSeekDriftMs = 0f;
            m_PendingSeekExpectedMs = 0;
            m_PendingSeekActualMs = 0;
            m_PendingAlignBeatInBar = 0;
            m_PendingExecuteWindowNextBeatInBar = 0;
            m_PendingExecutePreBeatIndex = 0;
            m_PendingCreatedServerBeat = 0;
            m_PendingSeekTargetMs = 0;
        }

        private void LogSeekSkip(string reason, int expectedMs, int actualMs, float driftMs)
        {
            if (!m_EnableDebugLog)
            {
                return;
            }

            if (Time.unscaledTime - m_LastSeekSkipLogTimeSec < m_DebugLogIntervalSec)
            {
                return;
            }

            string lastBeatText = m_HasLastBeatCue
                ? $"{m_LastBeatCueName}(measure={m_LastBeatCueMeasure}, beat={m_LastBeatCueBeatInBar})"
                : "none";

            string lastPreBeatText = string.IsNullOrEmpty(m_LastPreBeatName)
                ? "none"
                : m_LastPreBeatName;

            Debug.Log(
                $"[BGMSyncController] Seek見送り reason={reason} expected={expectedMs}ms actual={actualMs}ms drift={driftMs:F1}ms " +
                $"lastBeatCue={lastBeatText} lastPreBeat={lastPreBeatText} preCount={m_PreBeatCountSinceLastBeat} " +
                $"pendingAlign={m_PendingAlignBeatInBar} pendingExecuteNext={m_PendingExecuteWindowNextBeatInBar} " +
                $"pendingExecutePreBeat={m_PendingExecutePreBeatIndex} pendingMs={m_PendingSeekTargetMs}ms " +
                $"obs={m_ObservationCount} seekCount={m_SeekCount} barSeekCount={m_SeekCountInCurrentBarWindow}");

            m_LastSeekSkipLogTimeSec = Time.unscaledTime;
        }
    }
}