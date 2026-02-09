namespace KanbanApi.Models;

public class Board{

    public int Id {get; private set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Column> Columns { get; private set; } = new List<Column>();
    public ICollection<BoardMember> Members { get; private set; } = new List<BoardMember>();

    //Default constructor
    private Board(){}

    // Parameterized constructor
    public Board(string name){
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Board name cannot be empty!", nameof(name));

        //trimming the name in case of white spaces
        Name = name.Trim();

        Columns = new List<Column>
        {
            new Column("Backlog", 0, this),
            new Column("To Do", 1, this),
            new Column("In Progress", 2, this),
            new Column("Done", 3, this)
        };
    }

    public void AddUser(BoardMember user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));
        
        if (Members.Any(u => u.UserId == user.UserId)) 
            throw new InvalidOperationException("User already exists on this board.");
        Members.Add(user);
    }
}


