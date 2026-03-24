using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Filters;

/// <summary>
/// Runs DataAnnotation validation on any DTO argument before the handler executes.
/// Usage: .WithValidation&lt;YourDto&gt;()
/// Returns 400 ValidationProblem automatically — the handler never runs on bad input.
/// </summary>
public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<ValidationEndpointFilter<T>>();
}

public class ValidationEndpointFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        var argument = ctx.Arguments.OfType<T>().FirstOrDefault();

        if (argument is not null)
        {
            var results = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(
                argument,
                new ValidationContext(argument),
                results,
                validateAllProperties: true);

            if (!valid)
            {
                var errors = results.ToDictionary(
                    r => r.MemberNames.FirstOrDefault() ?? "error",
                    r => new[] { r.ErrorMessage ?? "Invalid value." });

                return TypedResults.ValidationProblem(errors);
            }
        }

        return await next(ctx);
    }
}