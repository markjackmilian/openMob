using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace openMob.Core.Services.Markdown;

/// <summary>
/// Parses Markdown text into a platform-agnostic <see cref="MarkdownDocument"/> AST
/// using the Markdig library. Walks the Markdig AST and converts each node to the
/// simplified <see cref="MarkdownNode"/> hierarchy for consumption by MAUI renderers.
/// </summary>
/// <remarks>
/// This class is <c>internal sealed</c> — visible to tests via <c>InternalsVisibleTo</c>.
/// The pipeline enables advanced extensions (pipe tables, etc.).
/// </remarks>
internal sealed class MarkdigMarkdownParser : IMarkdownParser
{
    /// <summary>The Markdig pipeline configured with advanced extensions.</summary>
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Initialises a new instance of <see cref="MarkdigMarkdownParser"/>
    /// with a pipeline that supports pipe tables and other advanced extensions.
    /// </summary>
    public MarkdigMarkdownParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <inheritdoc />
    public MarkdownDocument Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var markdigDoc = Markdig.Markdown.Parse(markdown, _pipeline);
        var blocks = ConvertBlocks(markdigDoc);
        return new MarkdownDocument(blocks);
    }

    /// <summary>
    /// Converts a sequence of Markdig block elements to our AST block nodes.
    /// </summary>
    /// <param name="container">The Markdig container block to walk.</param>
    /// <returns>A read-only list of converted block nodes.</returns>
    private IReadOnlyList<MarkdownBlock> ConvertBlocks(ContainerBlock container)
    {
        var blocks = new List<MarkdownBlock>();

        foreach (var block in container)
        {
            var converted = ConvertBlock(block);
            if (converted is not null)
                blocks.Add(converted);
        }

        return blocks;
    }

    /// <summary>
    /// Converts a single Markdig block to our AST block node.
    /// Returns <c>null</c> for unsupported block types.
    /// </summary>
    /// <param name="block">The Markdig block to convert.</param>
    /// <returns>The converted block node, or <c>null</c> if unsupported.</returns>
    private MarkdownBlock? ConvertBlock(Block block)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading),
            ParagraphBlock paragraph => ConvertParagraph(paragraph),
            FencedCodeBlock fencedCode => ConvertFencedCodeBlock(fencedCode),
            CodeBlock codeBlock => ConvertCodeBlock(codeBlock),
            QuoteBlock quote => ConvertQuoteBlock(quote),
            ListBlock list => ConvertListBlock(list),
            ListItemBlock listItem => ConvertListItemBlock(listItem),
            Table table => ConvertTable(table),
            ThematicBreakBlock => new ThematicBreakNode(),
            _ => null, // Unsupported block types are silently skipped
        };
    }

    /// <summary>Converts a Markdig <see cref="HeadingBlock"/> to a <see cref="HeadingNode"/>.</summary>
    private HeadingNode ConvertHeading(HeadingBlock heading)
    {
        var inlines = heading.Inline is not null
            ? ConvertInlines(heading.Inline)
            : Array.Empty<MarkdownInline>();

        return new HeadingNode(heading.Level, inlines);
    }

    /// <summary>Converts a Markdig <see cref="ParagraphBlock"/> to a <see cref="ParagraphNode"/>.</summary>
    private ParagraphNode ConvertParagraph(ParagraphBlock paragraph)
    {
        var inlines = paragraph.Inline is not null
            ? ConvertInlines(paragraph.Inline)
            : Array.Empty<MarkdownInline>();

        return new ParagraphNode(inlines);
    }

    /// <summary>Converts a Markdig <see cref="FencedCodeBlock"/> to a <see cref="CodeBlockNode"/>.</summary>
    private static CodeBlockNode ConvertFencedCodeBlock(FencedCodeBlock fencedCode)
    {
        var language = string.IsNullOrWhiteSpace(fencedCode.Info) ? null : fencedCode.Info;
        var code = ExtractCodeBlockText(fencedCode);
        return new CodeBlockNode(language, code);
    }

    /// <summary>Converts a generic Markdig <see cref="CodeBlock"/> (indented) to a <see cref="CodeBlockNode"/>.</summary>
    private static CodeBlockNode ConvertCodeBlock(CodeBlock codeBlock)
    {
        var code = ExtractCodeBlockText(codeBlock);
        return new CodeBlockNode(null, code);
    }

    /// <summary>Extracts the text content from a Markdig code block's lines.</summary>
    private static string ExtractCodeBlockText(LeafBlock codeBlock)
    {
        if (codeBlock.Lines.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < codeBlock.Lines.Count; i++)
        {
            var line = codeBlock.Lines.Lines[i];
            sb.Append(line.Slice);
            if (i < codeBlock.Lines.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Converts a Markdig <see cref="QuoteBlock"/> to a <see cref="BlockquoteNode"/>.</summary>
    private BlockquoteNode ConvertQuoteBlock(QuoteBlock quote)
    {
        var blocks = ConvertBlocks(quote);
        return new BlockquoteNode(blocks);
    }

    /// <summary>Converts a Markdig <see cref="ListBlock"/> to a <see cref="ListNode"/>.</summary>
    private ListNode ConvertListBlock(ListBlock list)
    {
        var items = new List<ListItemNode>();

        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                items.Add(ConvertListItemBlock(listItem));
            }
        }

        return new ListNode(list.IsOrdered, items);
    }

    /// <summary>Converts a Markdig <see cref="ListItemBlock"/> to a <see cref="ListItemNode"/>.</summary>
    private ListItemNode ConvertListItemBlock(ListItemBlock listItem)
    {
        var blocks = ConvertBlocks(listItem);
        return new ListItemNode(blocks);
    }

    /// <summary>Converts a Markdig <see cref="Table"/> to a <see cref="TableNode"/>.</summary>
    private TableNode ConvertTable(Table table)
    {
        TableRowNode? header = null;
        var rows = new List<TableRowNode>();

        foreach (var row in table)
        {
            if (row is TableRow tableRow)
            {
                var convertedRow = ConvertTableRow(tableRow);
                if (tableRow.IsHeader)
                    header = convertedRow;
                else
                    rows.Add(convertedRow);
            }
        }

        // If no header was found, create an empty one
        header ??= new TableRowNode(Array.Empty<TableCellNode>());

        return new TableNode(header, rows);
    }

    /// <summary>Converts a Markdig <see cref="TableRow"/> to a <see cref="TableRowNode"/>.</summary>
    private TableRowNode ConvertTableRow(TableRow row)
    {
        var cells = new List<TableCellNode>();

        foreach (var cell in row)
        {
            if (cell is TableCell tableCell)
            {
                cells.Add(ConvertTableCell(tableCell));
            }
        }

        return new TableRowNode(cells);
    }

    /// <summary>Converts a Markdig <see cref="TableCell"/> to a <see cref="TableCellNode"/>.</summary>
    private TableCellNode ConvertTableCell(TableCell cell)
    {
        // A TableCell is a ContainerBlock that may contain ParagraphBlocks.
        // We extract inlines from the first paragraph if present.
        var inlines = new List<MarkdownInline>();

        foreach (var block in cell)
        {
            if (block is ParagraphBlock paragraph && paragraph.Inline is not null)
            {
                inlines.AddRange(ConvertInlines(paragraph.Inline));
            }
        }

        return new TableCellNode(inlines);
    }

    /// <summary>
    /// Converts a Markdig inline container to our AST inline nodes.
    /// </summary>
    /// <param name="container">The Markdig inline container to walk.</param>
    /// <returns>A read-only list of converted inline nodes.</returns>
    private IReadOnlyList<MarkdownInline> ConvertInlines(ContainerInline container)
    {
        var inlines = new List<MarkdownInline>();

        var current = container.FirstChild;
        while (current is not null)
        {
            var converted = ConvertInline(current);
            if (converted is not null)
                inlines.Add(converted);

            current = current.NextSibling;
        }

        return inlines;
    }

    /// <summary>
    /// Converts a single Markdig inline to our AST inline node.
    /// Returns <c>null</c> for unsupported inline types (e.g. line breaks).
    /// </summary>
    /// <param name="inline">The Markdig inline to convert.</param>
    /// <returns>The converted inline node, or <c>null</c> if unsupported.</returns>
    private MarkdownInline? ConvertInline(Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => new TextNode(literal.Content.ToString()),
            EmphasisInline emphasis => ConvertEmphasis(emphasis),
            CodeInline code => new CodeInlineNode(code.Content),
            LinkInline link => ConvertLink(link),
            LineBreakInline => new TextNode("\n"),
            _ => null, // Unsupported inline types are silently skipped
        };
    }

    /// <summary>
    /// Converts a Markdig <see cref="EmphasisInline"/> to an <see cref="EmphasisNode"/>.
    /// Determines bold/italic from the delimiter character and count.
    /// </summary>
    private EmphasisNode ConvertEmphasis(EmphasisInline emphasis)
    {
        // DelimiterChar is '*' or '_', DelimiterCount determines emphasis level:
        // count 1 = italic, count 2 = bold, count >= 3 = bold+italic
        var isBold = emphasis.DelimiterCount >= 2;
        var isItalic = emphasis.DelimiterCount == 1 || emphasis.DelimiterCount >= 3;

        var inlines = ConvertInlines(emphasis);
        return new EmphasisNode(isBold, isItalic, inlines);
    }

    /// <summary>Converts a Markdig <see cref="LinkInline"/> to a <see cref="LinkNode"/>.</summary>
    private LinkNode ConvertLink(LinkInline link)
    {
        var inlines = ConvertInlines(link);
        return new LinkNode(link.Url ?? string.Empty, link.Title, inlines);
    }
}
