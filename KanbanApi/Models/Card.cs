namespace KanbanApi.Models;

public class Card
{
    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int ColumnId { get; private set; }
    public Column Column { get; private set; } = null!;

    // Default constructor
    private Card() { }

    // Parameterized constructor
    public Card(string title, Column column)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Card title cannot be empty!", nameof(title));

        if (column == null)
            throw new ArgumentNullException(nameof(column));

        Title = title.Trim();
        Column = column;
        ColumnId = column.Id;
    }
}
