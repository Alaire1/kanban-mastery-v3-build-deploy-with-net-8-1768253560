using KanbanApi.Dtos;

namespace KanbanApi.Services;

public abstract record ColumnResult
{
    public record Created(ColumnApiResponseDto Dto) : ColumnResult;
    public record Updated(ColumnApiResponseDto Dto) : ColumnResult;
    public record Deleted : ColumnResult;
    public record BoardNotFound : ColumnResult;
    public record ColumnNotFound : ColumnResult;
    public record Forbidden : ColumnResult;
    public record PositionTaken : ColumnResult;
    public record HasCards : ColumnResult;
}