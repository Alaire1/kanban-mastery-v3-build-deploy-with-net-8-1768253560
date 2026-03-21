using System.Text.RegularExpressions;

namespace KanbanApi.Validation;

internal static class NameValidator
{ 
    // to check if name contains only letters, numbers and single spaces between words
    private static readonly Regex NamePattern = new(
        "^[A-Za-z0-9]+(?: [A-Za-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryValidateAndNormalize(
    string? value,
    out string normalized,
    out string error,
    int maxLength = 100)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Column name cannot be empty.";
            return false;
        }

        normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            error = $"Column name cannot exceed {maxLength} characters.";
            return false;
        }

        if (!NamePattern.IsMatch(normalized))
        {
            error = "Column name can contain only letters, numbers, and single spaces between words.";
            return false;
        }
        return true;
    }
}