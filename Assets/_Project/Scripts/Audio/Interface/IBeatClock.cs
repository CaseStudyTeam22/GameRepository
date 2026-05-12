using System;

namespace GamblingAction.Audio
{
    // ”î•ñ‚Ì’ñ‹Ÿ‘‹Œû
    public interface IBeatClock
    {
        // Œ»İ‰½”–Ú‚© (1n‚Ü‚è)
        int CurrentBeat { get; }

        // 1¬ß‚Ì””
        int BeatsPerBar { get; }

        // 1”‚Ì’·‚³ (•b)
        float BeatDuration { get; }

        // 1¬ß‚Ì’·‚³ (•b)
        float BarDuration { get; }

        // ””­‰ÎBˆø”‚ÍŒ»İ‚Ì””Ô†
        event Action<int> OnBeat;

        // ¬ß“ª”­‰Î
        event Action OnBar;

        // Ä¶I—¹”­‰Î
        event Action OnEnd;
    }
}
