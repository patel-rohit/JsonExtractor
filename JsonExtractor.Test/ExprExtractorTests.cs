using System.Collections.Generic;
using System.Text.Json;
using Xunit;

public class ExprExtractorTests
{
    const string SampleJson = """
                {
                  "data": {
                    "id": 99,
                    "kioskLis": [
                      { "kioskId": 5 },
                      { "kioskId": 4 }
                    ]
                  },
                  "user": {
                    "firstName": "Rohit",
                    "lastName": "Patel",
                    "email": null
                  }
                }
                """;

    private readonly JsonElement _root;

    public ExprExtractorTests()
    {
        var doc = JsonDocument.Parse(SampleJson);
        _root = doc.RootElement;
    }

    // ---------------------------
    // concat(...)
    // ---------------------------

    [Fact]
    public void ExtractExpr_Concat_TwoFields()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "concat($.user.firstName, ' ', $.user.lastName)"
        );

        Assert.Equal("Rohit Patel", result);
    }

    [Fact]
    public void ExtractExpr_Concat_LiteralsOnly()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "concat('Hello', ' ', 'World')"
        );

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ExtractExpr_Concat_MixedFieldsAndLiterals()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "concat('User: ', $.user.firstName)"
        );

        Assert.Equal("User: Rohit", result);
    }

    // ---------------------------
    // first(...)
    // ---------------------------

    [Fact]
    public void ExtractExpr_First_OnArrayFanOut()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "first($.data.kioskLis[*].kioskId)"
        );

        Assert.Equal(5, Convert.ToInt32(result));
    }

    [Fact]
    public void ExtractExpr_First_OnEmptyResult_ReturnsNull()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "first($.data.unknown[*])"
        );

        Assert.Null(result);
    }

    // ---------------------------
    // coalesce(...)
    // ---------------------------

    [Fact]
    public void ExtractExpr_Coalesce_FirstIsNull()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "coalesce($.user.email, 'n/a')"
        );

        Assert.Equal("n/a", result);
    }

    [Fact]
    public void ExtractExpr_Coalesce_FirstIsNotNull()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "coalesce($.user.firstName, 'n/a')"
        );

        Assert.Equal("Rohit", result);
    }

    [Fact]
    public void ExtractExpr_Coalesce_AllNull_ReturnsNull()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "coalesce($.user.email, $.data.unknown)"
        );

        Assert.Null(result);
    }

    // ---------------------------
    // Numeric literals
    // ---------------------------

    [Fact]
    public void ExtractExpr_Concat_WithNumericLiteral()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "concat('Id: ', 99)"
        );

        Assert.Equal("Id: 99", result);
    }

    // ---------------------------
    // Edge cases
    // ---------------------------

    [Fact]
    public void ExtractExpr_WhitespaceTolerance()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "  concat(  $.user.firstName , ' ' , $.user.lastName  )  "
        );

        Assert.Equal("Rohit Patel", result);
    }

    [Fact]
    public void ExtractExpr_SingleArgumentConcat()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "concat($.user.firstName)"
        );

        Assert.Equal("Rohit", result);
    }

    // ---------------------------
    // Intentional limitations
    // ---------------------------

    [Fact]
    public void ExtractExpr_UnknownFunction_Throws()
    {
        Assert.Throws<System.InvalidOperationException>(() =>
        {
            ExprExtractor.ExtractExpr(_root, "unknown($.data.id)");
        });
    }

    [Fact]
    public void ExtractExpr_InvalidExpressionFormat_Throws()
    {
        Assert.Throws<System.InvalidOperationException>(() =>
        {
            ExprExtractor.ExtractExpr(_root, "$.data.id");
        });
    }

    [Fact]
    public void ExtractExpr_IndexAccess_NotSupported_ReturnsNull()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "first($.data.kioskLis[0].kioskId)"
        );

        Assert.Null(result);
    }

    [Fact]
    public void ExtractExpr_Filters_NotSupported_ReturnsNull()
    {
        var result = ExprExtractor.ExtractExpr(
            _root,
            "first($.data.kioskLis[?(@.kioskId == 5)].kioskId)"
        );

        Assert.Null(result);
    }
}
