namespace FSH.Modules.Auditing.Contracts;

/// <summary>
/// Masks or hashes sensitive fields before persistence or externalization.
/// </summary>
public interface IAuditMaskingService
{
    object ApplyMasking(object payload);
}
