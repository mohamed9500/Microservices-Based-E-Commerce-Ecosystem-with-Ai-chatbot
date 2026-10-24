using System.ComponentModel.DataAnnotations;

namespace Nexus.BuildingBlocks.Web;

public static class Validation
{
    /// <summary>Validates DataAnnotations on a DTO and throws <see cref="ValidationFailedException"/> (HTTP 400).</summary>
    public static T Validated<T>(this T? dto) where T : class
    {
        if (dto is null) throw new ValidationFailedException("body", "A request body is required.");
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true))
        {
            var errors = results
                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
                .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Invalid value.").ToArray());
            throw new ValidationFailedException(errors);
        }
        return dto;
    }

    public static bool IsSafeImageUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.Length <= 500 &&
        ((url.StartsWith('/') && !url.StartsWith("//")) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
