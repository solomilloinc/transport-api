using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections;
using System.Linq.Expressions;

namespace Transport.Tests;

/// <summary>
/// In-memory implementation of DbSet that works with async LINQ operations.
/// Unlike Mock&lt;DbSet&lt;T&gt;&gt;, this properly implements IQueryable so that
/// .Where(), .FirstOrDefaultAsync(), etc. work correctly.
/// </summary>
public class TestDbSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T> where T : class
{
    public override IEntityType EntityType => throw new NotSupportedException("EntityType is not available in test context");
    private readonly List<T> _data;
    private readonly IQueryable<T> _queryable;
    private readonly TestAsyncQueryProvider<T> _provider;
    private readonly bool _autoIdentity;
    private readonly Action<T>? _onAdd;

    public TestDbSet(List<T> data, bool autoIdentity = false, Action<T>? onAdd = null)
    {
        _data = data;
        _queryable = data.AsQueryable();
        _provider = new TestAsyncQueryProvider<T>(_queryable.Provider);
        _autoIdentity = autoIdentity;
        _onAdd = onAdd;
    }

    // IQueryable members - override DbSet's throwing explicit implementations
    IQueryProvider IQueryable.Provider => _provider;
    Expression IQueryable.Expression => _queryable.Expression;
    Type IQueryable.ElementType => _queryable.ElementType;
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _queryable.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _queryable.GetEnumerator();

    // IAsyncEnumerable
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(_data.GetEnumerator());
    }

    private void HandleAdd(T entity)
    {
        if (_autoIdentity)
        {
            var idPropertyName = typeof(T).Name + "Id";
            var idProp = typeof(T).GetProperty(idPropertyName);
            if (idProp != null && idProp.PropertyType == typeof(int))
            {
                int newId = _data.Count + 1;
                idProp.SetValue(entity, newId);
            }
        }

        _data.Add(entity);
        _onAdd?.Invoke(entity);
    }

    // DbSet overrides for adding entities
    public override EntityEntry<T> Add(T entity)
    {
        HandleAdd(entity);
        return null!;
    }

    public override ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        HandleAdd(entity);
        return ValueTask.FromResult<EntityEntry<T>>(null!);
    }

    public override void AddRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
            HandleAdd(entity);
    }

    public override EntityEntry<T> Update(T entity)
    {
        return null!;
    }

    public override void UpdateRange(IEnumerable<T> entities)
    {
        // No-op: entities are already in the in-memory list and modified by reference
    }
}
