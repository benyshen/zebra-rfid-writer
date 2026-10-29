using System;
using System.Text;

namespace RfidProbe
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string cmd = args.Length > 0 ? args[0] : "dump";
            if (cmd == "dump")
            {
                Dump.Run(args.Length > 1 ? args[1] : "api-dump.txt");
                return;
            }
            if (cmd == "listen")
            {
                ClientMode.Run(
                    args.Length > 1 ? int.Parse(args[1]) : 5084,
                    args.Length > 2 ? int.Parse(args[2]) : 5085);
                return;
            }
            if (cmd == "dump2") { Dump2.Run(args.Length > 1 ? args[1] : "api-dump2.txt"); return; }
            if (cmd == "mini")
            {
                Mini.Run(args.Length > 1 ? args[1] : "ctor");
                return;
            }
            Console.WriteLine("unknown command");
        }
    }
}
