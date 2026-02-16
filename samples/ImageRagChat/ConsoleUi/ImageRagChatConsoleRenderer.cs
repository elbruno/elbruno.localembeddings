using Spectre.Console;

namespace ImageRagChat.ConsoleUi;

/// <summary>
/// Console renderer for the ImageRagChat interactive search application.
/// </summary>
internal static class ImageRagChatConsoleRenderer
{
    public static void PrintBanner()
    {
        AnsiConsole.Write(new FigletText("Image RAG Chat")
            .LeftJustified()
            .Color(Color.Aqua));
        AnsiConsole.MarkupLine("[grey]CLIP-based interactive text-to-image semantic search[/]");
        AnsiConsole.WriteLine();
    }

    public static void PrintStepHeader(string text)
    {
        AnsiConsole.MarkupLine($"[bold yellow]▸ {Markup.Escape(text)}[/]");
    }

    public static void PrintInfo(string text)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(text)}[/]");
    }

    public static void PrintSuccess(string text)
    {
        AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(text)}[/]");
    }

    public static void PrintError(string text)
    {
        AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(text)}[/]");
    }

    public static void PrintInstructions()
    {
        var panel = new Panel(
            "[bold]Commands:[/]\n" +
            "  • Type a [green]natural language query[/] to search images\n" +
            "  • [yellow]image:[/][cyan]<path>[/] — Search for images similar to another image\n" +
            "  • [yellow]help[/] — Show this help\n" +
            "  • [yellow]exit[/] / [yellow]quit[/] — Exit the application")
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public static string? ReadUserInput()
    {
        AnsiConsole.Markup("[bold aqua]🔍 > [/]");
        return Console.ReadLine();
    }

    public static void PrintResults(List<(string ImagePath, float Score)> results, string query, TimeSpan searchTime)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]Results for [green]\"{Markup.Escape(query)}\"[/][/]  [grey]({searchTime.TotalMilliseconds:F0}ms)[/]")
            .AddColumn(new TableColumn("[bold]#[/]").Centered())
            .AddColumn(new TableColumn("[bold]Image[/]"))
            .AddColumn(new TableColumn("[bold]Score[/]").Centered());

        for (int i = 0; i < results.Count; i++)
        {
            var (imagePath, score) = results[i];
            string scoreColor = score > 0.3f ? "green" : score > 0.2f ? "yellow" : "grey";
            table.AddRow(
                $"{i + 1}",
                Markup.Escape(Path.GetFileName(imagePath)),
                $"[{scoreColor}]{score:F4}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public static void PrintNoResults()
    {
        AnsiConsole.MarkupLine("[yellow]No matching images found.[/]");
        AnsiConsole.WriteLine();
    }

    public static void PrintGoodbye()
    {
        AnsiConsole.MarkupLine("[bold aqua]Thank you for using Image RAG Chat! 👋[/]");
    }

    public static void PrintIndexingProgress(int current, int total, string fileName)
    {
        AnsiConsole.MarkupLine($"  [grey][{current}/{total}][/] Indexed: [cyan]{Markup.Escape(fileName)}[/]");
    }
}
