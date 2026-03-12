namespace Smith.Rendering;

/// <summary>
/// 彩色终端输出实现，使用 ANSI 转义序列渲染
/// </summary>
public class TerminalGuiRenderer : IConsoleRenderer
{
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

    /// <summary>
    /// 输出格式化表格，自动计算列宽对齐
    /// </summary>
    public void Table(string[] headers, List<string[]> rows)
    {
        var colWidths = CalculateColumnWidths(headers, rows);
        PrintHeaders(headers, colWidths);
        PrintSeparator(colWidths);
        PrintRows(rows, colWidths);
    }

    public void NewLine() => OutputLine("");

    /// <summary>
    /// 提示用户确认操作，等待输入 y/Y 表示同意
    /// </summary>
    public bool Confirm(string message)
    {
        Console.Write($"{message} (y/N): ");
        var input = Console.ReadLine();
        return input?.Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// 计算表格各列的显示宽度
    /// </summary>
    private static int[] CalculateColumnWidths(string[] headers, List<string[]> rows)
    {
        var widths = headers.Select(h => h.Length).ToArray();
        foreach (var row in rows)
        {
            for (int i = 0; i < row.Length && i < widths.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);
        }
        return widths;
    }

    private void PrintHeaders(string[] headers, int[] widths)
    {
        var line = "  " + string.Join("", headers.Select((h, i) =>
            $"{Bold}{White}{h.PadRight(widths[i] + 2)}{Reset}"));
        OutputLine(line);
    }

    private void PrintSeparator(int[] widths)
    {
        var line = "  " + string.Join("", widths.Select(w => new string('-', w) + "  "));
        OutputLine(line);
    }

    private void PrintRows(List<string[]> rows, int[] widths)
    {
        foreach (var row in rows)
        {
            var line = "  " + string.Join("", row.Select((cell, i) =>
                cell.PadRight(widths[i] + 2)));
            OutputLine(line);
        }
    }

    private static void OutputLine(string text)
    {
        Console.WriteLine(text);
    }
}
