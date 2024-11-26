using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using ScaleNet.Common.Transport.Tcp.Base.Core;

namespace ScaleNet.Common.Transport.Components.Statistics
{
    public class TcpStatisticsStringData
    {
        public string? PendingBytes { get; set; }
        public string? CongestionLevel { get; set; }
        public string? TotalBytesSent { get; set; }
        public string? TotalBytesReceived { get; set; }
        public string? TotalMessageDispatched { get; set; }

        public string? SendRate { get; set; }
        public string? ReceiveRate { get; set; }
        public string? MessageDispatchRate { get; set; }
        public string? TotalMessageReceived { get; internal set; }
    }

    public class TcpStatistics
    {
        private readonly TcpStatisticsStringData _sessionStatsJson = new();
        public float CongestionLevel;
        private long _currentTimestamp;
        public long DeltaBytesReceived;
        public long DeltaBytesSent;
        public long DeltaMessageReceived;
        public long DeltaMessageSent;
        private long _lastTimeStamp;
        public float MessageDispatchRate;
        public float MessageReceiveRate;
        public int PendingBytes;
        public float ReceiveRate;

        public float SendRate;
        public long TotalBytesReceived;
        public long TotalBytesSent;
        public long TotalMessageReceived;
        public long TotalMessageSent;


        public TcpStatistics()
        {
        }


        public TcpStatistics(SessionStatistics refstats)
        {
            PendingBytes = refstats.PendingBytes;
            CongestionLevel = refstats.CongestionLevel;
            TotalBytesSent = refstats.TotalBytesSent;
            TotalBytesReceived = refstats.TotalBytesReceived;
            TotalMessageSent = refstats.TotalMessageDispatched;
            TotalMessageReceived = refstats.TotalMessageReceived;

            DeltaBytesReceived = refstats.TotalBytesSent;
            DeltaBytesSent = refstats.TotalBytesReceived;
            DeltaMessageReceived = refstats.TotalMessageReceived;
            DeltaMessageSent = refstats.TotalMessageDispatched;

            _lastTimeStamp = 0;
            _currentTimestamp = 0;
            SendRate = 0;
            ReceiveRate = 0;
        }


        internal void Update(SessionStatistics refstats, long elapsedMilliseconds)
        {
            _lastTimeStamp = _currentTimestamp;
            _currentTimestamp = elapsedMilliseconds;
            long deltaTMs = _currentTimestamp - _lastTimeStamp;

            DeltaBytesReceived = refstats.DeltaBytesReceived;
            DeltaBytesSent = refstats.DeltaBytesSent;

            SendRate = (float)(refstats.DeltaBytesSent / (double)(1 + deltaTMs)) * 1000;
            ReceiveRate = (float)(refstats.DeltaBytesReceived / (double)(1 + deltaTMs)) * 1000;

            MessageDispatchRate = (float)((refstats.TotalMessageDispatched - TotalMessageSent) / (double)(1 + deltaTMs)) * 1000;
            MessageReceiveRate = (float)((refstats.TotalMessageReceived - TotalMessageReceived) / (double)(1 + deltaTMs)) * 1000;

            PendingBytes = refstats.PendingBytes;
            CongestionLevel = refstats.CongestionLevel;
            TotalBytesSent = refstats.TotalBytesSent;
            TotalBytesReceived = refstats.TotalBytesReceived;
            TotalMessageSent = refstats.TotalMessageDispatched;
            TotalMessageReceived = refstats.TotalMessageReceived;

            DeltaMessageReceived = refstats.DeltaMessageReceived;
            DeltaMessageSent = refstats.DeltaMessageSent;
        }


        internal void Clear()
        {
            PendingBytes = 0;
            CongestionLevel = 0;

            MessageDispatchRate = 0;
            MessageReceiveRate = 0;

            _lastTimeStamp = 0;
            _currentTimestamp = 0;
            SendRate = 0;
            ReceiveRate = 0;

            //TotalMessageReceived= 0;
            //TotalMessageSent= 0;
        }


        public static TcpStatistics GetAverageStatistics(List<TcpStatistics> statList)
        {
            TcpStatistics averageStats = new();
            foreach (TcpStatistics? stat in statList)
            {
                averageStats.PendingBytes += stat.PendingBytes;
                averageStats.CongestionLevel += stat.CongestionLevel;
                averageStats.TotalBytesSent += stat.DeltaBytesSent;
                averageStats.TotalBytesReceived += stat.DeltaBytesReceived;
                averageStats.SendRate += stat.SendRate;
                averageStats.ReceiveRate += stat.ReceiveRate;
                averageStats.TotalMessageSent += stat.TotalMessageSent;
                averageStats.TotalMessageReceived += stat.TotalMessageReceived;
                averageStats.MessageDispatchRate += stat.MessageDispatchRate;
                averageStats.MessageReceiveRate += stat.MessageReceiveRate;
            }

            averageStats.CongestionLevel /= statList.Count;
            return averageStats;
        }


        public override string ToString()
        {
            string format = string.Format(
                "# TCP :\n" +
                "Total Pending Bytes:         {0}\n" +
                "Total Congestion Level:      {1:P}\n" +
                "Total Bytes Sent:            {2}\n" +
                "Total Bytes Received:        {3}\n" +
                "Total Message Sent:          {6:N0}\n" +
                "Total Message Received:      {7:N0}\n" +
                "Data Send Rate:              {4}\n" +
                "Data Receive Rate:           {5}\n" +
                "Message Send Rate:           {8:N1} Msg/s\n" +
                "Message Receive Rate:        {9:N1} Msg/s\n",
                UdpStatisticsPublisher.BytesToString(PendingBytes), CongestionLevel,
                TcpServerStatisticsPublisher.BytesToString(TotalBytesSent),
                TcpServerStatisticsPublisher.BytesToString(TotalBytesReceived),
                TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s",
                TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s", TotalMessageSent, TotalMessageReceived, MessageDispatchRate, MessageReceiveRate);
            return format;
        }


        public TcpStatisticsStringData Stringify()
        {
            _sessionStatsJson.PendingBytes = UdpStatisticsPublisher.BytesToString(PendingBytes);
            _sessionStatsJson.CongestionLevel = CongestionLevel.ToString("P");
            _sessionStatsJson.TotalBytesSent = TcpServerStatisticsPublisher.BytesToString(TotalBytesSent);
            _sessionStatsJson.TotalBytesReceived = TcpServerStatisticsPublisher.BytesToString(TotalBytesReceived);
            _sessionStatsJson.SendRate = TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s";
            _sessionStatsJson.ReceiveRate = TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s";
            _sessionStatsJson.MessageDispatchRate = MessageDispatchRate.ToString("N1");
            _sessionStatsJson.TotalMessageDispatched = TotalMessageSent.ToString();
            _sessionStatsJson.TotalMessageReceived = TotalMessageReceived.ToString();

            return _sessionStatsJson;
        }
    }

    public readonly ref struct SessionStatistics
    {
        public readonly int PendingBytes;
        public readonly float CongestionLevel;
        public readonly long TotalBytesSent;
        public readonly long TotalBytesReceived;
        public readonly long DeltaBytesReceived;
        public readonly long DeltaBytesSent;
        public readonly long TotalMessageReceived;
        public readonly long TotalMessageDispatched;
        public readonly long DeltaMessageSent;
        public readonly long DeltaMessageReceived;


        public SessionStatistics(
            int pendingBytes,
            float congestionLevel,
            long totalBytesSent,
            long totalBytesReceived,
            long deltaBytesSent,
            long deltaBytesReveived,
            long totalMessageSent,
            long totalMessageReceived,
            long deltaMessageSent,
            long deltaMessageReceived)
        {
            PendingBytes = pendingBytes;
            CongestionLevel = congestionLevel;
            TotalBytesSent = totalBytesSent;
            TotalBytesReceived = totalBytesReceived;
            TotalMessageDispatched = totalMessageSent;
            DeltaBytesReceived = deltaBytesReveived;
            DeltaBytesSent = deltaBytesSent;
            TotalMessageReceived = totalMessageReceived;
            DeltaMessageSent = deltaMessageSent;
            DeltaMessageReceived = deltaMessageReceived;
        }
    }

    internal class TcpServerStatisticsPublisher
    {
        private static readonly string[] DataSuffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        private readonly TcpStatistics _generalStats = new();
        internal readonly ConcurrentDictionary<Guid, IAsyncSession> Sessions;
        private readonly Stopwatch _sw = new();


        public TcpServerStatisticsPublisher(in ConcurrentDictionary<Guid, IAsyncSession> sessions)
        {
            Sessions = sessions;
            _sw.Start();
        }


        internal ConcurrentDictionary<Guid, TcpStatistics> Stats { get; } = new();


        private void GetSessionStats()
        {
            int count = 1;
            _generalStats.Clear();
            foreach (Guid item in Stats.Keys)
            {
                if (!Sessions.ContainsKey(item))
                    Stats.TryRemove(item, out _);
            }

            foreach (KeyValuePair<Guid, IAsyncSession> session in Sessions)
            {
                if (Stats.ContainsKey(session.Key))
                {
                    SessionStatistics val = session.Value.GetSessionStatistics();
                    Stats[session.Key].Update(val, _sw.ElapsedMilliseconds); //here
                }
                else
                {
                    SessionStatistics val = session.Value.GetSessionStatistics();
                    Stats[session.Key] = new TcpStatistics(val);
                }

                _generalStats.PendingBytes += Stats[session.Key].PendingBytes;
                _generalStats.CongestionLevel += Stats[session.Key].CongestionLevel;
                _generalStats.TotalBytesSent += Stats[session.Key].DeltaBytesSent;
                _generalStats.TotalBytesReceived += Stats[session.Key].DeltaBytesReceived;
                _generalStats.SendRate += Stats[session.Key].SendRate;
                _generalStats.ReceiveRate += Stats[session.Key].ReceiveRate;
                _generalStats.TotalMessageSent += Stats[session.Key].DeltaMessageSent;
                _generalStats.TotalMessageReceived += Stats[session.Key].DeltaMessageReceived;
                _generalStats.MessageDispatchRate += Stats[session.Key].MessageDispatchRate;
                _generalStats.MessageReceiveRate += Stats[session.Key].MessageReceiveRate;
                count++;
            }

            _generalStats.CongestionLevel /= count;
        }


        public static string BytesToString(long byteCount)
        {
            if (byteCount == 0)
                return "0" + DataSuffix[0];

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return Math.Sign(byteCount) * num + " " + DataSuffix[place];
        }


        internal void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
        {
            GetSessionStats();
            generalStats = this._generalStats;
            sessionStats = Stats;
        }
    }
}