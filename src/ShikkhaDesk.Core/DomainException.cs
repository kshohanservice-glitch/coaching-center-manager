namespace ShikkhaDesk.Core;

/// <summary>A business-rule violation that is safe to show to the user (message is already localised).</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Attempting an action the signed-in user is not permitted to perform.</summary>
public sealed class AuthorizationException : Exception
{
    public AuthorizationException(string message) : base(message) { }
}

/// <summary>Input that failed validation.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(IEnumerable<string> errors)
        : base(string.Join(Environment.NewLine, errors)) =>
        Errors = errors.ToList();

    public IReadOnlyList<string> Errors { get; } = Array.Empty<string>();
}

/// <summary>The requested record does not exist.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
