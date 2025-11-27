namespace FSH.Framework.Core.Domain;
public interface IAuditableEntity
{
    DateTimeOffset CreatedOnUtc { get; }
    string? CreatedBy { get; }
    DateTimeOffset? LastModifiedOnUtc { get; }
    string? LastModifiedBy { get; }
}