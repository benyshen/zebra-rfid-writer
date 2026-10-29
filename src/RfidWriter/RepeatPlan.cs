using System;

namespace RfidWriter
{
    /// <summary>打印“重复次数”写入计划，验证 张索引→序列号 映射（不访问读写器）</summary>
    static class RepeatPlan
    {
        public static void Run(int count, int repeat)
        {
            var opt = new SerialOptions { Prefix = "", Suffix = "", Start = "800000000001", Radix = 16, Digits = 12, Step = 1 };
            Console.WriteLine("写入张数=" + count + " 重复次数=" + repeat);
            for (int i = 0; i < count; i++)
            {
                int seq = SerialFormat.SeqIndexOf(i, repeat);
                Console.WriteLine("  第 " + (i + 1) + " 张 -> 序列号 #" + (seq + 1) + " -> " + SerialFormat.Build(opt, seq));
            }
            bool ok = true;
            for (int i = 1; i < count; i++)
            {
                string a = SerialFormat.Build(opt, SerialFormat.SeqIndexOf(i - 1, repeat));
                string b = SerialFormat.Build(opt, SerialFormat.SeqIndexOf(i, repeat));
                bool sameGroup = (i % repeat) != 0;
                if (sameGroup && a != b) { ok = false; Console.WriteLine("  !! " + i + "/" + (i + 1) + " 应同值却不同"); }
                if (!sameGroup && a == b) { ok = false; Console.WriteLine("  !! " + i + "/" + (i + 1) + " 应换值却相同"); }
            }
            Console.WriteLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
            Environment.Exit(ok ? 0 : 1);
        }
    }
}
