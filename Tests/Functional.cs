namespace Tests;

public static class Functional
{
    public static Func<T, T> Flatten<T>(this IEnumerable<Func<T, T>> funcs) => x => funcs.Aggregate(x, (acc, func) => func(acc));
}