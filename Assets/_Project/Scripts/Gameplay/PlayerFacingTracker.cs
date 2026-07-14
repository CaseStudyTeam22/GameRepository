using GamblingAction.Core.Dto;

namespace GamblingAction.Gameplay
{
    public class PlayerFacingTracker
    {
        private string m_CurrentFacingDir = Directions.Up;

        public string CurrentFacingDir => m_CurrentFacingDir;

        public void Initialize(string defaultDir = Directions.Up)
        {
            m_CurrentFacingDir = string.IsNullOrEmpty(defaultDir) ? Directions.Up : defaultDir;
        }

        public void ResetToDefault(string defaultDir = Directions.Up)
        {
            m_CurrentFacingDir = string.IsNullOrEmpty(defaultDir) ? Directions.Up : defaultDir;
        }

        public void ApplyIntentDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;

            switch (dir)
            {
                case Directions.Up:
                case Directions.Down:
                case Directions.Left:
                case Directions.Right:
                    m_CurrentFacingDir = dir;
                    break;
            }
        }
    }
}