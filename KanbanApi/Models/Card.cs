namespace KanbanApi.Models;

public class Card{

    public int Id {get; set; }

    public string Title {get; set; } = string.Empty;
    
    // Column Properties
    public int ColumnId {get; set; }
    public Column Column {get; set; } = null!
}