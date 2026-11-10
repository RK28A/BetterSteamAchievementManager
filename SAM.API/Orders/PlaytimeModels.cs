using System;

namespace SAM.API.Orders
{
    internal sealed class PlaytimeStatus
    {
        public string State { get; set; } = "starting";
        public string Message { get; set; }
        public DateTime StartedUtc { get; set; }
        public double ElapsedSeconds { get; set; }
    }
    internal sealed class PlaytimeSettings
    {
        public bool Randomized { get; set; }
        public DelayRange Duration { get; set; } = new() { Minimum = 30, Maximum = 60 };
        public TimeSpan Sample(double random)
        {
            var duration = Duration.Sample(Randomized ? random : 0);
            if (duration <= TimeSpan.Zero) throw new InvalidOperationException("Choisissez une durée supérieure à zéro.");
            return duration;
        }
    }
}
