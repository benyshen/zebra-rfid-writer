using System;
using System.Threading;

namespace RfidWriter
{
    /// <summary>验证监听模式的 IP 过滤：用错误 IP 应该拒绝，用正确 IP 应该连上</summary>
    static class FilterTest
    {
        public static void Run(int port, string goodIp)
        {
            // 1) 用明显错误的 IP 过滤，应始终连不上
            var e1 = new RfidEngine();
            e1.Log += delegate (string m) { Console.WriteLine("[e1] " + m); };
            e1.ConnectListen(port, "10.99.99.99");
            Thread.Sleep(6000);
            Console.WriteLine("过滤错误 IP 后 IsConnected=" + e1.IsConnected + "（期望 False）");
            bool ok1 = !e1.IsConnected;
            e1.Dispose();
            Thread.Sleep(1500);

            // 2) 用正确 IP，应能连上
            var e2 = new RfidEngine();
            e2.Log += delegate (string m) { Console.WriteLine("[e2] " + m); };
            e2.ConnectListen(port, goodIp);
            bool ok2 = false;
            for (int i = 0; i < 200; i++)
            {
                Thread.Sleep(250);
                if (e2.IsConnected) { ok2 = true; break; }
            }
            Console.WriteLine("过滤正确 IP (" + goodIp + ") 后 IsConnected=" + e2.IsConnected + "（期望 True）");
            e2.Dispose();

            Console.WriteLine((ok1 && ok2) ? "RESULT: PASS" : "RESULT: FAIL");
            Environment.Exit(ok1 && ok2 ? 0 : 1);
        }
    }
}
