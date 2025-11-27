using FSH.Modules.Auditing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FSH.Modules.Auditing.Persistence;

/// <summary>
/// Builds property-level diffs for EF Core entries. Skips navigations by default.
/// </summary>
internal static class EntityDiffBuilder
{
    internal sealed record Diff(
        string DbContext,
        string? Schema,
        string Table,
        string EntityName,
        string Key,
        EntityOperation Operation,
        IReadOnlyList<PropertyChange> Changes);

    public static List<Diff> Build(IEnumerable<EntityEntry> entries)
    {
        var list = new List<Diff>();

        foreach (var e in entries)
        {
            var entityType = e.Metadata;
            var table = entityType.GetTableName() ?? entityType.GetDefaultTableName() ?? entityType.DisplayName();
            var schema = entityType.GetSchema();
            var key = BuildKey(e);
            var op = e.State switch
            {
                EntityState.Added => EntityOperation.Insert,
                EntityState.Modified => DetectSoftDelete(e) ? EntityOperation.SoftDelete : EntityOperation.Update,
                EntityState.Deleted => EntityOperation.Delete,
                _ => EntityOperation.None
            };

            var changes = new List<PropertyChange>();
            foreach (var p in e.Properties)
            {
                if (p.Metadata.IsShadowProperty() && !p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsConcurrencyToken) continue;
                if (p.Metadata.IsIndexerProperty()) continue;
                if (p.Metadata.IsKey()) continue; // keys are in "key" string already
                if (p.Metadata.IsNullable == false && p.Metadata.ClrType.IsClass && p.Metadata.IsForeignKey()) continue; // nav FKs often noisy

                // Include only scalar types
                if (!IsScalar(p.Metadata.ClrType)) continue;

                var name = p.Metadata.Name;
                var typeName = ToSimpleTypeName(p.Metadata.ClrType);

                object? oldVal = null;
                object? newVal = null;
                var isModified = false;

                switch (e.State)
                {
                    case EntityState.Added:
                        newVal = p.CurrentValue;
                        isModified = true;
                        break;

                    case EntityState.Modified:
                        oldVal = p.OriginalValue;
                        newVal = p.CurrentValue;
                        isModified = p.IsModified && !Equals(oldVal, newVal);
                        break;

                    case EntityState.Deleted:
                        oldVal = p.OriginalValue;
                        isModified = true;
                        break;
                }

                if (isModified)
                {
                    changes.Add(new PropertyChange(
                        Name: name,
                        DataType: typeName,
                        OldValue: oldVal,
                        NewValue: newVal,
                        IsSensitive: IsSensitive(name)));
                }
            }

            if (changes.Count > 0)
            {
                list.Add(new Diff(
                    DbContext: e.Context.GetType().Name,
                    Schema: schema,
                    Table: table!,
                    EntityName: entityType.ClrType.Name,
                    Key: key,
                    Operation: op,
                    Changes: changes));
            }
        }

        return list;
    }

    private static string BuildKey(EntityEntry entry)
    {
        var keyProps = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToArray();
        if (keyProps.Length == 0) return $"<no-key>";
        return string.Join("|", keyProps.Select(k => $"{k.Metadata.Name}:{k.CurrentValue ?? k.OriginalValue}"));
    }

    private static bool DetectSoftDelete(EntityEntry entry)
    {
        // Convention: boolean property named "IsDeleted" flipped to true
        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase)
                                                        && p.Metadata.ClrType == typeof(bool));
        if (prop is null) return false;
        var orig = prop.OriginalValue as bool? ?? false;
        var curr = prop.CurrentValue as bool? ?? false;
        return orig == false && curr == true;
    }

    private static bool IsSensitive(string propertyName)
    {
        // Simple heuristic. Replace with attribute-based masking later.
        var n = propertyName.ToLowerInvariant();
        return n.Contains("password") || n.Contains("secret") || n.Contains("token");
    }

    private static bool IsScalar(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) ||
               t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid) ||
               t == typeof(TimeSpan) || t == typeof(byte[]) || t == typeof(bool);
    }

    private static string ToSimpleTypeName(Type t)
        => (Nullable.GetUnderlyingType(t) ?? t).Name;
}
