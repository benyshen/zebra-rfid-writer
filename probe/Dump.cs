using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RfidProbe
{
    static class Dump
    {
        public static void Run(string outPath)
        {
            var sb = new StringBuilder();
            var asm = typeof(Symbol.RFID3.RFIDReader).Assembly;
            string[] wanted =
            {
                "Symbol.RFID3.RFIDReader", "Symbol.RFID3.Actions", "Symbol.RFID3.TagAccess",
                "Symbol.RFID3.TagAccess+WriteAccessParams", "Symbol.RFID3.TagAccess+ReadAccessParams",
                "Symbol.RFID3.TagData", "Symbol.RFID3.Events", "Symbol.RFID3.Events+ReadEventData",
                "Symbol.RFID3.Events+ReadEventArgs", "Symbol.RFID3.Inventory", "Symbol.RFID3.AccessFilter",
                "Symbol.RFID3.AccessOperationResult", "Symbol.RFID3.MEMORY_BANK", "Symbol.RFID3.RFIDResults",
                "Symbol.RFID3.Config", "Symbol.RFID3.Antennas+Config", "Symbol.RFID3.Antennas+AntennaRfConfig",
                "Symbol.RFID3.Antennas+AntennaProperties"
            };

            sb.AppendLine("ASSEMBLY: " + asm.FullName + "  runtime=" + Environment.Version + " os=" + IntPtr.Size * 8 + "bit");
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
            Console.WriteLine("dumped to " + outPath);
        }
    }
}
