using System.Collections.Generic;
using System.Text.Json;
using Xunit;

public class JsonExtractorTests
{
    private readonly JsonElement _root;

    public JsonExtractorTests()
    {
        const string SampleJson = """
        {
          "data": {
            "id": 99,
            "name": "Test",
            "active": true,
            "price": 12.5,
            "kioskLis": [
              { "kioskId": 5 },
              { "kioskId": 4 }
            ],
            "meta": {
              "tags": ["a", "b"]
            }
          },
          "user": {
            "firstName": "Rohit",
            "lastName": "Patel",
            "email": null
          },
          "emptyObj": {},
          "emptyArr": []
        }
        """;

        var doc = JsonDocument.Parse(SampleJson);
        _root = doc.RootElement;
    }

    // ---------------------------
    // Basic scalars
    // ---------------------------

    [Fact]
    public void ExtractPath_IntScalar()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.id");

        Assert.Equal(JsonExtractResultType.Scalar, result.Type);
        Assert.Equal(99, Convert.ToInt32(result.Value));
    }

    [Fact]
    public void ExtractPath_StringScalar()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.name");

        Assert.Equal(JsonExtractResultType.Scalar, result.Type);
        Assert.Equal("Test", result.Value);
    }

    [Fact]
    public void ExtractPath_BoolScalar()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.active");

        Assert.Equal(JsonExtractResultType.Scalar, result.Type);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public void ExtractPath_DecimalScalar()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.price");

        Assert.Equal(JsonExtractResultType.Scalar, result.Type);
        Assert.Equal(12.5m, result.Value);
    }

    // ---------------------------
    // Array fan-out
    // ---------------------------
    [Fact]
    public void ExtractPath_ArrayFanOut_KioskIds()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.kioskLis[*].kioskId");

        Assert.Equal(JsonExtractResultType.List, result.Type);

        var list = Assert.IsType<List<object>>(result.Value);

        var expected = JsonSerializer.Serialize(new List<object> { 5L, 4L });
        var actual = JsonSerializer.Serialize(list);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExtractPath_ArrayFanOut_RootArray()
    {
        var json = """
                {
                  "items": [
                    { "price": 10 },
                    { "price": 20 }
                  ]
                }
                """;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = JsonExtractor.ExtractPath(root, "$.items[*].price");

        Assert.Equal(JsonExtractResultType.List, result.Type);

        var list = Assert.IsType<List<object>>(result.Value);

        var expected = JsonSerializer.Serialize(new List<object> { 10L, 20L });
        var actual = JsonSerializer.Serialize(list);

        Assert.Equal(expected, actual);
    }
    [Fact]
    public void ExtractPath_NestedArrayFanOut()
    {
        var json = """
    {
      "orders": [
        { "items": [ { "price": 10 }, { "price": 20 } ] },
        { "items": [ { "price": 5 } ] }
      ]
    }
    """;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = JsonExtractor.ExtractPath(root, "$.orders[*].items[*].price");

        Assert.Equal(JsonExtractResultType.List, result.Type);

        var list = Assert.IsType<List<object>>(result.Value);

        var expected = JsonSerializer.Serialize(new List<object> { 10L, 20L, 5L });
        var actual = JsonSerializer.Serialize(list);

        Assert.Equal(expected, actual);
    }



    // ---------------------------
    // Raw object / array nodes
    // ---------------------------

    [Fact]
    public void ExtractPath_ObjectNode_ReturnsObjectType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.meta");

        Assert.Equal(JsonExtractResultType.Object, result.Type);

        var json = Assert.IsType<string>(result.Value);
        Assert.Contains("\"tags\"", json);
    }

    [Fact]
    public void ExtractPath_ArrayNode_ReturnsArrayType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.kioskLis");

        Assert.Equal(JsonExtractResultType.Array, result.Type);

        var json = Assert.IsType<string>(result.Value);
        Assert.Contains("\"kioskId\"", json);
    }

    [Fact]
    public void ExtractPath_EmptyObject_ReturnsObjectType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.emptyObj");

        Assert.Equal(JsonExtractResultType.Object, result.Type);
        Assert.Equal("{}", result.Value);
    }

    [Fact]
    public void ExtractPath_EmptyArray_ReturnsArrayType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.emptyArr");

        Assert.Equal(JsonExtractResultType.Array, result.Type);
        Assert.Equal("[]", result.Value);
    }

    // ---------------------------
    // Null safety
    // ---------------------------

    [Fact]
    public void ExtractPath_ExplicitJsonNull_ReturnsNullType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.user.email");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ExtractPath_MissingPath_ReturnsNullType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.unknown");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ExtractPath_MissingDeepPath_ReturnsNullType()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.kioskLis[*].missing");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }

    // ---------------------------
    // Edge cases
    // ---------------------------

    [Fact]
    public void ExtractPath_EmptyPath_ReturnsNullType()
    {
        var result = JsonExtractor.ExtractPath(_root, "");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ExtractPath_PathWithoutDollar_Works()
    {
        var result = JsonExtractor.ExtractPath(_root, "data.id");

        Assert.Equal(JsonExtractResultType.Scalar, result.Type);
        Assert.Equal(99L, Convert.ToInt32(result.Value));
    }

    [Fact]
    public void ExtractPath_PathWithExtraWhitespace_Works()
    {
        var result = JsonExtractor.ExtractPath(_root, "  $.data.id  ");

        Assert.Equal(JsonExtractResultType.Scalar, result.Type);
        Assert.Equal(99, Convert.ToInt32(result.Value));
    }

    [Fact]
    public void ExtractPath_WildcardOnlyOnArrayNode()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.kioskLis[*]");

        Assert.Equal(JsonExtractResultType.List, result.Type);

        var list = Assert.IsType<List<object>>(result.Value);
        Assert.Equal(2, list.Count);
        Assert.Contains("\"kioskId\"", list[0].ToString());
    }

    // ---------------------------
    // Intentional limitations
    // ---------------------------

    [Fact]
    public void ExtractPath_IndexAccess_NotSupported_ReturnsNull()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.kioskLis[0].kioskId");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ExtractPath_Filters_NotSupported_ReturnsNull()
    {
        var result = JsonExtractor.ExtractPath(_root, "$.data.kioskLis[?(@.kioskId == 5)].kioskId");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ExtractPath_RecursiveDescent_NotSupported_ReturnsNull()
    {
        var result = JsonExtractor.ExtractPath(_root, "$..kioskId");

        Assert.Equal(JsonExtractResultType.Null, result.Type);
        Assert.Null(result.Value);
    }
}
