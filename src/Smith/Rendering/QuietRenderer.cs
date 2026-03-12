namespace Smith.Rendering;

/// <summary>
/// 静默模式输出（供脚本使用），只输出错误信息到 stderr
/// </summary>
public class QuietRenderer : IConsoleRenderer
{
    public void Success(string message) { }
    public void Error(string message) => Console.Error.WriteLine($"ERROR: {message}");
    public void Warning(string message) { }
    public void Info(string message) { }
    public void Title(string title) { }
    public void KeyValue(string key, string value, int keyWidth = 20) { }
    public void Table(string[] headers, List<string[]> rows) { }
    public void NewLine() { }

    /// <summary>静默模式下默认拒绝确认</summary>
    public bool Confirm(string message) => false;
}
