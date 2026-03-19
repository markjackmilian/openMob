using openMob.Core.Services.Markdown;
using openMob.Helpers;

namespace openMob.Views.Controls.Markdown;

/// <summary>
/// Renders Markdown text as native MAUI views. Parses the input via
/// <see cref="IMarkdownParser"/> and walks the AST to build a visual tree
/// of Labels, Borders, Grids, and FormattedText spans.
/// </summary>
public partial class MarkdownView : ContentView
{
    private MarkdownDocument? _cachedDocument;
    private string? _cachedMarkdownText;

    /// <summary>
    /// Shared <see cref="IMarkdownParser"/> instance set once during app initialisation.
    /// The parser is stateless and thread-safe, so a single shared instance is appropriate.
    /// Set this from <c>ChatPage.OnAppearing</c> or <c>MauiProgram.cs</c> after DI is configured.
    /// </summary>
    internal static IMarkdownParser? SharedParser { get; set; }

    /// <summary>Bindable property for the raw Markdown text to render.</summary>
    public static readonly BindableProperty MarkdownTextProperty =
        BindableProperty.Create(nameof(MarkdownText), typeof(string), typeof(MarkdownView), string.Empty,
            propertyChanged: OnMarkdownTextChanged);

    /// <summary>Initialises the Markdown view.</summary>
    public MarkdownView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the raw Markdown text to render.</summary>
    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    private static void OnMarkdownTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MarkdownView view)
        {
            view.RenderMarkdown(newValue as string ?? string.Empty);
        }
    }

    /// <summary>
    /// Returns the shared <see cref="IMarkdownParser"/> instance.
    /// </summary>
    private static IMarkdownParser? ResolveParser()
    {
        return SharedParser;
    }

    /// <summary>
    /// Parses the Markdown text and renders native MAUI views into the container.
    /// </summary>
    /// <param name="markdown">The raw Markdown text.</param>
    private void RenderMarkdown(string markdown)
    {
        ContentContainer.Children.Clear();

        if (string.IsNullOrWhiteSpace(markdown))
            return;

        var parser = ResolveParser();
        if (parser is null)
        {
            // Fallback: render as plain text if parser is not available
            ContentContainer.Children.Add(CreatePlainTextLabel(markdown));
            return;
        }

        // Cache: only re-parse if text changed
        if (markdown != _cachedMarkdownText)
        {
            _cachedDocument = parser.Parse(markdown);
            _cachedMarkdownText = markdown;
        }

        if (_cachedDocument is null)
            return;

        foreach (var block in _cachedDocument.Blocks)
        {
            var view = RenderBlock(block);
            if (view is not null)
            {
                ContentContainer.Children.Add(view);
            }
        }
    }

    /// <summary>
    /// Renders a single block-level Markdown node to a MAUI View.
    /// </summary>
    /// <param name="block">The block node to render.</param>
    /// <returns>A MAUI View representing the block, or <c>null</c> if unsupported.</returns>
    private View? RenderBlock(MarkdownBlock block)
    {
        return block switch
        {
            HeadingNode heading => RenderHeading(heading),
            ParagraphNode paragraph => RenderParagraph(paragraph),
            CodeBlockNode codeBlock => RenderCodeBlock(codeBlock),
            BlockquoteNode blockquote => RenderBlockquote(blockquote),
            ListNode list => RenderList(list),
            TableNode table => RenderTable(table),
            ThematicBreakNode => RenderThematicBreak(),
            _ => null,
        };
    }

    /// <summary>Renders a heading node (h1-h6) as a styled Label.</summary>
    private static Label RenderHeading(HeadingNode heading)
    {
        var label = new Label
        {
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 12, 0, 8),
            TextColor = GetThemeColor("ColorOnBackgroundLight", "ColorOnBackgroundDark"),
        };

        // Set font size and family based on heading level
        switch (heading.Level)
        {
            case 1:
                label.FontSize = (double)Application.Current!.Resources["FontSizeTitle1"];
                label.FontFamily = "InterBold";
                break;
            case 2:
                label.FontSize = (double)Application.Current!.Resources["FontSizeTitle2"];
                label.FontFamily = "InterBold";
                break;
            case 3:
                label.FontSize = (double)Application.Current!.Resources["FontSizeTitle3"];
                label.FontFamily = "InterSemiBold";
                break;
            case 4:
                label.FontSize = (double)Application.Current!.Resources["FontSizeHeadline"];
                label.FontFamily = "InterSemiBold";
                break;
            case 5:
                label.FontSize = (double)Application.Current!.Resources["FontSizeSubheadline"];
                label.FontFamily = "InterMedium";
                break;
            default:
                label.FontSize = (double)Application.Current!.Resources["FontSizeFootnote"];
                label.FontFamily = "InterMedium";
                break;
        }

        label.FormattedText = BuildFormattedString(heading.Inlines);
        return label;
    }

    /// <summary>Renders a paragraph node as a Label with FormattedText.</summary>
    private static Label RenderParagraph(ParagraphNode paragraph)
    {
        var label = new Label
        {
            FontFamily = "InterRegular",
            FontSize = (double)Application.Current!.Resources["FontSizeBody"],
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 4, 0, 4),
            TextColor = GetThemeColor("ColorOnBackgroundLight", "ColorOnBackgroundDark"),
        };

        label.FormattedText = BuildFormattedString(paragraph.Inlines);
        return label;
    }

    /// <summary>Renders a fenced code block with monospace font and copy button.</summary>
    private View RenderCodeBlock(CodeBlockNode codeBlock)
    {
        var codeLabel = new Label
        {
            Text = codeBlock.Code,
            FontFamily = "monospace",
            FontSize = (double)Application.Current!.Resources["FontSizeFootnote"],
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = GetThemeColor("ColorOnBackgroundLight", "ColorOnBackgroundDark"),
        };

        var scrollView = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = codeLabel,
        };

        var contentStack = new VerticalStackLayout
        {
            Spacing = 4,
        };

        // Optional language label
        if (!string.IsNullOrWhiteSpace(codeBlock.Language))
        {
            var langLabel = new Label
            {
                Text = codeBlock.Language,
                FontFamily = "InterMedium",
                FontSize = (double)Application.Current!.Resources["FontSizeCaption2"],
                TextColor = GetThemeColor("ColorOnBackgroundSecondaryLight", "ColorOnBackgroundSecondaryDark"),
                HorizontalOptions = LayoutOptions.End,
            };
            contentStack.Children.Add(langLabel);
        }

        contentStack.Children.Add(scrollView);

        var border = new Border
        {
            Style = (Style)Application.Current!.Resources["CodeBlockBorder"],
            BackgroundColor = GetThemeColor("ColorCodeBlockBackgroundLight", "ColorCodeBlockBackgroundDark"),
            Content = contentStack,
            Margin = new Thickness(0, 4, 0, 4),
        };

        return border;
    }

    /// <summary>Renders a blockquote with left border and italic text.</summary>
    private View RenderBlockquote(BlockquoteNode blockquote)
    {
        var innerStack = new VerticalStackLayout
        {
            Spacing = 4,
        };

        foreach (var block in blockquote.Blocks)
        {
            var view = RenderBlock(block);
            if (view is not null)
            {
                // Apply italic to paragraph labels inside blockquotes
                if (view is Label label)
                {
                    label.FontAttributes = FontAttributes.Italic;
                }

                innerStack.Children.Add(view);
            }
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(3, GridUnitType.Absolute)),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = (double)Application.Current!.Resources["SpacingSm"],
        };

        var leftBar = new BoxView
        {
            Color = GetThemeColor("ColorOutlineLight", "ColorOutlineDark"),
            WidthRequest = 3,
            VerticalOptions = LayoutOptions.Fill,
        };

        grid.Add(leftBar, 0, 0);
        grid.Add(innerStack, 1, 0);

        var border = new Border
        {
            Style = (Style)Application.Current!.Resources["BlockquoteBorder"],
            BackgroundColor = GetThemeColor("ColorSurfaceSecondaryLight", "ColorSurfaceSecondaryDark"),
            Content = grid,
            Margin = new Thickness(0, 4, 0, 4),
        };

        return border;
    }

    /// <summary>Renders an ordered or unordered list.</summary>
    private View RenderList(ListNode list, int nestingLevel = 0)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 2,
            Margin = new Thickness(nestingLevel * (double)Application.Current!.Resources["SpacingSm"], 0, 0, 0),
        };

        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            var prefix = list.IsOrdered ? $"{i + 1}. " : "\u2022 ";

            var itemGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                },
                ColumnSpacing = 4,
            };

            var bulletLabel = new Label
            {
                Text = prefix,
                FontFamily = "InterRegular",
                FontSize = (double)Application.Current!.Resources["FontSizeBody"],
                VerticalOptions = LayoutOptions.Start,
                TextColor = GetThemeColor("ColorOnBackgroundSecondaryLight", "ColorOnBackgroundSecondaryDark"),
            };

            var contentStack = new VerticalStackLayout { Spacing = 2 };

            foreach (var block in item.Blocks)
            {
                // Handle nested lists
                if (block is ListNode nestedList)
                {
                    contentStack.Children.Add(RenderList(nestedList, nestingLevel + 1));
                }
                else
                {
                    var view = RenderBlock(block);
                    if (view is not null)
                    {
                        contentStack.Children.Add(view);
                    }
                }
            }

            itemGrid.Add(bulletLabel, 0, 0);
            itemGrid.Add(contentStack, 1, 0);
            stack.Children.Add(itemGrid);
        }

        return stack;
    }

    /// <summary>Renders a table with header and body rows.</summary>
    private View RenderTable(TableNode table)
    {
        var columnCount = table.Header.Cells.Count;
        if (columnCount == 0)
            return new Label { Text = string.Empty };

        var grid = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 0,
        };

        // Define columns
        for (var c = 0; c < columnCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // Define rows: 1 header + N body rows
        var totalRows = 1 + table.Rows.Count;
        for (var r = 0; r < totalRows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Render header row
        for (var c = 0; c < table.Header.Cells.Count; c++)
        {
            var cell = table.Header.Cells[c];
            var cellView = CreateTableCell(cell, isHeader: true);
            grid.Add(cellView, c, 0);
        }

        // Render body rows
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            for (var c = 0; c < row.Cells.Count && c < columnCount; c++)
            {
                var cell = row.Cells[c];
                var cellView = CreateTableCell(cell, isHeader: false, isAlternateRow: r % 2 == 1);
                grid.Add(cellView, c, r + 1);
            }
        }

        var scrollView = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = grid,
            Margin = new Thickness(0, 4, 0, 4),
        };

        return scrollView;
    }

    /// <summary>Creates a single table cell view.</summary>
    private static Border CreateTableCell(TableCellNode cell, bool isHeader, bool isAlternateRow = false)
    {
        var label = new Label
        {
            FontFamily = isHeader ? "InterSemiBold" : "InterRegular",
            FontSize = (double)Application.Current!.Resources["FontSizeFootnote"],
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center,
        };

        label.FormattedText = BuildFormattedString(cell.Inlines);

        var bgColor = isHeader
            ? GetThemeColor("ColorSurfaceSecondaryLight", "ColorSurfaceSecondaryDark")
            : isAlternateRow
                ? GetThemeColor("ColorSurfaceSecondaryLight", "ColorSurfaceSecondaryDark")
                : Colors.Transparent;

        var border = new Border
        {
            Content = label,
            Padding = new Thickness((double)Application.Current!.Resources["SpacingSm"]),
            BackgroundColor = bgColor,
            Stroke = GetThemeColor("ColorSeparatorLight", "ColorSeparatorDark"),
            StrokeThickness = 0.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 0 },
        };

        return border;
    }

    /// <summary>Renders a thematic break (horizontal rule).</summary>
    private static View RenderThematicBreak()
    {
        return new BoxView
        {
            HeightRequest = 1,
            Color = GetThemeColor("ColorSeparatorLight", "ColorSeparatorDark"),
            Margin = new Thickness(0, (double)Application.Current!.Resources["SpacingMd"]),
        };
    }

    // ─── Inline Rendering ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="FormattedString"/> from a list of inline Markdown nodes.
    /// </summary>
    /// <param name="inlines">The inline nodes to render.</param>
    /// <returns>A formatted string with styled spans.</returns>
    private static FormattedString BuildFormattedString(IReadOnlyList<MarkdownInline> inlines)
    {
        var formatted = new FormattedString();

        foreach (var inline in inlines)
        {
            AddInlineSpans(formatted, inline, FontAttributes.None);
        }

        return formatted;
    }

    /// <summary>
    /// Recursively adds spans for an inline node to a FormattedString.
    /// </summary>
    /// <param name="formatted">The target FormattedString.</param>
    /// <param name="inline">The inline node to render.</param>
    /// <param name="parentAttributes">Font attributes inherited from parent nodes.</param>
    private static void AddInlineSpans(FormattedString formatted, MarkdownInline inline, FontAttributes parentAttributes)
    {
        switch (inline)
        {
            case TextNode text:
                formatted.Spans.Add(new Span
                {
                    Text = text.Text,
                    FontFamily = "InterRegular",
                    FontAttributes = parentAttributes,
                    TextColor = GetThemeColor("ColorOnBackgroundLight", "ColorOnBackgroundDark"),
                });
                break;

            case EmphasisNode emphasis:
                var attrs = parentAttributes;
                if (emphasis.IsBold)
                    attrs |= FontAttributes.Bold;
                if (emphasis.IsItalic)
                    attrs |= FontAttributes.Italic;

                foreach (var child in emphasis.Inlines)
                {
                    AddInlineSpans(formatted, child, attrs);
                }
                break;

            case CodeInlineNode code:
                formatted.Spans.Add(new Span
                {
                    Text = code.Code,
                    FontFamily = "monospace",
                    FontSize = (double)Application.Current!.Resources["FontSizeFootnote"],
                    BackgroundColor = GetThemeColor("ColorCodeInlineLight", "ColorCodeInlineDark"),
                    FontAttributes = parentAttributes,
                });
                break;

            case LinkNode link:
                var linkText = string.Join("", link.Inlines.OfType<TextNode>().Select(t => t.Text));
                var span = new Span
                {
                    Text = string.IsNullOrEmpty(linkText) ? link.Url : linkText,
                    TextColor = GetThemeColor("ColorPrimaryLight", "ColorPrimaryDark"),
                    TextDecorations = TextDecorations.Underline,
                    FontFamily = "InterRegular",
                    FontAttributes = parentAttributes,
                };

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (_, _) =>
                {
                    try
                    {
                        await Launcher.OpenAsync(new Uri(link.Url));
                    }
                    catch (UriFormatException)
                    {
                        // Invalid URL format — silently ignore
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open link: {ex.Message}");
                    }
                };
                span.GestureRecognizers.Add(tapGesture);

                formatted.Spans.Add(span);
                break;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a plain text fallback label.</summary>
    private static Label CreatePlainTextLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontFamily = "InterRegular",
            FontSize = (double)Application.Current!.Resources["FontSizeBody"],
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = GetThemeColor("ColorOnBackgroundLight", "ColorOnBackgroundDark"),
        };
    }

    /// <summary>
    /// Gets a theme-aware color from the application resources.
    /// Returns the light or dark variant based on the current app theme.
    /// </summary>
    /// <param name="lightKey">The resource key for the light theme color.</param>
    /// <param name="darkKey">The resource key for the dark theme color.</param>
    /// <returns>The appropriate color for the current theme.</returns>
    private static Color GetThemeColor(string lightKey, string darkKey)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var key = isDark ? darkKey : lightKey;

        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return Colors.Transparent;
    }
}
