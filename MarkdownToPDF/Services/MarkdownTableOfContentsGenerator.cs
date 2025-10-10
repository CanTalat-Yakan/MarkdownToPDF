using System.Text;

namespace MarkdownToPDF.Services;

internal sealed class MarkdownTableOfContentsGenerator
{
    public string Build(
        IReadOnlyList<MarkdownHeadingNumbering.HeaderDescriptor> headers,
        FormattingOptions opts,
        string pageBreakHtml)
    {
        if (headers.Count == 0) return string.Empty;

        string headerText = string.IsNullOrWhiteSpace(opts.TableOfContentsHeaderText)
            ? "Table of Contents"
            : opts.TableOfContentsHeaderText.Trim();

        var html = BuildHtmlToc(headers, headerText);
        var sb2 = new StringBuilder();
        sb2.AppendLine(html);
        sb2.AppendLine();
        sb2.AppendLine(pageBreakHtml);
        sb2.AppendLine();
        return sb2.ToString().TrimEnd('\r', '\n');
    }

    private sealed class TocNode
    {
        public required int Level { get; init; }
        public required string Title { get; init; }
        public required string Href { get; init; }
        public List<TocNode> Children { get; } = new();
    }

    private static string BuildHtmlToc(IReadOnlyList<MarkdownHeadingNumbering.HeaderDescriptor> headers, string headerText)
    {
        // Build a tree from headers (H1 skipped); Level is LogicalLevel (1=H2)
        var roots = new List<TocNode>();
        var stack = new Stack<TocNode>();

        foreach (var h in headers)
        {
            if (h.MarkdownLevel == 1) continue;
            int level = h.LogicalLevel;
            var node = new TocNode
            {
                Level = level,
                Title = string.IsNullOrEmpty(h.Numbering) ? h.Text : $"{h.Numbering} {h.Text}",
                Href = $"#{h.Anchor}"
            };

            if (stack.Count == 0)
            {
                roots.Add(node);
                stack.Push(node);
                continue;
            }

            while (stack.Count > 0 && stack.Peek().Level >= level)
                stack.Pop();

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!--__HTML_TOC_BEGIN__-->");
        sb.AppendLine("<nav id=\"md2pdf-toc\" aria-label=\"Table of contents\">");
        sb.AppendLine($"  <h2>{System.Net.WebUtility.HtmlEncode(headerText)}</h2>");
        sb.Append(RenderList(roots, isRoot: true));
        sb.AppendLine("</nav>");
        sb.AppendLine("<!--__HTML_TOC_END__-->");
        return sb.ToString();
    }

    private static string RenderList(List<TocNode> nodes, bool isRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine(isRoot ? "  <ol class=\"toc-list\" role=\"list\">" : "    <ol role=\"list\">");

        foreach (var n in nodes)
        {
            string safeTitle = System.Net.WebUtility.HtmlEncode(n.Title);
            sb.AppendLine("    <li>");
            sb.AppendLine($"        <a href=\"{n.Href}\">" +
                          $"<span class=\"title\">{safeTitle}<span class=\"leaders\" aria-hidden=\"true\"></span></span> " +
                          $"<span data-href=\"{n.Href}\" class=\"page\"><span class=\"visually-hidden\">Page\u00A0</span><!--page--></span>" +
                          "</a>");
            if (n.Children.Count > 0)
            {
                sb.Append(RenderList(n.Children, isRoot: false));
            }
            sb.AppendLine("    </li>");
        }

        sb.AppendLine("  </ol>");
        return sb.ToString();
    }
}