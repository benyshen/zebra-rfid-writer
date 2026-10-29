using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Symbol.RFID3;

namespace RfidWriter
{
    /// <summary>
    /// 天线功率设置：把 dBm 映射到读写器的功率档位索引（TransmitPowerIndex），
    /// 并逐天线写入 AntennaRfConfig。功率调低可减少串读远处/无关标签。
    /// </summary>
    public static class AntennaPower
    {
        /// <summary>功率档位表（dBm）。SDK 给的是原始整数，需要按量级判断单位。</summary>
        public static double[] DbmTable(ReaderCapabilities caps)
        {
            if (caps == null) return new double[0];
            int[] raw;
            try { raw = caps.TransmitPowerLevelValues; }
            catch { return new double[0]; }
            if (raw == null || raw.Length == 0) return new double[0];
            double max = raw.Max(v => (double)v);
            double div = max > 1000 ? 100.0 : (max > 100 ? 10.0 : 1.0);
            var list = new List<double>(raw.Length);
            foreach (var v in raw) list.Add(Math.Round(v / div, 2));
            return list.ToArray();
        }

        /// <summary>最近档位索引 + 该档位实际 dBm</summary>
        public static KeyValuePair<int, double> FindIndex(double[] table, double dbm)
        {
            if (table == null || table.Length == 0) return new KeyValuePair<int, double>(-1, dbm);
            int best = 0;
            double bestDelta = double.MaxValue;
            for (int i = 0; i < table.Length; i++)
            {
                double d = Math.Abs(table[i] - dbm);
                if (d < bestDelta) { bestDelta = d; best = i; }
            }
            return new KeyValuePair<int, double>(best, table[best]);
        }

        /// <summary>当前各天线的功率（dBm），按天线号索引（1 起）</summary>
        public static Dictionary<int, double> CurrentDbm(RFIDReader reader, double[] table)
        {
            var res = new Dictionary<int, double>();
            if (reader == null) return res;
            int n = 4;
            try { n = reader.ReaderCapabilities.NumAntennaSupported; } catch { }
            for (ushort a = 1; a <= n; a++)
            {
                try
                {
                    var cfg = reader.Config.Antennas[a].GetRfConfig();
                    int idx = cfg.TransmitPowerIndex;
                    double dbm = (table != null && idx >= 0 && idx < table.Length) ? table[idx] : idx;
                    res[a] = dbm;
                }
                catch { }
            }
            return res;
        }

        /// <summary>
        /// 设置天线功率。allAntennas=true 时对所有天线生效，否则只设 antenna 指定的那一根。
        /// 返回每个成功设置的天线 → 实际 dBm。
        /// </summary>
        public static Dictionary<int, double> SetDbm(RFIDReader reader, double dbm, bool allAntennas, int antenna)
        {
            var done = new Dictionary<int, double>();
            if (reader == null) throw new InvalidOperationException("未连接读写器");
            var table = DbmTable(reader.ReaderCapabilities);
            var kv = FindIndex(table, dbm);

            int n = 4;
            try { n = reader.ReaderCapabilities.NumAntennaSupported; } catch { }
            int from = allAntennas ? 1 : antenna;
            int to = allAntennas ? n : antenna;

            for (int a = from; a <= to; a++)
            {
                var rf = reader.Config.Antennas[(ushort)a].GetRfConfig();
                rf.TransmitPowerIndex = (ushort)(kv.Key >= 0 ? kv.Key : (int)dbm);
                reader.Config.Antennas[(ushort)a].SetRfConfig(rf);
                Thread.Sleep(30);
                var back = reader.Config.Antennas[(ushort)a].GetRfConfig();
                int idx = back.TransmitPowerIndex;
                double actual = (table != null && idx >= 0 && idx < table.Length) ? table[idx] : idx;
                done[a] = actual;
            }
            return done;
        }
    }
}
