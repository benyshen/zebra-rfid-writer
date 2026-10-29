using System;
using System.Windows.Forms;

namespace RfidWriter
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--restore")
            {
                Restore.Run(args.Length > 1 ? int.Parse(args[1]) : 5084, args[2], args[3]);
                return;
            }
            if (args.Length > 0 && args[0] == "--filtertest")
            {
                FilterTest.Run(args.Length > 1 ? int.Parse(args[1]) : 5084,
                               args.Length > 2 ? args[2] : "192.168.100.22");
                return;
            }
            if (args.Length > 0 && args[0] == "--repeatplan")
            {
                RepeatPlan.Run(args.Length > 1 ? int.Parse(args[1]) : 6, args.Length > 2 ? int.Parse(args[2]) : 2);
                return;
            }
            if (args.Length > 0 && args[0] == "--killtest")
            {
                KillTest.Run(args.Length > 1 ? int.Parse(args[1]) : 5084);
                return;
            }
            if (args.Length > 0 && args[0] == "--diag")
            {
                Diag.Run(args.Length > 1 ? int.Parse(args[1]) : 5084);
                return;
            }
            if (args.Length > 0 && args[0] == "--powerinfo")
            {
                PowerProbe.Run(args.Length > 1 ? int.Parse(args[1]) : 5084);
                return;
            }
            if (args.Length > 0 && args[0] == "--inv")
            {
                Inv.Run(args.Length > 1 ? int.Parse(args[1]) : 5084, args.Length > 2 ? int.Parse(args[2]) : 4);
                return;
            }
            if (args.Length > 0 && args[0] == "--selftest")
            {
                int port = args.Length > 1 ? int.Parse(args[1]) : 5084;
                int count = args.Length > 2 ? int.Parse(args[2]) : 2;
                SelfTest.Run(port, count);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "程序异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.Run(new MainForm());
        }
    }
}
