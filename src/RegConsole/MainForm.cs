using System.Globalization;

namespace RegConsole;

/// <summary>
/// レジスタ叩き簡易コンソール（記事用サンプル）。
/// UI はデザイナを使わずコードで組み立てている（1ファイルで通して読めるように）。
/// 実務では業務仕様のツールを別に用意する前提の、あくまで「型」を示すサンプル。
/// </summary>
public sealed class MainForm : Form
{
    // ─── 機器に合わせて変える箇所（コマンドの書式） ─────────────
    // {0}=アドレス, {1}=値。ここを機器のプロトコルに合わせて書き換える。
    private const string ReadTemplate = "RD 0x{0:X2}";
    private const string WriteTemplate = "WR 0x{0:X2} 0x{1:X4}";

    private readonly SerialLink _link = new();

    // UI 部品
    private readonly ComboBox _cmbPort = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbBaud = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnConnect = new() { Text = "接続", Width = 80 };
    private readonly Label _lblStatus = new() { Text = "未接続", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(8, 6, 0, 0) };

    private readonly TextBox _txtAddr = new() { Width = 70, Text = "00" };
    private readonly TextBox _txtValue = new() { Width = 70, Text = "0000" };

    private readonly ComboBox _cmbCommands = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly FlowLayoutPanel _macroPanel = new() { AutoSize = true, WrapContents = true, Dock = DockStyle.Top };

    private readonly TextBox _txtLog = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9f),
        BackColor = Color.FromArgb(30, 30, 30),
        ForeColor = Color.Gainsboro,
    };

    public MainForm()
    {
        Text = "レジスタ叩き簡易コンソール（サンプル）";
        Width = 720;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        WireEvents();

        RefreshPorts();
        LoadCommands();
        UpdateConnState();
    }

    // ─── UI 構築 ─────────────────────────────────────────────
    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 接続
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // レジスタ
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // コマンド
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // マクロ
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // ログ

        // 接続グループ
        var connFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var btnRefresh = new Button { Text = "更新", Width = 60 };
        btnRefresh.Click += (_, _) => RefreshPorts();
        connFlow.Controls.Add(new Label { Text = "COM:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        connFlow.Controls.Add(_cmbPort);
        connFlow.Controls.Add(btnRefresh);
        connFlow.Controls.Add(new Label { Text = "ボーレート:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        connFlow.Controls.Add(_cmbBaud);
        connFlow.Controls.Add(_btnConnect);
        connFlow.Controls.Add(_lblStatus);
        root.Controls.Add(WrapGroup("接続", connFlow), 0, 0);

        // レジスタ操作グループ
        var regFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var btnRead = new Button { Text = "Read", Width = 80 };
        var btnWrite = new Button { Text = "Write", Width = 80 };
        btnRead.Click += (_, _) => DoRead();
        btnWrite.Click += (_, _) => DoWrite();
        regFlow.Controls.Add(new Label { Text = "アドレス(hex):", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        regFlow.Controls.Add(_txtAddr);
        regFlow.Controls.Add(btnRead);
        regFlow.Controls.Add(new Label { Text = "値(hex):", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });
        regFlow.Controls.Add(_txtValue);
        regFlow.Controls.Add(btnWrite);
        root.Controls.Add(WrapGroup("レジスタ操作", regFlow), 0, 1);

        // コマンドリスト（CSV）グループ
        var cmdFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var btnSend = new Button { Text = "送信", Width = 80 };
        var btnReload = new Button { Text = "再読込", Width = 70 };
        btnSend.Click += (_, _) => SendSelectedCommand();
        btnReload.Click += (_, _) => LoadCommands();
        cmdFlow.Controls.Add(_cmbCommands);
        cmdFlow.Controls.Add(btnSend);
        cmdFlow.Controls.Add(btnReload);
        root.Controls.Add(WrapGroup("コマンドリスト（commands.csv）", cmdFlow), 0, 2);

        // マクロボタングループ
        root.Controls.Add(WrapGroup("マクロボタン（ワンタッチ送信）", _macroPanel), 0, 3);

        // ロググループ
        var logHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        logHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        logHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logHost.Controls.Add(_txtLog, 0, 0);
        var logButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var btnSave = new Button { Text = "ログ保存", Width = 90 };
        var btnClear = new Button { Text = "クリア", Width = 70 };
        btnSave.Click += (_, _) => SaveLog();
        btnClear.Click += (_, _) => _txtLog.Clear();
        logButtons.Controls.Add(btnSave);
        logButtons.Controls.Add(btnClear);
        logHost.Controls.Add(logButtons, 0, 1);
        root.Controls.Add(WrapGroup("ログ", logHost, fill: true), 0, 4);

        Controls.Add(root);

        // ボーレート候補
        foreach (var b in new[] { "9600", "19200", "38400", "57600", "115200" })
            _cmbBaud.Items.Add(b);
        _cmbBaud.SelectedItem = "115200";
    }

    // fill=false: 中身に合わせて高さが決まる枠（上部の各グループ）
    // fill=true : セルいっぱいに広がる枠（ログのように伸ばしたいもの）
    private static GroupBox WrapGroup(string title, Control content, bool fill = false) => new()
    {
        Text = title,
        AutoSize = !fill,
        Dock = DockStyle.Fill,
        Padding = new Padding(8),
        Controls = { content },
    };

    private void WireEvents()
    {
        _btnConnect.Click += (_, _) => ToggleConnect();
        // 受信は別スレッドで発火するので UI へマーシャリングする
        _link.LineReceived += OnLineReceived;
        FormClosed += (_, _) => _link.Dispose();
    }

    // ─── 接続まわり ──────────────────────────────────────────
    private void RefreshPorts()
    {
        var selected = _cmbPort.SelectedItem as string;
        _cmbPort.Items.Clear();
        foreach (var p in SerialLink.GetPortNames())
            _cmbPort.Items.Add(p);
        if (selected is not null && _cmbPort.Items.Contains(selected))
            _cmbPort.SelectedItem = selected;
        else if (_cmbPort.Items.Count > 0)
            _cmbPort.SelectedIndex = 0;
    }

    private void ToggleConnect()
    {
        if (_link.IsOpen)
        {
            _link.Close();
            AppendLog("[切断しました]");
        }
        else
        {
            if (_cmbPort.SelectedItem is not string port)
            {
                MessageBox.Show("COMポートを選んでください。");
                return;
            }
            int baud = int.Parse((string)_cmbBaud.SelectedItem!, CultureInfo.InvariantCulture);
            try
            {
                _link.Open(port, baud);
                AppendLog($"[接続しました] {port} @ {baud}bps");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"接続に失敗しました：{ex.Message}");
            }
        }
        UpdateConnState();
    }

    private void UpdateConnState()
    {
        bool open = _link.IsOpen;
        _btnConnect.Text = open ? "切断" : "接続";
        _lblStatus.Text = open ? "接続中" : "未接続";
        _lblStatus.ForeColor = open ? Color.ForestGreen : Color.Gray;
        _cmbPort.Enabled = !open;
        _cmbBaud.Enabled = !open;
    }

    // ─── レジスタ操作 ────────────────────────────────────────
    private void DoRead()
    {
        if (!TryHex(_txtAddr.Text, out int addr)) { MessageBox.Show("アドレスが16進として不正です。"); return; }
        Send(string.Format(CultureInfo.InvariantCulture, ReadTemplate, addr));
    }

    private void DoWrite()
    {
        if (!TryHex(_txtAddr.Text, out int addr)) { MessageBox.Show("アドレスが16進として不正です。"); return; }
        if (!TryHex(_txtValue.Text, out int val)) { MessageBox.Show("値が16進として不正です。"); return; }
        Send(string.Format(CultureInfo.InvariantCulture, WriteTemplate, addr, val));
    }

    private static bool TryHex(string s, out int value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    // ─── コマンドリスト（CSV） ───────────────────────────────
    private void LoadCommands()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "commands.csv");
        var defs = CommandCsv.Load(path);

        _cmbCommands.Items.Clear();
        _macroPanel.Controls.Clear();

        foreach (var def in defs)
        {
            _cmbCommands.Items.Add(def); // CommandDef.ToString() が Name を返す

            // マクロ的なワンタッチボタンを自動生成
            var btn = new Button
            {
                Text = def.Name,
                AutoSize = true,
                Margin = new Padding(3),
                Tag = def,
            };
            btn.Click += (s, _) =>
            {
                if (((Button)s!).Tag is CommandDef d) Send(d.Command);
            };
            var tip = new ToolTip();
            tip.SetToolTip(btn, $"{def.Command}\n{def.Description}");
            _macroPanel.Controls.Add(btn);
        }

        if (_cmbCommands.Items.Count > 0) _cmbCommands.SelectedIndex = 0;
        AppendLog($"[commands.csv を読み込みました：{defs.Count} 件]");
    }

    private void SendSelectedCommand()
    {
        if (_cmbCommands.SelectedItem is CommandDef def)
            Send(def.Command);
    }

    // ─── 送受信とログ ────────────────────────────────────────
    private void Send(string text)
    {
        if (!_link.IsOpen)
        {
            MessageBox.Show("先に接続してください。");
            return;
        }
        try
        {
            _link.SendLine(text);
            AppendLog(">> " + text);
        }
        catch (Exception ex)
        {
            AppendLog($"[送信エラー] {ex.Message}");
        }
    }

    private void OnLineReceived(string text)
    {
        // DataReceived はスレッドプール上で発火するので UI スレッドへ渡す
        if (InvokeRequired) { BeginInvoke((Action)(() => OnLineReceived(text))); return; }
        AppendLog("<< " + text.TrimEnd('\r', '\n'));
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired) { BeginInvoke((Action)(() => AppendLog(line))); return; }
        _txtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}");
    }

    private void SaveLog()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "テキスト (*.txt)|*.txt|すべて (*.*)|*.*",
            FileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            File.WriteAllText(dlg.FileName, _txtLog.Text, System.Text.Encoding.UTF8);
    }
}
