namespace Estacionamento.Domain.Common;

public enum DomainErrorKind
{
    Validation,
    NotFound,
    Conflict,
    RuleViolation,
    Forbidden
}

public sealed class DomainException(string message, DomainErrorKind kind = DomainErrorKind.RuleViolation)
    : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;

    public static DomainException Validation(string message) => new(message, DomainErrorKind.Validation);
    public static DomainException NotFound(string message) => new(message, DomainErrorKind.NotFound);
    public static DomainException Conflict(string message) => new(message, DomainErrorKind.Conflict);
    public static DomainException RuleViolation(string message) => new(message, DomainErrorKind.RuleViolation);
    public static DomainException Forbidden(string message) => new(message, DomainErrorKind.Forbidden);
}
