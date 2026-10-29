using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RfidWriter
{
    static class Inv
    {
        public static void Run(int port, int secs)
        {
            var engine = new RfidEngine();
            engine.Log += delegate (string m) { Console.WriteLine("[log] " + m); };
            var seen = new Dictionary<string, int>();
            engine.TagSeen += delegate (TagInfo t) { lock (seen) { int c; seen.TryGetValue(t.Epc, out c); seen[t.Epc] = c + 1; } };
            engine.ConnectListen(port);
            for (int i = 0; i < 120 && !engine.IsConnected; i++) Thread.Sleep(250);
            if (!engine.IsConnected) { Console.WriteLine("FAIL no conn"); Environment.Exit(2); }
            engine.StartInventory();
            Thread.Sleep(secs * 1000);
            engine.StopInventory();
            Thread.Sleep(300);
            List<string> keys;
            lock (seen) keys = seen.Keys.OrderBy(k => k).ToList();
            Console.WriteLine("TOTAL " + keys.Count);
            foreach (var k in keys) Console.WriteLine("  " + k + "  x" + seen[k]);
            engine.Dispose();
            Environment.Exit(0);
        }
    }
}
