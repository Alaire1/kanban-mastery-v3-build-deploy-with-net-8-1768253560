namespace KanbanApi.Tests;

public static class TestConsole
{
    private static readonly object _lock = new();

    public static void Green(string text) => Write(text, ConsoleColor.Green);
    public static void Yellow(string text) => Write(text, ConsoleColor.Yellow);
    public static void Cyan(string text) => Write(text, ConsoleColor.Cyan);
    public static void Red(string text) => Write(text, ConsoleColor.Red);

    public static string Value(object? value, ConsoleColor color)
    {
        var code = color switch
        {
            ConsoleColor.Black => "30",
            ConsoleColor.DarkRed or ConsoleColor.Red => "31",
            ConsoleColor.DarkGreen or ConsoleColor.Green => "32",
            ConsoleColor.DarkYellow or ConsoleColor.Yellow => "33",
            ConsoleColor.DarkBlue or ConsoleColor.Blue => "34",
            ConsoleColor.DarkMagenta or ConsoleColor.Magenta => "35",
            ConsoleColor.DarkCyan or ConsoleColor.Cyan => "36",
            _ => "37"
        };

        return $"\u001b[{code}m{value}\u001b[0m";
    }

    public static void LabelValue(string label, object? value, ConsoleColor valueColor)
    {
        lock (_lock)
        {
            var old = Console.ForegroundColor;
            try
            {
                Console.Write(label);
                Console.ForegroundColor = valueColor;
                Console.WriteLine(value?.ToString());
            }
            finally
            {
                Console.ForegroundColor = old;
            }
        }
    }

    public static void Write(string text, ConsoleColor color)
    {
        lock (_lock)
        {
            var old = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            }
            finally
            {
                Console.ForegroundColor = old;
            }
        }
    }
}
