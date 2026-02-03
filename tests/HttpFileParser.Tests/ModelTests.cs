namespace HttpFileParser.Tests;

using HttpFileParser.Model;
using HttpFileParser.Parsing;

public class SourceSpanTests
{
    [Fact]
    public void Empty_ReturnsZeroValues()
    {
        var span = SourceSpan.Empty;

        Assert.Equal(0, span.StartLine);
        Assert.Equal(0, span.StartColumn);
        Assert.Equal(0, span.EndLine);
        Assert.Equal(0, span.EndColumn);
        Assert.Equal(0, span.StartOffset);
        Assert.Equal(0, span.EndOffset);
    }

    [Fact]
    public void Length_ReturnsEndOffsetMinusStartOffset()
    {
        var span = new SourceSpan(1, 1, 1, 10, 0, 10);

        Assert.Equal(10, span.Length);
    }

    [Fact]
    public void Length_WhenSpanIsEmpty_ReturnsZero()
    {
        var span = new SourceSpan(1, 1, 1, 1, 5, 5);

        Assert.Equal(0, span.Length);
    }

    #region Contains Tests

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        // Span from line 2, col 5 to line 4, col 10
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Point at line 3, col 7 (middle of span)
        Assert.True(span.Contains(3, 7));
    }

    [Fact]
    public void Contains_PointAtStartBoundary_ReturnsTrue()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Exactly at start position
        Assert.True(span.Contains(2, 5));
    }

    [Fact]
    public void Contains_PointAtEndBoundary_ReturnsTrue()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Exactly at end position
        Assert.True(span.Contains(4, 10));
    }

    [Fact]
    public void Contains_PointBeforeStartLine_ReturnsFalse()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Line before start
        Assert.False(span.Contains(1, 5));
    }

    [Fact]
    public void Contains_PointAfterEndLine_ReturnsFalse()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Line after end
        Assert.False(span.Contains(5, 5));
    }

    [Fact]
    public void Contains_PointOnStartLineBeforeStartColumn_ReturnsFalse()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Same line as start, but column before start column
        Assert.False(span.Contains(2, 4));
    }

    [Fact]
    public void Contains_PointOnEndLineAfterEndColumn_ReturnsFalse()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Same line as end, but column after end column
        Assert.False(span.Contains(4, 11));
    }

    [Fact]
    public void Contains_PointOnStartLineAfterStartColumn_ReturnsTrue()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Same line as start, column after start column
        Assert.True(span.Contains(2, 6));
    }

    [Fact]
    public void Contains_PointOnEndLineBeforeEndColumn_ReturnsTrue()
    {
        var span = new SourceSpan(2, 5, 4, 10, 20, 50);

        // Same line as end, column before end column
        Assert.True(span.Contains(4, 9));
    }

    [Theory]
    [InlineData(1, 1, true)]   // At start boundary
    [InlineData(1, 5, true)]   // At end boundary
    [InlineData(1, 3, true)]   // Middle
    [InlineData(1, 0, false)]  // Before start column
    [InlineData(1, 6, false)]  // After end column
    [InlineData(0, 3, false)]  // Before start line
    [InlineData(2, 3, false)]  // After end line
    public void Contains_SingleLineSpan_VariousPoints(int line, int column, bool expected)
    {
        // Single line span from col 1 to col 5 on line 1
        var span = new SourceSpan(1, 1, 1, 5, 0, 5);

        Assert.Equal(expected, span.Contains(line, column));
    }

    [Theory]
    [InlineData(1, 5, true)]   // Start boundary
    [InlineData(3, 10, true)]  // End boundary
    [InlineData(2, 1, true)]   // Middle line, any column should work
    [InlineData(2, 100, true)] // Middle line, far column should work
    [InlineData(1, 4, false)]  // Start line, before start column
    [InlineData(3, 11, false)] // End line, after end column
    public void Contains_MultiLineSpan_VariousPoints(int line, int column, bool expected)
    {
        // Multi-line span: line 1 col 5 to line 3 col 10
        var span = new SourceSpan(1, 5, 3, 10, 5, 40);

        Assert.Equal(expected, span.Contains(line, column));
    }

    #endregion

    #region Merge Tests

    [Fact]
    public void Merge_NonOverlappingSpans_ReturnsSpanCoveringBoth()
    {
        var first = new SourceSpan(1, 1, 1, 10, 0, 10);
        var second = new SourceSpan(3, 1, 3, 10, 30, 40);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(1, merged.StartColumn);
        Assert.Equal(3, merged.EndLine);
        Assert.Equal(10, merged.EndColumn);
        Assert.Equal(0, merged.StartOffset);
        Assert.Equal(40, merged.EndOffset);
    }

    [Fact]
    public void Merge_OverlappingSpans_ReturnsSpanCoveringBoth()
    {
        var first = new SourceSpan(1, 1, 2, 10, 0, 20);
        var second = new SourceSpan(2, 5, 3, 15, 15, 35);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(1, merged.StartColumn);
        Assert.Equal(3, merged.EndLine);
        Assert.Equal(15, merged.EndColumn);
        Assert.Equal(0, merged.StartOffset);
        Assert.Equal(35, merged.EndOffset);
    }

    [Fact]
    public void Merge_SameLineSpans_ReturnsMergedSpan()
    {
        var first = new SourceSpan(1, 1, 1, 5, 0, 5);
        var second = new SourceSpan(1, 8, 1, 15, 8, 15);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(1, merged.StartColumn);
        Assert.Equal(1, merged.EndLine);
        Assert.Equal(15, merged.EndColumn);
        Assert.Equal(0, merged.StartOffset);
        Assert.Equal(15, merged.EndOffset);
    }

    [Fact]
    public void Merge_SameLineOverlappingSpans_ReturnsMergedSpan()
    {
        var first = new SourceSpan(1, 1, 1, 10, 0, 10);
        var second = new SourceSpan(1, 5, 1, 15, 5, 15);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(1, merged.StartColumn);
        Assert.Equal(1, merged.EndLine);
        Assert.Equal(15, merged.EndColumn);
        Assert.Equal(0, merged.StartOffset);
        Assert.Equal(15, merged.EndOffset);
    }

    [Fact]
    public void Merge_SecondSpanStartsBeforeFirst_ReturnsCorrectStart()
    {
        var first = new SourceSpan(2, 5, 2, 10, 20, 25);
        var second = new SourceSpan(1, 1, 2, 3, 0, 18);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(1, merged.StartColumn);
        Assert.Equal(2, merged.EndLine);
        Assert.Equal(10, merged.EndColumn);
        Assert.Equal(0, merged.StartOffset);
        Assert.Equal(25, merged.EndOffset);
    }

    [Fact]
    public void Merge_FirstSpanEndsAfterSecond_ReturnsCorrectEnd()
    {
        var first = new SourceSpan(1, 1, 3, 15, 0, 45);
        var second = new SourceSpan(2, 1, 2, 10, 15, 25);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(1, merged.StartColumn);
        Assert.Equal(3, merged.EndLine);
        Assert.Equal(15, merged.EndColumn);
        Assert.Equal(0, merged.StartOffset);
        Assert.Equal(45, merged.EndOffset);
    }

    [Fact]
    public void Merge_IdenticalSpans_ReturnsSameSpan()
    {
        var first = new SourceSpan(1, 5, 2, 10, 5, 25);
        var second = new SourceSpan(1, 5, 2, 10, 5, 25);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(first, merged);
    }

    [Fact]
    public void Merge_StartOnSameLinePicksMinColumn()
    {
        var first = new SourceSpan(1, 10, 2, 5, 10, 20);
        var second = new SourceSpan(1, 5, 2, 10, 5, 25);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(1, merged.StartLine);
        Assert.Equal(5, merged.StartColumn);
    }

    [Fact]
    public void Merge_EndOnSameLinePicksMaxColumn()
    {
        var first = new SourceSpan(1, 1, 2, 15, 0, 30);
        var second = new SourceSpan(1, 5, 2, 10, 5, 25);

        var merged = SourceSpan.Merge(first, second);

        Assert.Equal(2, merged.EndLine);
        Assert.Equal(15, merged.EndColumn);
    }

    #endregion
}

public class HttpDocumentTests
{
    [Fact]
    public void HasErrors_WhenNoErrors_ReturnsFalse()
    {
        var doc = new HttpDocument(
            null,
            Array.Empty<HttpDocumentItem>(),
            Array.Empty<HttpDiagnostic>());

        Assert.False(doc.HasErrors);
    }

    [Fact]
    public void HasErrors_WhenOnlyWarnings_ReturnsFalse()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "Test warning", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.False(doc.HasErrors);
    }

    [Fact]
    public void HasErrors_WhenHasErrorDiagnostic_ReturnsTrue()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "Test error", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.True(doc.HasErrors);
    }

    [Fact]
    public void HasErrors_WhenMixedDiagnostics_ReturnsTrue()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "Test warning", SourceSpan.Empty),
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "Test error", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.True(doc.HasErrors);
    }

    [Fact]
    public void HasWarnings_WhenNoWarnings_ReturnsFalse()
    {
        var doc = new HttpDocument(
            null,
            Array.Empty<HttpDocumentItem>(),
            Array.Empty<HttpDiagnostic>());

        Assert.False(doc.HasWarnings);
    }

    [Fact]
    public void HasWarnings_WhenOnlyErrors_ReturnsFalse()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "Test error", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.False(doc.HasWarnings);
    }

    [Fact]
    public void HasWarnings_WhenHasWarningDiagnostic_ReturnsTrue()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "Test warning", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.True(doc.HasWarnings);
    }

    [Fact]
    public void HasWarnings_WhenMixedDiagnostics_ReturnsTrue()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "Test warning", SourceSpan.Empty),
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "Test error", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.True(doc.HasWarnings);
    }

    [Fact]
    public void HasWarnings_WhenMultipleWarnings_ReturnsTrue()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "First warning", SourceSpan.Empty),
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "Second warning", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.True(doc.HasWarnings);
    }

    [Fact]
    public void HasErrors_WhenMultipleErrors_ReturnsTrue()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "First error", SourceSpan.Empty),
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "Second error", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.True(doc.HasErrors);
    }

    [Fact]
    public void Requests_ReturnsOnlyHttpRequestItems()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, null,
            Array.Empty<HttpHeader>(), null,
            Array.Empty<HttpDirective>(),
            Array.Empty<Comment>(),
            SourceSpan.Empty);
        var comment = new Comment("test comment", SourceSpan.Empty);
        var variable = new FileVariable("test", "value", SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request, comment, variable };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        Assert.Single(doc.Requests);
        Assert.Equal("GET", doc.Requests.First().Method);
    }

    [Fact]
    public void Variables_ReturnsOnlyFileVariableItems()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, null,
            Array.Empty<HttpHeader>(), null,
            Array.Empty<HttpDirective>(),
            Array.Empty<Comment>(),
            SourceSpan.Empty);
        var variable1 = new FileVariable("var1", "value1", SourceSpan.Empty);
        var variable2 = new FileVariable("var2", "value2", SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request, variable1, variable2 };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        Assert.Equal(2, doc.Variables.Count());
        Assert.Contains(doc.Variables, v => v.Name == "var1");
        Assert.Contains(doc.Variables, v => v.Name == "var2");
    }

    [Fact]
    public void Comments_ReturnsOnlyCommentItems()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, null,
            Array.Empty<HttpHeader>(), null,
            Array.Empty<HttpDirective>(),
            Array.Empty<Comment>(),
            SourceSpan.Empty);
        var comment1 = new Comment("comment 1", SourceSpan.Empty);
        var comment2 = new Comment("comment 2", SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request, comment1, comment2 };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        Assert.Equal(2, doc.Comments.Count());
    }

    [Fact]
    public void GetRequestByName_WhenNoRequests_ReturnsNull()
    {
        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), Array.Empty<HttpDiagnostic>());

        Assert.Null(doc.GetRequestByName("any"));
    }

    [Fact]
    public void GetRequestByName_WhenRequestHasNoName_ReturnsNull()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, null,
            Array.Empty<HttpHeader>(), null,
            Array.Empty<HttpDirective>(),
            Array.Empty<Comment>(),
            SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        Assert.Null(doc.GetRequestByName("test"));
    }

    [Fact]
    public void GetRequestByName_WhenNameMatches_ReturnsRequest()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, "myRequest",
            Array.Empty<HttpHeader>(), null,
            new List<HttpDirective> { new HttpDirective("name", "myRequest", SourceSpan.Empty) },
            Array.Empty<Comment>(),
            SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        var found = doc.GetRequestByName("myRequest");
        Assert.NotNull(found);
        Assert.Equal("GET", found.Method);
    }

    [Fact]
    public void GetRequestByName_IsCaseInsensitive()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, "MyRequest",
            Array.Empty<HttpHeader>(), null,
            new List<HttpDirective> { new HttpDirective("name", "MyRequest", SourceSpan.Empty) },
            Array.Empty<Comment>(),
            SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        Assert.NotNull(doc.GetRequestByName("myrequest"));
        Assert.NotNull(doc.GetRequestByName("MYREQUEST"));
    }

    [Fact]
    public void FilePath_ReturnsProvidedPath()
    {
        var doc = new HttpDocument("test.http", Array.Empty<HttpDocumentItem>(), Array.Empty<HttpDiagnostic>());

        Assert.Equal("test.http", doc.FilePath);
    }

    [Fact]
    public void Items_ReturnsAllItems()
    {
        var request = new HttpRequest(
            "GET", "https://example.com", null, null,
            Array.Empty<HttpHeader>(), null,
            Array.Empty<HttpDirective>(),
            Array.Empty<Comment>(),
            SourceSpan.Empty);
        var comment = new Comment("test", SourceSpan.Empty);
        var variable = new FileVariable("var", "val", SourceSpan.Empty);

        var items = new List<HttpDocumentItem> { request, comment, variable };
        var doc = new HttpDocument(null, items, Array.Empty<HttpDiagnostic>());

        Assert.Equal(3, doc.Items.Count);
    }

    [Fact]
    public void Diagnostics_ReturnsAllDiagnostics()
    {
        var diagnostics = new List<HttpDiagnostic>
        {
            new HttpDiagnostic(HttpDiagnosticSeverity.Warning, "warn", SourceSpan.Empty),
            new HttpDiagnostic(HttpDiagnosticSeverity.Error, "err", SourceSpan.Empty)
        };

        var doc = new HttpDocument(null, Array.Empty<HttpDocumentItem>(), diagnostics);

        Assert.Equal(2, doc.Diagnostics.Count);
    }
}

public class HttpRequestTests
{
    private static HttpRequest CreateRequestWithDirectives(params HttpDirective[] directives)
    {
        return new HttpRequest(
            method: "GET",
            rawUrl: "https://example.com/api",
            httpVersion: null,
            name: null,
            headers: Array.Empty<HttpHeader>(),
            body: null,
            directives: directives.ToList(),
            leadingComments: Array.Empty<Comment>(),
            span: SourceSpan.Empty);
    }

    [Fact]
    public void GetDirectiveValue_WhenDirectiveExists_ReturnsValue()
    {
        var directive = new HttpDirective("name", "myRequest", SourceSpan.Empty);
        var request = CreateRequestWithDirectives(directive);

        var value = request.GetDirectiveValue("name");

        Assert.Equal("myRequest", value);
    }

    [Fact]
    public void GetDirectiveValue_WhenDirectiveNotFound_ReturnsNull()
    {
        var directive = new HttpDirective("name", "myRequest", SourceSpan.Empty);
        var request = CreateRequestWithDirectives(directive);

        var value = request.GetDirectiveValue("no-redirect");

        Assert.Null(value);
    }

    [Fact]
    public void GetDirectiveValue_WhenNoDirectives_ReturnsNull()
    {
        var request = CreateRequestWithDirectives();

        var value = request.GetDirectiveValue("name");

        Assert.Null(value);
    }

    [Fact]
    public void GetDirectiveValue_WhenDirectiveHasNullValue_ReturnsNull()
    {
        var directive = new HttpDirective("no-redirect", null, SourceSpan.Empty);
        var request = CreateRequestWithDirectives(directive);

        var value = request.GetDirectiveValue("no-redirect");

        Assert.Null(value);
    }

    [Fact]
    public void GetDirectiveValue_IsCaseInsensitive()
    {
        var directive = new HttpDirective("Name", "myRequest", SourceSpan.Empty);
        var request = CreateRequestWithDirectives(directive);

        Assert.Equal("myRequest", request.GetDirectiveValue("name"));
        Assert.Equal("myRequest", request.GetDirectiveValue("NAME"));
        Assert.Equal("myRequest", request.GetDirectiveValue("NaMe"));
    }

    [Fact]
    public void GetDirectiveValue_WithMultipleDirectives_ReturnsFirstMatch()
    {
        var directives = new[]
        {
            new HttpDirective("name", "first", SourceSpan.Empty),
            new HttpDirective("no-redirect", null, SourceSpan.Empty),
            new HttpDirective("name", "second", SourceSpan.Empty)
        };
        var request = CreateRequestWithDirectives(directives);

        var value = request.GetDirectiveValue("name");

        Assert.Equal("first", value);
    }

    [Fact]
    public void GetDirectiveValue_WithEmptyStringValue_ReturnsEmptyString()
    {
        var directive = new HttpDirective("note", "", SourceSpan.Empty);
        var request = CreateRequestWithDirectives(directive);

        var value = request.GetDirectiveValue("note");

        Assert.Equal("", value);
    }

    [Theory]
    [InlineData("name", "testName")]
    [InlineData("no-redirect", null)]
    [InlineData("note", "This is a note")]
    [InlineData("no-cookie-jar", null)]
    public void GetDirectiveValue_WellKnownDirectives(string directiveName, string? expectedValue)
    {
        var directive = new HttpDirective(directiveName, expectedValue, SourceSpan.Empty);
        var request = CreateRequestWithDirectives(directive);

        var value = request.GetDirectiveValue(directiveName);

        Assert.Equal(expectedValue, value);
    }
}

public class HttpDiagnosticTests
{
    [Fact]
    public void ToString_WithoutCode_FormatsCorrectly()
    {
        var span = new SourceSpan(10, 5, 10, 15, 100, 110);
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Error,
            "Unexpected token",
            span);

        var result = diagnostic.ToString();

        Assert.Equal("Error (10,5): Unexpected token", result);
    }

    [Fact]
    public void ToString_WithCode_IncludesCode()
    {
        var span = new SourceSpan(10, 5, 10, 15, 100, 110);
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Error,
            "Unexpected token",
            span,
            code: "HTTP001");

        var result = diagnostic.ToString();

        Assert.Equal("Error HTTP001: (10,5): Unexpected token", result);
    }

    [Fact]
    public void ToString_WarningSeverity_FormatsCorrectly()
    {
        var span = new SourceSpan(5, 1, 5, 20, 50, 70);
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Warning,
            "Deprecated header",
            span);

        var result = diagnostic.ToString();

        Assert.Equal("Warning (5,1): Deprecated header", result);
    }

    [Fact]
    public void ToString_WarningWithCode_FormatsCorrectly()
    {
        var span = new SourceSpan(5, 1, 5, 20, 50, 70);
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Warning,
            "Deprecated header",
            span,
            code: "HTTP100");

        var result = diagnostic.ToString();

        Assert.Equal("Warning HTTP100: (5,1): Deprecated header", result);
    }

    [Fact]
    public void ToString_AtLineOne_FormatsCorrectly()
    {
        var span = new SourceSpan(1, 1, 1, 10, 0, 10);
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Error,
            "Invalid method",
            span);

        var result = diagnostic.ToString();

        Assert.Equal("Error (1,1): Invalid method", result);
    }

    [Fact]
    public void ToString_WithEmptySpan_FormatsCorrectly()
    {
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Warning,
            "General warning",
            SourceSpan.Empty);

        var result = diagnostic.ToString();

        Assert.Equal("Warning (0,0): General warning", result);
    }

    [Theory]
    [InlineData(HttpDiagnosticSeverity.Error, "Error")]
    [InlineData(HttpDiagnosticSeverity.Warning, "Warning")]
    public void ToString_IncludesSeverityName(HttpDiagnosticSeverity severity, string expectedPrefix)
    {
        var diagnostic = new HttpDiagnostic(
            severity,
            "Test message",
            SourceSpan.Empty);

        var result = diagnostic.ToString();

        Assert.StartsWith(expectedPrefix, result);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var span = new SourceSpan(1, 2, 3, 4, 5, 6);
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Error,
            "Test message",
            span,
            code: "TEST001");

        Assert.Equal(HttpDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Test message", diagnostic.Message);
        Assert.Equal(span, diagnostic.Span);
        Assert.Equal("TEST001", diagnostic.Code);
    }

    [Fact]
    public void Constructor_CodeDefaultsToNull()
    {
        var diagnostic = new HttpDiagnostic(
            HttpDiagnosticSeverity.Warning,
            "Test message",
            SourceSpan.Empty);

        Assert.Null(diagnostic.Code);
    }
}

public class HttpDocumentItemSpanTests
{
    [Fact]
    public void Comment_Span_ReturnsCorrectSpan()
    {
        var span = new SourceSpan(1, 1, 1, 20, 0, 20);
        var comment = new Comment("This is a comment", span);

        Assert.Equal(span, comment.Span);
        Assert.Equal(1, comment.Span.StartLine);
        Assert.Equal(1, comment.Span.StartColumn);
        Assert.Equal(20, comment.Span.Length);
    }

    [Fact]
    public void FileVariable_Span_ReturnsCorrectSpan()
    {
        var span = new SourceSpan(5, 1, 5, 30, 100, 130);
        var fileVar = new FileVariable("baseUrl", "https://example.com", span);

        Assert.Equal(span, fileVar.Span);
        Assert.Equal(5, fileVar.Span.StartLine);
        Assert.Equal(30, fileVar.Span.Length);
    }

    [Fact]
    public void HttpRequest_Span_ReturnsCorrectSpan()
    {
        var span = new SourceSpan(10, 1, 15, 1, 200, 350);
        var request = new HttpRequest(
            method: "POST",
            rawUrl: "https://api.example.com/users",
            httpVersion: null,
            name: null,
            headers: new List<HttpHeader>(),
            body: null,
            directives: new List<HttpDirective>(),
            leadingComments: new List<Comment>(),
            span: span);

        Assert.Equal(span, request.Span);
        Assert.Equal(10, request.Span.StartLine);
        Assert.Equal(15, request.Span.EndLine);
        Assert.Equal(150, request.Span.Length);
    }
}

public class HttpRequestBodyTests
{
    [Fact]
    public void TextBody_Span_ReturnsCorrectSpan()
    {
        var span = new SourceSpan(5, 1, 8, 1, 50, 100);
        var body = new TextBody("{\"name\": \"John\"}", span);

        Assert.Equal(span, body.Span);
        Assert.Equal(50, body.Span.Length);
    }

    [Fact]
    public void TextBody_Content_ReturnsContent()
    {
        var content = "{\"key\": \"value\"}";
        var body = new TextBody(content, SourceSpan.Empty);

        Assert.Equal(content, body.Content);
    }

    [Fact]
    public void FileReferenceBody_Span_ReturnsCorrectSpan()
    {
        var span = new SourceSpan(10, 1, 10, 20, 200, 220);
        var body = new FileReferenceBody("./data.json", null, false, span);

        Assert.Equal(span, body.Span);
        Assert.Equal(20, body.Span.Length);
    }

    [Fact]
    public void FileReferenceBody_AllProperties_SetCorrectly()
    {
        var span = new SourceSpan(1, 1, 1, 25, 0, 25);
        var body = new FileReferenceBody("./template.json", "utf-8", true, span);

        Assert.Equal("./template.json", body.FilePath);
        Assert.Equal("utf-8", body.Encoding);
        Assert.True(body.ProcessVariables);
        Assert.Equal(span, body.Span);
    }

    [Fact]
    public void FileReferenceBody_NullEncoding_Allowed()
    {
        var body = new FileReferenceBody("./data.bin", null, false, SourceSpan.Empty);

        Assert.Null(body.Encoding);
    }

    [Fact]
    public void MultipartBody_Span_ReturnsCorrectSpan()
    {
        var span = new SourceSpan(3, 1, 20, 1, 30, 300);
        var body = new MultipartBody("boundary123", new List<MultipartSection>(), span);

        Assert.Equal(span, body.Span);
        Assert.Equal(270, body.Span.Length);
    }

    [Fact]
    public void MultipartBody_Boundary_ReturnsCorrectBoundary()
    {
        var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
        var body = new MultipartBody(boundary, new List<MultipartSection>(), SourceSpan.Empty);

        Assert.Equal(boundary, body.Boundary);
    }

    [Fact]
    public void MultipartBody_Sections_ReturnsAllSections()
    {
        var sections = new List<MultipartSection>
        {
            new(new List<HttpHeader>(), null, SourceSpan.Empty),
            new(new List<HttpHeader>(), new TextBody("content", SourceSpan.Empty), SourceSpan.Empty)
        };
        var body = new MultipartBody("boundary", sections, SourceSpan.Empty);

        Assert.Equal(2, body.Sections.Count);
    }
}

public class HttpDirectiveTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var span = new SourceSpan(1, 3, 1, 25, 3, 25);
        var directive = new HttpDirective("name", "myRequest", span);

        Assert.Equal("name", directive.Name);
        Assert.Equal("myRequest", directive.Value);
        Assert.Equal(span, directive.Span);
    }

    [Fact]
    public void Constructor_WithNullValue_Allowed()
    {
        var directive = new HttpDirective("no-redirect", null, SourceSpan.Empty);

        Assert.Equal("no-redirect", directive.Name);
        Assert.Null(directive.Value);
    }

    [Fact]
    public void Constructor_WithEmptyValue_Allowed()
    {
        var directive = new HttpDirective("note", "", SourceSpan.Empty);

        Assert.Equal("", directive.Value);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("no-redirect")]
    [InlineData("note")]
    [InlineData("no-cookie-jar")]
    [InlineData("prompt")]
    public void WellKnown_Constants_MatchExpectedValues(string expected)
    {
        var wellKnownValue = expected switch
        {
            "name" => HttpDirective.WellKnown.Name,
            "no-redirect" => HttpDirective.WellKnown.NoRedirect,
            "note" => HttpDirective.WellKnown.Note,
            "no-cookie-jar" => HttpDirective.WellKnown.NoCookieJar,
            "prompt" => HttpDirective.WellKnown.Prompt,
            _ => throw new ArgumentException()
        };

        Assert.Equal(expected, wellKnownValue);
    }
}

public class MultipartSectionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var headers = new List<HttpHeader>
        {
            new("Content-Disposition", "form-data; name=\"field1\"", SourceSpan.Empty)
        };
        var body = new TextBody("field value", SourceSpan.Empty);
        var span = new SourceSpan(5, 1, 8, 1, 50, 100);

        var section = new MultipartSection(headers, body, span);

        Assert.Single(section.Headers);
        Assert.Equal("Content-Disposition", section.Headers[0].Name);
        Assert.NotNull(section.Body);
        Assert.IsType<TextBody>(section.Body);
        Assert.Equal(span, section.Span);
    }

    [Fact]
    public void Constructor_WithNullBody_Allowed()
    {
        var headers = new List<HttpHeader>
        {
            new("Content-Disposition", "form-data; name=\"empty\"", SourceSpan.Empty)
        };

        var section = new MultipartSection(headers, null, SourceSpan.Empty);

        Assert.Null(section.Body);
    }

    [Fact]
    public void Constructor_WithEmptyHeaders_Allowed()
    {
        var section = new MultipartSection(
            new List<HttpHeader>(),
            new TextBody("content", SourceSpan.Empty),
            SourceSpan.Empty);

        Assert.Empty(section.Headers);
    }

    [Fact]
    public void Constructor_WithMultipleHeaders_AllPreserved()
    {
        var headers = new List<HttpHeader>
        {
            new("Content-Disposition", "form-data; name=\"file\"; filename=\"test.txt\"", SourceSpan.Empty),
            new("Content-Type", "text/plain", SourceSpan.Empty),
            new("Content-Transfer-Encoding", "binary", SourceSpan.Empty)
        };

        var section = new MultipartSection(headers, null, SourceSpan.Empty);

        Assert.Equal(3, section.Headers.Count);
        Assert.Equal("Content-Disposition", section.Headers[0].Name);
        Assert.Equal("Content-Type", section.Headers[1].Name);
        Assert.Equal("Content-Transfer-Encoding", section.Headers[2].Name);
    }

    [Fact]
    public void Constructor_WithFileReferenceBody_SetsCorrectly()
    {
        var headers = new List<HttpHeader>
        {
            new("Content-Disposition", "form-data; name=\"document\"", SourceSpan.Empty)
        };
        var body = new FileReferenceBody("./document.pdf", null, false, SourceSpan.Empty);

        var section = new MultipartSection(headers, body, SourceSpan.Empty);

        Assert.IsType<FileReferenceBody>(section.Body);
        var fileBody = (FileReferenceBody)section.Body!;
        Assert.Equal("./document.pdf", fileBody.FilePath);
    }

    [Fact]
    public void Span_ReturnsCorrectSourceSpan()
    {
        var span = new SourceSpan(10, 1, 15, 30, 200, 350);
        var section = new MultipartSection(new List<HttpHeader>(), null, span);

        Assert.Equal(10, section.Span.StartLine);
        Assert.Equal(15, section.Span.EndLine);
        Assert.Equal(150, section.Span.Length);
    }
}

public class TokenTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var token = new Token(TokenType.RequestLine, "GET https://example.com", 1, 1, 0, 23);

        Assert.Equal(TokenType.RequestLine, token.Type);
        Assert.Equal("GET https://example.com", token.Text);
        Assert.Equal(1, token.Line);
        Assert.Equal(1, token.Column);
        Assert.Equal(0, token.StartOffset);
        Assert.Equal(23, token.EndOffset);
    }

    [Fact]
    public void Length_ReturnsEndOffsetMinusStartOffset()
    {
        var token = new Token(TokenType.Header, "Content-Type: application/json", 2, 1, 25, 55);

        Assert.Equal(30, token.Length);
    }

    [Fact]
    public void Length_WhenEmpty_ReturnsZero()
    {
        var token = new Token(TokenType.BlankLine, "", 3, 1, 100, 100);

        Assert.Equal(0, token.Length);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var token = new Token(TokenType.RequestLine, "GET /api", 1, 1, 0, 8);

        var result = token.ToString();

        Assert.Equal("RequestLine(1,1): \"GET /api\"", result);
    }

    [Fact]
    public void ToString_EscapesNewlines()
    {
        var token = new Token(TokenType.BodyLine, "line1\r\nline2", 5, 1, 100, 113);

        var result = token.ToString();

        Assert.Equal("BodyLine(5,1): \"line1\\r\\nline2\"", result);
    }

    [Fact]
    public void ToString_EscapesCarriageReturn()
    {
        var token = new Token(TokenType.BodyLine, "text\rwith\rcr", 1, 1, 0, 13);

        var result = token.ToString();

        Assert.Contains("\\r", result);
    }

    [Fact]
    public void ToString_EscapesLineFeed()
    {
        var token = new Token(TokenType.BodyLine, "text\nwith\nlf", 1, 1, 0, 13);

        var result = token.ToString();

        Assert.Contains("\\n", result);
    }

    [Theory]
    [InlineData(TokenType.RequestDelimiter, "### Request")]
    [InlineData(TokenType.Comment, "# This is a comment")]
    [InlineData(TokenType.VariableDefinition, "@baseUrl = https://api.com")]
    [InlineData(TokenType.Directive, "# @name myRequest")]
    [InlineData(TokenType.RequestLine, "POST https://api.com/users")]
    [InlineData(TokenType.Header, "Authorization: Bearer token")]
    [InlineData(TokenType.BodyLine, "{\"key\": \"value\"}")]
    [InlineData(TokenType.BlankLine, "")]
    [InlineData(TokenType.EOF, "")]
    public void AllTokenTypes_CanBeCreated(TokenType type, string text)
    {
        var token = new Token(type, text, 1, 1, 0, text.Length);

        Assert.Equal(type, token.Type);
        Assert.Equal(text, token.Text);
    }

    [Fact]
    public void Token_WithLargeOffset_HandlesCorrectly()
    {
        var token = new Token(TokenType.BodyLine, "content", 1000, 50, 999999, 1000006);

        Assert.Equal(1000, token.Line);
        Assert.Equal(50, token.Column);
        Assert.Equal(999999, token.StartOffset);
        Assert.Equal(1000006, token.EndOffset);
        Assert.Equal(7, token.Length);
    }

    [Fact]
    public void Token_AtLineZero_HandlesBoundaryCase()
    {
        // While typically lines are 1-based, test that 0 is handled
        var token = new Token(TokenType.EOF, "", 0, 0, 0, 0);

        Assert.Equal(0, token.Line);
        Assert.Equal(0, token.Column);
    }

    [Fact]
    public void ToString_IncludesTypeLineAndColumn()
    {
        var token = new Token(TokenType.Header, "Accept: */*", 5, 3, 100, 111);

        var result = token.ToString();

        Assert.Contains("Header", result);
        Assert.Contains("5,3", result);
        Assert.Contains("Accept: */*", result);
    }
}
