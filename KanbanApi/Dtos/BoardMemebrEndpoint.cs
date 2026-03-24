using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Dtos;

public record AddMemberRequest(
    [Required] string UserId,
    string? Role
);