namespace MarkUp.Core;

/// <summary>
/// Statistics about a markdown document.
/// </summary>
public sealed class DocumentStatistics
{
    public int Characters { get; init; }
    public int CharactersNoSpaces { get; init; }
    public int Words { get; init; }
    public int Lines { get; init; }
    public int Paragraphs { get; init; }

    /// <summary>
    /// Computes statistics for the given text.
    /// </summary>
    public static DocumentStatistics Compute(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new DocumentStatistics
            {
                Characters = 0,
                CharactersNoSpaces = 0,
                Words = 0,
                Lines = 0,
                Paragraphs = 0
            };
        }

        var characters = text.Length;
        var charactersNoSpaces = 0;
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
                charactersNoSpaces++;
        }

        var words = CountWords(text);
        var lines = CountLines(text);
        var paragraphs = CountParagraphs(text);

        return new DocumentStatistics
        {
            Characters = characters,
            CharactersNoSpaces = charactersNoSpaces,
            Words = words,
            Lines = lines,
            Paragraphs = paragraphs
        };
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        int count = 0;
        bool inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    count++;
                    inWord = false;
                }
            }
            else
            {
                inWord = true;
            }
        }
        if (inWord)
            count++;
        return count;
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                count++;
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
            }
            else if (text[i] == '\n')
            {
                count++;
            }
        }
        return count;
    }

    private static int CountParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int count = 0;
        bool inParagraph = false;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (inParagraph)
                {
                    count++;
                    inParagraph = false;
                }
            }
            else
            {
                inParagraph = true;
            }
        }
        if (inParagraph)
            count++;
        return count;
    }

    public override string ToString()
    {
        return $"Words: {Words:N0}  |  Characters: {Characters:N0}  |  Lines: {Lines:N0}";
    }
}
