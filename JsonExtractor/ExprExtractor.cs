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
            ["coalesce"] = args => args.FirstOrDefault(a => a != null),
            ["parse_json"] = ParseJson,
            ["json_get"] = JsonGet,
            ["join"] = Join
        };

    static object JsonGet(List<object> args)
    {
        if (args.Count != 2)
            throw new InvalidOperationException("json_get expects 2 arguments");

        if (args[0] is not JsonElement json)
            return null;

        var path = args[1]?.ToString();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        // force path to behave like $.tiers
        if (!path.StartsWith("$"))
            path = "$." + path;

        var result = JsonExtractor.ExtractPath(json, path);
        return result.Value;
    }
    static object Join(List<object> args)
    {
        if (args.Count != 2)
            throw new InvalidOperationException("join expects 2 arguments");

        var separator = args[0]?.ToString() ?? ",";
        var value = args[1];

        if (value == null)
            return "";

        if (value is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            return string.Join(
                separator,
                el.EnumerateArray().Select(x => x.ToString())
            );
        }

        if (value is IEnumerable<object> list)
        {
            return string.Join(separator, list.Select(x => x?.ToString()));
        }

        throw new InvalidOperationException("join expects an array");
    }

    static object ParseJson(List<object> args)
    {
        if (args.Count != 1)
            throw new InvalidOperationException("parse_json expects 1 argument");

        var input = args[0];

        if (input == null)
            return null;

        if (input is JsonElement)
            return input;

        if (input is not string s || string.IsNullOrWhiteSpace(s))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.Clone(); // important
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON in parse_json: {ex.Message}");
        }
    }

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

                // 🔥 NEW: nested function call
                if (a.Contains('(') && a.EndsWith(")"))
                    return ExtractExpr(root, a);

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
        int parenDepth = 0;

        foreach (var c in input)
        {
            if (c == '\'')
            {
                inString = !inString;
                current += c;
                continue;
            }

            if (!inString)
            {
                if (c == '(')
                    parenDepth++;

                if (c == ')')
                    parenDepth--;
            }

            if (c == ',' && !inString && parenDepth == 0)
            {
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
            result.Add(current.Trim());

        return result;
    }

}
