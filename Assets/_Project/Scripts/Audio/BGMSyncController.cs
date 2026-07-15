using UnityEngine;
using GamblingAction.Domain;

namespace GamblingAction.Audio
{
    /// <summary>
    /// サーバー拍を基準にした近似再生位置と、実際の再生位置との差分を観測し、
    /// Cue 未取得時は expectedMs、Cue 取得後は Cue 基準位置へ Seek する。
    /// </summary>
    public class BGMSyncController : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool m_EnableDebugLog = true;
        [SerializeField] private float m_DebugLogIntervalSec = 0.25f;

        [Header("Seek Correction")]
        [SerializeField] private float m_WarmupDurationSec = 0.5f;
        [SerializeField] private int m_MinObservationCountBeforeSeek = 2;
        [SerializeField] private float m_SeekThresholdMs = 180f;
        [SerializeField] private float m_SeekCooldownSec = 0.75f;
        [SerializeField] private int m_MaxSeekCountPerPlay = 25;
        [SerializeField] private float m_PostSeekSettleDurationSec = 0.12f;
        [SerializeField] private int m_MaxSeekCountPerBarWindow = 2;
        [SerializeField] private float m_SeekAfterRtpcFailureThresholdMs = 140f;
        [SerializeField] private int m_MinRemainingMsToReserveSeek = 24;
        [SerializeField] private int m_MaxAudibleSeekJumpMs = 240;
        [SerializeField] private float m_ForceSeekDriftMs = 420f;
        [SerializeField] private float m_FocusRecoveryLockThresholdMs = 200f;
        [SerializeField] private float m_FocusRecoveryUnlockThresholdMs = 80f;
        [SerializeField] private int m_FocusRecoveryUnlockStableFrames = 8;
        [SerializeField] private float m_FocusRecoveryResumeDelaySec = 0.25f;

        [Header("RTPC Correction")]
        [SerializeField] private string m_PlaybackSpeedRtpcName = "playbackSpeed";
        [SerializeField] private float m_RtpcNeutralValue = 1.0f;
        [SerializeField] private float m_RtpcMaxDelta = 0.04f;
        [SerializeField] private float m_RtpcStartThresholdMs = 35f;
        [SerializeField] private float m_RtpcStopThresholdMs = 18f;
        [SerializeField] private float m_RtpcMaxEvaluateSec = 0.30f;
        [SerializeField] private float m_RtpcMinImproveMs = 10f;

        private IBeatClock m_BeatClock;
        private IGameState m_GameState;
        private GameObject m_Owner;
        private AK.Wwise.Event m_SeekEvent;

        private uint m_PlayingId = 0u;
        private bool m_IsSyncActive = false;

        private int m_LastReceivedBeat = 1;
        private float m_LastServerBeatReceivedLocalTimeSec = 0f;
        private long m_LastServerBeatSequence = 0;
        private long m_LastServerRoundId = 0;

        private float m_BeatDurationSec = 0.375f;
        private int m_BeatsPerBar = 4;
        private float m_BarDurationSec = 1.5f;

        private float m_LastDebugLogTimeSec = 0f;
        private float m_LastSeekSkipLogTimeSec = -999f;

        private float m_SyncStartTimeSec = 0f;
        private float m_LastSeekTimeSec = -999f;
        private float m_LastSeekAppliedTimeSec = -999f;
        private int m_ObservationCount = 0;
        private int m_SeekCount = 0;
        private int m_SeekCountInCurrentBarWindow = 0;
        private int m_LastKnownTimeLeft = -1;

        private bool m_HasPendingSeek = false;
        private int m_PendingSeekTargetMs = 0;
        private int m_PendingServerTargetAbsoluteBeat = 0;
        private int m_PendingBgmTargetAbsoluteBeat = 0;
        private int m_PendingBgmLoopTargetBeatInBar = 0;
        private int m_PendingRemainingMsToBoundary = 0;
        private int m_PendingExecuteWindowLoopStartBeatInBar = 0;
        private int m_PendingExecuteWindowLoopEndBeatInBar = 0;
        private int m_PendingExecutePreBeatMeasure = 0;
        private int m_PendingExecutePreBeatSlot = 0;
        private int m_PendingCreatedRoundId = 0;
        private bool m_PendingCreatedFromRtpcFailure = false;

        private bool m_HasLastBeatCue = false;
        private string m_LastBeatCueName = string.Empty;
        private int m_LastBeatCueMeasure = 0;
        private int m_LastBeatCueBeatInBar = 0;
        private string m_LastPreBeatName = string.Empty;
        private int m_PreBeatCountSinceLastBeat = 0;

        private bool m_FocusRecoveryPending = false;
        private bool m_FocusRecoveryLocked = false;
        private int m_FocusRecoveryRecoverCount = 0;
        private float m_FocusRecoveryResumeUntilSec = -1f;

        private bool m_RtpcActive = false;
        private int m_RtpcTargetAbsoluteBeat = 0;
        private float m_RtpcStartDriftMs = 0f;
        private float m_RtpcLastEvaluatedDriftMs = 0f;
        private float m_RtpcStartTimeSec = 0f;
        private bool m_RtpcFailedForTarget = false;
        private int m_RtpcFailedTargetAbsoluteBeat = 0;

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
            m_LastServerBeatReceivedLocalTimeSec = 0f;
            m_LastServerBeatSequence = 0;
            m_LastServerRoundId = 0;

            m_PendingExecutePreBeatMeasure = 0;
            m_PendingExecutePreBeatSlot = 0;

            m_LastDebugLogTimeSec = 0f;
            m_LastSeekSkipLogTimeSec = -999f;

            m_SyncStartTimeSec = Time.unscaledTime;
            m_ObservationCount = 0;
            m_LastSeekTimeSec = -999f;
            m_SeekCount = 0;
            m_LastKnownTimeLeft = m_GameState.TimeLeft;

            m_HasPendingSeek = false;
            m_PendingSeekTargetMs = 0;
            
            m_FocusRecoveryPending = false;
            m_FocusRecoveryLocked = false;
            m_FocusRecoveryRecoverCount = 0;
            m_FocusRecoveryResumeUntilSec = -1f;

            m_HasLastBeatCue = false;
            m_LastBeatCueName = string.Empty;
            m_LastBeatCueMeasure = 0;
            m_LastBeatCueBeatInBar = 0;
            m_LastPreBeatName = string.Empty;
            m_PreBeatCountSinceLastBeat = 0;

            LastDriftMs = 0f;
            LastExpectedPositionMs = 0;
            LastActualPositionMs = 0;

            if (m_GameState != null && m_GameState.BeatIntervalMs > 0)
            {
                m_BeatDurationSec = m_GameState.BeatIntervalMs / 1000f;
                m_BeatsPerBar = m_GameState.BeatsPerBar > 0 ? m_GameState.BeatsPerBar : 4;
                m_BarDurationSec = m_BeatDurationSec * m_BeatsPerBar;
            }

            ResetRtpc("syncStart", true);
        }

        public void StopSync()
        {
            m_IsSyncActive = false;
            m_PlayingId = 0u;

            m_LastServerBeatReceivedLocalTimeSec = 0f;
            m_LastServerBeatSequence = 0;
            m_LastServerRoundId = 0;

            m_ObservationCount = 0;
            m_SeekCount = 0;
            m_LastKnownTimeLeft = -1;

            m_PendingExecutePreBeatMeasure = 0;
            m_PendingExecutePreBeatSlot = 0;

            m_HasPendingSeek = false;
            m_PendingSeekTargetMs = 0;

            m_FocusRecoveryPending = false;
            m_FocusRecoveryLocked = false;
            m_FocusRecoveryRecoverCount = 0;
            m_FocusRecoveryResumeUntilSec = -1f;

            m_HasLastBeatCue = false;
            m_LastBeatCueName = string.Empty;
            m_LastBeatCueMeasure = 0;
            m_LastBeatCueBeatInBar = 0;
            m_LastPreBeatName = string.Empty;
            m_PreBeatCountSinceLastBeat = 0;

            LastDriftMs = 0f;
            LastExpectedPositionMs = 0;
            LastActualPositionMs = 0;

            ResetRtpc("syncStop", true);
        }

        public void HandleBeat(int beat)
        {
            if (!m_IsSyncActive)
            {
                return;
            }

            m_LastReceivedBeat = beat;

            if (m_GameState == null)
            {
                return;
            }

            if (m_GameState.BeatIntervalMs <= 0 || m_GameState.BeatsPerBar <= 0)
            {
                return;
            }

            m_LastServerBeatReceivedLocalTimeSec = Time.unscaledTime;
            m_LastServerBeatSequence = m_GameState.BeatSequence;
            m_LastServerRoundId = m_GameState.RoundId;
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
            bool hadLastBeatCue = m_HasLastBeatCue;
            int previousMeasure = m_LastBeatCueMeasure;

            m_HasLastBeatCue = true;
            m_LastBeatCueName = info.userCueName;
            m_LastBeatCueMeasure = measureIndex;
            m_LastBeatCueBeatInBar = beatInBar;
            m_PreBeatCountSinceLastBeat = 0;

            if (hadLastBeatCue && measureIndex != previousMeasure)
            {
                m_SeekCountInCurrentBarWindow = 0;

                if (m_SeekCount > 0)
                {
                    m_SeekCount = Mathf.Max(0, m_SeekCount - 1);
                }
            }

            if (m_HasPendingSeek)
            {
                if (m_GameState != null && m_GameState.NextAbsoluteBeat > m_PendingServerTargetAbsoluteBeat)
                {
                    InvalidatePendingSeek("serverTargetPassedByState");
                }
                else if (beatInBar == m_PendingExecuteWindowLoopEndBeatInBar)
                {
                    InvalidatePendingSeek("windowExpiredByBeatCue");
                }
            }

            if (m_EnableDebugLog)
            {
                Debug.Log(
                    $"[BGMSyncController] BeatCue cue={info.userCueName} measure={measureIndex} beat={beatInBar} " +
                    $"actual={actualPositionMs}ms serverBeat={m_LastReceivedBeat}");
            }
        }

        private void HandlePreBeatCue(AkMusicSyncCallbackInfo info)
        {
            m_LastPreBeatName = info.userCueName;

            if (!TryParsePreBeatCueLocation(info.userCueName, out int measureIndex, out int preBeatSlot))
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
                    $"[BGMSyncController] PreBeatCue cue={info.userCueName} measure={measureIndex} slot={preBeatSlot} " +
                    $"lastBeat={m_LastBeatCueName} nextBeat={nextBeatInBar} preCount={m_PreBeatCountSinceLastBeat}");
            }

            TryExecutePendingSeekFromPreBeatStage2(info.userCueName, measureIndex, preBeatSlot);
        }

        private void Update()
        {
            if (!m_IsSyncActive || m_PlayingId == 0u)
            {
                return;
            }

            if (m_GameState == null || !m_GameState.GameActive || m_GameState.TimeLeft <= 0)
            {
                if (m_RtpcActive)
                {
                    ResetRtpc("gameInactive", true);
                }
                return;
            }

            if (m_GameState.BeatIntervalMs > 0)
            {
                m_BeatDurationSec = m_GameState.BeatIntervalMs / 1000f;
                m_BeatsPerBar = m_GameState.BeatsPerBar > 0 ? m_GameState.BeatsPerBar : 4;
                m_BarDurationSec = m_BeatDurationSec * m_BeatsPerBar;
            }

            if (m_LastKnownTimeLeft >= 0 && m_GameState.TimeLeft > m_LastKnownTimeLeft)
            {
                m_ObservationCount = 0;
                m_SeekCount = 0;
                m_SeekCountInCurrentBarWindow = 0;
                ClearPendingSeek();
                ResetRtpc("roundRestart", true);
            }

            m_LastKnownTimeLeft = m_GameState.TimeLeft;

            if (Time.unscaledTime - m_LastSeekAppliedTimeSec < m_PostSeekSettleDurationSec)
            {
                if (m_RtpcActive)
                {
                    ResetRtpc("postSeekSettling", false);
                }
                return;
            }

            if (!TryCalculateExpectedPositionMsFromServer(out int expectedMs, out int remainingMsToNextBoundary, out int targetAbsoluteBeat))
            {
                return;
            }

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

            if (m_FocusRecoveryPending)
            {
                m_FocusRecoveryPending = false;

                if (Mathf.Abs(driftMs) >= m_FocusRecoveryLockThresholdMs)
                {
                    m_FocusRecoveryLocked = true;
                    m_FocusRecoveryRecoverCount = 0;

                    if (m_RtpcActive)
                    {
                        ResetRtpc("focusRecoveryLock", false);
                    }

                    if (m_HasPendingSeek)
                    {
                        InvalidatePendingSeek("focusRecoveryLock");
                    }

                    if (m_EnableDebugLog)
                    {
                        Debug.Log(
                            $"[BGMSyncController] focus復帰判定 lock drift={driftMs:F1}ms threshold={m_FocusRecoveryLockThresholdMs:F1}ms");
                    }

                    return;
                }

                if (m_EnableDebugLog)
                {
                    Debug.Log(
                        $"[BGMSyncController] focus復帰判定 unlock drift={driftMs:F1}ms threshold={m_FocusRecoveryLockThresholdMs:F1}ms");
                }
            }

            if (m_FocusRecoveryLocked)
            {
                if (Mathf.Abs(driftMs) <= m_FocusRecoveryUnlockThresholdMs)
                {
                    m_FocusRecoveryRecoverCount++;

                    if (m_FocusRecoveryRecoverCount >= m_FocusRecoveryUnlockStableFrames)
                    {
                        m_FocusRecoveryLocked = false;
                        m_FocusRecoveryRecoverCount = 0;
                        m_FocusRecoveryResumeUntilSec = Time.unscaledTime + m_FocusRecoveryResumeDelaySec;

                        if (m_EnableDebugLog)
                        {
                            Debug.Log(
                                $"[BGMSyncController] focusRecovery解除 frame={Time.frameCount} now={Time.unscaledTime:F3} " +
                                $"drift={driftMs:F1}ms unlockThreshold={m_FocusRecoveryUnlockThresholdMs:F1}ms " +
                                $"stableFrames={m_FocusRecoveryUnlockStableFrames} resumeUntil={m_FocusRecoveryResumeUntilSec:F3}");
                        }

                        return;
                    }
                    else
                    {
                        if (m_EnableDebugLog && Time.unscaledTime - m_LastSeekSkipLogTimeSec >= m_DebugLogIntervalSec)
                        {
                            Debug.Log(
                                $"[BGMSyncController] 補正停止中 reason=focusRecovery drift={driftMs:F1}ms recoverCount={m_FocusRecoveryRecoverCount}");
                            m_LastSeekSkipLogTimeSec = Time.unscaledTime;
                        }

                        return;
                    }
                }
                else
                {
                    m_FocusRecoveryRecoverCount = 0;

                    if (m_EnableDebugLog && Time.unscaledTime - m_LastSeekSkipLogTimeSec >= m_DebugLogIntervalSec)
                    {
                        Debug.Log(
                            $"[BGMSyncController] 補正停止中 reason=focusRecovery drift={driftMs:F1}ms recoverCount=0");
                        m_LastSeekSkipLogTimeSec = Time.unscaledTime;
                    }

                    return;
                }
            }

            if (m_EnableDebugLog && Time.unscaledTime - m_LastDebugLogTimeSec >= m_DebugLogIntervalSec)
            {
                m_LastDebugLogTimeSec = Time.unscaledTime;
            }

            if (Time.unscaledTime < m_FocusRecoveryResumeUntilSec)
            {
                if (m_EnableDebugLog && Time.unscaledTime - m_LastSeekSkipLogTimeSec >= m_DebugLogIntervalSec)
                {
                    Debug.Log(
                        $"[BGMSyncController] 補正再開待機中 frame={Time.frameCount} now={Time.unscaledTime:F3} " +
                        $"resumeUntil={m_FocusRecoveryResumeUntilSec:F3} remain={(m_FocusRecoveryResumeUntilSec - Time.unscaledTime):F3} " +
                        $"drift={driftMs:F1}ms");
                    m_LastSeekSkipLogTimeSec = Time.unscaledTime;
                }

                return;
            }

            EvaluateRtpcCorrection(expectedMs, actualMs, driftMs, remainingMsToNextBoundary, targetAbsoluteBeat);

            if (!m_RtpcActive)
            {
                TryReserveSeekCorrectionStage2(expectedMs, actualMs, driftMs, remainingMsToNextBoundary, targetAbsoluteBeat);
            }
        }

        private float CalculatePlaybackSpeedRtpcValue(float driftMs)
        {
            float normalized = Mathf.Clamp(driftMs / m_SeekThresholdMs, -1f, 1f);
            return m_RtpcNeutralValue + (normalized * m_RtpcMaxDelta);
        }

        private void EvaluateRtpcCorrection(int expectedMs, int actualMs, float driftMs, int remainingMsToNextBoundary, int targetAbsoluteBeat)
        {
            if (string.IsNullOrEmpty(m_PlaybackSpeedRtpcName) || m_Owner == null)
            {
                return;
            }

            if (targetAbsoluteBeat <= 0)
            {
                if (m_RtpcActive)
                {
                    ResetRtpc("invalidTarget", false);
                }
                return;
            }

            if (m_RtpcFailedForTarget && m_RtpcFailedTargetAbsoluteBeat != targetAbsoluteBeat)
            {
                m_RtpcFailedForTarget = false;
                m_RtpcFailedTargetAbsoluteBeat = 0;
            }

            float absDriftMs = Mathf.Abs(driftMs);

            if (m_HasPendingSeek)
            {
                if (m_RtpcActive)
                {
                    ResetRtpc("pendingSeek", false);
                }
                return;
            }

            if (absDriftMs <= m_RtpcStopThresholdMs)
            {
                if (m_RtpcActive)
                {
                    ResetRtpc("driftConverged", true);
                }
                return;
            }

            if (absDriftMs >= m_SeekThresholdMs)
            {
                if (m_RtpcActive)
                {
                    MarkRtpcFailure(targetAbsoluteBeat, "driftTooLarge", driftMs);
                }
                return;
            }

            if (remainingMsToNextBoundary <= 0)
            {
                if (m_RtpcActive)
                {
                    MarkRtpcFailure(targetAbsoluteBeat, "boundaryPassed", driftMs);
                }
                return;
            }

            if (m_RtpcFailedForTarget && m_RtpcFailedTargetAbsoluteBeat == targetAbsoluteBeat)
            {
                return;
            }

            if (!m_RtpcActive)
            {
                if (absDriftMs < m_RtpcStartThresholdMs)
                {
                    return;
                }

                m_RtpcActive = true;
                m_RtpcTargetAbsoluteBeat = targetAbsoluteBeat;
                m_RtpcStartDriftMs = driftMs;
                m_RtpcLastEvaluatedDriftMs = driftMs;
                m_RtpcStartTimeSec = Time.unscaledTime;

                if (m_EnableDebugLog)
                {
                    Debug.Log(
                        $"[BGMSyncController] RTPC開始 targetAbs={targetAbsoluteBeat} expected={expectedMs}ms actual={actualMs}ms drift={driftMs:F1}ms");
                }
            }
            else if (m_RtpcTargetAbsoluteBeat != targetAbsoluteBeat)
            {
                ResetRtpc("targetAdvanced", true);
                return;
            }

            float rtpcValue = CalculatePlaybackSpeedRtpcValue(driftMs);
            WwiseGameSyncAPI.SetPlaybackSpeedRtpc(m_PlaybackSpeedRtpcName, rtpcValue, m_Owner);

            float activeDurationSec = Time.unscaledTime - m_RtpcStartTimeSec;
            float totalImprovementMs = Mathf.Abs(m_RtpcStartDriftMs) - absDriftMs;
            float worseningMs = absDriftMs - Mathf.Abs(m_RtpcLastEvaluatedDriftMs);

            if (activeDurationSec >= m_RtpcMaxEvaluateSec && totalImprovementMs < m_RtpcMinImproveMs)
            {
                MarkRtpcFailure(targetAbsoluteBeat, "improve不足", driftMs);
                return;
            }

            if (activeDurationSec >= 0.10f && worseningMs > m_RtpcMinImproveMs)
            {
                MarkRtpcFailure(targetAbsoluteBeat, "drift悪化", driftMs);
                return;
            }

            m_RtpcLastEvaluatedDriftMs = driftMs;
        }

        private void MarkRtpcFailure(int targetAbsoluteBeat, string reason, float driftMs)
        {
            m_RtpcFailedForTarget = true;
            m_RtpcFailedTargetAbsoluteBeat = targetAbsoluteBeat;

            if (m_EnableDebugLog)
            {
                Debug.Log(
                    $"[BGMSyncController] RTPC失敗 reason={reason} targetAbs={targetAbsoluteBeat} drift={driftMs:F1}ms");
            }

            ResetRtpc("failure", false);
        }

        private void ResetRtpc(string reason, bool clearFailureState)
        {
            if (!string.IsNullOrEmpty(m_PlaybackSpeedRtpcName) && m_Owner != null)
            {
                WwiseGameSyncAPI.ResetPlaybackSpeedRtpc(m_PlaybackSpeedRtpcName, m_RtpcNeutralValue, m_Owner);
            }

            if (m_EnableDebugLog && m_RtpcActive)
            {
                Debug.Log($"[BGMSyncController] RTPC解除 reason={reason}");
            }

            m_RtpcActive = false;
            m_RtpcTargetAbsoluteBeat = 0;
            m_RtpcStartDriftMs = 0f;
            m_RtpcLastEvaluatedDriftMs = 0f;
            m_RtpcStartTimeSec = 0f;

            if (clearFailureState)
            {
                m_RtpcFailedForTarget = false;
                m_RtpcFailedTargetAbsoluteBeat = 0;
            }
        }

        private bool TryCalculateExpectedPositionMsFromServer(out int expectedMs, out int remainingMsToNextBoundary, out int targetAbsoluteBeat)
        {
            expectedMs = 0;
            remainingMsToNextBoundary = 0;
            targetAbsoluteBeat = 0;

            if (m_GameState == null)
            {
                return false;
            }

            if (m_GameState.BeatIntervalMs <= 0 || m_GameState.BeatsPerBar <= 0)
            {
                return false;
            }

            if (m_GameState.CurrentBeat <= 0 || m_GameState.CurrentBarIndex <= 0)
            {
                return false;
            }

            if (m_LastServerBeatReceivedLocalTimeSec <= 0f)
            {
                return false;
            }

            if (m_LastServerBeatSequence != m_GameState.BeatSequence)
            {
                return false;
            }

            if (m_LastServerRoundId != m_GameState.RoundId)
            {
                return false;
            }

            int beatIntervalMs = m_GameState.BeatIntervalMs;
            int beatsPerBar = m_GameState.BeatsPerBar;
            int barDurationMs = beatIntervalMs * beatsPerBar;

            float elapsedLocalSec = Time.unscaledTime - m_LastServerBeatReceivedLocalTimeSec;
            elapsedLocalSec = Mathf.Max(0f, elapsedLocalSec);
            int elapsedLocalMs = Mathf.RoundToInt(elapsedLocalSec * 1000f);

            int elapsedInBeatMs = Mathf.Clamp(elapsedLocalMs, 0, beatIntervalMs - 1);
            remainingMsToNextBoundary = Mathf.Clamp(beatIntervalMs - elapsedLocalMs, 0, beatIntervalMs);

            int beatStartOffsetMs = (m_GameState.CurrentBeat - 1) * beatIntervalMs;
            expectedMs = beatStartOffsetMs + elapsedInBeatMs;

            if (barDurationMs > 0)
            {
                expectedMs %= barDurationMs;
                if (expectedMs < 0)
                {
                    expectedMs += barDurationMs;
                }
            }

            targetAbsoluteBeat = m_GameState.NextAbsoluteBeat;
            return true;
        }

        private bool TryBuildSeekPlan(int remainingMsToNextBoundary, int serverTargetAbsoluteBeat, out int bgmTargetAbsoluteBeat, out int seekTargetMs, out int executeWindowStartBeatInBar, out int executeWindowEndBeatInBar)
        {
            bgmTargetAbsoluteBeat = 0;
            seekTargetMs = 0;
            executeWindowStartBeatInBar = 0;
            executeWindowEndBeatInBar = 0;

            if (m_GameState == null)
            {
                return false;
            }

            if (!m_HasLastBeatCue)
            {
                return false;
            }

            if (m_BeatsPerBar <= 0)
            {
                return false;
            }

            if (serverTargetAbsoluteBeat <= 0)
            {
                return false;
            }

            if (remainingMsToNextBoundary < m_MinRemainingMsToReserveSeek)
            {
                return false;
            }

            int beatDurationMs = Mathf.RoundToInt(m_BeatDurationSec * 1000f);
            int barDurationMs = Mathf.RoundToInt(m_BarDurationSec * 1000f);

            if (beatDurationMs <= 0 || barDurationMs <= 0)
            {
                return false;
            }

            if (!TryGetNextReservablePreBeat(out int executePreBeatMeasure, out int executePreBeatSlot))
            {
                return false;
            }

            int serverTargetBeatInBar = ((serverTargetAbsoluteBeat - 1) % m_BeatsPerBar) + 1;
            bgmTargetAbsoluteBeat = serverTargetAbsoluteBeat;

            seekTargetMs = 0;

            executeWindowStartBeatInBar = m_LastBeatCueBeatInBar;
            executeWindowEndBeatInBar = GetNextBeatInBar(m_LastBeatCueBeatInBar);

            m_PendingBgmLoopTargetBeatInBar = serverTargetBeatInBar;
            m_PendingExecutePreBeatMeasure = executePreBeatMeasure;
            m_PendingExecutePreBeatSlot = executePreBeatSlot;

            return true;
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


        private bool TryParsePreBeatCueLocation(string cueName, out int measureIndex, out int preBeatSlot)
        {
            measureIndex = 0;
            preBeatSlot = 0;

            if (string.IsNullOrEmpty(cueName))
            {
                return false;
            }

            if (!cueName.StartsWith("PreBeat"))
            {
                return false;
            }

            string suffix = cueName.Substring(7);

            if (suffix.Length < 2)
            {
                return false;
            }

            string slotPart = suffix.Substring(suffix.Length - 1, 1);
            string measurePart = suffix.Substring(0, suffix.Length - 1);

            if (!int.TryParse(measurePart, out measureIndex))
            {
                return false;
            }

            if (!int.TryParse(slotPart, out preBeatSlot))
            {
                return false;
            }

            if (measureIndex < 1)
            {
                return false;
            }

            if (preBeatSlot < 1 || preBeatSlot > 8)
            {
                return false;
            }

            return true;
        }

        private void TryReserveSeekCorrectionStage2(int expectedMs, int actualMs, float driftMs, int remainingMsToNextBoundary, int serverTargetAbsoluteBeat)
        {
            if (m_SeekEvent == null || m_Owner == null)
            {
                LogSeekSkip("seekEventOrOwnerMissing", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_GameState == null)
            {
                LogSeekSkip("gameStateMissing", expectedMs, actualMs, driftMs);
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

            if (m_PreBeatCountSinceLastBeat >= 2)
            {
                LogSeekSkip("現在区間のPreBeat消費済み", expectedMs, actualMs, driftMs);
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

            if (Time.unscaledTime - m_LastSeekTimeSec < m_SeekCooldownSec)
            {
                LogSeekSkip("cooldown中", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_SeekCount >= m_MaxSeekCountPerPlay && Mathf.Abs(driftMs) < (m_SeekThresholdMs * 2f))
            {
                LogSeekSkip("seek回数上限", expectedMs, actualMs, driftMs);
                return;
            }

            if (m_SeekCountInCurrentBarWindow >= m_MaxSeekCountPerBarWindow)
            {
                LogSeekSkip("bar内seek回数上限", expectedMs, actualMs, driftMs);
                return;
            }

            float absDriftMs = Mathf.Abs(driftMs);
            bool rtpcFailedForThisTarget =
                m_RtpcFailedForTarget &&
                m_RtpcFailedTargetAbsoluteBeat == serverTargetAbsoluteBeat;

            if (remainingMsToNextBoundary < m_MinRemainingMsToReserveSeek)
            {
                LogSeekSkip("残り時間不足", expectedMs, actualMs, driftMs);
                return;
            }

            if (rtpcFailedForThisTarget && absDriftMs < m_SeekAfterRtpcFailureThresholdMs)
            {
                LogSeekSkip("RTPC失敗後drift小", expectedMs, actualMs, driftMs);
                return;
            }

            bool shouldSeek =
                absDriftMs >= m_SeekThresholdMs ||
                (rtpcFailedForThisTarget && absDriftMs >= m_SeekAfterRtpcFailureThresholdMs);

            if (!shouldSeek)
            {
                LogSeekSkip("seek不要", expectedMs, actualMs, driftMs);
                return;
            }

            if (!TryBuildSeekPlan(remainingMsToNextBoundary, serverTargetAbsoluteBeat, out int bgmTargetAbsoluteBeat, out int seekTargetMs, out int executeWindowStartBeatInBar, out int executeWindowEndBeatInBar))
            {
                LogSeekSkip("seekPlan構築失敗", expectedMs, actualMs, driftMs);
                return;
            }

            m_HasPendingSeek = true;
            m_PendingSeekTargetMs = seekTargetMs;

            m_PendingServerTargetAbsoluteBeat = serverTargetAbsoluteBeat;
            m_PendingBgmTargetAbsoluteBeat = bgmTargetAbsoluteBeat;
            m_PendingRemainingMsToBoundary = remainingMsToNextBoundary;
            m_PendingBgmLoopTargetBeatInBar = ((serverTargetAbsoluteBeat - 1) % m_BeatsPerBar) + 1;
            m_PendingExecuteWindowLoopStartBeatInBar = executeWindowStartBeatInBar;
            m_PendingExecuteWindowLoopEndBeatInBar = executeWindowEndBeatInBar;
            //m_PendingCreatedRoundId = (int)m_GameState.RoundId;
            m_PendingCreatedFromRtpcFailure =
                m_RtpcFailedForTarget && m_RtpcFailedTargetAbsoluteBeat == serverTargetAbsoluteBeat;

            Debug.Log(
                $"[BGMSyncController] Seek予約 frame={Time.frameCount} now={Time.unscaledTime:F3} " +
                $"serverTargetAbs={m_PendingServerTargetAbsoluteBeat} bgmTargetAbs={m_PendingBgmTargetAbsoluteBeat} " +
                $"loopTargetBeat={m_PendingBgmLoopTargetBeatInBar} remain={m_PendingRemainingMsToBoundary}ms " +
                $"execLoopWindow={m_PendingExecuteWindowLoopStartBeatInBar}->{m_PendingExecuteWindowLoopEndBeatInBar} " +
                $"execPreBeat={m_PendingExecutePreBeatMeasure}:{m_PendingExecutePreBeatSlot} " +
                $"expected={expectedMs}ms actual={actualMs}ms drift={driftMs:F1}ms rtpcFailed={m_PendingCreatedFromRtpcFailure}");
        }

        private void TryExecutePendingSeekFromPreBeatStage2(string preBeatCueName, int preBeatMeasure, int preBeatSlot)
        {
            if (!m_HasPendingSeek)
            {
                return;
            }

            if (m_GameState == null)
            {
                InvalidatePendingSeek("gameStateMissingAtExecute");
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

            if ((int)m_GameState.RoundId != m_PendingCreatedRoundId)
            {
                InvalidatePendingSeek("roundChanged");
                return;
            }

            int targetAbsoluteBeatNow = m_GameState.NextAbsoluteBeat;
            if (targetAbsoluteBeatNow > m_PendingServerTargetAbsoluteBeat)
            {
                InvalidatePendingSeek("serverTargetAdvanced");
                return;
            }

            if (preBeatMeasure != m_PendingExecutePreBeatMeasure)
            {
                InvalidatePendingSeek("preBeatMeasureMismatch");
                return;
            }

            if (preBeatSlot > m_PendingExecutePreBeatSlot)
            {
                InvalidatePendingSeek("preBeatWindowPassed");
                return;
            }

            if (preBeatSlot < m_PendingExecutePreBeatSlot)
            {
                return;
            }

            if (Time.unscaledTime - m_LastSeekTimeSec < m_SeekCooldownSec)
            {
                InvalidatePendingSeek("cooldownMiss");
                return;
            }

            if (m_SeekCount >= m_MaxSeekCountPerPlay)
            {
                InvalidatePendingSeek("seek回数上限AtExecute");
                return;
            }

            if (m_SeekCountInCurrentBarWindow >= m_MaxSeekCountPerBarWindow)
            {
                InvalidatePendingSeek("bar内seek回数上限AtExecute");
                return;
            }

            if (!TryGetActualPositionMs(out int actualMsNow))
            {
                InvalidatePendingSeek("actualPosition取得失敗");
                return;
            }

            if (!TryCalculateExpectedPositionMsFromServer(out int expectedMsNow, out int remainingMsNow, out int recalculatedTargetAbsoluteBeat))
            {
                InvalidatePendingSeek("expected再計算失敗");
                return;
            }

            if (recalculatedTargetAbsoluteBeat > m_PendingServerTargetAbsoluteBeat)
            {
                InvalidatePendingSeek("serverTargetAdvancedAfterRecalc");
                return;
            }

            int barDurationMs = Mathf.RoundToInt(m_BarDurationSec * 1000f);

            if (remainingMsNow < m_MinRemainingMsToReserveSeek)
            {
                InvalidatePendingSeek("execute時残り時間不足");
                return;
            }

            int seekTargetMsNow = CalculateSeekTargetMsAtExecute(m_PendingBgmLoopTargetBeatInBar, remainingMsNow);
            float driftMsNow = CalculateWrappedDriftMs(expectedMsNow, actualMsNow, barDurationMs);

            int seekJumpMs = Mathf.RoundToInt(
            CalculateWrappedDriftMs(seekTargetMsNow, actualMsNow, barDurationMs));

            if (Mathf.Abs(seekJumpMs) > m_MaxAudibleSeekJumpMs &&
                Mathf.Abs(driftMsNow) < m_ForceSeekDriftMs)
            {
                if (m_EnableDebugLog)
                {
                    Debug.Log(
                        $"[BGMSyncController] Seek見送り reason=jump大 preCue={preBeatCueName} " +
                        $"serverTargetAbs={m_PendingServerTargetAbsoluteBeat} bgmTargetAbs={m_PendingBgmTargetAbsoluteBeat} " +
                        $"loopTargetBeat={m_PendingBgmLoopTargetBeatInBar} targetMs={seekTargetMsNow}ms " +
                        $"actualNow={actualMsNow}ms jump={seekJumpMs}ms driftNow={driftMsNow:F1}ms");
                }

                InvalidatePendingSeek("jump大");
                return;
            }

            AKRESULT result = AkUnitySoundEngine.SeekOnEvent(m_SeekEvent.Id, m_Owner, seekTargetMsNow, false, m_PlayingId);
            if (result != AKRESULT.AK_Success)
            {
                Debug.LogWarning(
                    $"[BGMSyncController] Seek失敗 result={result} preCue={preBeatCueName} " +
                    $"serverTargetAbs={m_PendingServerTargetAbsoluteBeat} bgmTargetAbs={m_PendingBgmTargetAbsoluteBeat} " +
                    $"loopTargetBeat={m_PendingBgmLoopTargetBeatInBar} targetMs={seekTargetMsNow}ms " +
                    $"expectedNow={expectedMsNow}ms actualNow={actualMsNow}ms jump={seekJumpMs}ms driftNow={driftMsNow:F1}ms"); InvalidatePendingSeek("seekCallFailed");
                return;
            }

            ResetRtpc("seekApplied", true);

            m_LastSeekTimeSec = Time.unscaledTime;
            m_LastSeekAppliedTimeSec = Time.unscaledTime;
            m_SeekCount++;
            m_SeekCountInCurrentBarWindow++;
            m_ObservationCount = 0;

            Debug.Log(
                $"[BGMSyncController] Seek実行 preCue={preBeatCueName} serverTargetAbs={m_PendingServerTargetAbsoluteBeat} " +
                $"bgmTargetAbs={m_PendingBgmTargetAbsoluteBeat} loopTargetBeat={m_PendingBgmLoopTargetBeatInBar} " +
                $"targetMs={seekTargetMsNow}ms expectedNow={expectedMsNow}ms actualNow={actualMsNow}ms " +
                $"jump={seekJumpMs}ms driftNow={driftMsNow:F1}ms seekCount={m_SeekCount}");
            ClearPendingSeek();
        }

        private bool TryGetNextReservablePreBeat(out int executePreBeatMeasure, out int executePreBeatSlot)
        {
            executePreBeatMeasure = 0;
            executePreBeatSlot = 0;

            if (!m_HasLastBeatCue)
            {
                return false;
            }

            if (m_PreBeatCountSinceLastBeat >= 2)
            {
                return false;
            }

            GetPreBeatSlotRangeForStartBeat(m_LastBeatCueBeatInBar, out int firstSlot, out int secondSlot);

            executePreBeatMeasure = m_LastBeatCueMeasure;
            executePreBeatSlot = m_PreBeatCountSinceLastBeat == 0 ? firstSlot : secondSlot;
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

        private int CalculateSeekTargetMsAtExecute(int loopTargetBeatInBar, int remainingMsToNextBoundary)
        {
            int targetBoundaryMs = GetBeatStartPositionMs(loopTargetBeatInBar);
            return NormalizePositionMs(targetBoundaryMs - remainingMsToNextBoundary);
        }

        private void GetPreBeatSlotRangeForStartBeat(int startBeatInBar, out int firstSlot, out int secondSlot)
        {
            switch (startBeatInBar)
            {
                case 1:
                    firstSlot = 1;
                    secondSlot = 2;
                    break;
                case 2:
                    firstSlot = 3;
                    secondSlot = 4;
                    break;
                case 3:
                    firstSlot = 5;
                    secondSlot = 6;
                    break;
                default:
                    firstSlot = 7;
                    secondSlot = 8;
                    break;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                m_FocusRecoveryPending = true;
                m_FocusRecoveryRecoverCount = 0;

                if (m_EnableDebugLog)
                {
                    Debug.Log("[BGMSyncController] focus喪失 pending=true");
                }

                return;
            }

            if (m_EnableDebugLog)
            {
                Debug.Log("[BGMSyncController] focus復帰");
            }
        }

        private void InvalidatePendingSeek(string reason)
        {
            if (m_EnableDebugLog)
            {
                Debug.Log(
                    $"[BGMSyncController] Seek失効 reason={reason} " +
                    $"serverTargetAbs={m_PendingServerTargetAbsoluteBeat} bgmTargetAbs={m_PendingBgmTargetAbsoluteBeat} " +
                    $"loopTargetBeat={m_PendingBgmLoopTargetBeatInBar} " +
                    $"targetMs={m_PendingSeekTargetMs}ms execLoopWindow={m_PendingExecuteWindowLoopStartBeatInBar}->{m_PendingExecuteWindowLoopEndBeatInBar} " +
                    $"execPreBeat={m_PendingExecutePreBeatMeasure}:{m_PendingExecutePreBeatSlot} " +
                    $"lastBeatCue={(m_HasLastBeatCue ? m_LastBeatCueName : "none")} lastPreBeat={m_LastPreBeatName}");
            }

            ClearPendingSeek();
        }

        private void ClearPendingSeek()
        {
            m_HasPendingSeek = false;
            m_PendingSeekTargetMs = 0;
            m_PendingBgmLoopTargetBeatInBar = 0;
            m_PendingExecuteWindowLoopStartBeatInBar = 0;
            m_PendingExecuteWindowLoopEndBeatInBar = 0;
            m_PendingExecutePreBeatMeasure = 0;
            m_PendingExecutePreBeatSlot = 0;

            m_PendingServerTargetAbsoluteBeat = 0;
            m_PendingBgmTargetAbsoluteBeat = 0;
            m_PendingRemainingMsToBoundary = 0;
            m_PendingCreatedRoundId = 0;
            m_PendingCreatedFromRtpcFailure = false;
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
                $"lastBeatCue={lastBeatText} lastPreBeat={lastPreBeatText} " +
                $"pendingServerTargetAbs={m_PendingServerTargetAbsoluteBeat} pendingBgmTargetAbs={m_PendingBgmTargetAbsoluteBeat} " +
                $"pendingLoopTargetBeat={m_PendingBgmLoopTargetBeatInBar} " +
                $"pendingLoopWindow={m_PendingExecuteWindowLoopStartBeatInBar}->{m_PendingExecuteWindowLoopEndBeatInBar} " +
                $"pendingExecPreBeat={m_PendingExecutePreBeatMeasure}:{m_PendingExecutePreBeatSlot} " +
                $"obs={m_ObservationCount} seekCount={m_SeekCount} barSeekCount={m_SeekCountInCurrentBarWindow}");

            m_LastSeekSkipLogTimeSec = Time.unscaledTime;
        }
    }
}