namespace FSH.Framework.Core.Abstractions;
public interface IAppUser
{
    string? FirstName { get; }
    string? LastName { get; }
    Uri? ImageUrl { get; }
    bool IsActive { get; }
    string? RefreshToken { get; }
    DateTime RefreshTokenExpiryTime { get; }
}