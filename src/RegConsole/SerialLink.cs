using System.IO.Ports;
using System.Text;

namespace RegConsole;

/// <summary>
/// SerialPort の薄いラッパ。UI から直接 SerialPort を触らず、
/// 「開く・閉じる・1行送る・受信したら通知する」だけに責務を絞る。
/// 受信は別スレッドで発生するので、UI 側でスレッド跨ぎ（Invoke）する前提。
/// </summary>
public sealed class SerialLink : IDisposable
{
    private SerialPort? _port;

    /// <summary>1行受信したら発火（引数は受信文字列）。別スレッドから呼ばれる点に注意。</summary>
    public event Action<string>? LineReceived;

    public bool IsOpen => _port is { IsOpen: true };

    /// <summary>接続中の PC で見えている COM ポート名の一覧。</summary>
    public static string[] GetPortNames() => SerialPort.GetPortNames();

    public void Open(string portName, int baudRate)
    {
        Close(); // 二重オープン防止

        var port = new SerialPort(portName, baudRate)
        {
            Encoding = Encoding.UTF8,
            NewLine = "\r\n",        // 機器に合わせて変更する箇所（CR / LF / CRLF）
            ReadTimeout = 500,
            WriteTimeout = 500,
            DtrEnable = true,
            RtsEnable = true,
        };
        port.DataReceived += OnDataReceived;
        port.Open();
        _port = port;
    }

    public void Close()
    {
        if (_port is null) return;
        try
        {
            _port.DataReceived -= OnDataReceived;
            if (_port.IsOpen) _port.Close();
        }
        catch { /* 取り外し等での例外は握りつぶす */ }
        finally
        {
            _port.Dispose();
            _port = null;
        }
    }

    /// <summary>NewLine 付きで1行送信する。</summary>
    public void SendLine(string text)
    {
        if (_port is not { IsOpen: true })
            throw new InvalidOperationException("ポートが開いていません。");
        _port.WriteLine(text);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is not { IsOpen: true }) return;
        try
        {
            // 受信バッファにある分をまとめて読む（行区切りに拘らず素直に流す）
            string chunk = _port.ReadExisting();
            if (!string.IsNullOrEmpty(chunk))
                LineReceived?.Invoke(chunk);
        }
        catch (Exception ex)
        {
            LineReceived?.Invoke($"[受信エラー] {ex.Message}");
        }
    }

    public void Dispose() => Close();
}
