using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Symbol.RFID3;

namespace RfidWriter
{
    public enum ConnMode { Listen, Host }

    public class TagInfo
    {
        public string Epc;
        public ushort Pc;
        public ushort Antenna;
        public sbyte Rssi;
        public ushort Seen;
        public DateTime FirstSeen = DateTime.Now;
    }

    public class WriteOutcome
    {
        public bool Ok;
        public string Message = "";
        public string NewEpc;      // EPC 写入成功后标签的新 EPC（大写十六进制）
    }

    /// <summary>
    /// Zebra FXSeries Host SDK 封装。
    /// 支持两种连接方式：
    ///   Listen - 读写器作为 LLRP Client 主动连入本机（SDK 的 AcceptConnection）
    ///   Host   - 本机作为客户端主动连接读写器 IP:端口
    /// </summary>
    public class RfidEngine : IDisposable
    {
        private RFIDReader _reader;
        private Thread _workerThread;
        private TcpListener _listener;
        private volatile bool _disconnecting;
        private volatile bool _connected;

        public ConnectionMode Mode { get; private set; }
        public event Action<string> Log;
        public event Action<TagInfo> TagSeen;
        public event Action<bool, string> ConnectionChanged;   // connected, message

        public bool IsConnected { get { return _connected && _reader != null; } }

        public RFIDReader Reader { get { return _reader; } }

        private void L(string msg)
        {
            var h = Log;
            if (h != null) h(msg);
        }

        private void Raise(bool connected, string msg)
        {
            var h = ConnectionChanged;
            if (h != null) h(connected, msg);
        }

        // ---------------------------------------------------------------- 连接
        public void ConnectListen(int port)
        {
            ConnectListen(port, null);
        }

        /// <summary>监听模式；onlyFromIp 非空时只接受来自该 IP 的读写器连入</summary>
        public void ConnectListen(int port, string onlyFromIp)
        {
            if (_workerThread != null) throw new InvalidOperationException("监听线程已存在");
            Mode = ConnectionMode.Listen;
            _disconnecting = false;
            string filter = string.IsNullOrEmpty(onlyFromIp) ? null : onlyFromIp.Trim();
            _workerThread = new Thread(delegate () { ListenLoop(port, filter); });
            _workerThread.IsBackground = true;
            _workerThread.Start();
        }

        private void ListenLoop(int port, string onlyFromIp)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
            }
            catch (Exception ex)
            {
                L("监听端口 " + port + " 失败：" + Describe(ex));
                Raise(false, "端口占用 " + port);
                return;
            }

            L("正在监听 0.0.0.0:" + port + "，等待读写器连入 ..." +
              (onlyFromIp != null ? "（只接受 " + onlyFromIp + "）" : "（接受任意 IP）"));
            while (!_disconnecting)
            {
                Socket sock = null;
                try { sock = _listener.AcceptSocket(); }
                catch { if (_disconnecting) break; continue; }
                if (_disconnecting) break;

                string peer = "?";
                try { peer = ((IPEndPoint)sock.RemoteEndPoint).Address.ToString(); } catch { }
                if (onlyFromIp != null && peer != onlyFromIp)
                {
                    L("拒绝来自 " + peer + " 的连接（只接受 " + onlyFromIp + "）");
                    try { sock.Close(); } catch { }
                    continue;
                }
                L("读写器已连入：" + peer);
                try { sock.NoDelay = true; } catch { }

                RFIDReader reader = null;
                try
                {
                    reader = new RFIDReader(peer, (uint)port, 5000);
                    _reader = reader;
                    L("LLRP 握手 (AcceptConnection) ...");
                    reader.AcceptConnection(sock);
                    AttachEvents(reader);
                    _connected = true;
                    L("连接成功，读写器：" + peer);
                    Raise(true, peer);
                    MonitorSession(reader);
                }
                catch (Exception ex)
                {
                    if (!_disconnecting) L("会话异常：" + Describe(ex));
                }
                finally
                {
                    _connected = false;
                    _reader = null;
                    try { if (reader != null) reader.Dispose(); } catch { }
                    try { if (sock != null) sock.Close(); } catch { }
                    if (!_disconnecting)
                    {
                        L("读写器连接已断开，等待其重新连入 ...");
                        Raise(false, "已断开，等待重连");
                    }
                }
            }

            try { if (_listener != null) _listener.Stop(); } catch { }
            _listener = null;
        }

        /// <summary>轮询会话是否还活着；返回即表示会话结束</summary>
        private void MonitorSession(RFIDReader reader)
        {
            int bad = 0;   // 连续判定为断开的次数，避免 SDK 瞬时误报导致无谓重建
            while (!_disconnecting)
            {
                Thread.Sleep(400);
                bool alive;
                try { alive = reader.IsConnected; }
                catch { alive = false; }
                if (alive) { bad = 0; continue; }
                bad++;
                if (bad >= 3) return;
            }
        }

        public void ConnectHost(string ip, int port)
        {
            if (_workerThread != null) throw new InvalidOperationException("连接线程已存在");
            Mode = ConnectionMode.Host;
            _disconnecting = false;
            _workerThread = new Thread(delegate () { HostLoop(ip, port); });
            _workerThread.IsBackground = true;
            _workerThread.Start();
        }

        private void HostLoop(string ip, int port)
        {
            int attempt = 0;
            while (!_disconnecting)
            {
                attempt++;
                RFIDReader reader = null;
                try
                {
                    reader = new RFIDReader(ip, (uint)port, 5000);
                    _reader = reader;
                    L("正在连接读写器 " + ip + ":" + port + (attempt > 1 ? "（第 " + attempt + " 次）" : "") + " ...");
                    reader.Connect();
                    AttachEvents(reader);
                    _connected = true;
                    L("连接成功，读写器：" + ip);
                    Raise(true, ip);
                    MonitorSession(reader);
                }
                catch (Exception ex)
                {
                    if (_disconnecting) break;
                    if (attempt == 1 || attempt % 5 == 0) L("连接失败：" + Describe(ex) + "，稍后重试 ...");
                }
                finally
                {
                    _connected = false;
                    _reader = null;
                    try { if (reader != null) reader.Dispose(); } catch { }
                }
                if (_disconnecting) break;
                Raise(false, "未连接，重试中");
                for (int i = 0; i < 30 && !_disconnecting; i++) Thread.Sleep(100);
            }
        }

        private void AttachEvents(RFIDReader reader)
        {
            reader.Events.ReadNotify += OnReadNotify;
            reader.Events.NotifyReaderDisconnectEvent = true;
            reader.Events.NotifyInventoryStartEvent = true;
            reader.Events.NotifyInventoryStopEvent = true;
        }

        private void OnReadNotify(object sender, Events.ReadEventArgs e)
        {
            try
            {
                TagData td = e.ReadEventData.TagData;
                if (td == null || string.IsNullOrEmpty(td.TagID)) return;
                var ti = new TagInfo
                {
                    Epc = td.TagID.ToUpperInvariant(),
                    Pc = td.PC,
                    Antenna = td.AntennaID,
                    Rssi = td.PeakRSSI,
                    Seen = td.TagSeenCount
                };
                var h = TagSeen;
                if (h != null) h(ti);
            }
            catch { }
        }

        public string GetReaderInfo()
        {
            var r = _reader;
            if (r == null || !_connected) return "";
            var sb = new StringBuilder();
            try { sb.Append("型号 " + r.ReaderCapabilities.ModelName); } catch { }
            try { sb.Append("  固件 " + r.ReaderCapabilities.FirwareVersion); } catch { }
            try { sb.Append("  天线数 " + r.ReaderCapabilities.NumAntennaSupported); } catch { }
            try { sb.Append("  功率档位 " + r.ReaderCapabilities.TransmitPowerLevelValues.Length); } catch { }
            return sb.ToString();
        }

        public void Disconnect()
        {
            _disconnecting = true;
            _connected = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
            try
            {
                if (_reader != null)
                {
                    try { if (_reader.IsConnected) _reader.Disconnect(); } catch { }
                    _reader.Dispose();
                }
            }
            catch { }
            _reader = null;
            _workerThread = null;
            var h = ConnectionChanged;
            if (h != null) h(false, "已断开");
        }

        // ---------------------------------------------------------------- 功率
        public double[] GetPowerTable()
        {
            Require();
            return AntennaPower.DbmTable(_reader.ReaderCapabilities);
        }

        public Dictionary<int, double> GetAntennaPower()
        {
            Require();
            return AntennaPower.CurrentDbm(_reader, GetPowerTable());
        }

        public Dictionary<int, double> SetAntennaPower(double dbm, bool allAntennas, int antenna)
        {
            Require();
            var res = AntennaPower.SetDbm(_reader, dbm, allAntennas, antenna);
            L("天线功率设置为 " + dbm.ToString("0.#") + " dBm（" + (allAntennas ? "全部天线" : "天线 " + antenna) + "）");
            return res;
        }

        // ---------------------------------------------------------------- 盘点
        public void StartInventory()
        {
            Require();
            _reader.Actions.Inventory.Perform();
        }

        public void StopInventory()
        {
            if (_reader == null) return;
            try { _reader.Actions.Inventory.Stop(); } catch (Exception ex) { L("停止盘点: " + Describe(ex)); }
        }

        // ---------------------------------------------------------------- 写入
        /// <summary>写 EPC 区（整段 EPC，SDK 自动处理 PC 长度位）</summary>
        public WriteOutcome WriteEpc(string epc, byte[] data, uint password, bool verify)
        {
            var res = new WriteOutcome();
            Require();
            var p = new TagAccess.WriteSpecificFieldAccessParams();
            p.WriteData = data;
            p.WriteDataLength = (uint)data.Length;
            p.AccessPassword = password;
            try
            {
                _reader.Actions.TagAccess.WriteTagIDWait(epc, p, null);
            }
            catch (Exception ex)
            {
                res.Message = Describe(ex);
                return res;
            }

            string newEpc = ToHex(data);
            res.NewEpc = newEpc;
            if (verify)
            {
                try
                {
                    var rp = new TagAccess.ReadAccessParams();
                    rp.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                    rp.ByteOffset = 4;
                    rp.ByteCount = (uint)data.Length;
                    rp.AccessPassword = password;
                    TagData td = _reader.Actions.TagAccess.ReadWait(newEpc, rp, null);
                    string got = td != null ? (td.MemoryBankData ?? "").ToUpperInvariant() : "";
                    if (got != newEpc)
                    {
                        res.Message = "写入后校验不一致（读回 " + got + "）";
                        return res;
                    }
                }
                catch (Exception ex)
                {
                    res.Message = "写入成功但校验读取失败：" + Describe(ex);
                    return res;
                }
            }
            res.Ok = true;
            res.Message = "写入成功";
            return res;
        }

        /// <summary>写 USER 区（按字偏移）</summary>
        public WriteOutcome WriteUser(string epc, int wordOffset, byte[] data, uint password, bool verify)
        {
            var res = new WriteOutcome();
            Require();
            var p = new TagAccess.WriteAccessParams();
            p.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
            p.ByteOffset = (uint)(wordOffset * 2);
            p.WriteData = data;
            p.WriteDataLength = (uint)data.Length;
            p.AccessPassword = password;
            try
            {
                _reader.Actions.TagAccess.WriteWait(epc, p, null);
            }
            catch (Exception ex)
            {
                res.Message = Describe(ex);
                return res;
            }
            if (verify)
            {
                try
                {
                    var rp = new TagAccess.ReadAccessParams();
                    rp.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
                    rp.ByteOffset = (uint)(wordOffset * 2);
                    rp.ByteCount = (uint)data.Length;
                    rp.AccessPassword = password;
                    TagData td = _reader.Actions.TagAccess.ReadWait(epc, rp, null);
                    string got = td != null ? (td.MemoryBankData ?? "").ToUpperInvariant() : "";
                    if (got != ToHex(data))
                    {
                        res.Message = "写入后校验不一致（读回 " + got + "）";
                        return res;
                    }
                }
                catch (Exception ex)
                {
                    res.Message = "写入成功但校验读取失败：" + Describe(ex);
                    return res;
                }
            }
            res.Ok = true;
            res.Message = "写入成功";
            return res;
        }

        public static string ToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("X2"));
            return sb.ToString();
        }

        private void Require()
        {
            if (_reader == null || !_connected) throw new InvalidOperationException("未连接读写器");
        }

        public static string Describe(Exception ex)
        {
            var ofe = ex as OperationFailureException;
            if (ofe != null)
                return ofe.Result + " " + ofe.StatusDescription +
                       (string.IsNullOrEmpty(ofe.VendorMessage) ? "" : " [" + ofe.VendorMessage + "]");
            var iue = ex as InvalidUsageException;
            if (iue != null) return "参数错误 " + iue.Info;
            try { return ex.Message; } catch { return "未知错误"; }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }

    public enum ConnectionMode { Listen, Host }
}
