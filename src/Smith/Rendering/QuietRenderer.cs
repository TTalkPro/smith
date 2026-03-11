namespace Smith.Rendering;

/// <summary>
/// 静默模式输出（供脚本使用），只输出关键信息到 stdout
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
}
