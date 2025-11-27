namespace FSH.Framework.Persistence;

public interface IConnectionStringValidator
{
    bool TryValidate(string connectionString, string? dbProvider = null);
}