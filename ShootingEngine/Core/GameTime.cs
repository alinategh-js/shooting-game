namespace ShootingEngine.Core;

/// <summary>
/// Phase 4: fixed simulation timestep with an accumulator (common game-loop pattern).
/// World animation uses <see cref="SimulationTimeSeconds"/> so motion stays stable under frame spikes.
/// </summary>
public sealed class GameTime
{
    public const float FixedDeltaSeconds = 1f / 60f;
    public const int MaxStepsPerFrame = 6;
    public const double MaxFrameSeconds = 0.25;

    private double _accumulator;

    /// <summary>Advances only in whole fixed steps (typically 1/60 s).</summary>
    public float SimulationTimeSeconds { get; private set; }

    /// <summary>0..1 blend between the last committed state and the next fixed step (for future interpolation).</summary>
    public float InterpolationAlpha { get; private set; }

    public void AdvanceSimulation(double frameSeconds)
    {
        if (frameSeconds <= 0)
        {
            InterpolationAlpha = 0f;
            return;
        }

        if (frameSeconds > MaxFrameSeconds)
        {
            frameSeconds = MaxFrameSeconds;
        }

        _accumulator += frameSeconds;

        int steps = 0;
        while (_accumulator >= FixedDeltaSeconds && steps < MaxStepsPerFrame)
        {
            SimulationTimeSeconds += FixedDeltaSeconds;
            _accumulator -= FixedDeltaSeconds;
            steps++;
        }

        InterpolationAlpha = (float)(_accumulator / FixedDeltaSeconds);
        if (float.IsNaN(InterpolationAlpha) || InterpolationAlpha < 0f || InterpolationAlpha > 1f)
        {
            InterpolationAlpha = 0f;
        }
    }
}

