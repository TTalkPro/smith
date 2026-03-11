namespace Smith.Rendering;

/// <summary>
/// 控制台输出抽象接口
/// </summary>
public interface IConsoleRenderer
{
    void Success(string message);
    void Error(string message);
    void Warning(string message);
    void Info(string message);
    void Title(string title);
    void KeyValue(string key, string value, int keyWidth = 20);
    void Table(string[] headers, List<string[]> rows);
    void NewLine();
}
