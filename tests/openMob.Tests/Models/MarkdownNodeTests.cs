using openMob.Core.Services.Markdown;

namespace openMob.Tests.Models;

/// <summary>
/// Unit tests for the Markdown AST node record types.
/// Verifies construction, property access, and record equality.
/// </summary>
public sealed class MarkdownNodeTests
{
    // ─── MarkdownDocument ────────────────────────────────────────────────────

    [Fact]
    public void MarkdownDocument_WhenCreated_HasCorrectBlocks()
    {
        // Arrange
        var blocks = new List<MarkdownBlock>
        {
            new ParagraphNode(Array.Empty<MarkdownInline>()),
            new ThematicBreakNode(),
        };

        // Act
        var doc = new MarkdownDocument(blocks);

        // Assert
        doc.Blocks.Should().HaveCount(2);
        doc.Blocks[0].Should().BeOfType<ParagraphNode>();
        doc.Blocks[1].Should().BeOfType<ThematicBreakNode>();
    }

    [Fact]
    public void MarkdownDocument_WhenCreatedWithEmptyBlocks_HasEmptyCollection()
    {
        // Act
        var doc = new MarkdownDocument(Array.Empty<MarkdownBlock>());

        // Assert
        doc.Blocks.Should().BeEmpty();
    }

    // ─── HeadingNode ─────────────────────────────────────────────────────────

    [Fact]
    public void HeadingNode_WhenCreated_HasCorrectLevelAndInlines()
    {
        // Arrange
        var inlines = new List<MarkdownInline> { new TextNode("Title") };

        // Act
        var heading = new HeadingNode(2, inlines);

        // Assert
        heading.Level.Should().Be(2);
        heading.Inlines.Should().ContainSingle();
        heading.Inlines[0].Should().BeOfType<TextNode>()
            .Which.Text.Should().Be("Title");
    }

    // ─── ParagraphNode ───────────────────────────────────────────────────────

    [Fact]
    public void ParagraphNode_WhenCreated_HasCorrectInlines()
    {
        // Arrange
        var inlines = new List<MarkdownInline>
        {
            new TextNode("Hello "),
            new EmphasisNode(true, false, new List<MarkdownInline> { new TextNode("world") }),
        };

        // Act
        var paragraph = new ParagraphNode(inlines);

        // Assert
        paragraph.Inlines.Should().HaveCount(2);
        paragraph.Inlines[0].Should().BeOfType<TextNode>();
        paragraph.Inlines[1].Should().BeOfType<EmphasisNode>();
    }

    // ─── CodeBlockNode ───────────────────────────────────────────────────────

    [Fact]
    public void CodeBlockNode_WhenCreated_HasCorrectLanguageAndCode()
    {
        // Act
        var codeBlock = new CodeBlockNode("csharp", "var x = 42;");

        // Assert
        codeBlock.Language.Should().Be("csharp");
        codeBlock.Code.Should().Be("var x = 42;");
    }

    [Fact]
    public void CodeBlockNode_WhenCreatedWithNullLanguage_HasNullLanguage()
    {
        // Act
        var codeBlock = new CodeBlockNode(null, "some code");

        // Assert
        codeBlock.Language.Should().BeNull();
        codeBlock.Code.Should().Be("some code");
    }

    // ─── TextNode ────────────────────────────────────────────────────────────

    [Fact]
    public void TextNode_WhenCreated_HasCorrectText()
    {
        // Act
        var textNode = new TextNode("Hello world");

        // Assert
        textNode.Text.Should().Be("Hello world");
    }

    // ─── EmphasisNode ────────────────────────────────────────────────────────

    [Fact]
    public void EmphasisNode_WhenBold_HasIsBoldTrue()
    {
        // Act
        var emphasis = new EmphasisNode(true, false, Array.Empty<MarkdownInline>());

        // Assert
        emphasis.IsBold.Should().BeTrue();
        emphasis.IsItalic.Should().BeFalse();
    }

    [Fact]
    public void EmphasisNode_WhenItalic_HasIsItalicTrue()
    {
        // Act
        var emphasis = new EmphasisNode(false, true, Array.Empty<MarkdownInline>());

        // Assert
        emphasis.IsItalic.Should().BeTrue();
        emphasis.IsBold.Should().BeFalse();
    }

    [Fact]
    public void EmphasisNode_WhenBoldAndItalic_HasBothTrue()
    {
        // Act
        var emphasis = new EmphasisNode(true, true, Array.Empty<MarkdownInline>());

        // Assert
        emphasis.IsBold.Should().BeTrue();
        emphasis.IsItalic.Should().BeTrue();
    }

    // ─── CodeInlineNode ──────────────────────────────────────────────────────

    [Fact]
    public void CodeInlineNode_WhenCreated_HasCorrectCode()
    {
        // Act
        var codeInline = new CodeInlineNode("Console.WriteLine");

        // Assert
        codeInline.Code.Should().Be("Console.WriteLine");
    }

    // ─── LinkNode ────────────────────────────────────────────────────────────

    [Fact]
    public void LinkNode_WhenCreated_HasCorrectUrlAndTitle()
    {
        // Arrange
        var inlines = new List<MarkdownInline> { new TextNode("Click here") };

        // Act
        var link = new LinkNode("https://example.com", "Example", inlines);

        // Assert
        link.Url.Should().Be("https://example.com");
        link.Title.Should().Be("Example");
        link.Inlines.Should().ContainSingle();
    }

    [Fact]
    public void LinkNode_WhenCreatedWithNullTitle_HasNullTitle()
    {
        // Act
        var link = new LinkNode("https://example.com", null, Array.Empty<MarkdownInline>());

        // Assert
        link.Title.Should().BeNull();
    }

    // ─── ListNode ────────────────────────────────────────────────────────────

    [Fact]
    public void ListNode_WhenOrdered_HasIsOrderedTrue()
    {
        // Arrange
        var items = new List<ListItemNode>
        {
            new(new List<MarkdownBlock> { new ParagraphNode(Array.Empty<MarkdownInline>()) }),
        };

        // Act
        var list = new ListNode(true, items);

        // Assert
        list.IsOrdered.Should().BeTrue();
        list.Items.Should().ContainSingle();
    }

    [Fact]
    public void ListNode_WhenUnordered_HasIsOrderedFalse()
    {
        // Act
        var list = new ListNode(false, Array.Empty<ListItemNode>());

        // Assert
        list.IsOrdered.Should().BeFalse();
    }

    // ─── TableNode ───────────────────────────────────────────────────────────

    [Fact]
    public void TableNode_WhenCreated_HasHeaderAndRows()
    {
        // Arrange
        var headerCells = new List<TableCellNode>
        {
            new(new List<MarkdownInline> { new TextNode("Name") }),
            new(new List<MarkdownInline> { new TextNode("Age") }),
        };
        var header = new TableRowNode(headerCells);

        var rowCells = new List<TableCellNode>
        {
            new(new List<MarkdownInline> { new TextNode("Alice") }),
            new(new List<MarkdownInline> { new TextNode("30") }),
        };
        var rows = new List<TableRowNode> { new(rowCells) };

        // Act
        var table = new TableNode(header, rows);

        // Assert
        table.Header.Cells.Should().HaveCount(2);
        table.Rows.Should().ContainSingle();
        table.Rows[0].Cells.Should().HaveCount(2);
    }

    // ─── BlockquoteNode ──────────────────────────────────────────────────────

    [Fact]
    public void BlockquoteNode_WhenCreated_HasCorrectBlocks()
    {
        // Arrange
        var blocks = new List<MarkdownBlock>
        {
            new ParagraphNode(new List<MarkdownInline> { new TextNode("Quote text") }),
        };

        // Act
        var blockquote = new BlockquoteNode(blocks);

        // Assert
        blockquote.Blocks.Should().ContainSingle();
        blockquote.Blocks[0].Should().BeOfType<ParagraphNode>();
    }

    // ─── ThematicBreakNode ───────────────────────────────────────────────────

    [Fact]
    public void ThematicBreakNode_WhenCreated_IsCorrectType()
    {
        // Act
        var thematicBreak = new ThematicBreakNode();

        // Assert
        thematicBreak.Should().BeOfType<ThematicBreakNode>();
        thematicBreak.Should().BeAssignableTo<MarkdownBlock>();
    }

    // ─── Record equality ─────────────────────────────────────────────────────

    [Fact]
    public void TextNode_WithSameText_AreEqual()
    {
        // Arrange
        var node1 = new TextNode("Hello");
        var node2 = new TextNode("Hello");

        // Assert
        node1.Should().Be(node2);
    }

    [Fact]
    public void TextNode_WithDifferentText_AreNotEqual()
    {
        // Arrange
        var node1 = new TextNode("Hello");
        var node2 = new TextNode("World");

        // Assert
        node1.Should().NotBe(node2);
    }

    [Fact]
    public void CodeBlockNode_WithSameValues_AreEqual()
    {
        // Arrange
        var node1 = new CodeBlockNode("csharp", "var x = 1;");
        var node2 = new CodeBlockNode("csharp", "var x = 1;");

        // Assert
        node1.Should().Be(node2);
    }
}
