namespace MarkdownToPDF.Models;

public sealed class MarkdownFileModel
{
    public string FilePath { get; }
    public string FileName { get; }
    public MarkdownFileModel(string path)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
    }
}