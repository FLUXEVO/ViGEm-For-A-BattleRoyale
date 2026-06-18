using System;
using System.Collections.Generic;

namespace VigemStickDriftUi
{
    public class InputBindingManager
    {
        private readonly HashSet<string> _activeKeyNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _turboHoldStartedUtc = new(StringComparer.OrdinalIgnoreCase);

        public void SetKeyState(string keyName, bool isDown)
        {
            if (isDown)
            {
                _activeKeyNames.Add(keyName);
            }
            else
            {
                _activeKeyNames.Remove(keyName);
                _turboHoldStartedUtc.Remove(keyName); // Reset turbo tracking when released
            }
        }

        public bool IsActionPressed(string actionName, string boundKey, bool turboEnabled, int turboHz)
        {
            if (string.IsNullOrEmpty(boundKey) || string.Equals(boundKey, "None", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check if physical key is pressed
            bool isDown = _activeKeyNames.Contains(boundKey);
            if (!isDown)
            {
                _turboHoldStartedUtc.Remove(actionName);
                return false;
            }

            // If turbo is off, a simple hold returns true
            if (!turboEnabled)
            {
                return true;
            }

            // Handle Turbo rapid-fire timing cycles
            if (!_turboHoldStartedUtc.TryGetValue(actionName, out DateTime startedUtc))
            {
                startedUtc = DateTime.UtcNow;
                _turboHoldStartedUtc[actionName] = startedUtc;
            }

            int safeHz = Math.Max(1, turboHz);
            double intervalMs = 1000.0 / safeHz;
            double totalElapsedMs = (DateTime.UtcNow - startedUtc).TotalMilliseconds;
            int cycleIndex = (int)(totalElapsedMs / intervalMs);

            // Alternate between on and off states every interval cycle
            return cycleIndex % 2 == 0;
        }

        public void Clear()
        {
            _activeKeyNames.Clear();
            _turboHoldStartedUtc.Clear();
        }
    }
}