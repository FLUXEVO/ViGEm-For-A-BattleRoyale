using System;
using System.Collections.Generic;
using System.Linq;

namespace VigemStickDriftUi
{
    public class JitterEngine
    {
        private const int DefaultPatternStepDelayMs = 10;
        private int _patternStepIndex;
        private DateTime? _jitterHeldSinceUtc;
        private bool _isJitterBindDown;

        public bool IsJitterActive { get; private set; }

        public void SetTriggerState(bool isDown)
        {
            if (isDown)
            {
                if (!_isJitterBindDown)
                {
                    _isJitterBindDown = true;
                    _jitterHeldSinceUtc = DateTime.UtcNow;
                    _patternStepIndex = 0;
                }
            }
            else
            {
                _isJitterBindDown = false;
                _jitterHeldSinceUtc = null;
                _patternStepIndex = 0;
            }
        }

        public void Reset()
        {
            _isJitterBindDown = false;
            _jitterHeldSinceUtc = null;
            _patternStepIndex = 0;
            IsJitterActive = false;
        }

        public void CalculateOutput(
            int basePercentX,
            int basePercentY,
            string patternType,
            int delayMs,
            int radiusPercent,
            int extraDriftPercent,
            List<PatternStep> customPoints,
            out int outPercentX,
            out int outPercentY,
            out int nextIntervalMs,
            out bool waitingForDelay)
        {
            outPercentX = basePercentX;
            outPercentY = basePercentY;
            nextIntervalMs = DefaultPatternStepDelayMs;
            waitingForDelay = false;
            IsJitterActive = false;

            List<PatternStep> configuredPattern = GetPatternSteps(patternType, radiusPercent, customPoints);
            bool canUseJitterPattern = !string.Equals(patternType, "Off", StringComparison.OrdinalIgnoreCase) && configuredPattern.Count > 0;

            if (_isJitterBindDown)
            {
                bool delayElapsed = true;
                if (_jitterHeldSinceUtc.HasValue)
                {
                    double elapsedMs = (DateTime.UtcNow - _jitterHeldSinceUtc.Value).TotalMilliseconds;
                    delayElapsed = elapsedMs >= delayMs;
                    waitingForDelay = !delayElapsed;
                }

                if (delayElapsed && canUseJitterPattern)
                {
                    PatternStep step = GetCurrentPatternStep(configuredPattern);
                    outPercentX = Math.Clamp(outPercentX + step.X, 0, 100);
                    outPercentY = Math.Clamp(outPercentY + step.Y, 0, 100);
                    nextIntervalMs = step.Delay;
                    IsJitterActive = true;
                }
                else
                {
                    // Fall back to extra steady drift if pattern is off or waiting for delay
                    outPercentY = Math.Clamp(outPercentY + extraDriftPercent, 0, 100);
                }
            }
            else
            {
                _patternStepIndex = 0;
            }
        }

        private PatternStep GetCurrentPatternStep(List<PatternStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return new PatternStep { X = 0, Y = 0, Delay = DefaultPatternStepDelayMs };
            }
            if (_patternStepIndex >= steps.Count)
            {
                _patternStepIndex = 0;
            }
            PatternStep step = steps[_patternStepIndex];
            _patternStepIndex = (_patternStepIndex + 1) % steps.Count;
            return step;
        }

        public List<PatternStep> GetPatternSteps(string patternType, int radius, List<PatternStep> customPoints)
        {
            if (string.Equals(patternType, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return new List<PatternStep>();
            }
            
            List<PatternStep> rawPattern = string.Equals(patternType, "Custom", StringComparison.OrdinalIgnoreCase)
                ? customPoints.Select(p => new PatternStep { X = p.X, Y = p.Y, Delay = p.Delay }).ToList()
                : string.Equals(patternType, "Circle", StringComparison.OrdinalIgnoreCase)
                    ? BuildCirclePattern(radius)
                    : BuildShakePattern(radius);

            return CenterAndScalePattern(rawPattern, radius);
        }

        private List<PatternStep> BuildShakePattern(int radius)
        {
            return new List<PatternStep>
            {
                new() { X = -radius, Y = 0, Delay = DefaultPatternStepDelayMs },
                new() { X = radius, Y = 0, Delay = DefaultPatternStepDelayMs },
                new() { X = -(int)(radius * 0.6), Y = (int)(radius * 0.6), Delay = DefaultPatternStepDelayMs },
                new() { X = (int)(radius * 0.6), Y = -(int)(radius * 0.6), Delay = DefaultPatternStepDelayMs }
            };
        }

        private List<PatternStep> BuildCirclePattern(int radius)
        {
            var steps = new List<PatternStep>();
            const int totalSteps = 8;
            for (int i = 0; i < totalSteps; i++)
            {
                double angle = (2 * Math.PI / totalSteps) * i;
                steps.Add(new PatternStep
                {
                    X = (int)Math.Round(Math.Cos(angle) * radius),
                    Y = (int)Math.Round(Math.Sin(angle) * radius),
                    Delay = DefaultPatternStepDelayMs
                });
            }
            return steps;
        }

        private List<PatternStep> CenterAndScalePattern(List<PatternStep> steps, int radius)
        {
            if (steps == null || steps.Count == 0) return new List<PatternStep>();

            double averageX = steps.Average(s => s.X);
            double averageY = steps.Average(s => s.Y);

            var centered = steps.Select(s => new PatternStep
            {
                X = s.X - (int)Math.Round(averageX),
                Y = s.Y - (int)Math.Round(averageY),
                Delay = s.Delay
            }).ToList();

            int maxMagnitude = centered.Select(s => Math.Max(Math.Abs(s.X), Math.Abs(s.Y))).DefaultIfEmpty(0).Max();
            if (maxMagnitude <= 0 || radius <= 0) return centered;

            double scale = radius / (double)maxMagnitude;
            return centered.Select(s => new PatternStep
            {
                X = Math.Clamp((int)Math.Round(s.X * scale), -100, 100),
                Y = Math.Clamp((int)Math.Round(s.Y * scale), -100, 100),
                Delay = s.Delay
            }).ToList();
        }
    }

    public class PatternStep
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Delay { get; set; }
    }
}