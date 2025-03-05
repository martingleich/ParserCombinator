namespace ParserCombinator;
using static Parser;

public static class Program
{
    private static int ParseInt(IEnumerable<char> digits) => digits.Aggregate(0, (accum, d) => accum * 10 + (d - '0'));
    private static Parser<Func<int, int, int>> Op(char op, Func<int, int, int> f) => Char(op).Return(f);

    public static void Main()
    {
        var number = Digit.Some().Select(ParseInt);
        var opSum = OneOf(
            Op('+', (a, b) => a + b),
            Op('-', (a, b) => a - b));
        var opProduct = OneOf(
            Op('*', (a, b) => a * b),
            Op('/', (a, b) => a / b));
        var opNegate = Char('-').Skip(Whitespaces).Any().Select(x => x.Count() % 2 == 1);
        var expr = Recursive<int>(expr =>
        {
            var bracketedExpr = Surround(Char('(').Skip(Whitespaces), expr, Char(')'));
            var term = OneOf(number, bracketedExpr);
            var negation = Sequence(opNegate, term, (neg, t) => neg ? -t : t).Skip(Whitespaces);
            var product = ChainLeft(negation, opProduct.Skip(Whitespaces));
            return ChainLeft(product, opSum.Skip(Whitespaces));
        });
        Console.WriteLine(expr("1+2* -3"));
        Console.WriteLine(expr("2*( 1 - 2)"));
        Console.WriteLine(expr("- -1"));
    }
}