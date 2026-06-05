using UnityEngine;

namespace GamblingAction.Audio
{
    // ””Ô†‚É‰‚¶‚½SE‚ğÄ¶‚·‚é
    public class BeatSEPlayer : MonoBehaviour
    {
        [Header("SE")]
        [Tooltip("1-3”–Ú‚É–Â‚ç‚·SE")]
        [SerializeField] private AK.Wwise.Event m_BeatSE;
        [Tooltip("4”–Ú‚É–Â‚ç‚·SE")]
        [SerializeField] private AK.Wwise.Event m_BarSE;

        private IBeatClock m_BeatClock;
        private uint m_CurrentPlayingID_Beat;
        private uint m_CurrentPlayingID_Bar;


        private void Start()
        {
            BeatClock beatClock = FindFirstObjectByType<BeatClock>();

            if (beatClock == null)
            {
                Debug.LogError("[BeatSEPlayer] BeatClock‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñB");
                return;
            }

            m_BeatClock = beatClock;
            m_BeatClock.OnBeat += HandleBeat;
        }

        private void OnDestroy()
        {
            Debug.Log("[SEPlayer] OnDestroy");

            if (m_BeatClock == null)
            {
                return;
            }

            m_BeatClock.OnBeat -= HandleBeat;

            WwiseSoundAPI.Instance?.StopAll(gameObject);
        }

        // OnBeat‚©‚ç””Ô†‚É‰‚¶‚ÄÄ¶
        private void HandleBeat(int beat)
        {
            // 4”–Ú‚Æ1`3”–Ú
            if (beat == m_BeatClock.BeatsPerBar)
            {
                if (WwiseSoundAPI.Instance == null)
                {
                    Debug.LogWarning("[BarSEPlayer] WwiseSoundAPI‚ÌInstance‚ªnull‚Å‚·B");
                    return;
                }

                m_CurrentPlayingID_Bar = WwiseSoundAPI.Instance.Play(m_BarSE, gameObject);
            }
            else
            {
                if (WwiseSoundAPI.Instance == null)
                {
                    Debug.LogWarning("[BeatSEPlayer] WwiseSoundAPI‚ÌInstance‚ªnull‚Å‚·B");
                    return;
                }
                m_CurrentPlayingID_Beat = WwiseSoundAPI.Instance.Play(m_BeatSE, gameObject);
            }
        }
    }
}