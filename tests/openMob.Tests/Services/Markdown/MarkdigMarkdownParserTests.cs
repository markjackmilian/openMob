using openMob.Core.Services.Markdown;

namespace openMob.Tests.Services.Markdown;

/// <summary>
/// Unit tests for <see cref="MarkdigMarkdownParser"/>.
/// Covers all block and inline node types produced by the parser.
/// </summary>
public sealed class MarkdigMarkdownParserTests
{
    private readonly MarkdigMarkdownParser _sut = new();

    // ─── Empty / null input ──────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEmptyString_ReturnsEmptyDocument()
    {
        // Act
        var result = _sut.Parse(string.Empty);

        // Assert
        result.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhenNullInput_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Plain text ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenPlainText_ReturnsSingleParagraphWithTextNode()
    {
        // Arrange
        var markdown = "Hello world";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var paragraph = result.Blocks[0].Should().BeOfType<ParagraphNode>().Subject;
        paragraph.Inlines.Should().ContainSingle();
        paragraph.Inlines[0].Should().BeOfType<TextNode>()
            .Which.Text.Should().Be("Hello world");
    }

    // ─── Headings ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenH1Header_ReturnsHeadingNodeWithLevel1()
    {
        // Arrange
        var markdown = "# Hello World";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var heading = result.Blocks[0].Should().BeOfType<HeadingNode>().Subject;
        heading.Level.Should().Be(1);
        heading.Inlines.Should().ContainSingle();
        heading.Inlines[0].Should().BeOfType<TextNode>()
            .Which.Text.Should().Be("Hello World");
    }

    [Theory]
    [InlineData("## Heading 2", 2)]
    [InlineData("### Heading 3", 3)]
    [InlineData("#### Heading 4", 4)]
    [InlineData("##### Heading 5", 5)]
    [InlineData("###### Heading 6", 6)]
    public void Parse_WhenH2ThroughH6_ReturnsCorrectHeadingLevels(string markdown, int expectedLevel)
    {
        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        result.Blocks[0].Should().BeOfType<HeadingNode>()
            .Which.Level.Should().Be(expectedLevel);
    }

    // ─── Emphasis ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenBoldText_ReturnsEmphasisNodeWithIsBoldTrue()
    {
        // Arrange
        var markdown = "**bold text**";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var paragraph = result.Blocks[0].Should().BeOfType<ParagraphNode>().Subject;
        paragraph.Inlines.Should().ContainSingle();
        var emphasis = paragraph.Inlines[0].Should().BeOfType<EmphasisNode>().Subject;
        emphasis.IsBold.Should().BeTrue();
        emphasis.IsItalic.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenItalicText_ReturnsEmphasisNodeWithIsItalicTrue()
    {
        // Arrange
        var markdown = "*italic text*";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var paragraph = result.Blocks[0].Should().BeOfType<ParagraphNode>().Subject;
        paragraph.Inlines.Should().ContainSingle();
        var emphasis = paragraph.Inlines[0].Should().BeOfType<EmphasisNode>().Subject;
        emphasis.IsItalic.Should().BeTrue();
        emphasis.IsBold.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenBoldItalicText_ReturnsNestedEmphasisNodes()
    {
        // Arrange — Markdig parses ***text*** as nested emphasis:
        // outer italic (*) wrapping inner bold (**)
        var markdown = "***bold and italic***";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var paragraph = result.Blocks[0].Should().BeOfType<ParagraphNode>().Subject;
        paragraph.Inlines.Should().ContainSingle();
        var outerEmphasis = paragraph.Inlines[0].Should().BeOfType<EmphasisNode>().Subject;
        // Outer is italic (delimiter count 1)
        outerEmphasis.IsItalic.Should().BeTrue();
        // Inner is bold (delimiter count 2)
        outerEmphasis.Inlines.Should().ContainSingle();
        var innerEmphasis = outerEmphasis.Inlines[0].Should().BeOfType<EmphasisNode>().Subject;
        innerEmphasis.IsBold.Should().BeTrue();
    }

    // ─── Inline code ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenInlineCode_ReturnsCodeInlineNode()
    {
        // Arrange
        var markdown = "Use `Console.WriteLine` here";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var paragraph = result.Blocks[0].Should().BeOfType<ParagraphNode>().Subject;
        paragraph.Inlines.Should().Contain(i => i is CodeInlineNode);
        var codeInline = paragraph.Inlines.OfType<CodeInlineNode>().First();
        codeInline.Code.Should().Be("Console.WriteLine");
    }

    // ─── Fenced code blocks ──────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenFencedCodeBlock_ReturnsCodeBlockNodeWithLanguage()
    {
        // Arrange
        var markdown = "```csharp\nvar x = 42;\n```";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var codeBlock = result.Blocks[0].Should().BeOfType<CodeBlockNode>().Subject;
        codeBlock.Language.Should().Be("csharp");
        codeBlock.Code.Should().Contain("var x = 42;");
    }

    [Fact]
    public void Parse_WhenFencedCodeBlockNoLanguage_ReturnsCodeBlockNodeWithNullLanguage()
    {
        // Arrange
        var markdown = "```\nsome code\n```";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var codeBlock = result.Blocks[0].Should().BeOfType<CodeBlockNode>().Subject;
        codeBlock.Language.Should().BeNull();
        codeBlock.Code.Should().Contain("some code");
    }

    // ─── Lists ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenUnorderedList_ReturnsListNodeWithIsOrderedFalse()
    {
        // Arrange
        var markdown = "- Item 1\n- Item 2\n- Item 3";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var list = result.Blocks[0].Should().BeOfType<ListNode>().Subject;
        list.IsOrdered.Should().BeFalse();
        list.Items.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_WhenOrderedList_ReturnsListNodeWithIsOrderedTrue()
    {
        // Arrange
        var markdown = "1. First\n2. Second\n3. Third";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var list = result.Blocks[0].Should().BeOfType<ListNode>().Subject;
        list.IsOrdered.Should().BeTrue();
        list.Items.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_WhenNestedList_ReturnsNestedListItemNodes()
    {
        // Arrange
        var markdown = "- Parent\n  - Child 1\n  - Child 2";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var list = result.Blocks[0].Should().BeOfType<ListNode>().Subject;
        // The parent item should contain a nested list
        var parentItem = list.Items[0];
        parentItem.Blocks.Should().Contain(b => b is ListNode);
    }

    // ─── Blockquote ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenBlockquote_ReturnsBlockquoteNode()
    {
        // Arrange
        var markdown = "> This is a quote";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var blockquote = result.Blocks[0].Should().BeOfType<BlockquoteNode>().Subject;
        blockquote.Blocks.Should().NotBeEmpty();
        blockquote.Blocks[0].Should().BeOfType<ParagraphNode>();
    }

    // ─── Table ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenTable_ReturnsTableNodeWithHeaderAndRows()
    {
        // Arrange
        var markdown = "| Name | Age |\n|------|-----|\n| Alice | 30 |\n| Bob | 25 |";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var table = result.Blocks[0].Should().BeOfType<TableNode>().Subject;
        table.Header.Cells.Should().HaveCount(2);
        table.Rows.Should().HaveCount(2);
    }

    // ─── Link ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenLink_ReturnsLinkNodeWithUrlAndInlines()
    {
        // Arrange
        var markdown = "[Click here](https://example.com)";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().ContainSingle();
        var paragraph = result.Blocks[0].Should().BeOfType<ParagraphNode>().Subject;
        paragraph.Inlines.Should().ContainSingle();
        var link = paragraph.Inlines[0].Should().BeOfType<LinkNode>().Subject;
        link.Url.Should().Be("https://example.com");
        link.Inlines.Should().ContainSingle();
        link.Inlines[0].Should().BeOfType<TextNode>()
            .Which.Text.Should().Be("Click here");
    }

    // ─── Thematic break ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenHorizontalRule_ReturnsThematicBreakNode()
    {
        // Arrange
        var markdown = "Above\n\n---\n\nBelow";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().Contain(b => b is ThematicBreakNode);
    }

    // ─── Mixed content ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenMixedContent_ReturnsCorrectNodeSequence()
    {
        // Arrange
        var markdown = "# Title\n\nSome text\n\n```python\nprint('hello')\n```\n\n- Item 1\n- Item 2";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.Blocks.Should().HaveCountGreaterOrEqualTo(4);
        result.Blocks[0].Should().BeOfType<HeadingNode>();
        result.Blocks[1].Should().BeOfType<ParagraphNode>();
        result.Blocks[2].Should().BeOfType<CodeBlockNode>();
        result.Blocks[3].Should().BeOfType<ListNode>();
    }
}
