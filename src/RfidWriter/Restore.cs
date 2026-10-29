using System;
using System.Threading;
using Symbol.RFID3;

namespace RfidWriter
{
    /// <summary>修复工具：把某标签的 EPC 从 current 写回 original（含 PC 长度位处理）</summary>
    static class Restore
    {
        public static void Run(int port, string currentEpc, string originalEpc)
        {
            var engine = new RfidEngine();
            engine.Log += delegate (string m) { Console.WriteLine("[log] " + m); };
            engine.ConnectListen(port);
            for (int i = 0; i < 120 && !engine.IsConnected; i++) Thread.Sleep(250);
            if (!engine.IsConnected) { Console.WriteLine("FAIL no connection"); Environment.Exit(2); }
            Console.WriteLine("connected: " + engine.GetReaderInfo());

            var reader = engine.Reader;
            byte[] data = SerialFormat.HexToBytes(originalEpc);
            int words = data.Length / 2;

            // 方式1：整体写 ID
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var r = engine.WriteEpc(currentEpc, data, 0, true);
                Console.WriteLine("attempt" + attempt + " WriteTagIDWait: " + (r.Ok ? "OK " + r.NewEpc : r.Message));
                if (r.Ok) { engine.Dispose(); Environment.Exit(0); }
                Thread.Sleep(300);
            }

            // 方式2：先写 PC 字（ByteOffset=2），再写 EPC 数据（ByteOffset=4）
            Console.WriteLine("--- 尝试分步写：PC 字 + EPC 数据 ---");
            ushort pc = (ushort)((words << 11) & 0xFFFF);      // PC = 长度(字)<<11
            byte[] pcBytes = new byte[] { (byte)(pc >> 8), (byte)(pc & 0xFF) };
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var wp = new TagAccess.WriteAccessParams();
                    wp.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                    wp.ByteOffset = 2;
                    wp.WriteData = pcBytes;
                    wp.WriteDataLength = 2;
                    wp.AccessPassword = 0;
                    reader.Actions.TagAccess.WriteWait(currentEpc, wp, null);
                    Console.WriteLine("PC 写入 OK (0x" + pc.ToString("X4") + ")");
                    break;
                }
                catch (Exception ex) { Console.WriteLine("PC 写入失败: " + RfidEngine.Describe(ex)); Thread.Sleep(300); }
            }
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    var wp = new TagAccess.WriteAccessParams();
                    wp.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                    wp.ByteOffset = 4;
                    wp.WriteData = data;
                    wp.WriteDataLength = (uint)data.Length;
                    wp.AccessPassword = 0;
                    reader.Actions.TagAccess.WriteWait(currentEpc, wp, null);
                    Console.WriteLine("EPC 数据写入 OK");
                    // 校验
                    var rp = new TagAccess.ReadAccessParams();
                    rp.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                    rp.ByteOffset = 4;
                    rp.ByteCount = (uint)data.Length;
                    var td = reader.Actions.TagAccess.ReadWait(originalEpc, rp, null);
                    Console.WriteLine("校验读回: " + (td == null ? "null" : td.MemoryBankData));
                    engine.Dispose();
                    Environment.Exit(0);
                }
                catch (Exception ex) { Console.WriteLine("EPC 写入失败: " + RfidEngine.Describe(ex)); Thread.Sleep(300); }
            }

            engine.Dispose();
            Console.WriteLine("RESTORE FAILED");
            Environment.Exit(1);
        }
    }
}
