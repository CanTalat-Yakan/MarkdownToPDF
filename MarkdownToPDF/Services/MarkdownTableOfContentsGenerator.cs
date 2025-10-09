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

        bool numbered = string.Equals(opts.TableOfContentsBulletStyle, "1.", StringComparison.Ordinal);

        var sb = new StringBuilder();
        sb.AppendLine($"## {headerText}");

        foreach (var h in headers)
        {
            if (h.MarkdownLevel == 1) continue;
            int indentLevel = h.LogicalLevel <= 1 ? 0 : h.LogicalLevel - 1;

            string indent = string.Empty;
            if (opts.IndentTableOfContents)
                indent = numbered ? new string(' ', indentLevel * 4) : new string(' ', indentLevel * 2);

            string labelCore = string.IsNullOrEmpty(h.Numbering) ? h.Text : $"{h.Numbering} {h.Text}";
            string bullet = numbered ? "1." : "-";
            sb.AppendLine($"{indent}{bullet} [{labelCore}](#{h.Anchor})");
        }

        sb.AppendLine();
        sb.AppendLine(pageBreakHtml);
        sb.AppendLine();
        return sb.ToString().TrimEnd('\r', '\n');
    }
}