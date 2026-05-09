using UnityEngine;

namespace GamblingAction.Audio
{
    // Pitch Shifter・EQ・Delay・Tremolo・Playback Speed をキー操作で個別にオンオフ・量を調整するスクリプト
    // 後々ボタンに変更予定
    public class AudioEffectController : MonoBehaviour
    {
        [Header("Pitch Shifter")]
        [Tooltip("Pitch Shifter のオンオフキー")]
        [SerializeField] private KeyCode m_PitchShifterToggleKey = KeyCode.Q;
        [Tooltip("Pitch Shifter の量を増加するキー")]
        [SerializeField] private KeyCode m_PitchShifterUpKey = KeyCode.W;
        [Tooltip("Pitch Shifter の量を減少するキー")]
        [SerializeField] private KeyCode m_PitchShifterDownKey = KeyCode.S;
        [Tooltip("1回のキー操作で増減する Pitch Shift 量")]
        [SerializeField] private float m_PitchShifterStep = 100f;
        [Tooltip("Pitch Shift の最小値")]
        [SerializeField] private float m_PitchShifterMin = -4800f;
        [Tooltip("Pitch Shift の最大値")]
        [SerializeField] private float m_PitchShifterMax = 4800f;

        [Header("EQ")]
        [Tooltip("EQ のオンオフキー")]
        [SerializeField] private KeyCode m_EQToggleKey = KeyCode.E;
        [Tooltip("EQ の量を増加するキー")]
        [SerializeField] private KeyCode m_EQUpKey = KeyCode.R;
        [Tooltip("EQ の量を減少するキー")]
        [SerializeField] private KeyCode m_EQDownKey = KeyCode.F;
        [Tooltip("1回のキー操作で増減する EQ Gain 量")]
        [SerializeField] private float m_EQStep = 3f;
        [Tooltip("EQ Gain の最小値")]
        [SerializeField] private float m_EQMin = -48f;
        [Tooltip("EQ Gain の最大値")]
        [SerializeField] private float m_EQMax = 48f;

        [Header("Delay")]
        [Tooltip("Delay のオンオフキー")]
        [SerializeField] private KeyCode m_DelayToggleKey = KeyCode.T;
        [Tooltip("Delay Wet/Dry Mix の増加キー")]
        [SerializeField] private KeyCode m_DelayMixUpKey = KeyCode.Y;
        [Tooltip("Delay Wet/Dry Mix の減少キー")]
        [SerializeField] private KeyCode m_DelayMixDownKey = KeyCode.H;
        [Tooltip("Delay Feedback の増加キー")]
        [SerializeField] private KeyCode m_DelayFeedbackUpKey = KeyCode.U;
        [Tooltip("Delay Feedback の減少キー")]
        [SerializeField] private KeyCode m_DelayFeedbackDownKey = KeyCode.J;
        [Tooltip("1回のキー操作で増減する Wet/Dry Mix 量")]
        [SerializeField] private float m_DelayMixStep = 10f;
        [Tooltip("1回のキー操作で増減する Feedback 量")]
        [SerializeField] private float m_DelayFeedbackStep = 10f;

        [Header("Tremolo")]
        [Tooltip("Tremolo のオンオフキー")]
        [SerializeField] private KeyCode m_TremoloToggleKey = KeyCode.Z;
        [Tooltip("Tremolo Depth の増加キー")]
        [SerializeField] private KeyCode m_TremoloDepthUpKey = KeyCode.X;
        [Tooltip("Tremolo Depth の減少キー")]
        [SerializeField] private KeyCode m_TremoloDepthDownKey = KeyCode.C;
        [Tooltip("Tremolo Frequency の増加キー")]
        [SerializeField] private KeyCode m_TremoloFrequencyUpKey = KeyCode.V;
        [Tooltip("Tremolo Frequency の減少キー")]
        [SerializeField] private KeyCode m_TremoloFrequencyDownKey = KeyCode.B;
        [Tooltip("1回のキー操作で増減する Depth 量")]
        [SerializeField] private float m_TremoloDepthStep = 10f;
        [Tooltip("1回のキー操作で増減する Frequency 量")]
        [SerializeField] private float m_TremoloFrequencyStep = 1f;

        [Header("Playback Speed")]
        [Tooltip("再生速度を増加するキー")]
        [SerializeField] private KeyCode m_PlaybackSpeedUpKey = KeyCode.Alpha1;
        [Tooltip("再生速度を減少するキー")]
        [SerializeField] private KeyCode m_PlaybackSpeedDownKey = KeyCode.Alpha2;
        [Tooltip("1回のキー操作で増減する再生速度量")]
        [SerializeField] private float m_PlaybackSpeedStep = 0.1f;

        // 現在のオンオフ状態
        private bool m_IsPitchShifterOn = false;
        private bool m_IsEQOn = false;
        private bool m_IsDelayOn = false;
        private bool m_IsTremoloOn = false;

        // 現在の量（オフ時も値を保持してオン時に反映する）
        private float m_CurrentPitchShifterAmount = 1200f;
        private float m_CurrentEQGainAmount = 12f;
        private float m_CurrentDelayMix = 50f;
        private float m_CurrentDelayFeedback = 50f;
        private float m_CurrentTremoloDepth = 50f;
        private float m_CurrentTremoloFrequency = 5f;
        private float m_CurrentPlaybackSpeed = 1.0f;

        void Start()
        {
            // 起動時は全てオフ（RTPC値を0に設定）
            AkUnitySoundEngine.SetRTPCValue("PitchShifter_Amount", 0f);
            AkUnitySoundEngine.SetRTPCValue("EQ_Gain", 0f);
            AkUnitySoundEngine.SetRTPCValue("Delay_Mix", 0f);
            AkUnitySoundEngine.SetRTPCValue("Delay_Feedback", 0f);
            AkUnitySoundEngine.SetRTPCValue("Tremolo_Depth", 0f);
            AkUnitySoundEngine.SetRTPCValue("Tremolo_Frequency", 0f);
            AkUnitySoundEngine.SetRTPCValue("Playback_Speed", 1.0f);
        }

        void Update()
        {
            // Pitch Shifter
            if (Input.GetKeyDown(m_PitchShifterToggleKey))
                TogglePitchShifter();
            if (Input.GetKeyDown(m_PitchShifterUpKey))
                AdjustPitchShifter(m_PitchShifterStep);
            if (Input.GetKeyDown(m_PitchShifterDownKey))
                AdjustPitchShifter(-m_PitchShifterStep);

            // EQ
            if (Input.GetKeyDown(m_EQToggleKey))
                ToggleEQ();
            if (Input.GetKeyDown(m_EQUpKey))
                AdjustEQ(m_EQStep);
            if (Input.GetKeyDown(m_EQDownKey))
                AdjustEQ(-m_EQStep);

            // Delay
            if (Input.GetKeyDown(m_DelayToggleKey))
                ToggleDelay();
            if (Input.GetKeyDown(m_DelayMixUpKey))
                AdjustDelayMix(m_DelayMixStep);
            if (Input.GetKeyDown(m_DelayMixDownKey))
                AdjustDelayMix(-m_DelayMixStep);
            if (Input.GetKeyDown(m_DelayFeedbackUpKey))
                AdjustDelayFeedback(m_DelayFeedbackStep);
            if (Input.GetKeyDown(m_DelayFeedbackDownKey))
                AdjustDelayFeedback(-m_DelayFeedbackStep);

            // Tremolo
            if (Input.GetKeyDown(m_TremoloToggleKey))
                ToggleTremolo();
            if (Input.GetKeyDown(m_TremoloDepthUpKey))
                AdjustTremoloDepth(m_TremoloDepthStep);
            if (Input.GetKeyDown(m_TremoloDepthDownKey))
                AdjustTremoloDepth(-m_TremoloDepthStep);
            if (Input.GetKeyDown(m_TremoloFrequencyUpKey))
                AdjustTremoloFrequency(m_TremoloFrequencyStep);
            if (Input.GetKeyDown(m_TremoloFrequencyDownKey))
                AdjustTremoloFrequency(-m_TremoloFrequencyStep);

            // Playback Speed
            if (Input.GetKeyDown(m_PlaybackSpeedUpKey))
                AdjustPlaybackSpeed(m_PlaybackSpeedStep);
            if (Input.GetKeyDown(m_PlaybackSpeedDownKey))
                AdjustPlaybackSpeed(-m_PlaybackSpeedStep);
        }

        // Pitch Shifter のオンオフを切り替える
        private void TogglePitchShifter()
        {
            m_IsPitchShifterOn = !m_IsPitchShifterOn;
            float value = m_IsPitchShifterOn ? m_CurrentPitchShifterAmount : 0f;
            AkUnitySoundEngine.SetRTPCValue("PitchShifter_Amount", value);
            Debug.Log($"[AudioEffectController] PitchShifter: {(m_IsPitchShifterOn ? "ON" : "OFF")} (value: {value})");
        }

        // Pitch Shifter の量を増減する
        private void AdjustPitchShifter(float step)
        {
            m_CurrentPitchShifterAmount = Mathf.Clamp(m_CurrentPitchShifterAmount + step, m_PitchShifterMin, m_PitchShifterMax);
            if (m_IsPitchShifterOn)
                AkUnitySoundEngine.SetRTPCValue("PitchShifter_Amount", m_CurrentPitchShifterAmount);
            Debug.Log($"[AudioEffectController] PitchShifter Amount: {m_CurrentPitchShifterAmount}");
        }

        // EQ のオンオフを切り替える
        private void ToggleEQ()
        {
            m_IsEQOn = !m_IsEQOn;
            float value = m_IsEQOn ? m_CurrentEQGainAmount : 0f;
            AkUnitySoundEngine.SetRTPCValue("EQ_Gain", value);
            Debug.Log($"[AudioEffectController] EQ: {(m_IsEQOn ? "ON" : "OFF")} (value: {value})");
        }

        // EQ の量を増減する
        private void AdjustEQ(float step)
        {
            m_CurrentEQGainAmount = Mathf.Clamp(m_CurrentEQGainAmount + step, m_EQMin, m_EQMax);
            if (m_IsEQOn)
                AkUnitySoundEngine.SetRTPCValue("EQ_Gain", m_CurrentEQGainAmount);
            Debug.Log($"[AudioEffectController] EQ Gain: {m_CurrentEQGainAmount}");
        }

        // Delay のオンオフを切り替える
        private void ToggleDelay()
        {
            m_IsDelayOn = !m_IsDelayOn;
            float mix = m_IsDelayOn ? m_CurrentDelayMix : 0f;
            float feedback = m_IsDelayOn ? m_CurrentDelayFeedback : 0f;
            AkUnitySoundEngine.SetRTPCValue("Delay_Mix", mix);
            AkUnitySoundEngine.SetRTPCValue("Delay_Feedback", feedback);
            Debug.Log($"[AudioEffectController] Delay: {(m_IsDelayOn ? "ON" : "OFF")} (Mix: {mix}, Feedback: {feedback})");
        }

        // Delay の Wet/Dry Mix を増減する
        private void AdjustDelayMix(float step)
        {
            m_CurrentDelayMix = Mathf.Clamp(m_CurrentDelayMix + step, 0f, 100f);
            if (m_IsDelayOn)
                AkUnitySoundEngine.SetRTPCValue("Delay_Mix", m_CurrentDelayMix);
            Debug.Log($"[AudioEffectController] Delay Mix: {m_CurrentDelayMix}");
        }

        // Delay の Feedback を増減する
        private void AdjustDelayFeedback(float step)
        {
            m_CurrentDelayFeedback = Mathf.Clamp(m_CurrentDelayFeedback + step, 0f, 100f);
            if (m_IsDelayOn)
                AkUnitySoundEngine.SetRTPCValue("Delay_Feedback", m_CurrentDelayFeedback);
            Debug.Log($"[AudioEffectController] Delay Feedback: {m_CurrentDelayFeedback}");
        }

        // Tremolo のオンオフを切り替える
        private void ToggleTremolo()
        {
            m_IsTremoloOn = !m_IsTremoloOn;
            float depth = m_IsTremoloOn ? m_CurrentTremoloDepth : 0f;
            float frequency = m_IsTremoloOn ? m_CurrentTremoloFrequency : 0f;
            AkUnitySoundEngine.SetRTPCValue("Tremolo_Depth", depth);
            AkUnitySoundEngine.SetRTPCValue("Tremolo_Frequency", frequency);
            Debug.Log($"[AudioEffectController] Tremolo: {(m_IsTremoloOn ? "ON" : "OFF")} (Depth: {depth}, Frequency: {frequency})");
        }

        // Tremolo の Depth を増減する
        private void AdjustTremoloDepth(float step)
        {
            m_CurrentTremoloDepth = Mathf.Clamp(m_CurrentTremoloDepth + step, 0f, 100f);
            if (m_IsTremoloOn)
                AkUnitySoundEngine.SetRTPCValue("Tremolo_Depth", m_CurrentTremoloDepth);
            Debug.Log($"[AudioEffectController] Tremolo Depth: {m_CurrentTremoloDepth}");
        }

        // Tremolo の Frequency を増減する
        private void AdjustTremoloFrequency(float step)
        {
            m_CurrentTremoloFrequency = Mathf.Clamp(m_CurrentTremoloFrequency + step, 0f, 20f);
            if (m_IsTremoloOn)
                AkUnitySoundEngine.SetRTPCValue("Tremolo_Frequency", m_CurrentTremoloFrequency);
            Debug.Log($"[AudioEffectController] Tremolo Frequency: {m_CurrentTremoloFrequency}");
        }

        // 再生速度を増減する
        private void AdjustPlaybackSpeed(float step)
        {
            m_CurrentPlaybackSpeed = Mathf.Clamp(m_CurrentPlaybackSpeed + step, 0.5f, 2.0f);
            AkUnitySoundEngine.SetRTPCValue("Playback_Speed", m_CurrentPlaybackSpeed);
            Debug.Log($"[AudioEffectController] Playback Speed: {m_CurrentPlaybackSpeed}");
        }
    }
}