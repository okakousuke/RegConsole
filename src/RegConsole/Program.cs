namespace RegConsole;

internal static class Program
{
    /// <summary>アプリのエントリポイント。</summary>
    [STAThread]
    static void Main()
    {
        // .NET 8 の WinForms 定型初期化（高DPI等の既定設定）
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
