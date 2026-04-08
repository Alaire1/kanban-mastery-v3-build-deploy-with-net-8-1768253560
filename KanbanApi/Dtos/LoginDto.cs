namespace KanbanApi.Dtos
{
    public class LoginDto
    {
        public string? Identifier { get; set; }
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
