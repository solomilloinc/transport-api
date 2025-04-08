using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using Transport.Tests;

public class TestAsyncQueryProvider<T> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().First();
        var queryableType = typeof(TestAsyncEnumerable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression)
        => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression)
        => _inner.Execute<TResult>(expression);

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var result = Execute(expression);
        return Task.FromResult((TResult)result!);
    }

    TResult IAsyncQueryProvider.ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var result = Execute(expression);
        var resultType = typeof(TResult);

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var taskResultType = resultType.GetGenericArguments()[0]; // Ej: Driver
            var method = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(taskResultType);
            return (TResult)method.Invoke(null, new[] { result })!;
        }

        throw new InvalidOperationException($"Unsupported TResult type: {typeof(TResult)}");
    }

}
