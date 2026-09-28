using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ELearning.Api.Common;

/// <summary>
/// Bind enum từ query/route dạng UPPER_SNAKE_CASE ("FILL_IN" → QuestionType.FillIn), khớp với JSON (docs/05-api.md mục 1).
/// Model binding mặc định chỉ hiểu "FillIn".
/// </summary>
public sealed class SnakeCaseEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type.IsEnum && context.BindingInfo.BindingSource != BindingSource.Body
            ? new SnakeCaseEnumModelBinder(type)
            : null;
    }
}

internal sealed class SnakeCaseEnumModelBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.CompletedTask;
        }

        // Không nhận số ("1") để API chỉ có một dạng biểu diễn enum.
        var candidate = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (!long.TryParse(value, out _)
            && Enum.TryParse(enumType, candidate, ignoreCase: true, out var parsed)
            && Enum.IsDefined(enumType, parsed!))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Giá trị '{value}' không hợp lệ.");
        }

        return Task.CompletedTask;
    }
}
