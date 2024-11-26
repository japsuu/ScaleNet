using System.Diagnostics;
using ScaleNet.Common.Transport.Tcp.Base.Core;

namespace ScaleNet.Common.Transport.Components.Statistics
{
    internal class TcpClientStatisticsPublisher
    {
        private readonly TcpStatistics _generalStats = new();
        internal readonly IAsyncSession Session;
        private readonly Stopwatch _sw = new();


        public TcpClientStatisticsPublisher(IAsyncSession session)
        {
            Session = session;
            _sw.Start();
        }


        private void GetSessionStats()
        {
            SessionStatistics stats = Session.GetSessionStatistics();
            _generalStats.Update(stats, _sw.ElapsedMilliseconds);
        }


        internal void GetStatistics(out TcpStatistics generalStats)
        {
            GetSessionStats();
            generalStats = _generalStats;
        }
    }
}