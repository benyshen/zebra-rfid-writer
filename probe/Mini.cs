using System;

namespace RfidProbe
{
    static class Mini
    {
        public static void Run(string step)
        {
            Console.WriteLine("step: start " + step); Console.Out.Flush();
            Symbol.RFID3.RFIDReader reader = null;
            try
            {
                if (step == "ctor")
                {
                    reader = new Symbol.RFID3.RFIDReader();
                    Console.WriteLine("ctor ok, HostName='" + reader.HostName + "' Port=" + reader.Port + " IsConnected=" + reader.IsConnected);
                }
                else if (step == "ctorargs")
                {
                    reader = new Symbol.RFID3.RFIDReader("127.0.0.1", 5084, 5000);
                    Console.WriteLine("ctorargs ok");
                }
                else if (step == "events")
                {
                    reader = new Symbol.RFID3.RFIDReader();
                    reader.Events.ReadNotify += delegate (object s, Symbol.RFID3.Events.ReadEventArgs e) { };
                    Console.WriteLine("events ok");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION " + e.GetType().FullName + ": " + e.Message);
            }
            Console.Out.Flush();
            Environment.Exit(0);
        }
    }
}
