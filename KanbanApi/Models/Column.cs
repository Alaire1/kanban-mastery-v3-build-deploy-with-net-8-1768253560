namespace KanbanApi.Models;

public class Column
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }

    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;

    public List<Card> Cards { get; set; } = new List<Card>();

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
        Cards = new List<Card>();
    }

    public void AddCard(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        card.Column = this;
        Cards.Add(card);
    }
}
