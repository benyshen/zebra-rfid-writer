using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RfidWriter
{
    public class MainForm : Form
    {
        // ---------- 引擎 ----------
        private readonly RfidEngine _engine = new RfidEngine();
        private readonly object _pendLock = new object();
        private readonly Dictionary<string, TagInfo> _pending = new Dictionary<string, TagInfo>();
        private readonly Dictionary<string, DataGridViewRow> _rows = new Dictionary<string, DataGridViewRow>();
        private readonly Dictionary<string, string> _originalEpc = new Dictionary<string, string>();
        private int _tagCount;
        private int _writtenCount;
        private volatile bool _stopWrite;
        private volatile bool _userDisconnect;
        private volatile bool _writing;

        // ---------- 控件 ----------
        private ComboBox cbMode;
        private TextBox tbHost;
        private NumericUpDown numPort;
        private Button btnConnect, btnDisconnect;
        private Label lblConnState, lblReaderInfo;
        private NumericUpDown numPower;
        private CheckBox chkAllAnt;
        private Button btnApplyPower;
        private Label lblPowerState;

        private Button btnStart, btnStop, btnClear, btnExport;
        private Label lblTotal, lblWritten;
        private DataGridView grid;

        private RadioButton rbEpc, rbUser;
        private NumericUpDown numUserWord;
        private TextBox tbPrefix, tbStart, tbSuffix, tbPassword;
        private ComboBox cbRadix, cbEncode;
        private NumericUpDown numDigits, numStep, numCount, numRetry, numRepeat;
        private CheckBox chkVerify, chkResize;
        private Label lblPreview, lblWriteState;
        private Button btnWrite, btnStopWrite;
        private ProgressBar progress;

        private TextBox tbLog;
        private System.Windows.Forms.Timer uiTimer;

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            using (var g = CreateGraphics())
            {
                float sc = g.DpiX / 96f;
                if (Math.Abs(sc - 1f) > 0.01f) ScaleUi(sc);
            }
            _engine.Log += OnEngineLog;
            _engine.TagSeen += OnTagSeen;
            _engine.ConnectionChanged += OnConnChanged;
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 250;
            uiTimer.Tick += delegate { DrainPending(); };
            uiTimer.Start();
        }

        // ======================================================= 高 DPI 手工缩放
        private void ScaleUi(float s)
        {
            ClientSize = new Size((int)Math.Round(ClientSize.Width * s), (int)Math.Round(ClientSize.Height * s));
            MinimumSize = new Size((int)Math.Round(MinimumSize.Width * s), (int)Math.Round(MinimumSize.Height * s));
            ScaleChildControls(Controls, s);
        }

        private static void ScaleChildControls(Control.ControlCollection controls, float s)
        {
            foreach (Control c in controls)
            {
                c.Left = (int)Math.Round(c.Left * s);
                c.Top = (int)Math.Round(c.Top * s);
                c.Width = (int)Math.Round(c.Width * s);
                c.Height = (int)Math.Round(c.Height * s);
                var gv = c as DataGridView;
                if (gv != null)
                {
                    foreach (DataGridViewColumn col in gv.Columns)
                        col.Width = (int)Math.Round(col.Width * s);
                    gv.ColumnHeadersHeight = (int)Math.Round(gv.ColumnHeadersHeight * s);
                    gv.RowTemplate.Height = (int)Math.Round(gv.RowTemplate.Height * s);
                }
                if (c.HasChildren) ScaleChildControls(c.Controls, s);
            }
        }

        // ======================================================= UI 构建
        private void BuildUi()
        {
            Text = "Zebra RFID 读写工具  v1.0";
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(1180, 764);
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 247, 249);

            // ---------- ① 连接 ----------
            var gbConn = Group("① 读写器连接", 10, 8, 1160, 90);
            var lMode = new Label { Text = "连接方式", Left = 14, Top = 26, Width = 62 };
            cbMode = new ComboBox { Left = 80, Top = 22, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cbMode.Items.Add("读写器主动连入（监听模式）");
            cbMode.Items.Add("本机主动连接读写器");
            cbMode.SelectedIndex = 0;
            cbMode.SelectedIndexChanged += delegate { UpdateModeFields(); };

            var lHost = new Label { Text = "IP 地址", Left = 374, Top = 26, Width = 52 };
            tbHost = new TextBox { Left = 430, Top = 22, Width = 130, Text = "192.168.100.22" };
            var lPort = new Label { Text = "端口", Left = 572, Top = 26, Width = 36 };
            numPort = new NumericUpDown { Left = 612, Top = 22, Width = 80, Minimum = 1, Maximum = 65535, Value = 5084 };
            btnConnect = new Button { Text = "连接", Left = 706, Top = 20, Width = 90, Height = 28 };
            btnDisconnect = new Button { Text = "断开", Left = 802, Top = 20, Width = 90, Height = 28, Enabled = false };
            btnConnect.Click += delegate { DoConnect(); };
            btnDisconnect.Click += delegate { DoDisconnect(); };
            lblConnState = new Label
            {
                Left = 906, Top = 26, Width = 244, Text = "未连接",
                ForeColor = Color.Firebrick,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            lblReaderInfo = new Label { Left = 14, Top = 60, Width = 500, Height = 20, Text = "", ForeColor = Color.DimGray };

            var lPower = new Label { Text = "天线功率", Left = 520, Top = 60, Width = 62 };
            numPower = new NumericUpDown
            {
                Left = 610, Top = 56, Width = 76, DecimalPlaces = 1,
                Minimum = 0, Maximum = 33, Increment = 0.5M, Value = 29.2M
            };
            var lDbm = new Label { Text = "dBm", Left = 690, Top = 60, Width = 34 };
            chkAllAnt = new CheckBox { Text = "全部天线", Left = 726, Top = 59, Width = 84, Checked = true };
            btnApplyPower = new Button { Text = "应用功率", Left = 814, Top = 54, Width = 90, Height = 26, Enabled = false };
            btnApplyPower.Click += delegate { ApplyPower(); };
            lblPowerState = new Label { Left = 912, Top = 60, Width = 240, Height = 20, Text = "", ForeColor = Color.DimGray };

            gbConn.Controls.AddRange(new Control[] { lMode, cbMode, lHost, tbHost, lPort, numPort, btnConnect, btnDisconnect,
                lblConnState, lblReaderInfo, lPower, numPower, lDbm, chkAllAnt, btnApplyPower, lblPowerState });

            // ---------- ② 标签列表 ----------
            var gbTags = Group("② 标签读取列表", 10, 106, 762, 360);
            btnStart = new Button { Text = "开始盘点", Left = 12, Top = 24, Width = 96, Height = 30 };
            btnStop = new Button { Text = "停止盘点", Left = 114, Top = 24, Width = 96, Height = 30, Enabled = false };
            btnClear = new Button { Text = "清空列表", Left = 216, Top = 24, Width = 88, Height = 30 };
            btnExport = new Button { Text = "导出 CSV", Left = 310, Top = 24, Width = 88, Height = 30 };
            lblTotal = new Label { Left = 412, Top = 30, Width = 130, Text = "标签总数：0", Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold) };
            lblWritten = new Label
            {
                Left = 546, Top = 30, Width = 120, Text = "已写入：0",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), ForeColor = Color.SeaGreen
            };
            btnStart.Click += delegate { DoStartInventory(); };
            btnStop.Click += delegate { DoStopInventory(); };
            btnClear.Click += delegate { ClearTags(); };
            btnExport.Click += delegate { ExportCsv(); };

            grid = new DataGridView
            {
                Left = 12, Top = 62, Width = 738, Height = 286,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                ReadOnly = true, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersHeight = 28, RowTemplate = { Height = 24 }
            };
            grid.Columns.Add(Col("序号", 50));
            grid.Columns.Add(Col("EPC（十六进制）", 272));
            grid.Columns.Add(Col("天线", 50));
            grid.Columns.Add(Col("RSSI", 58));
            grid.Columns.Add(Col("次数", 52));
            grid.Columns.Add(Col("写入状态", 190));
            grid.Columns[1].DefaultCellStyle.Font = new Font("Consolas", 9F);
            grid.Columns[5].DefaultCellStyle.ForeColor = Color.DimGray;

            var menu = new ContextMenuStrip();
            var miCopy = menu.Items.Add("复制 EPC");
            miCopy.Click += delegate { CopySelectedEpc(); };
            var miRevert = menu.Items.Add("还原该标签为原始 EPC");
            miRevert.Click += delegate { RevertSelectedEpc(); };
            grid.ContextMenuStrip = menu;
            grid.CellMouseDown += delegate (object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    grid.ClearSelection();
                    grid.Rows[e.RowIndex].Selected = true;
                }
            };
            gbTags.Controls.AddRange(new Control[] { btnStart, btnStop, btnClear, btnExport, lblTotal, lblWritten, grid });

            // ---------- ③ 序列化写入 ----------
            var gbW = Group("③ 序列化写入", 778, 106, 392, 360);
            int lx = 12, cx = 100;

            // 行1 目标存储区
            gbW.Controls.Add(new Label { Text = "目标存储区", Left = lx, Top = 28, Width = 82 });
            rbEpc = new RadioButton { Text = "EPC 区", Left = cx, Top = 24, Width = 84, Checked = true };
            rbUser = new RadioButton { Text = "USER 区", Left = cx + 86, Top = 24, Width = 96 };
            rbEpc.CheckedChanged += delegate { UpdateWritePreview(); };
            rbUser.CheckedChanged += delegate { UpdateWritePreview(); };
            rbUser.CheckedChanged += delegate { numUserWord.Enabled = rbUser.Checked; };
            gbW.Controls.AddRange(new Control[] { rbEpc, rbUser });

            // 行2 USER 偏移 + 访问密码
            gbW.Controls.Add(new Label { Text = "USER 起始字", Left = lx, Top = 58, Width = 82 });
            numUserWord = new NumericUpDown { Left = cx, Top = 54, Width = 60, Maximum = 255, Value = 0, Enabled = false };
            gbW.Controls.Add(numUserWord);
            gbW.Controls.Add(new Label { Text = "访问密码", Left = 178, Top = 58, Width = 62 });
            tbPassword = new TextBox { Left = 242, Top = 54, Width = 122, Text = "00000000" };
            gbW.Controls.Add(tbPassword);

            // 行3 前缀 + 后缀
            gbW.Controls.Add(new Label { Text = "前缀(HEX)", Left = lx, Top = 88, Width = 82 });
            tbPrefix = new TextBox { Left = cx, Top = 84, Width = 96, Text = "" };
            gbW.Controls.Add(tbPrefix);
            gbW.Controls.Add(new Label { Text = "后缀(HEX)", Left = 206, Top = 88, Width = 62 });
            tbSuffix = new TextBox { Left = 270, Top = 84, Width = 94, Text = "" };
            gbW.Controls.Add(tbSuffix);

            // 行4 起始值 + 步进
            gbW.Controls.Add(new Label { Text = "起始值", Left = lx, Top = 118, Width = 82 });
            tbStart = new TextBox { Left = cx, Top = 114, Width = 106, Text = "1" };
            gbW.Controls.Add(tbStart);
            gbW.Controls.Add(new Label { Text = "步进", Left = 216, Top = 118, Width = 36 });
            numStep = new NumericUpDown { Left = 254, Top = 114, Width = 110, Minimum = 0, Maximum = 1000000, Value = 1 };
            gbW.Controls.Add(numStep);

            // 行5 进制 + 位数 + 数值编码
            gbW.Controls.Add(new Label { Text = "进制", Left = lx, Top = 148, Width = 34 });
            cbRadix = new ComboBox { Left = 50, Top = 144, Width = 72, DropDownStyle = ComboBoxStyle.DropDownList };
            cbRadix.Items.Add("16 进制");
            cbRadix.Items.Add("10 进制");
            cbRadix.SelectedIndex = 0;
            gbW.Controls.Add(cbRadix);
            gbW.Controls.Add(new Label { Text = "位数", Left = 130, Top = 148, Width = 34 });
            numDigits = new NumericUpDown { Left = 166, Top = 144, Width = 52, Minimum = 0, Maximum = 64, Value = 12 };
            gbW.Controls.Add(numDigits);
            gbW.Controls.Add(new Label { Text = "数值编码", Left = 226, Top = 148, Width = 60 });
            cbEncode = new ComboBox { Left = 288, Top = 144, Width = 78, DropDownStyle = ComboBoxStyle.DropDownList };
            cbEncode.Items.Add("HEX 原样");
            cbEncode.Items.Add("ASCII");
            cbEncode.SelectedIndex = 0;
            gbW.Controls.Add(cbEncode);

            // 行6 张数 + 重复次数 + 重试
            gbW.Controls.Add(new Label { Text = "写入张数", Left = lx, Top = 178, Width = 62 });
            numCount = new NumericUpDown { Left = 76, Top = 174, Width = 58, Minimum = 1, Maximum = 10000, Value = 10 };
            gbW.Controls.Add(numCount);
            gbW.Controls.Add(new Label { Text = "重复", Left = 142, Top = 178, Width = 36 });
            numRepeat = new NumericUpDown { Left = 180, Top = 174, Width = 46, Minimum = 1, Maximum = 10000, Value = 1 };
            gbW.Controls.Add(numRepeat);
            gbW.Controls.Add(new Label { Text = "重试", Left = 234, Top = 178, Width = 36 });
            numRetry = new NumericUpDown { Left = 272, Top = 174, Width = 44, Minimum = 0, Maximum = 5, Value = 2 };
            gbW.Controls.Add(numRetry);

            // 行7 两个复选框
            chkVerify = new CheckBox { Text = "写入后校验", Left = lx, Top = 202, Width = 96, Checked = true };
            gbW.Controls.Add(chkVerify);
            chkResize = new CheckBox { Text = "允许改变 EPC 长度（有风险）", Left = 114, Top = 202, Width = 266 };
            gbW.Controls.Add(chkResize);

            // 行8 预览
            lblPreview = new Label
            {
                Left = lx, Top = 226, Width = 368, Height = 42,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White,
                ForeColor = Color.FromArgb(22, 50, 79), Padding = new Padding(4),
                Text = "预览："
            };
            gbW.Controls.Add(lblPreview);

            // 行9 按钮
            btnWrite = new Button
            {
                Text = "开始写入", Left = lx, Top = 274, Width = 130, Height = 32,
                BackColor = Color.FromArgb(43, 108, 176), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            btnStopWrite = new Button { Text = "停止写入", Left = lx + 140, Top = 274, Width = 106, Height = 32, Enabled = false };
            btnWrite.Click += delegate { StartWrite(); };
            btnStopWrite.Click += delegate { _stopWrite = true; Log("用户请求停止写入"); };
            gbW.Controls.AddRange(new Control[] { btnWrite, btnStopWrite });

            // 行10 进度 + 状态
            progress = new ProgressBar { Left = lx, Top = 312, Width = 368, Height = 16 };
            lblWriteState = new Label
            {
                Left = lx, Top = 332, Width = 368, Height = 20, Text = "就绪",
                ForeColor = Color.DimGray
            };
            gbW.Controls.AddRange(new Control[] { progress, lblWriteState });

            // 事件绑定（参数变化 -> 刷新预览）
            tbPrefix.TextChanged += delegate { UpdateWritePreview(); };
            tbSuffix.TextChanged += delegate { UpdateWritePreview(); };
            tbStart.TextChanged += delegate { UpdateWritePreview(); };
            cbRadix.SelectedIndexChanged += delegate { UpdateWritePreview(); };
            cbEncode.SelectedIndexChanged += delegate { UpdateWritePreview(); };
            numDigits.ValueChanged += delegate { UpdateWritePreview(); };
            numStep.ValueChanged += delegate { UpdateWritePreview(); };
            numRepeat.ValueChanged += delegate { UpdateWritePreview(); };

            // ---------- ④ 日志 ----------
            var gbLog = Group("④ 运行日志", 10, 474, 1160, 280);
            tbLog = new TextBox
            {
                Left = 12, Top = 24, Width = 1136, Height = 244,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(252, 252, 252), Font = new Font("Consolas", 9F)
            };
            gbLog.Controls.Add(tbLog);

            Controls.AddRange(new Control[] { gbConn, gbTags, gbW, gbLog });
            UpdateModeFields();
            UpdateWritePreview();
        }

        private static DataGridViewTextBoxColumn Col(string header, int width)
        {
            return new DataGridViewTextBoxColumn { HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };
        }

        private static GroupBox Group(string text, int x, int y, int w, int h)
        {
            return new GroupBox
            {
                Text = text, Left = x, Top = y, Width = w, Height = h,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 50, 79), BackColor = Color.White
            };
        }

        private void UpdateModeFields()
        {
            bool listen = cbMode.SelectedIndex == 0;
            tbHost.Enabled = true;   // IP 栏始终可改
            var tip = new ToolTip();
            tip.SetToolTip(tbHost, listen
                ? "监听模式：填读写器 IP 则只接受该 IP 连入（留空接受任意 IP）"
                : "主动连接模式：本机去连这个 IP");
            lblConnState.Text = listen ? "未连接（等待读写器连入）" : "未连接";
        }

        // ======================================================= 连接
        private void DoConnect()
        {
            _userDisconnect = false;
            try
            {
                string ipText = tbHost.Text.Trim();
                System.Net.IPAddress parsed;
                if (ipText.Length > 0 && !System.Net.IPAddress.TryParse(ipText, out parsed))
                {
                    Log("IP 地址格式不正确：" + ipText);
                    return;
                }
                btnConnect.Enabled = false;
                if (cbMode.SelectedIndex == 0)
                {
                    _engine.ConnectListen((int)numPort.Value, tbHost.Text.Trim());
                    SetConnState("监听中，等待读写器连入 ...", Color.DarkOrange);
                }
                else
                {
                    _engine.ConnectHost(tbHost.Text.Trim(), (int)numPort.Value);
                    SetConnState("正在连接 ...", Color.DarkOrange);
                }
                Log("连接方式：" + cbMode.Text +
                    (cbMode.SelectedIndex == 0
                        ? "，IP 过滤：" + (tbHost.Text.Trim().Length > 0 ? "只接受 " + tbHost.Text.Trim() : "任意")
                        : "，目标 " + tbHost.Text.Trim()));
                if (cbMode.SelectedIndex == 0 && tbHost.Text.Trim().Length == 0)
                    Log("提示：监听模式下 IP 栏留空 = 接受任意读写器连入；填 IP 可只允许该读写器连入");
            }
            catch (Exception ex)
            {
                btnConnect.Enabled = true;
                Log("连接异常：" + RfidEngine.Describe(ex));
            }
        }

        private void DoDisconnect()
        {
            _userDisconnect = true;
            _engine.Disconnect();
            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            SetConnState("已断开", Color.Firebrick);
            lblReaderInfo.Text = "";
            btnApplyPower.Enabled = false;
            lblPowerState.Text = "";
        }

        private void OnConnChanged(bool connected, string msg)
        {
            BeginInvokeSafe(delegate
            {
                if (connected)
                {
                    SetConnState("已连接：" + msg, Color.SeaGreen);
                    btnConnect.Enabled = false;
                    btnDisconnect.Enabled = true;
                    lblReaderInfo.Text = "读写器信息：" + _engine.GetReaderInfo();
                    btnApplyPower.Enabled = true;
                    PopulatePowerCombo();
                    Log("读写器已连接：" + msg);
                }
                else if (!_userDisconnect && cbMode.SelectedIndex == 0)
                {
                    // 读写器主动连入模式：会话掉了，监听线程会自动等待它重连
                    SetConnState("读写器已断开，等待重新连入 ...", Color.DarkOrange);
                    btnConnect.Enabled = false;
                    btnDisconnect.Enabled = true;
                    btnApplyPower.Enabled = false;
                    lblReaderInfo.Text = "";
                    lblPowerState.Text = "";
                }
                else
                {
                    SetConnState("未连接" + (string.IsNullOrEmpty(msg) ? "" : "（" + msg + "）"), Color.Firebrick);
                    btnConnect.Enabled = true;
                    btnDisconnect.Enabled = false;
                    btnApplyPower.Enabled = false;
                    lblReaderInfo.Text = "";
                    lblPowerState.Text = "";
                }
            });
        }

        private void SetConnState(string text, Color color)
        {
            lblConnState.Text = text;
            lblConnState.ForeColor = color;
        }

        // ---------------- 天线功率 ----------------
        private void PopulatePowerCombo()
        {
            try
            {
                var table = _engine.GetPowerTable();
                if (table != null && table.Length > 0)
                {
                    numPower.Minimum = (decimal)table.Min();
                    numPower.Maximum = (decimal)table.Max();
                    numPower.Increment = 0.1M;
                    Log("功率档位表 " + table.Length + " 档，范围 " + table.Min().ToString("0.#") + "~" +
                        table.Max().ToString("0.#") + " dBm");
                }
                var cur = AntennaPower.CurrentDbm(_engine.Reader, table);
                if (cur.Count > 0)
                {
                    decimal v = (decimal)cur.Values.Max();
                    if (v >= numPower.Minimum && v <= numPower.Maximum) numPower.Value = v;
                    var parts = new List<string>();
                    foreach (var kv in cur.OrderBy(k => k.Key)) parts.Add("A" + kv.Key + ":" + kv.Value.ToString("0.#"));
                    lblPowerState.Text = "当前 " + string.Join(" ", parts) + " dBm";
                }
            }
            catch (Exception ex) { Log("读取功率档位失败：" + RfidEngine.Describe(ex)); }
        }

        private void ApplyPower()
        {
            if (!_engine.IsConnected) { Log("读写器未连接" + (cbMode.SelectedIndex == 0 && !_userDisconnect ? "（等待读写器重新连入中，请稍候）" : "，请先连接")); return; }
            double dbm = (double)numPower.Value;
            bool all = chkAllAnt.Checked;
            int ant = 1;
            btnApplyPower.Enabled = false;
            Log("正在设置天线功率 " + dbm + " dBm（" + (all ? "全部天线" : "天线 " + ant) + "）...");
            var th = new Thread(delegate ()
            {
                try
                {
                    var done = _engine.SetAntennaPower(dbm, all, ant);
                    var sb = new System.Text.StringBuilder();
                    foreach (var kv in done.OrderBy(k => k.Key)) sb.Append("A" + kv.Key + "=" + kv.Value.ToString("0.#") + "dBm ");
                    string msg = "已设置：" + sb.ToString().Trim();
                    Log(msg);
                    BeginInvokeSafe(delegate
                    {
                        lblPowerState.Text = "当前 " + sb.ToString().Trim();
                        btnApplyPower.Enabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Log("功率设置失败：" + RfidEngine.Describe(ex));
                    BeginInvokeSafe(delegate { btnApplyPower.Enabled = true; });
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        // ======================================================= 盘点
        private void DoStartInventory()
        {
            if (!_engine.IsConnected) { Log("读写器未连接，请稍候再试"); return; }
            try
            {
                ClearTags();
                _engine.StartInventory();
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                Log("开始盘点");
            }
            catch (Exception ex)
            {
                Log("开始盘点失败：" + RfidEngine.Describe(ex));
            }
        }

        private void DoStopInventory()
        {
            _engine.StopInventory();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            Log("停止盘点，共 " + _tagCount + " 个标签");
        }

        private void OnTagSeen(TagInfo ti)
        {
            lock (_pendLock)
            {
                TagInfo cur;
                if (_pending.TryGetValue(ti.Epc, out cur))
                {
                    cur.Seen += ti.Seen;
                    cur.Rssi = ti.Rssi;
                    cur.Antenna = ti.Antenna;
                    cur.Pc = ti.Pc;
                }
                else
                {
                    TagInfo old;
                    if (_rows.ContainsKey(ti.Epc)) return;      // 已在表格中，忽略（由定时器更新）
                    if (_pending.TryGetValue(ti.Epc, out old)) return;
                    _pending[ti.Epc] = ti;
                }
            }
        }

        private void DrainPending()
        {
            List<TagInfo> fresh = null;
            lock (_pendLock)
            {
                if (_pending.Count > 0)
                {
                    fresh = _pending.Values.ToList();
                    _pending.Clear();
                }
            }
            if (fresh == null || fresh.Count == 0) return;

            grid.SuspendLayout();
            foreach (var t in fresh)
            {
                DataGridViewRow row;
                if (_rows.TryGetValue(t.Epc, out row))
                {
                    row.Cells[4].Value = t.Seen;
                    row.Cells[3].Value = t.Rssi;
                    row.Cells[2].Value = t.Antenna;
                }
                else
                {
                    _tagCount++;
                    int idx = grid.Rows.Add(_tagCount, t.Epc, t.Antenna, t.Rssi, t.Seen, "待写");
                    row = grid.Rows[idx];
                    _rows[t.Epc] = row;
                    if (!_originalEpc.ContainsKey(t.Epc)) _originalEpc[t.Epc] = t.Epc;
                }
            }
            grid.ResumeLayout();
            lblTotal.Text = "标签总数：" + _tagCount;
        }

        private void ClearTags()
        {
            lock (_pendLock) _pending.Clear();
            _rows.Clear();
            _originalEpc.Clear();
            grid.Rows.Clear();
            _tagCount = 0;
            _writtenCount = 0;
            lblTotal.Text = "标签总数：0";
            lblWritten.Text = "已写入：0";
        }

        private void ExportCsv()
        {
            if (grid.Rows.Count == 0) { Log("列表为空，无内容可导出"); return; }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = "rfid_tags_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var sb = new StringBuilder();
            sb.AppendLine("序号,EPC,天线,RSSI,次数,写入状态");
            foreach (DataGridViewRow r in grid.Rows)
            {
                sb.AppendLine(string.Join(",", new string[]
                {
                    Convert.ToString(r.Cells[0].Value), Convert.ToString(r.Cells[1].Value),
                    Convert.ToString(r.Cells[2].Value), Convert.ToString(r.Cells[3].Value),
                    Convert.ToString(r.Cells[4].Value), Convert.ToString(r.Cells[5].Value)
                }));
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            Log("已导出：" + dlg.FileName);
        }

        // ======================================================= 序列化
        private void UpdateWritePreview()
        {
            try
            {
                int rep = numRepeat == null ? 1 : (int)numRepeat.Value;
                if (rep < 1) rep = 1;
                if (rep == 1)
                {
                    lblPreview.Text = "第 1 张：" + BuildHexString(0) + "\r\n第 2 张：" + BuildHexString(1);
                }
                else
                {
                    lblPreview.Text = "第 1~" + rep + " 张：" + BuildHexString(0) + "\r\n" +
                                      "第 " + (rep + 1) + "~" + (2 * rep) + " 张：" + BuildHexString(1);
                }
                if (rbUser.Checked) lblPreview.Text += "   （USER 区 字偏移 " + numUserWord.Value + "）";
            }
            catch (Exception ex)
            {
                lblPreview.Text = "预览错误：" + ex.Message;
            }
        }

        /// <summary>读取界面上的序列化参数</summary>
        private SerialOptions GetOptions()
        {
            var o = new SerialOptions();
            o.Prefix = tbPrefix.Text;
            o.Suffix = tbSuffix.Text;
            o.Start = tbStart.Text;
            o.Radix = cbRadix.SelectedIndex == 1 ? 10 : 16;
            o.Digits = (int)numDigits.Value;
            o.Encode = cbEncode.SelectedIndex;
            o.Step = (long)numStep.Value;
            return o;
        }

        /// <summary>生成第 index 条的十六进制数据串（不补齐到标签容量）</summary>
        private string BuildHexString(int index)
        {
            return SerialFormat.Build(GetOptions(), index);
        }

        private static byte[] HexToBytes(string hex)
        {
            return SerialFormat.HexToBytes(hex);
        }

        // ======================================================= 写入
        private void StartWrite()
        {
            if (_writing) return;
            if (!_engine.IsConnected) { Log("读写器未连接" + (cbMode.SelectedIndex == 0 && !_userDisconnect ? "（等待读写器重新连入中，请稍候）" : "，请先连接")); return; }
            if (_rows.Count == 0) { Log("列表为空：请先盘点读取标签"); return; }

            bool toEpc = rbEpc.Checked;
            int userWord = (int)numUserWord.Value;
            uint pwd = (uint)SerialOptions.ParseNumber(tbPassword.Text);
            int count = (int)numCount.Value;
            int retry = (int)numRetry.Value;
            int repeat = (int)numRepeat.Value;
            if (repeat < 1) repeat = 1;
            if (repeat > 1)
            {
                Log("写入重复次数 = " + repeat + "：每 " + repeat + " 张标签写入同一序列号（共 " + count + " 张）");
                if (toEpc)
                    Log("提示：重复写 EPC 会让这 " + repeat + " 张标签 EPC 完全相同，之后无法按 EPC 区分它们；要保区分请改 USER 区或重复=1");
            }
            if (count % repeat != 0)
                Log("提示：写入张数 " + count + " 不是重复次数 " + repeat + " 的整数倍，最后一组只有 " + (count % repeat) + " 张");
            bool verify = chkVerify.Checked;
            bool resize = chkResize.Checked;

            var targets = _rows.Keys.ToList();           // 保持发现顺序
            if (count > targets.Count)
            {
                Log("提示：设定写入 " + count + " 张，但列表只有 " + targets.Count + " 个标签，将按列表数量写入");
                count = targets.Count;
            }
            _writing = true;
            _stopWrite = false;
            btnWrite.Enabled = false;
            btnStopWrite.Enabled = true;
            progress.Minimum = 0;
            progress.Maximum = count;
            progress.Value = 0;
            _writtenCount = 0;

            var thread = new Thread(delegate ()
            {
                int ok = 0, fail = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_stopWrite) break;
                    string epc = targets[i];
                    int idx = i + 1;
                    SetWriteState("正在写入第 " + idx + " / " + count + " 张：" + epc);
                    SetRowStatus(epc, "写入中 ...", Color.DarkOrange);

                    // 会话中途掉线时等待自动重连，避免整批失败
                    if (!_engine.IsConnected)
                    {
                        SetRowStatus(epc, "等待读写器重连 ...", Color.DarkOrange);
                        Log("第 " + idx + " 张：读写器未连接，等待重连（最多 20 秒）...");
                        bool okConn = false;
                        for (int w = 0; w < 80; w++)
                        {
                            if (_stopWrite) break;
                            if (_engine.IsConnected) { okConn = true; break; }
                            Thread.Sleep(250);
                        }
                        if (!okConn)
                        {
                            fail++;
                            SetRowStatus(epc, "失败：读写器未连接", Color.Firebrick);
                            Log("第 " + idx + " 张放弃：读写器未连接");
                            StepProgress(idx);
                            continue;
                        }
                        Log("读写器已重新连接，继续写入");
                    }

                    byte[] data = null;
                    string genHex = null;
                    int seqIndex = SerialFormat.SeqIndexOf(i, repeat);
                    try
                    {
                        genHex = BuildHexString(seqIndex);
                        data = HexToBytes(genHex);
                        if (toEpc && !resize)
                        {
                            int cap = epc.Length / 2;
                            if (data.Length > cap)
                                throw new Exception("生成数据 " + data.Length + " 字节，超过该标签 EPC 容量 " + cap + " 字节");
                            if (data.Length < cap)
                            {
                                var pad = new byte[cap - data.Length];
                                data = pad.Concat(data).ToArray();
                                genHex = RfidEngine.ToHex(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        SetRowStatus(epc, "失败：" + ex.Message, Color.Firebrick);
                        Log("第 " + idx + " 张 " + epc + " 数据生成失败：" + ex.Message);
                        StepProgress(idx);
                        continue;
                    }

                    WriteOutcome res = null;
                    for (int attempt = 0; attempt <= retry; attempt++)
                    {
                        try
                        {
                            res = toEpc
                                ? _engine.WriteEpc(epc, data, pwd, verify)
                                : _engine.WriteUser(epc, userWord, data, pwd, verify);
                        }
                        catch (Exception ex)
                        {
                            res = new WriteOutcome { Ok = false, Message = RfidEngine.Describe(ex) };
                        }
                        if (res.Ok) break;
                        Thread.Sleep(120);
                    }

                    if (res != null && res.Ok)
                    {
                        ok++;
                        if (toEpc)
                        {
                            string newEpc = res.NewEpc;
                            SetRowEpc(epc, newEpc, "已写入 EPC → " + newEpc);
                            Log("第 " + idx + " 张写入成功：" + epc + " → " + newEpc + "（数据 " + RfidEngine.ToHex(data) +
                                "，序列号 #" + (seqIndex + 1) + "）");
                        }
                        else
                        {
                            SetRowStatus(epc, "已写入 USER @" + userWord + " 字（" + RfidEngine.ToHex(data) + "）", Color.SeaGreen);
                            Log("第 " + idx + " 张写入成功：" + epc + " USER 数据 " + RfidEngine.ToHex(data));
                        }
                        _writtenCount = ok;
                        SetWrittenLabel(ok);
                    }
                    else
                    {
                        fail++;
                        string msg = res == null ? "未知错误" : res.Message;
                        SetRowStatus(epc, "失败：" + msg, Color.Firebrick);
                        Log("第 " + idx + " 张写入失败：" + epc + " → " + msg);
                    }
                    StepProgress(idx);
                }

                SetWriteState("写入结束：成功 " + ok + " 张，失败 " + fail + " 张");
                Log("写入任务结束：成功 " + ok + "，失败 " + fail);
                BeginInvokeSafe(delegate
                {
                    _writing = false;
                    btnWrite.Enabled = true;
                    btnStopWrite.Enabled = false;
                });
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void StepProgress(int value)
        {
            BeginInvokeSafe(delegate
            {
                if (value <= progress.Maximum) progress.Value = value;
            });
        }

        private void SetWriteState(string text)
        {
            BeginInvokeSafe(delegate { lblWriteState.Text = text; });
        }

        private void SetWrittenLabel(int n)
        {
            BeginInvokeSafe(delegate { lblWritten.Text = "已写入：" + n; });
        }

        private void SetRowStatus(string epc, string status, Color color)
        {
            BeginInvokeSafe(delegate
            {
                DataGridViewRow r;
                if (!_rows.TryGetValue(epc, out r)) return;
                r.Cells[5].Value = status;
                r.Cells[5].Style.ForeColor = color;
            });
        }

        private void SetRowEpc(string oldEpc, string newEpc, string status)
        {
            BeginInvokeSafe(delegate
            {
                DataGridViewRow r;
                if (!_rows.TryGetValue(oldEpc, out r)) return;
                r.Cells[1].Value = newEpc;
                r.Cells[5].Value = status;
                r.Cells[5].Style.ForeColor = Color.SeaGreen;
                string orig;
                if (_originalEpc.TryGetValue(oldEpc, out orig)) _originalEpc.Remove(oldEpc);
                else orig = oldEpc;
                _originalEpc[newEpc] = orig;
                _rows.Remove(oldEpc);
                _rows[newEpc] = r;
            });
        }

        // ---------------- 右键菜单动作 ----------------
        private void CopySelectedEpc()
        {
            if (grid.SelectedRows.Count == 0) return;
            var v = Convert.ToString(grid.SelectedRows[0].Cells[1].Value);
            if (string.IsNullOrEmpty(v)) return;
            try { Clipboard.SetText(v); Log("已复制 EPC：" + v); } catch { }
        }

        private void RevertSelectedEpc()
        {
            if (grid.SelectedRows.Count == 0) { Log("请先选中一行"); return; }
            if (_writing) { Log("写入进行中，无法执行还原"); return; }
            string cur = Convert.ToString(grid.SelectedRows[0].Cells[1].Value);
            string orig;
            if (!_originalEpc.TryGetValue(cur, out orig) || orig == cur) { Log("该标签没有可还原的原始 EPC：" + cur); return; }
            if (!_engine.IsConnected) { Log("读写器未连接，请稍候再试"); return; }
            if (MessageBox.Show(this, "将标签 " + cur + "\r\n还原为 " + orig + " ？", "确认还原",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            uint pwd = (uint)SerialOptions.ParseNumber(tbPassword.Text);
            bool verify = chkVerify.Checked;
            SetRowStatus(cur, "还原中 ...", Color.DarkOrange);
            var th = new Thread(delegate ()
            {
                var res = _engine.WriteEpc(cur, SerialFormat.HexToBytes(orig), pwd, verify);
                if (res.Ok) { SetRowEpc(cur, orig, "已还原为原始 EPC"); Log("还原成功：" + cur + " → " + orig); }
                else { SetRowStatus(cur, "还原失败：" + res.Message, Color.Firebrick); Log("还原失败：" + cur + " → " + res.Message); }
            });
            th.IsBackground = true;
            th.Start();
        }

        // ======================================================= 日志/收尾
        private void OnEngineLog(string msg)
        {
            Log(msg);
        }

        private void Log(string msg)
        {
            if (InvokeRequired) { BeginInvokeSafe(delegate { Log(msg); }); return; }
            tbLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\r\n");
        }

        private void BeginInvokeSafe(Action a)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired) BeginInvoke(a);
                else a();
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { _stopWrite = true; _engine.Dispose(); } catch { }
            base.OnFormClosing(e);
        }
    }
}
