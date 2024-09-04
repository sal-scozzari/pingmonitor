using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pingmonitor
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        private static Queue<bool> results = new Queue<bool>();


        static void Main(string[] args)
        {
            log.Info($"pingmonitor starting");

            if (string.IsNullOrWhiteSpace(args[0]))
            {
                Console.WriteLine("Usage: pingmonitor n.n.n.n");
                return;
            }

            const int rateCountMax = 24;
            const int repeatDelayMS = 5000;
            const double serviceDegradedThresholdPercent = 50.0;
            const double serviceLostThresholdPercent = 5.0;

            log.Info($"Address = {args[0]}");
            log.Info($"Rate count max = {rateCountMax} samples");
            log.Info($"Repeat deplay = {repeatDelayMS} ms");
            log.Info($"Degraded service threshold = {serviceDegradedThresholdPercent} %");
            log.Info($"Lost service threshold = {serviceLostThresholdPercent} %");

            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();

            // Use the default Ttl value which is 128,
            // but change the fragmentation behavior.
            options.DontFragment = true;

            // Create a buffer of 32 bytes of data to be transmitted.
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 120;
            PingReply reply;

            bool serviceDegraded = false;
            DateTime serviceDegradedStart = DateTime.MinValue;
            bool serviceLost = false;
            DateTime serviceLostStart = DateTime.MinValue;
           
            while(true)
            {
                try
                {
                    reply = pingSender.Send(args[0], timeout, buffer, options);
                    if (reply.Status == IPStatus.Success)
                    {
                        results.Enqueue(true);
                        if (results.Count > rateCountMax)
                        {
                            results.Dequeue();
                        }

                        /*
                        log.Info("Address: {0}", reply.Address.ToString());
                        log.Info("RoundTrip time: {0}", reply.RoundtripTime);
                        log.Info("Time to live: {0}", reply.Options.Ttl);
                        log.Info("Don't fragment: {0}", reply.Options.DontFragment);
                        log.Info("Buffer size: {0}", reply.Buffer.Length);
                        */
                        log.Debug($"Reply succeeded, {reply.RoundtripTime / 1000.0:F3}, {SuccessRate():F2}");
                    }
                    else
                    {
                        results.Enqueue(false);
                        if (results.Count > rateCountMax)
                        {
                            results.Dequeue();
                        }

                        log.Debug($"Reply failed, 0.000, {SuccessRate():F2}");
                    }
                }
                catch (Exception)
                {
                    results.Enqueue(false);
                    if (results.Count > rateCountMax)
                    {
                        results.Dequeue();
                    }

                    log.Debug($"Send failed, 0.000, {SuccessRate():F2}");
                }

                if (SuccessRate() < serviceDegradedThresholdPercent && !serviceDegraded)
                {
                    serviceDegraded = true;
                    serviceDegradedStart = DateTime.Now;
                    log.Warn($"Service degraded, 0.000, {SuccessRate():F2}");
                }

                if (SuccessRate() > serviceDegradedThresholdPercent && serviceDegraded)
                {
                    serviceDegraded = false;
                    TimeSpan degradedTime = DateTime.Now - serviceDegradedStart;
                    log.Warn($"Service degradation ended after {degradedTime.Hours:D2}:{degradedTime.Minutes:D2}:{degradedTime.Seconds:D2}, 0.000, {SuccessRate():F2}");
                }

                if (SuccessRate() < serviceLostThresholdPercent && !serviceLost)
                {
                    serviceLost = true;
                    serviceLostStart = DateTime.Now;
                    log.Warn($"Service lost, 0.000, {SuccessRate():F2}");
                }

                if (SuccessRate() > serviceLostThresholdPercent && serviceLost)
                {
                    serviceLost = false;
                    TimeSpan lostTime = DateTime.Now - serviceLostStart;
                    log.Error($"Service loss ended after {lostTime.Hours:D2}:{lostTime.Minutes:D2}:{lostTime.Seconds:D2}, 0.000, {SuccessRate():F2}");
                }

                Thread.Sleep(repeatDelayMS);
            }
        }

        private static double SuccessRate()
        {
            int successCount = 0;
            foreach (bool result in results)
            {
                if (result)
                {
                    successCount++;
                }
            }
            return successCount / (double)results.Count() * 100.0;
        }
    }
}
