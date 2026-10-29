using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Symbol.RFID3;

namespace RfidProbe
{
    static class ClientMode
    {
        static Dictionary<string, TagData> tags = new Dictionary<string, TagData>();
        static int reportCount = 0;

        static void OnRead(object sender, Events.ReadEventArgs e)
        {
            reportCount++;
            TagData td = e.ReadEventData.TagData;
            if (td != null && td.TagID != null) lock (tags) { tags[td.TagID] = td; }
        }

        static string Ex(Exception e)
        {
            try { return e.GetType().FullName + ": " + e.Message + " | " + e.StackTrace; }
            catch { return "unprintable exception"; }
        }

        public static void Run(int listenPort, int loopPort)
        {
            var listener = new TcpListener(IPAddress.Any, listenPort);
            listener.Start();
            Console.WriteLine("LISTENING on 0.0.0.0:" + listenPort + " ...");
            var sock = listener.AcceptSocket();
            Console.WriteLine("ACCEPTED from " + sock.RemoteEndPoint);
            sock.NoDelay = true;
            string peerIp = ((IPEndPoint)sock.RemoteEndPoint).Address.ToString();

            var reader = new RFIDReader(peerIp, (uint)listenPort, 5000);
            Console.WriteLine("reader created host=" + reader.HostName + " port=" + reader.Port);

            Console.WriteLine("calling AcceptConnection...");
            try
            {
                reader.AcceptConnection(sock);
                Console.WriteLine("AcceptConnection returned, IsConnected=" + reader.IsConnected);
            }
            catch (Exception e)
            {
                Console.WriteLine("AcceptConnection threw " + Ex(e));
                try { Console.WriteLine("IsConnected=" + reader.IsConnected); } catch { }
                Console.Out.Flush();
                Environment.Exit(2);
            }

            try
            {
                reader.Events.ReadNotify += new Events.ReadNotifyHandler(OnRead);
                reader.Events.NotifyReaderDisconnectEvent = true;
                Console.WriteLine("events subscribed");
            }
            catch (Exception e) { Console.WriteLine("events err " + Ex(e)); }

            Console.Out.Flush();
            Thread.Sleep(300);
            Console.WriteLine("--- inventory 6s ---");
            Console.Out.Flush();
            try
            {
                reader.Actions.Inventory.Perform();
                Thread.Sleep(6000);
                reader.Actions.Inventory.Stop();
            }
            catch (Exception e) { Console.WriteLine("inventory err " + Ex(e)); }
            Thread.Sleep(500);
            Console.WriteLine("reports=" + reportCount + " uniqTags=" + tags.Count);
            foreach (var kv in tags)
                Console.WriteLine("  " + kv.Key + " ant=" + kv.Value.AntennaID + " rssi=" + kv.Value.PeakRSSI +
                                  " seen=" + kv.Value.TagSeenCount + " pc=" + kv.Value.PC.ToString("X"));

            if (tags.Count > 0)
            {
                string targetEpc = null;
                foreach (var k in tags.Keys) if (k.Length == 24) { targetEpc = k; break; }
                if (targetEpc == null) foreach (var k in tags.Keys) { targetEpc = k; break; }
                Console.WriteLine("--- write SAME value to " + targetEpc + " ---");
                Console.Out.Flush();
                int words = targetEpc.Length / 4;
                byte[] data = new byte[words * 2];
                for (int i = 0; i < data.Length; i++) data[i] = Convert.ToByte(targetEpc.Substring(i * 2, 2), 16);
                var wp = new TagAccess.WriteSpecificFieldAccessParams();
                wp.WriteDataLength = (uint)data.Length;
                wp.WriteData = data;
                wp.AccessPassword = 0;
                try { reader.Actions.TagAccess.WriteTagIDWait(targetEpc, wp, null); Console.WriteLine("WRITE-ID OK"); }
                catch (OperationFailureException ofe) { Console.WriteLine("WRITE-ID FAIL " + ofe.Result + " / " + ofe.StatusDescription); }
                catch (Exception e) { Console.WriteLine("WRITE-ID EX " + Ex(e)); }

                var rp = new TagAccess.ReadAccessParams();
                rp.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                rp.ByteOffset = 4;
                rp.ByteCount = (uint)data.Length;
                try
                {
                    var td = reader.Actions.TagAccess.ReadWait(targetEpc, rp, null);
                    Console.WriteLine("READBACK=" + (td == null ? "null" : td.MemoryBankData));
                }
                catch (Exception e) { Console.WriteLine("READ EX " + Ex(e)); }
            }

            try { reader.Disconnect(); } catch { }
            Console.WriteLine("DONE");
            Console.Out.Flush();
            Environment.Exit(0);
        }
    }
}
