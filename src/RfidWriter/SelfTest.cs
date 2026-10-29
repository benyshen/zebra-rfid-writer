using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Symbol.RFID3;

namespace RfidWriter
{
    /// <summary>
    /// 无界面自测：监听端口 -> 盘点 -> 对前 N 个标签写入序列号 -> 校验 -> 还原原始 EPC -> 校验
    /// 用法: RfidWriter.exe --selftest [端口] [写入张数]
    /// </summary>
    static class SelfTest
    {
        public static void Run(int port, int count)
        {
            var engine = new RfidEngine();
            engine.Log += delegate (string m) { Console.WriteLine("[log] " + m); };

            var seen = new Dictionary<string, TagInfo>();
            engine.TagSeen += delegate (TagInfo t)
            {
                lock (seen) { if (!seen.ContainsKey(t.Epc)) seen[t.Epc] = t; }
            };
            engine.ConnectionChanged += delegate (bool connOk, string msg)
            {
                Console.WriteLine("[conn] connected=" + connOk + " " + msg);
            };

            engine.ConnectListen(port);
            Console.WriteLine("等待读写器连入 " + port + " ...");
            for (int i = 0; i < 120 && !engine.IsConnected; i++) Thread.Sleep(250);
            if (!engine.IsConnected) { Console.WriteLine("FAIL: 未建立连接"); Environment.Exit(2); }
            Console.WriteLine("INFO: " + engine.GetReaderInfo());

            engine.StartInventory();
            Thread.Sleep(5000);
            engine.StopInventory();
            Thread.Sleep(500);

            List<TagInfo> tags;
            lock (seen) tags = seen.Values.OrderBy(t => t.FirstSeen).ToList();
            Console.WriteLine("盘点结果: 共 " + tags.Count + " 个标签");
            foreach (var t in tags.Take(10)) Console.WriteLine("   " + t.Epc + "  ant=" + t.Antenna + " rssi=" + t.Rssi);

            // 只选 96 位（24 hex）的标签做写入测试
            var targets = tags.Where(t => t.Epc.Length == 24).Take(count).ToList();
            if (targets.Count == 0) { Console.WriteLine("FAIL: 没有找到 96 位 EPC 标签"); Environment.Exit(3); }

            var opt = new SerialOptions { Prefix = "", Suffix = "", Start = "800000000001", Radix = 16, Digits = 12, Encode = 0, Step = 1 };
            int ok = 0, fail = 0;
            var originals = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < targets.Count; i++)
            {
                string epc = targets[i].Epc;
                byte[] data = SerialFormat.HexToBytes(SerialFormat.Build(opt, i));
                Console.WriteLine("写入 [" + epc + "] <- " + SerialFormat.ToHex(data));
                var res = engine.WriteEpc(epc, data, 0, true);
                Console.WriteLine("  -> " + (res.Ok ? "OK 新EPC=" + res.NewEpc : "FAIL " + res.Message));
                if (res.Ok) { ok++; originals.Add(new KeyValuePair<string, string>(res.NewEpc, epc)); }
                else fail++;
            }

            // 还原
            Console.WriteLine("--- 还原原始 EPC ---");
            foreach (var kv in originals)
            {
                byte[] back = SerialFormat.HexToBytes(kv.Value);
                var res = engine.WriteEpc(kv.Key, back, 0, true);
                Console.WriteLine("  还原 " + kv.Key + " -> " + kv.Value + " : " + (res.Ok ? "OK" : "FAIL " + res.Message));
            }

            // USER 区写入测试（单张，data 4 字 = 8 字节）
            var first = targets.FirstOrDefault();
            if (first != null && originals.Count > 0)
            {
                Console.WriteLine("--- USER 区写入测试（原始标签 " + first.Epc + "） ---");
                var ures = engine.WriteUser(first.Epc, 0, SerialFormat.HexToBytes("A1B2C3D4E5F60708"), 0, true);
                Console.WriteLine("  USER 写: " + (ures.Ok ? "OK" : "FAIL " + ures.Message));
            }

            Console.WriteLine("=== 自测结束: 成功 " + ok + " 失败 " + fail + " ===");
            engine.Dispose();
            Environment.Exit(ok > 0 && fail == 0 ? 0 : 1);
        }
    }
}
