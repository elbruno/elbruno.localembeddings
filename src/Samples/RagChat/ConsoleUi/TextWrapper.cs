namespace RagChat.ConsoleUi;

internal static class TextWrapper
{
    public static IReadOnlyList<string> Wrap(string text, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [string.Empty];
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 <= maxWidth)
            {
                currentLine += (currentLine.Length > 0 ? " " : string.Empty) + word;
            }
            else
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                }

                currentLine = word;
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        return lines;
    }
}
