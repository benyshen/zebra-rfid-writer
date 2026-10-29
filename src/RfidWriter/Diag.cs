using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Symbol.RFID3;

namespace RfidWriter
{
    /// <summary>诊断：盘点 → 停止 → 检查连接状态 → 试写/试设功率，定位“请先连接读写器”</summary>
    static class Diag
    {
        static void P(string s) { Console.WriteLine("  " + s); Console.Out.Flush(); }

        public static void Run(int port)
        {
            var engine = new RfidEngine();
            engine.Log += delegate (string m) { Console.WriteLine("[log] " + m); };
            var seen = new Dictionary<string, TagInfo>();
            engine.TagSeen += delegate (TagInfo t) { lock (seen) { if (!seen.ContainsKey(t.Epc)) seen[t.Epc] = t; } };

            engine.ConnectListen(port);
            for (int i = 0; i < 200 && !engine.IsConnected; i++) Thread.Sleep(250);
            if (!engine.IsConnected) { Console.WriteLine("FAIL: no connect"); Environment.Exit(2); }
            var reader = engine.Reader;
            reader.Events.StatusNotify += delegate (object s, Events.StatusEventArgs e)
            {
                string info = "?";
                try { info = e.StatusEventData.ToString(); } catch { }
                Console.WriteLine("[event] StatusNotify: " + info);
            };
            reader.Events.NotifyReaderDisconnectEvent = true;
            reader.Events.NotifyInventoryStopEvent = true;

            P("连接后 IsConnected=" + SafeBool(delegate { return reader.IsConnected; }));

            Console.WriteLine("=== 盘点 4s ===");
            engine.StartInventory();
            Thread.Sleep(4000);
            P("盘点中 IsConnected=" + SafeBool(delegate { return reader.IsConnected; }));
            lock (seen) P("读到 " + seen.Count + " 个标签");

            Console.WriteLine("=== 停止盘点 ===");
            engine.StopInventory();
            Thread.Sleep(1200);
            P("停止后 IsConnected=" + SafeBool(delegate { return reader.IsConnected; }));
            P("engine.IsConnected=" + engine.IsConnected);
            P("读型号=" + SafeStr(delegate { return reader.ReaderCapabilities.ModelName; }));

            Console.WriteLine("=== 停止后再设功率 ===");
            try
            {
                var done = engine.SetAntennaPower(18, true, 1);
                P("设功率 OK: " + string.Join(" ", done.OrderBy(k => k.Key).Select(kv => "A" + kv.Key + "=" + kv.Value)));
            }
            catch (Exception ex) { P("设功率失败: " + RfidEngine.Describe(ex)); }

            Console.WriteLine("=== 停止后写回原值（安全） ===");
            string epc = null;
            lock (seen) epc = seen.Keys.FirstOrDefault(k => k.Length == 24) ?? seen.Keys.FirstOrDefault();
            if (epc != null)
            {
                try
                {
                    var res = engine.WriteEpc(epc, SerialFormat.HexToBytes(epc), 0, true);
                    P("写 " + epc + " -> " + (res.Ok ? "OK" : "FAIL " + res.Message));
                }
                catch (Exception ex) { P("写异常: " + RfidEngine.Describe(ex)); }
            }

            Console.WriteLine("=== 停止后再启动盘点 ===");
            try
            {
                engine.StartInventory();
                Thread.Sleep(2000);
                engine.StopInventory();
                P("二次盘点 OK, IsConnected=" + SafeBool(delegate { return reader.IsConnected; }));
            }
            catch (Exception ex) { P("二次盘点失败: " + RfidEngine.Describe(ex)); }

            engine.Dispose();
            Environment.Exit(0);
        }

        static string SafeBool(Func<bool> f) { try { return f().ToString(); } catch (Exception e) { return "THROW " + e.GetType().Name; } }
        static string SafeStr(Func<string> f) { try { return f(); } catch (Exception e) { return "THROW " + e.GetType().Name + ": " + e.Message; } }
    }
}
