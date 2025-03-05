using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ParserCombinator;

public readonly record struct StringSlice(string Data, int Start)
{
    public static implicit operator StringSlice(string str) => new(str, 0);
    public int Length => Data.Length - Start;

    public override string ToString() => Data[Start..];

    public ParseResult<char> TryMatch(Func<char, bool> predicate, string description) =>
        Start >= Data.Length || !predicate(Data[Start])
            ? ParseResult.Failed([new Error(description, Start)], false)
            : ParseResult.Good(Data[Start], this with { Start = Start + 1 });
}

public readonly record struct Error(string Expected, int Position);

public readonly record struct ParseResult<T>
{
    private readonly T _value;
    private readonly bool _consumedAnything;
    private readonly StringSlice _remaining;
    private readonly ImmutableArray<Error> _errors;

    private ParseResult(T value, bool consumedAnything, StringSlice remaining, ImmutableArray<Error> errors)
    {
        _value = value;
        _errors = errors;
        _consumedAnything = consumedAnything;
        _remaining = remaining;
    }

    public T Value => Switch((v, _) => v, (e, _) => throw new InvalidOperationException(string.Concat(e)));
    public bool IsOkay => Switch((v, _) => true, (e, _) => false);
    public static ParseResult<T> Good(T value, StringSlice remaining) => new(value, true, remaining, []);

    public static ParseResult<T> Failed(ImmutableArray<Error> error, bool consumedAnything) =>
        new(default!, consumedAnything, default, error);

    public static implicit operator ParseResult<T>(ParseResult.ParseResultFailed failed) =>
        Failed(failed.Error, failed.ConsumedAnything);

    public TR Switch<TR>(Func<T, StringSlice, TR> good, Func<ImmutableArray<Error>, bool, TR> bad) =>
        _errors.Length > 0 ? bad(_errors, _consumedAnything) : good(_value, _remaining);

    public ParseResult<TR> Select<TR>(Func<T, TR> good) =>
        Switch((v, r) => ParseResult<TR>.Good(good(v), r), ParseResult<TR>.Failed);

    public ParseResult<TR> SelectGood<TR>(Func<T, StringSlice, ParseResult<TR>> good) =>
        Switch(good, ParseResult<TR>.Failed);

    public ParseResult<T> SelectFailed(Func<ImmutableArray<Error>, bool, ParseResult<T>> failed) =>
        Switch(Good, failed);

    public ParseResult<T> MarkConsumed() => Switch(ParseResult.Good, (e, _) => ParseResult.Failed(e, true));

    public override string ToString() => Switch((v, r) => $"{v} : '{r}'",
        (e, c) => $"Failed: {string.Concat(e)} {(c ? "tried" : "")}");
}

public static class ParseResult
{
    public readonly record struct ParseResultFailed(ImmutableArray<Error> Error, bool ConsumedAnything);

    public static ParseResultFailed Failed(ImmutableArray<Error> error, bool consumedAnything) =>
        new(error, consumedAnything);

    public static ParseResult<T> Good<T>(T value, StringSlice remaining) => ParseResult<T>.Good(value, remaining);
}

public delegate ParseResult<T> Parser<T>(StringSlice slice);

public static class Parser
{
    // Internals
    public static Parser<T> Just<T>(T value) => str => ParseResult.Good(value, str);

    public static Parser<char> Satisfy(Func<char, bool> predicate,
        [CallerArgumentExpression(nameof(predicate))] string description = "") =>
        str => str.TryMatch(predicate, description);

    public static readonly Parser<char> EndOfInput = str =>
        str.Length == 0
            ? ParseResult.Good('\0', str)
            : ParseResult.Failed([new Error("Expected end of input.", str.Start)], false);

    public static Parser<TR> Select<T, TR>(this Parser<T> parser, Func<T, TR> selector) => str =>
        parser(str).Select(selector);

    public static Parser<TR> SelectMany<T, TI, TR>(this Parser<T> parser, Func<T, Parser<TI>> selector,
        Func<T, TI, TR> resultSelector) => str =>
        parser(str).SelectGood((v, r) => selector(v)(r).Select(vi => resultSelector(v, vi)).MarkConsumed());

    public static Parser<T> Where<T>(this Parser<T> parser, Func<T, bool> predicate,
        [CallerArgumentExpression(nameof(predicate))]
        string description = "") => str =>
        parser(str).SelectGood((v, r) => predicate(v)
            ? ParseResult.Good(v, r)
            : ParseResult.Failed([new Error(description, str.Start)], true));

    public static Parser<IEnumerable<T>> Any<T>(this Parser<T> parser) => str =>
    {
        var result = new List<T>();
        while (true)
        {
            var matched = parser(str).Switch<ParseResult<IEnumerable<T>>?>((v, r) =>
                {
                    result.Add(v);
                    str = r;
                    return null;
                },
                (e, c) => c
                    ? ParseResult.Failed(e, true)
                    : ParseResult.Good(result.AsEnumerable(), str));
            if (matched is { } x)
                return x;
        }
    };

    public static Parser<T> OneOf<T>(params Parser<T>[] parsers) => str =>
    {
        var fails = new List<Error>();
        foreach (var p in parsers)
        {
            var match = p(str).Switch<ParseResult<T>?>(
                (v, e) => ParseResult<T>.Good(v, e),
                (e, c) =>
                {
                    fails.AddRange(e);
                    return c
                        ? ParseResult<T>.Failed([..fails], true)
                        : null;
                });
            if (match is { } result)
                return result;
        }

        return ParseResult.Failed([..fails], false);
    };

    public static Parser<T> Recursive<T>(Func<Parser<T>, Parser<T>> parser)
    {
        var result = default(Parser<T>);
        result = parser(str => (result ?? throw new StackOverflowException())(str));
        return result;
    }

    public static Parser<T> Try<T>(Parser<T> parser) => str => parser(str).SelectFailed(
        (e, _) => ParseResult.Failed(e, false));

    // Normal world.
    public static Parser<char> Char(char c) => Satisfy(x => x == c, $"'{c}'");
    public static Parser<TR> Return<T, TR>(this Parser<T> parser, TR value) => parser.Select(_ => value);
    public static readonly Parser<char> Digit = Satisfy(char.IsDigit);

    public static Parser<IEnumerable<T>> Some<T>(this Parser<T> parser) =>
        from first in parser
        from rest in Any(parser)
        select rest.Prepend(first);

    public static Parser<T> ChainLeft<T>(Parser<T> parserArg, Parser<Func<T, T, T>> parserOp)
    {
        var parserTail = Any(Sequence(parserOp, parserArg,
            (op, arg)
                => (op, arg)));
        return Sequence(parserArg, parserTail,
            (first, tail)
                => tail.Aggregate(first, (acc, op_arg) => op_arg.Item1(acc, op_arg.Item2)));
    }

    public static Parser<TR> Sequence<T1, T2, TR>(Parser<T1> parser1, Parser<T2> parser2,
        Func<T1, T2, TR> resultSelector) => from p1 in parser1
        from p2 in parser2
        select resultSelector(p1, p2);

    public static Parser<TR> Sequence<T1, T2, T3, TR>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3,
        Func<T1, T2, T3, TR> resultSelector) => from p1 in parser1
        from p2 in parser2
        from p3 in parser3
        select resultSelector(p1, p2, p3);

    public static Parser<T2> Surround<T1, T2, T3>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3)
        => from p1 in parser1
            from p2 in parser2
            from p3 in parser3
            select p2;

    public static readonly Parser<char> Whitespace = Satisfy(char.IsWhiteSpace);
    public static readonly Parser<IEnumerable<char>> Whitespaces = Whitespace.Any();

    public static Parser<T> Skip<T, TS>(this Parser<T> parser, Parser<TS> skip) =>
        from a in parser from _ in skip select a;

    public static Parser<int> Create(Func<Parser<int>> func) => func();
}