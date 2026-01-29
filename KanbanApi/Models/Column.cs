namespace KanbanApi.Models;

public class Column
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Position { get; private set; }

    public int BoardId { get; private set; }
    public Board Board { get; private set; } = null!;

    public ICollection<Card> Cards { get; private set; } = new List<Card>();

    // Default constructor
    private Column() { }

    // Parameterized constructor
    public Column(string name, int position, Board board)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be empty!", nameof(name));

        if (board == null)
            throw new ArgumentNullException(nameof(board), "Column must belong to a board.");

        if (position < 0 || position > 3)
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 3.");

        Name = name.Trim();
        Position = position;
        Board = board;
    }

    public Card AddCard(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        throw new ArgumentException("Card title cannot be empty!", nameof(title));

        title = title.Trim();

        var card = new Card(title, this);
        Cards.Add(card);
        return card;
    }
}
