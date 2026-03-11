namespace Smith.Rendering;

/// <summary>
/// Terminal.Gui 彩色输出实现 - AOT 兼容
/// 提供基本的彩色输出，使用 ANSI 转义序列
/// </summary>
public class TerminalGuiRenderer : IConsoleRenderer
{
    // ANSI 颜色代码
    private const string Green = "\u001b[32m";
    private const string Red = "\u001b[31m";
    private const string Yellow = "\u001b[33m";
    private const string Cyan = "\u001b[36m";
    private const string Magenta = "\u001b[35m";
    private const string White = "\u001b[37m";
    private const string Bold = "\u001b[1m";
    private const string Reset = "\u001b[0m";

    public void Success(string message) =>
        OutputLine($"{Green}✓{Reset} {message}");

    public void Error(string message) =>
        OutputLine($"{Red}✗ 错误:{Reset} {message}");

    public void Warning(string message) =>
        OutputLine($"{Yellow}⚠{Reset} {message}");

    public void Info(string message) =>
        OutputLine($"{Cyan}ℹ{Reset} {message}");

    public void Title(string title)
    {
        OutputLine("");
        OutputLine($"{Magenta}{Bold}═══ {title} {Reset}");
    }

    public void KeyValue(string key, string value, int keyWidth = 20)
    {
        OutputLine($"  {key.PadLeft(keyWidth)}  {value}");
    }

    public void Table(string[] headers, List<string[]> rows)
    {
        // 计算每列宽度
        var colWidths = new int[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            colWidths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (int i = 0; i < row.Length && i < colWidths.Length; i++)
            {
                colWidths[i] = Math.Max(colWidths[i], row[i].Length);
            }
        }

        // 打印表头
        var headerLine = "  ";
        for (int i = 0; i < headers.Length; i++)
        {
            headerLine += $"{Bold}{White}{headers[i].PadRight(colWidths[i] + 2)}{Reset}";
        }
        OutputLine(headerLine);

        // 打印分隔线
        var separator = "  ";
        for (int i = 0; i < headers.Length; i++)
        {
            separator += new string('-', colWidths[i]) + "  ";
        }
        OutputLine(separator);

        // 打印数据行
        foreach (var row in rows)
        {
            var rowLine = "  ";
            for (int i = 0; i < row.Length; i++)
            {
                rowLine += row[i].PadRight(colWidths[i] + 2);
            }
            OutputLine(rowLine);
        }
    }

    public void NewLine() => OutputLine("");

    private static void OutputLine(string text)
    {
        Console.WriteLine(text);
    }
}
