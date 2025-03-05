<<<<<<<< HEAD:Tests/Calculator.cs
﻿using ParserCombinator;

namespace Tests;

using static Parser;
========
﻿namespace ParserCombinator;
>>>>>>>> origin/master:ParserCombinator/Program.cs

public class Calculator
{
<<<<<<<< HEAD:Tests/Calculator.cs
    private static int ParseInt(IEnumerable<char> digits) => digits.Aggregate(0, (accum, d) => accum * 10 + (d - '0'));
    private static Parser<Func<int, int, int>> Op(char op, Func<int, int, int> f) => Char(op).Return(f);

    private static readonly Parser<int> Expression = Create(() =>
    {
        var number = Digit.Some().Select(ParseInt);
        var opSum = OneOf(
            Op('+', (a, b) => a + b),
            Op('-', (a, b) => a - b));
        var opProduct = OneOf(
            Op('*', (a, b) => a * b),
            Op('/', (a, b) => a / b));
        var opNegate = Char('-').Skip(Whitespaces).Any().Select(x => x.Count() % 2 == 1);

        return Recursive<int>(expr =>
        {
            var bracketedExpr = Surround(Char('(').Skip(Whitespaces), expr, Char(')'));
            var term = OneOf(number, bracketedExpr);
            var negation = Sequence(opNegate, term, (neg, t) => neg ? -t : t).Skip(Whitespaces);
            var product = ChainLeft(negation, opProduct.Skip(Whitespaces));
            return ChainLeft(product, opSum.Skip(Whitespaces));
        });
    });

    [Theory]
    [InlineData("1", 1)]
    [InlineData("456", 456)]
    [InlineData("-2", -2)]
    [InlineData("1+2* -3", -5)]
    [InlineData("2*( 1 - 2)", -2)]
    [InlineData("- -1", 1)]
    public void Good(string text, int value) => Assert.Equal(value, Expression(text).Value);

    [Theory]
    [InlineData("")]
    [InlineData("-(1 - 2")]
    [InlineData("1+2*")]
    [InlineData("2*")]
    [InlineData("-")]
    public void Fail(string text) => Assert.False(Expression(text).IsOkay);
========
    public static void Main()
    {
    }
>>>>>>>> origin/master:ParserCombinator/Program.cs
}