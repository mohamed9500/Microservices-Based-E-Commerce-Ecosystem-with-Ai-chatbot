namespace Nexus.BuildingBlocks.Web;

public class AppException(int status, string title, string? detail = null) : Exception(detail ?? title)
{
    public int Status { get; } = status;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
}

public sealed class NotFoundException(string detail = "The requested resource was not found.") : AppException(404, "Not Found", detail);
public sealed class ConflictException(string detail) : AppException(409, "Conflict", detail);
public sealed class ForbiddenException(string detail = "You do not have permission to perform this action.") : AppException(403, "Forbidden", detail);
public sealed class UnauthorizedException(string detail = "Authentication is required.") : AppException(401, "Unauthorized", detail);
public sealed class BadRequestException(string detail) : AppException(400, "Bad Request", detail);

public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : AppException(400, "Validation failed", "One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}
