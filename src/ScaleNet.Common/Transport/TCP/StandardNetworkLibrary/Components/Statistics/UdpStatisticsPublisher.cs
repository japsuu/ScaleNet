using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.Statistics
{
    public class UdpStatisticsStringData
    {
        public string? TotalDatagramSent { get; set; }
        public string? TotalDatagramReceived { get; set; }
        public string? TotalBytesSent { get; set; }
        public string? TotalBytesReceived { get; set; }
        public string? TotalMessageDropped { get; set; }

        public string? SendRate { get; set; }
        public string? ReceiveRate { get; set; }
        public string? SendPps { get; set; }
        public string? ReceivePps { get; set; }
    }

    public class UdpStatistics
    {
        private const string SEED =
            "\n# Udp:\n" +
            "Total Datagram Sent:         {0}\n" +
            "Total Datagram Received:     {1}\n" +
            "Total Datagram Dropped       {2}\n" +
            "Total Bytes Send:            {3}\n" +
            "Total Bytes Received:        {4}\n" +
            "Total Datagram Send Rate:    {5} Msg/s\n" +
            "Total Datagram Receive Rate: {6} Msg/s\n" +
            "Total Send Data Rate:        {7}\n" +
            "Total Receive Data Rate:     {8} ";

        private readonly UdpStatisticsStringData _data = new();

        public long PrevTimeStamp;
        public float ReceivePps;
        public float ReceiveRate;
        public float SendPps;

        public float SendRate;
        public long TotalBytesReceived;
        public long TotalBytesReceivedPrev;
        public long TotalBytesSent;
        public long TotalBytesSentPrev;
        public long TotalDatagramReceived;
        public long TotalDatagramReceivedPrev;
        public long TotalDatagramSent;

        public long TotalDatagramSentPrev;
        public long TotalMessageDropped;
        public long TotalMessageDroppedPrev;


        public override string ToString() =>
            string.Format(
                SEED,
                TotalDatagramSent.ToString(),
                TotalDatagramReceived.ToString(),
                TotalMessageDropped,
                UdpStatisticsPublisher.BytesToString(TotalBytesSent),
                UdpStatisticsPublisher.BytesToString(TotalBytesReceived),
                SendPps.ToString("N1"),
                ReceivePps.ToString("N1"),
                TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s",
                TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s");


        public UdpStatisticsStringData Stringify()
        {
            _data.TotalDatagramSent = TotalDatagramSent.ToString();
            _data.TotalDatagramReceived = TotalDatagramReceived.ToString();
            _data.TotalMessageDropped = TotalMessageDropped.ToString();
            _data.TotalBytesSent = UdpStatisticsPublisher.BytesToString(TotalBytesSent);
            _data.TotalBytesReceived = UdpStatisticsPublisher.BytesToString(TotalBytesReceived);
            _data.SendPps = SendPps.ToString("N1");
            _data.ReceivePps = ReceivePps.ToString("N1");
            _data.SendRate = TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s";
            _data.ReceiveRate = TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s";

            return _data;
        }


        internal void Reset()
        {
            TotalDatagramSent = 0;
            TotalDatagramReceived = 0;
            TotalBytesSent = 0;
            TotalBytesReceived = 0;
            TotalMessageDropped = 0;
            SendRate = 0;
            ReceiveRate = 0;
            SendPps = 0;
            ReceivePps = 0;
            TotalDatagramSentPrev = 0;
            TotalDatagramReceivedPrev = 0;
            TotalBytesSentPrev = 0;
            TotalBytesReceivedPrev = 0;
            TotalMessageDroppedPrev = 0;
        }
    }

    internal class UdpStatisticsPublisher
    {
        private static readonly string[] DataSuffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        private readonly UdpStatistics _generalstats = new();
        private readonly ConcurrentDictionary<IPEndPoint, UdpStatistics> _statistics;
        private readonly Stopwatch _sw = new();


        public UdpStatisticsPublisher(ConcurrentDictionary<IPEndPoint, UdpStatistics> statistics)
        {
            _sw.Start();
            _statistics = statistics;
        }


        private void GatherStatistics()
        {
            _generalstats.Reset();

            // Statistics object is shared dict between udp server and this.
            foreach (KeyValuePair<IPEndPoint, UdpStatistics> stat in _statistics)
            {
                long tsCurrent = _sw.ElapsedMilliseconds;
                double deltaT = tsCurrent - stat.Value.PrevTimeStamp;

                stat.Value.SendPps = (float)((stat.Value.TotalDatagramSent - stat.Value.TotalDatagramSentPrev) / deltaT) * 1000f;
                stat.Value.ReceivePps = (float)((stat.Value.TotalDatagramReceived - stat.Value.TotalDatagramReceivedPrev) / deltaT) * 1000f;
                stat.Value.SendRate = (float)((stat.Value.TotalBytesSent - stat.Value.TotalBytesSentPrev) / deltaT) * 1000;
                stat.Value.ReceiveRate = (float)((stat.Value.TotalBytesReceived - stat.Value.TotalBytesReceivedPrev) / deltaT) * 1000;

                stat.Value.TotalDatagramSentPrev = stat.Value.TotalDatagramSent;
                stat.Value.TotalDatagramReceivedPrev = stat.Value.TotalDatagramReceived;
                stat.Value.TotalBytesSentPrev = stat.Value.TotalBytesSent;
                stat.Value.TotalBytesReceivedPrev = stat.Value.TotalBytesReceived;

                stat.Value.PrevTimeStamp = tsCurrent;


                UdpStatistics? data1 = stat.Value;
                _generalstats.SendPps += data1.SendPps;
                _generalstats.SendRate += data1.SendRate;
                _generalstats.ReceiveRate += data1.ReceiveRate;
                _generalstats.TotalBytesReceived += data1.TotalBytesReceived;
                _generalstats.TotalBytesSent += data1.TotalBytesSent;
                _generalstats.TotalMessageDropped += data1.TotalMessageDropped;
                _generalstats.ReceivePps += data1.ReceivePps;
                _generalstats.TotalDatagramReceived += data1.TotalDatagramReceived;
                _generalstats.TotalDatagramSent += data1.TotalDatagramSent;
            }
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


        internal void GetStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            GatherStatistics();
            generalStats = _generalstats;
            sessionStats = _statistics;
        }
    }
}