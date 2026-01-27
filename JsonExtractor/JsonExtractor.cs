using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public static class JsonExtractor
{
    /*
     * SUPPORTED SYNTAX
     * ----------------
     *  1) Simple property access
     *     $.a.b.c
     *
     *  2) Array fan-out
     *     $.list[*]
     *     $.list[*].x
     *
     *  3) Mixed object + array traversal
     *     $.orders[*].items[*].price
     *
     * BEHAVIOR
     * --------
     *  - Always returns JsonExtractResult with explicit Type
     *
     *  - Type = Null
     *      * Path does not exist
     *      * JSON value is explicit null
     *
     *  - Type = Scalar
     *      * string
     *      * number
     *      * bool
     *
     *  - Type = List
     *      * Multiple scalar values
     *
     *  - Type = Object / Array
     *      * Returned as raw JSON text
     *
     * INTENTIONAL LIMITATIONS (BY DESIGN)
     * -----------------------------------
     *  - No filters:
     *      $.items[?(@.price > 10)]     ❌ NOT SUPPORTED
     *
     *  - No index access:
     *      $.items[0]                   ❌ NOT SUPPORTED
     *
     *  - No recursive descent:
     *      $..price                     ❌ NOT SUPPORTED
     *
     *  - No conditional logic
     *  - No computed values
     *
     * WHY IT IS LIMITED
     * -----------------
     *  This extractor is intentionally restricted to keep:
     *    - DB-driven mappings safe
     *    - performance predictable
     *    - behavior deterministic
     *    - queries auditable
     *
     *  For complex or semantic logic:
     *    - Use a dedicated expression / DSL layer instead
     *    - Do NOT extend this into full JSONPath
     */

    public static JsonExtractResult ExtractPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new JsonExtractResult
            {
                Type = JsonExtractResultType.Null,
                Value = null
            };
        }

        path = path.Trim();

        if (path.StartsWith("$."))
            path = path[2..];

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

        List<JsonElement> current = new() { root };

        foreach (var part in parts)
        {
            var next = new List<JsonElement>();

            // Case 1: wildcard array fan-out: [*]
            if (part == "[*]")
            {
                foreach (var el in current)
                {
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in el.EnumerateArray())
                            next.Add(item);
                    }
                }
            }
            // Case 2: property with wildcard: prop[*]
            else if (part.EndsWith("[*]"))
            {
                var prop = part[..^3];

                foreach (var el in current)
                {
                    if (el.ValueKind == JsonValueKind.Object &&
                        el.TryGetProperty(prop, out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                            next.Add(item);
                    }
                }
            }
            // Case 3: normal property access
            else
            {
                foreach (var el in current)
                {
                    if (el.ValueKind == JsonValueKind.Object &&
                        el.TryGetProperty(part, out var child))
                    {
                        next.Add(child);
                    }
                }
            }

            current = next;

            if (current.Count == 0)
                break;
        }

        if (current.Count == 0)
        {
            return new JsonExtractResult
            {
                Type = JsonExtractResultType.Null,
                Value = null
            };
        }

        if (current.Count == 1)
        {
            return WrapSingle(current[0]);
        }

        // Multiple results → List
        var values = current.Select(ExtractScalarOrRaw).ToList();

        return new JsonExtractResult
        {
            Type = JsonExtractResultType.List,
            Value = values
        };
    }

    static JsonExtractResult WrapSingle(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => new JsonExtractResult
            {
                Type = JsonExtractResultType.Scalar,
                Value = el.GetString()
            },

            JsonValueKind.Number => new JsonExtractResult
            {
                Type = JsonExtractResultType.Scalar,
                Value = el.TryGetInt64(out var l) ? l : el.GetDecimal()
            },

            JsonValueKind.True => new JsonExtractResult
            {
                Type = JsonExtractResultType.Scalar,
                Value = true
            },

            JsonValueKind.False => new JsonExtractResult
            {
                Type = JsonExtractResultType.Scalar,
                Value = false
            },

            JsonValueKind.Null => new JsonExtractResult
            {
                Type = JsonExtractResultType.Null,
                Value = null
            },

            JsonValueKind.Object => new JsonExtractResult
            {
                Type = JsonExtractResultType.Object,
                Value = el.ToString()
            },

            JsonValueKind.Array => new JsonExtractResult
            {
                Type = JsonExtractResultType.Array,
                Value = el.ToString()
            },

            _ => new JsonExtractResult
            {
                Type = JsonExtractResultType.Scalar,
                Value = el.ToString()
            }
        };
    }

    static object ExtractScalarOrRaw(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.ToString()
        };
    }
}
