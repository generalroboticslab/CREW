using System;
using System.Diagnostics;

namespace Dojo.Nakama
{
    /// <summary>
    /// Helper class for measuring RTT between server and clients
    /// </summary>
    public class NakamaRTT
    {
        private Stopwatch _watch;
        private double _averagedMS = 0.0;
        private long _recentMS = 0;

        /// <summary>
        /// Ping to start RTT measurement
        /// </summary>
        public void Ping()
        {
            _watch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Pong to stop measurement
        /// </summary>
        public void Pong()
        {
            if (_watch != null)
            {
                _recentMS = Math.Max(_watch.ElapsedMilliseconds, 0);
                _averagedMS = (_averagedMS + _recentMS) * 0.5;
            }
        }

        /// <summary>
        /// Reset RTT measurement results
        /// </summary>
        public void Reset()
        {
            _averagedMS = 0.0;
            _recentMS = 0;
            _watch = null;
        }

        /** Averaged measured RTT in milliseconds */
        public ulong MeasuredMSAvg => (ulong)_averagedMS;

        /** Measured RTT in milliseconds */
        public ulong MeasuredMS => (ulong)_recentMS;
    }
}
