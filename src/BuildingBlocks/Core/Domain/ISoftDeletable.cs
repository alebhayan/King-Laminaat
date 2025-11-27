namespace FSH.Framework.Core.Domain;
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedOnUtc { get; }
    string? DeletedBy { get; }
}
