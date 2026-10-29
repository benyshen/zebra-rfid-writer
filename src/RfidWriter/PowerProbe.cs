using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Symbol.RFID3;

namespace RfidWriter
{
    /// <summary>功率表探测：打印功率档位表、当前天线配置，并测试按 dBm 设置功率</summary>
    static class PowerProbe
    {
        public static void Run(int port)
        {
            var engine = new RfidEngine();
            engine.Log += delegate (string m) { Console.WriteLine("[log] " + m); };
            engine.ConnectListen(port);
            for (int i = 0; i < 120 && !engine.IsConnected; i++) Thread.Sleep(250);
            if (!engine.IsConnected) { Console.WriteLine("FAIL no connection"); Environment.Exit(2); }
            var r = engine.Reader;
            var caps = r.ReaderCapabilities;

            Console.WriteLine("Model=" + caps.ModelName + " FW=" + caps.FirwareVersion + " ant=" + caps.NumAntennaSupported);
            int[] raw = caps.TransmitPowerLevelValues;
            Console.WriteLine("raw count=" + (raw == null ? -1 : raw.Length));
            if (raw != null)
            {
                Console.WriteLine("  raw first=" + string.Join(",", raw.Take(6)));
                Console.WriteLine("  raw last =" + string.Join(",", raw.Skip(Math.Max(0, raw.Length - 6))));
            }
            var table = AntennaPower.DbmTable(caps);
            Console.WriteLine("dBm table: count=" + table.Length + " min=" + (table.Length > 0 ? table.Min() : 0) +
                              " max=" + (table.Length > 0 ? table.Max() : 0) + " sample=" +
                              string.Join(",", table.Skip(Math.Max(0, table.Length - 6)).Select(v => v.ToString("0.##"))));

            var cur = AntennaPower.CurrentDbm(r, table);
            foreach (var kv in cur.OrderBy(k => k.Key))
                Console.WriteLine("ant" + kv.Key + " 当前 " + kv.Value.ToString("0.##") + " dBm");

            foreach (double dbm in new double[] { 30, 25, 20, 15 })
            {
                try
                {
                    var done = engine.SetAntennaPower(dbm, true, 1);
                    Console.WriteLine("set " + dbm + " -> " + string.Join(" ", done.OrderBy(k => k.Key)
                        .Select(kv => "A" + kv.Key + "=" + kv.Value.ToString("0.##"))));
                }
                catch (Exception ex) { Console.WriteLine("set " + dbm + " err " + RfidEngine.Describe(ex)); }
            }

            // 低功率盘点对比：看串读是否减少
            foreach (double dbm in new double[] { 30, 15 })
            {
                engine.SetAntennaPower(dbm, true, 1);
                var seen = new HashSet<string>();
                Action<TagInfo> h = delegate (TagInfo t) { lock (seen) { seen.Add(t.Epc); } };
                engine.TagSeen += h;
                engine.StartInventory();
                Thread.Sleep(4000);
                engine.StopInventory();
                Thread.Sleep(300);
                engine.TagSeen -= h;
                lock (seen) Console.WriteLine("功率 " + dbm + "dBm 读到 " + seen.Count + " 个标签");
            }

            engine.Dispose();
            Environment.Exit(0);
        }
    }
}
