using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SaasPlatform.Api.Infrastructure;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, and short-circuits with a
/// 400 whose "title" is the first message (what the UI shows) and "errors" maps each field to its messages.
/// </summary>
public class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = char.ToLowerInvariant(failure.PropertyName[0]) + failure.PropertyName[1..];
                if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
                list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            var first = errors.Values.First()[0];
            context.Result = new BadRequestObjectResult(new { status = 400, title = first, errors });
            return;
        }

        await next();
    }
}
