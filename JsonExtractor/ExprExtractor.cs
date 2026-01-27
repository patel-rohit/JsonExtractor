using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public static class ExprExtractor
{
    static readonly Dictionary<string, Func<List<object>, object>> Functions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["concat"] = args => string.Concat(args.Select(a => a?.ToString())),
            ["first"] = args => (args[0] as List<object>)?.FirstOrDefault(),
            ["coalesce"] = args => args.FirstOrDefault(a => a != null)
        };

    public static object ExtractExpr(JsonElement root, string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
            throw new InvalidOperationException("Expression cannot be empty.");

        expr = expr.Trim(); // ✅ normalize outer whitespace

        var nameEnd = expr.IndexOf('(');
        if (nameEnd < 0 || !expr.EndsWith(")"))
            throw new InvalidOperationException($"Invalid expression: {expr}");

        var func = expr[..nameEnd];
        var argsPart = expr[(nameEnd + 1)..^1];

        var args = SplitArgs(argsPart)
            .Select(a =>
            {
                a = a.Trim();

                if (a.StartsWith("$"))
                {
                    var r = JsonExtractor.ExtractPath(root, a);
                    return (object)r.Value;   // ⬅ flatten JsonExtractResult to object
                }

                if (a.StartsWith("'") && a.EndsWith("'"))
                    return (object)a[1..^1];

                if (decimal.TryParse(a, out var d))
                    return (object)d;

                return null;
            })
            .ToList();

        if (!Functions.TryGetValue(func, out var fn))
            throw new InvalidOperationException($"Unknown function: {func}");

        return fn(args);
    }


    static List<string> SplitArgs(string input)
    {
        var result = new List<string>();
        var current = "";
        bool inString = false;

        foreach (var c in input)
        {
            if (c == '\'')
                inString = !inString;

            if (c == ',' && !inString)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
            result.Add(current);

        return result;
    }
}
