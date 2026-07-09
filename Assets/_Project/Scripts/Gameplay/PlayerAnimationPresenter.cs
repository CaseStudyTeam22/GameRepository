using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
    public class PlayerAnimationPresenter : MonoBehaviour
    {
        [SerializeField] private Animator m_Animator;

        private static readonly int m_FaceUpTrigger = Animator.StringToHash("FaceUp");
        private static readonly int m_FaceDownTrigger = Animator.StringToHash("FaceDown");
        private static readonly int m_FaceLeftTrigger = Animator.StringToHash("FaceLeft");
        private static readonly int m_FaceRightTrigger = Animator.StringToHash("FaceRight");

        private static readonly int m_DirUpState = Animator.StringToHash("Base Layer.Dir_Up");
        private static readonly int m_DirDownState = Animator.StringToHash("Base Layer.Dir_Down");
        private static readonly int m_DirLeftState = Animator.StringToHash("Base Layer.Dir_Left");
        private static readonly int m_DirRightState = Animator.StringToHash("Base Layer.Dir_Right");

        private PlayerView m_PlayerView;
        private IGameState m_State;
        private bool m_IsInitialized;
        private bool m_IsLocalPlayer;
        private bool m_IsP2View;
        private string m_CurrentDir;
        private string m_PendingOpponentDir;

        private void Awake()
        {
            m_PlayerView = GetComponent<PlayerView>();

            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void Start()
        {
            m_State = GameStateLocator.Current;
            if (m_State == null)
            {
                Debug.LogWarning("[PlayerAnimationPresenter] GameStateLocator.Current is null.");
                return;
            }

            m_State.OnPlayersChanged += HandlePlayersChanged;
            m_State.OnBeatChanged += HandleBeatChanged;
            m_State.OnPhaseChanged += HandlePhaseChanged;
            m_State.OnOpponentIntentRevealed += HandleOpponentIntentRevealed;
            LocalIntentBus.OnChanged += HandleLocalIntentChanged;

            TryInitialize();
            ApplyAnimatorPlaybackByPhase(m_State.Phase);
        }

        private void OnDestroy()
        {
            LocalIntentBus.OnChanged -= HandleLocalIntentChanged;

            if (m_State == null) return;

            m_State.OnPlayersChanged -= HandlePlayersChanged;
            m_State.OnBeatChanged -= HandleBeatChanged;
            m_State.OnPhaseChanged -= HandlePhaseChanged;
            m_State.OnOpponentIntentRevealed -= HandleOpponentIntentRevealed;
        }

        private void TryInitialize()
        {
            if (m_IsInitialized) return;
            if (m_PlayerView == null) return;
            if (m_State == null) return;
            if (string.IsNullOrEmpty(m_PlayerView.PlayerId)) return;
            if (string.IsNullOrEmpty(m_State.MyId)) return;

            PlayerDto me = m_State.Me;
            m_IsP2View = me != null && me.Role == Roles.P2;
            m_IsLocalPlayer = m_PlayerView.PlayerId == m_State.MyId;
            m_IsInitialized = true;

            ApplyImmediateDirection(GetDefaultDirection());
        }

        private void HandlePlayersChanged()
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (m_IsLocalPlayer) return;

            string dir = ResolveSyncedIntentDir();
            if (string.IsNullOrEmpty(dir)) return;

            if (m_State.CurrentBeat == 4)
            {
                ApplyBoardDirection(dir);
                m_PendingOpponentDir = null;
                return;
            }

            m_PendingOpponentDir = dir;
        }

        private void HandleBeatChanged()
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (m_IsLocalPlayer) return;
            if (m_State.CurrentBeat != 4) return;
            if (string.IsNullOrEmpty(m_PendingOpponentDir)) return;

            ApplyBoardDirection(m_PendingOpponentDir);
            m_PendingOpponentDir = null;
        }

        private void HandlePhaseChanged(EGamePhase phase)
        {
            TryInitialize();
            if (!m_IsInitialized) return;

            if (phase == EGamePhase.Countdown)
            {
                m_PendingOpponentDir = null;
                ApplyImmediateDirection(GetDefaultDirection());
            }

            ApplyAnimatorPlaybackByPhase(phase);
        }

        private void HandleLocalIntentChanged()
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (!m_IsLocalPlayer) return;

            string dir = LocalIntentBus.Current != null ? LocalIntentBus.Current.Dir : null;
            if (string.IsNullOrEmpty(dir)) return;

            ApplyBoardDirection(dir);
        }

        private void HandleOpponentIntentRevealed(OpponentIntentRevealedMessage msg)
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (m_IsLocalPlayer) return;
            if (msg == null || msg.Intent == null) return;
            if (string.IsNullOrEmpty(msg.Intent.Dir)) return;

            ApplyBoardDirection(msg.Intent.Dir);
            m_PendingOpponentDir = null;
        }

        private string ResolveSyncedIntentDir()
        {
            if (m_State == null) return null;
            if (m_PlayerView == null) return null;
            if (!m_State.Players.TryGetValue(m_PlayerView.PlayerId, out PlayerDto dto)) return null;

            return dto.Intent != null ? dto.Intent.Dir : null;
        }

        private string GetDefaultDirection()
        {
            return m_IsLocalPlayer ? Directions.Up : Directions.Down;
        }

        private void ApplyAnimatorPlaybackByPhase(EGamePhase phase)
        {
            if (m_Animator == null) return;

            m_Animator.speed = phase == EGamePhase.Battle ? 1f : 0f;
        }

        private void ApplyImmediateDirection(string dir)
        {
            if (!IsValidDirection(dir)) return;
            if (m_Animator == null) return;

            ResetFaceTriggers();
            m_Animator.Play(GetStateHash(dir), 0, 0f);
            m_Animator.Update(0f);
            m_CurrentDir = dir;
        }

        private void ApplyDirection(string dir)
        {
            if (!IsValidDirection(dir)) return;
            if (dir == m_CurrentDir) return;
            if (m_Animator == null) return;

            ResetFaceTriggers();

            m_Animator.SetTrigger(GetTriggerHash(dir));
            m_CurrentDir = dir;
        }

        private void ApplyBoardDirection(string dir)
        {
            ApplyDirection(ToViewDirection(dir));
        }

        private string ToViewDirection(string dir)
        {
            if (!m_IsP2View)
            {
                return dir;
            }

            switch (dir)
            {
                case Directions.Up:
                    return Directions.Down;
                case Directions.Down:
                    return Directions.Up;
                case Directions.Left:
                    return Directions.Right;
                case Directions.Right:
                    return Directions.Left;
                default:
                    return dir;
            }
        }

        private void ResetFaceTriggers()
        {
            m_Animator.ResetTrigger(m_FaceUpTrigger);
            m_Animator.ResetTrigger(m_FaceDownTrigger);
            m_Animator.ResetTrigger(m_FaceLeftTrigger);
            m_Animator.ResetTrigger(m_FaceRightTrigger);
        }

        private static bool IsValidDirection(string dir)
        {
            switch (dir)
            {
                case Directions.Up:
                case Directions.Down:
                case Directions.Left:
                case Directions.Right:
                    return true;
                default:
                    return false;
            }
        }

        private static int GetTriggerHash(string dir)
        {
            switch (dir)
            {
                case Directions.Up:
                    return m_FaceUpTrigger;
                case Directions.Down:
                    return m_FaceDownTrigger;
                case Directions.Left:
                    return m_FaceLeftTrigger;
                case Directions.Right:
                    return m_FaceRightTrigger;
                default:
                    return m_FaceUpTrigger;
            }
        }

        private static int GetStateHash(string dir)
        {
            switch (dir)
            {
                case Directions.Up:
                    return m_DirUpState;
                case Directions.Down:
                    return m_DirDownState;
                case Directions.Left:
                    return m_DirLeftState;
                case Directions.Right:
                    return m_DirRightState;
                default:
                    return m_DirUpState;
            }
        }
    }
}