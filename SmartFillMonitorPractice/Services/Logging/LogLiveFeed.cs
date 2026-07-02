using System;
using System.Collections.Generic;

namespace SmartFillMonitor.Services.Logging
{
    public sealed class LogLiveFeed : ILogLiveFeed
    {
        private const int MaxEntries = 500;
        private readonly object _syncRoot = new();
        private readonly List<string> _entries = new();

        public int Capacity => MaxEntries;
        public event EventHandler<string>? LogAppended;
        public event EventHandler? ResetRequested;

        public IReadOnlyList<string> GetSnapshot()
        {
            lock (_syncRoot)
            {
                return _entries.ToArray();
            }
        }

        public void Publish(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (_syncRoot)
            {
                _entries.Add(message);
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveAt(0);
                }
            }

            LogAppended?.Invoke(this, message);
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                _entries.Clear();
            }

            ResetRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
