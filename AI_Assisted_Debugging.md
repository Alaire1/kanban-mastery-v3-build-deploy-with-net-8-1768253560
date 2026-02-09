Broken code:
```
// public class BoardService : IBoardService
//async key missing 
public  Task<Board> CreateBoardAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Board name cannot be null or empty.", nameof(name));
            }

            var board = new Board(name);
            _context.Boards.Add(board);
            await _context.SaveChangesAsync();
            return board;
        }
```
Error message:
/kanban-mastery-v3-build-deploy-with-net-8-1768253560/KanbanApi/Services/BoardService.cs(28,13): error CS4032: The 'await' operator can only be used within an async method. Consider marking this method with the 'async' modifier and changing its return type to 'Task<Task<Board>>'. [/home/anita/Desktop/kanban-board/kanban-mastery-v3-build-deploy-with-net-8-1768253560/KanbanApi/KanbanApi.csproj]
kanban-mastery-v3-build-deploy-with-net-8-1768253560/KanbanApi/Services/BoardService.cs(29,20): error CS0029: Cannot implicitly convert type 'KanbanApi.Models.Board' to 'System.Threading.Tasks.Task<KanbanApi.Models.Board>' [/home/anita/Desktop/kanban-board/kanban-mastery-v3-build-deploy-with-net-8-1768253560/KanbanApi/KanbanApi.csproj]

Message to AI:
My code in BoardService.cs file is throwing error CS4032. The await operator can only be used within async method.
There's second error : CS0029: Cannot implicitly convert type KanbanApi.Model.Board to System.Tasks.Task<KanbanApi.Models.Board> What is exactly the problem and how can it be fixed?

Fix:
```
    public async Task<Board> CreateBoardAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Board name cannot be null or empty.", nameof(name));
            }

            var board = new Board(name);
            _context.Boards.Add(board);
            await _context.SaveChangesAsync();
            return board;
        }
    ```