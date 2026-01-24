namespace KanbanApi.Models;

public class Board{

    public int Id {get; set; }

    public string Name {get; set; } = string.Empty;

    public List<Column> Columns {get; set; } = new List<Column>();
    public List<BoardMember> Users {get; set; } = new List<BoardMember();


}