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
        return (IQueryable)Activator.CreateInstance(queryableType, StripIncludes(expression))!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(StripIncludes(expression));

    public object? Execute(Expression expression)
        => _inner.Execute(StripIncludes(expression));

    public TResult Execute<TResult>(Expression expression)
        => _inner.Execute<TResult>(StripIncludes(expression));

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var result = Execute(expression);
        return Task.FromResult((TResult)result!);
    }

    TResult IAsyncQueryProvider.ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var result = Execute(StripIncludes(expression));
        var resultType = typeof(TResult);

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var taskResultType = resultType.GetGenericArguments()[0];
            var method = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(taskResultType);
            return (TResult)method.Invoke(null, new[] { result })!;
        }

        throw new InvalidOperationException($"Unsupported TResult type: {typeof(TResult)}");
    }

    /// <summary>
    /// Strips EF Core Include/ThenInclude method calls from the expression tree
    /// so that in-memory LINQ can evaluate the query without errors.
    /// </summary>
    private static Expression StripIncludes(Expression expression)
    {
        return new IncludeRemovingExpressionVisitor().Visit(expression);
    }

    private class IncludeRemovingExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType?.FullName == "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions"
                && (node.Method.Name == "Include" || node.Method.Name == "ThenInclude"))
            {
                // Skip the Include/ThenInclude call, return its source argument
                return Visit(node.Arguments[0]);
            }

            return base.VisitMethodCall(node);
        }
    }
}
