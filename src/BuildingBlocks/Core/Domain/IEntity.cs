namespace FSH.Framework.Core.Domain;
public interface IEntity<out TId>
{
    TId Id { get; }
}