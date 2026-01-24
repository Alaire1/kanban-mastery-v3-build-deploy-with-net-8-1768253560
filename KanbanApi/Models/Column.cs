namespace KanbanApi.Models;

public class Column{
        public int Id { get; set; }
        
        public string Name { get; set; } = string.Empty;

        // Board Properties
        public int BoardId {get; set;}
        public Board Board { get; set; } = null!;
        
        //Cards Properities
        public List<Card> Cards { get; set; } = new List<Card>();
    }