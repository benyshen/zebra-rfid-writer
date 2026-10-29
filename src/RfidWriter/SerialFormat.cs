using System;
using System.Globalization;
using System.Text;

namespace RfidWriter
{
    /// <summary>序列化参数</summary>
    public class SerialOptions
    {
        public string Prefix = "";
        public string Suffix = "";
        public string Start = "1";
        public int Radix = 16;        // 16 / 10
        public int Digits = 12;       // 数值最少位数（左补 0）
        public int Encode = 0;        // 0 = HEX 原样，1 = ASCII 字符
        public long Step = 1;

        public static ulong ParseNumber(string s)
        {
            s = (s ?? "").Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            ulong v;
            if (s.Length == 0) return 0;
            if (ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return v;
            return 0;
        }
    }

    public static class SerialFormat
    {
        /// <summary>生成第 index 条的十六进制数据串（偶数长度，不补齐到标签容量）</summary>
        public static string Build(SerialOptions o, int index)
        {
            ulong start = SerialOptions.ParseNumber(o.Start);
            ulong v = start + (ulong)o.Step * (ulong)index;
            string numText = o.Radix == 10
                ? v.ToString(CultureInfo.InvariantCulture)
                : v.ToString("X");
            if (o.Digits > 0 && numText.Length < o.Digits) numText = numText.PadLeft(o.Digits, '0');

            string prefix = (o.Prefix ?? "").Trim().ToUpperInvariant();
            string suffix = (o.Suffix ?? "").Trim().ToUpperInvariant();

            if (o.Encode == 1)
            {
                var sb = new StringBuilder(prefix);
                foreach (char c in numText) sb.Append(((int)c).ToString("X2"));
                sb.Append(suffix);
                return sb.ToString();
            }
            string hex = prefix + numText + suffix;
            if (hex.Length % 2 != 0) hex = "0" + hex;
            return hex;
        }

        /// <summary>第 tagIndex 张标签（0 起）应使用的序列号序号：重复次数 R 表示每个序列值连写 R 张</summary>
        public static int SeqIndexOf(int tagIndex, int repeat)
        {
            if (repeat <= 1) return tagIndex;
            return tagIndex / repeat;
        }

        public static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 != 0) hex = "0" + hex;
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return b;
        }

        public static string ToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("X2"));
            return sb.ToString();
        }
    }
}
