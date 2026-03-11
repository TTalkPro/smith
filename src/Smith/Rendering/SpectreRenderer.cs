using Spectre.Console;

namespace Smith.Rendering;

/// <summary>
/// Spectre.Console renderer implementation
/// </summary>
public class SpectreRenderer : IConsoleRenderer
{
    public void Success(string message) =>
        AnsiConsole.MarkupLine($"[green]✓[/] {message}");

    public void Error(string message) =>
        AnsiConsole.MarkupLine($"[red]✗ 错误:[/] {message}");

    public void Warning(string message) =>
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {message}");

    public void Info(string message) =>
        AnsiConsole.MarkupLine($"[cyan]ℹ[/] {message}");

    public void Title(string title)
    {
        AnsiConsole.WriteLine();
        var separator = new string('═', title.Length + 2);
        AnsiConsole.MarkupLine($"[magenta bold]═══ {title} {separator}[/]");
    }

    public void KeyValue(string key, string value, int keyWidth = 20)
    {
        AnsiConsole.MarkupLine($"  [bold]{key.PadLeft(keyWidth)}[/]  {value}");
    }

    public void Table(string[] headers, List<string[]> rows)
    {
        var table = new Table();

        foreach (var header in headers)
        {
            table.AddColumn(new TableColumn(header).Alignment(Justify.Left));
        }

        foreach (var row in rows)
        {
            table.AddRow(row);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public void NewLine() => AnsiConsole.WriteLine();
}
