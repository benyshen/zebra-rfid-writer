using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RfidWriter
{
    /// <summary>掉线恢复测试：主动 Dispose 掉 SDK 会话（模拟读写器掉线），验证监听线程能否自动重新接入</summary>
    static class KillTest
    {
        static void P(string s) { Console.WriteLine("  " + s); Console.Out.Flush(); }

        public static void Run(int port)
        {
            var engine = new RfidEngine();
            engine.Log += delegate (string m) { Console.WriteLine("[log] " + m); };
            engine.ConnectionChanged += delegate (bool ok, string m) { Console.WriteLine("[conn] " + ok + " " + m); };
            var seen = new Dictionary<string, TagInfo>();
            engine.TagSeen += delegate (TagInfo t) { lock (seen) { if (!seen.ContainsKey(t.Epc)) seen[t.Epc] = t; } };

            engine.ConnectListen(port);
            for (int i = 0; i < 200 && !engine.IsConnected; i++) Thread.Sleep(250);
            if (!engine.IsConnected) { Console.WriteLine("FAIL 初次连接"); Environment.Exit(2); }
            engine.StartInventory();
            Thread.Sleep(3000);
            engine.StopInventory();
            Thread.Sleep(500);
            lock (seen) P("盘点 " + seen.Count + " 个标签，停止后 IsConnected=" + engine.IsConnected);

            Console.WriteLine("=== 模拟掉线：Dispose 掉当前会话 ===");
            try { engine.Reader.Dispose(); } catch (Exception ex) { P("dispose ex " + ex.Message); }
            Thread.Sleep(800);
            P("掉线后 IsConnected=" + engine.IsConnected);

            Console.WriteLine("=== 等待自动重连（最多 40s）===");
            bool back = false;
            for (int i = 0; i < 160; i++)
            {
                Thread.Sleep(250);
                if (engine.IsConnected) { back = true; break; }
            }
            P("自动重连 " + (back ? "成功" : "失败"));
            if (!back) { engine.Dispose(); Environment.Exit(3); }

            Console.WriteLine("=== 重连后：设功率 + 写回原值 ===");
            try
            {
                var done = engine.SetAntennaPower(18, true, 1);
                P("设功率 OK: " + string.Join(" ", done.OrderBy(k => k.Key).Select(kv => "A" + kv.Key + "=" + kv.Value)));
            }
            catch (Exception ex) { P("设功率失败: " + RfidEngine.Describe(ex)); }

            string epc;
            lock (seen) epc = seen.Keys.FirstOrDefault(k => k.Length == 24);
            if (epc != null)
            {
                var res = engine.WriteEpc(epc, SerialFormat.HexToBytes(epc), 0, true);
                P("写 " + epc + " -> " + (res.Ok ? "OK" : "FAIL " + res.Message));
            }
            engine.Dispose();
            Console.WriteLine(back ? "RESULT: PASS" : "RESULT: FAIL");
            Environment.Exit(back ? 0 : 1);
        }
    }
}
