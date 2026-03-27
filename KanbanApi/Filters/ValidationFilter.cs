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
                var errors = results
                    .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { "error" })
                        .Select(name => new { Name = name, Error = r.ErrorMessage ?? "Invalid value." }))
                    .GroupBy(x => x.Name)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.Error).ToArray()
                    );

                return TypedResults.ValidationProblem(errors);
            }
        }

        return await next(ctx);
    }
}