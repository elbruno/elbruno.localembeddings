using Microsoft.Extensions.VectorData;
using RagChat.Models;
using Spectre.Console;

namespace RagChat.ConsoleUi;

internal static class RagChatConsoleRenderer
{
    public static void PrintBanner()
    {
        AnsiConsole.Write(new Rule("[deepskyblue1]RAG Chat - Semantic Q&A Demo[/]").RuleStyle("grey").Centered());
        AnsiConsole.MarkupLine("[grey]Powered by LocalEmbeddings & Microsoft.Extensions.AI[/]");
        AnsiConsole.WriteLine();
    }

    public static void PrintStepHeader(string title)
    {
        AnsiConsole.Write(new Rule($"[yellow]{Markup.Escape(title)}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }

    public static void PrintInfo(string message) => AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(message)}[/]");

    public static void PrintSuccess(string message) => AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(message)}");

    public static void PrintWarning(string message) => AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(message)}[/]");

    public static void PrintStartupInstructions()
    {
        AnsiConsole.MarkupLine("  Ask questions about LocalAI Assistant (the fictional product in our FAQ).");
        AnsiConsole.MarkupLine("  Type [aqua]quit[/] or [aqua]exit[/] to end the session.");
        AnsiConsole.MarkupLine("  Type [aqua]help[/] to see example questions.");
        AnsiConsole.WriteLine();
    }

    public static void PrintChatStarted()
    {
        AnsiConsole.Write(new Panel("[white]Chat Session Started[/]").Border(BoxBorder.Rounded).BorderStyle(Style.Parse("deepskyblue1")).Expand());
        AnsiConsole.WriteLine();
    }

    public static void PrintHelp()
    {
        var table = new Table().NoBorder().HideHeaders();
        table.AddColumn("Command");
        table.AddColumn("Description");

        table.AddRow("• What are the system requirements?", string.Empty);
        table.AddRow("• How do I install the application?", string.Empty);
        table.AddRow("• What features does the code assistant have?", string.Empty);
        table.AddRow("• Is my data private and secure?", string.Empty);
        table.AddRow("• Why is the application running slowly?", string.Empty);
        table.AddRow("• What's the pricing for professional users?", string.Empty);
        table.AddRow("• How can I integrate with Visual Studio Code?", string.Empty);
        table.AddRow("• What should I do if the model won't load?", string.Empty);
        table.AddEmptyRow();
        table.AddRow("Commands", "list = show all documents, quit/exit = end session");

        AnsiConsole.Write(new Panel(table)
            .Header("[yellow]Example Questions[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("yellow"))
            .Expand());
        AnsiConsole.WriteLine();
    }

    public static void PrintDocumentList(IReadOnlyList<Document> documents)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [aqua]Knowledge Base Documents:[/]");
        AnsiConsole.WriteLine();

        var grouped = documents.GroupBy(d => d.Category);
        foreach (var group in grouped)
        {
            AnsiConsole.MarkupLine($"  [yellow][{Markup.Escape(group.Key ?? string.Empty)}][/]");
            foreach (var doc in group)
            {
                AnsiConsole.MarkupLine($"    • {Markup.Escape(doc.Title ?? string.Empty)}");
            }

            AnsiConsole.WriteLine();
        }
    }

    public static string? ReadUserInput()
    {
        AnsiConsole.Markup("[cyan]You:[/] ");
        return Console.ReadLine()?.Trim();
    }

    public static void PrintNoResults() => AnsiConsole.MarkupLine("  [yellow]No relevant documents found. Try rephrasing your question.[/]");

    public static void PrintResults(IReadOnlyList<VectorSearchResult<Document>> results, TimeSpan searchTime)
    {
        AnsiConsole.MarkupLine($"  [green]Found {results.Count} relevant document(s) in {searchTime.TotalMilliseconds:F0}ms:[/]");
        AnsiConsole.WriteLine();

        foreach (var result in results)
        {
            var score = (float)(result.Score ?? 0d);
            var similarityPercent = score * 100;
            var barLength = (int)(score * 20);
            var bar = new string('█', barLength) + new string('░', 20 - barLength);
            var barColor = score >= 0.5f ? "green" : score >= 0.35f ? "yellow" : "darkgoldenrod";

            AnsiConsole.MarkupLine($"  [{barColor}]{Markup.Escape(bar)}[/]{new string(' ', 1)}{similarityPercent:F1}% match");
            AnsiConsole.MarkupLine($"  📄 [white]{Markup.Escape(result.Record.Title ?? string.Empty)}[/]");
            AnsiConsole.MarkupLine($"     [grey]Category: {Markup.Escape(result.Record.Category ?? string.Empty)}[/]");

            foreach (var line in TextWrapper.Wrap(result.Record.Content, 70))
            {
                AnsiConsole.MarkupLine($"     {Markup.Escape(line)}");
            }

            AnsiConsole.WriteLine();
        }
    }

    public static void PrintDivider()
    {
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }

    public static void PrintGoodbye() => AnsiConsole.MarkupLine("  Goodbye! Thanks for trying RAG Chat.");

    public static void PrintSessionComplete()
    {
        var summary = new Markup("[white]RAG Chat demonstrates:[/]\n" +
                                 "• In-memory vector storage with embeddings\n" +
                                 "• Semantic similarity search using cosine similarity\n" +
                                 "• Clean DI integration with AddLocalEmbeddings()\n" +
                                 "• Interactive chat-style Q&A interface");

        AnsiConsole.Write(new Panel(summary)
            .Header("[deepskyblue1]Session Complete[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("deepskyblue1"))
            .Expand());
    }
}
