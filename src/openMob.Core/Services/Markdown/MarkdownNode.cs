namespace openMob.Core.Services.Markdown;

/// <summary>Base type for all Markdown AST nodes.</summary>
public abstract record MarkdownNode;

/// <summary>Base type for block-level Markdown elements.</summary>
public abstract record MarkdownBlock : MarkdownNode;

/// <summary>Base type for inline Markdown elements.</summary>
public abstract record MarkdownInline : MarkdownNode;

/// <summary>Root document node containing all top-level blocks.</summary>
/// <param name="Blocks">The top-level block elements in the document.</param>
public sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownNode;

// ─── Block Nodes ──────────────────────────────────────────────────────────────

/// <summary>A heading block (h1–h6).</summary>
/// <param name="Level">The heading level (1–6).</param>
/// <param name="Inlines">The inline content of the heading.</param>
public sealed record HeadingNode(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>A paragraph block containing inline content.</summary>
/// <param name="Inlines">The inline content of the paragraph.</param>
public sealed record ParagraphNode(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>A fenced or indented code block.</summary>
/// <param name="Language">The optional language identifier (e.g. "csharp").</param>
/// <param name="Code">The raw code text content.</param>
public sealed record CodeBlockNode(string? Language, string Code) : MarkdownBlock;

/// <summary>A blockquote containing nested block elements.</summary>
/// <param name="Blocks">The block elements inside the blockquote.</param>
public sealed record BlockquoteNode(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>An ordered or unordered list.</summary>
/// <param name="IsOrdered">Whether this is an ordered (numbered) list.</param>
/// <param name="Items">The list items.</param>
public sealed record ListNode(bool IsOrdered, IReadOnlyList<ListItemNode> Items) : MarkdownBlock;

/// <summary>A single item in a list, containing nested block elements.</summary>
/// <param name="Blocks">The block elements inside this list item.</param>
public sealed record ListItemNode(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>A table with a header row and body rows.</summary>
/// <param name="Header">The header row.</param>
/// <param name="Rows">The body rows.</param>
public sealed record TableNode(TableRowNode Header, IReadOnlyList<TableRowNode> Rows) : MarkdownBlock;

/// <summary>A single row in a table.</summary>
/// <param name="Cells">The cells in this row.</param>
public sealed record TableRowNode(IReadOnlyList<TableCellNode> Cells) : MarkdownBlock;

/// <summary>A single cell in a table row.</summary>
/// <param name="Inlines">The inline content of this cell.</param>
public sealed record TableCellNode(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>A thematic break (horizontal rule).</summary>
public sealed record ThematicBreakNode() : MarkdownBlock;

// ─── Inline Nodes ─────────────────────────────────────────────────────────────

/// <summary>A plain text span.</summary>
/// <param name="Text">The text content.</param>
public sealed record TextNode(string Text) : MarkdownInline;

/// <summary>An emphasis span (bold, italic, or both).</summary>
/// <param name="IsBold">Whether the text is bold.</param>
/// <param name="IsItalic">Whether the text is italic.</param>
/// <param name="Inlines">The inline content within the emphasis.</param>
public sealed record EmphasisNode(bool IsBold, bool IsItalic, IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

/// <summary>An inline code span.</summary>
/// <param name="Code">The code text content.</param>
public sealed record CodeInlineNode(string Code) : MarkdownInline;

/// <summary>A hyperlink.</summary>
/// <param name="Url">The link URL.</param>
/// <param name="Title">The optional link title.</param>
/// <param name="Inlines">The inline content (link text).</param>
public sealed record LinkNode(string Url, string? Title, IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;
