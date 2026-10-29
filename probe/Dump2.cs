using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RfidProbe
{
    static class Dump2
    {
        public static void Run(string outPath)
        {
            var sb = new StringBuilder();
            var asm = typeof(Symbol.RFID3.RFIDReader).Assembly;
            string[] wanted =
            {
                "Symbol.RFID3.TagAccess+WriteSpecificFieldAccessParams",
                "Symbol.RFID3.TagAccess+LockAccessParams",
                "Symbol.RFID3.ReaderCapabilities",
                "Symbol.RFID3.Antennas",
                "Symbol.RFID3.Antennas+SingulationControl",
                "Symbol.RFID3.OperationFailureException",
                "Symbol.RFID3.InvalidUsageException",
                "Symbol.RFID3.ACCESS_OPERATION_STATUS",
                "Symbol.RFID3.VersionInfo"
            };
            foreach (var name in wanted)
            {
                Type t = asm.GetType(name);
                if (t == null) { sb.AppendLine("### " + name + " = NOT FOUND"); continue; }
                sb.AppendLine("### " + t.FullName + (t.IsEnum ? " (enum: " + string.Join(",", Enum.GetNames(t)) + ")" : ""));
                if (t.IsEnum) continue;
                foreach (var c in t.GetConstructors())
                    sb.AppendLine("  ctor(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    sb.AppendLine("  prop " + p.PropertyType.Name + " " + p.Name + (p.CanWrite ? " {get;set;}" : " {get;}"));
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                   .Where(m => !m.IsSpecialName))
                    sb.AppendLine("  meth " + m.ReturnType.Name + " " + m.Name + "(" +
                        string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    sb.AppendLine("  field " + f.FieldType.Name + " " + f.Name);
            }
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine(sb.ToString());
        }
    }
}
