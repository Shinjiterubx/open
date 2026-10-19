using System.Text;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Markdown;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Exchange;

/// <summary>
/// Plain Markdown import / export.
/// Export writes <c>Name.md</c> plus a <c>Name.assets</c> folder for images.
/// Import takes the title from a leading <c># heading</c> (or the file name) and pulls in local images.
/// </summary>
public sealed class MarkdownFileFormat : INoteImporter, INoteExporter
{
    private const long MaxImportedImageSize = 50L * 1024 * 1024;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public string DisplayName => "Markdown";
    public IReadOnlyList<string> Extensions { get; } = new[] { ".md", ".markdown", ".txt" };
    public NoteExportFormat Format => NoteExportFormat.Markdown;
    public string Extension => ".md";

    public void Export(Note note, string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var assetFolderName = baseName.Replace(' ', '_') + ".assets";

        var body = MarkdownImageReferences.RewriteUrls(note.Content, url =>
            AssetName.IsAssetPath(url) ? assetFolderName + url[Note.AssetFolder.Length..] : url);

        var builder = new StringBuilder();
        if (!body.TrimStart().StartsWith("# ", StringComparison.Ordinal))
            builder.Append("# ").Append(note.DisplayTitle).Append("\n\n");
        builder.Append(body);

        File.WriteAllText(filePath, builder.ToString(), Utf8NoBom);

        if (note.Assets.Count == 0) return;
        var assetDirectory = Path.Combine(directory, assetFolderName);
        Directory.CreateDirectory(assetDirectory);
        foreach (var (name, data) in note.Assets)
            File.WriteAllBytes(Path.Combine(assetDirectory, name), data);
    }

    public Note Import(string filePath)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        var (title, content) = SplitTitle(text, Path.GetFileNameWithoutExtension(filePath));
        var fileInfo = new FileInfo(filePath);

        var note = new Note
        {
            Title = title,
            CreatedAt = fileInfo.CreationTime,
            UpdatedAt = DateTimeOffset.Now,
        };

        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        note.Content = MarkdownImageReferences.RewriteUrls(content, url => TryEmbedLocalImage(note, sourceDirectory, url) ?? url);
        return note;
    }

    private static (string Title, string Content) SplitTitle(string text, string fallbackTitle)
    {
        var trimmed = text.TrimStart('﻿', '\r', '\n');
        if (!trimmed.StartsWith("# ", StringComparison.Ordinal)) return (fallbackTitle, text);

        var lineEnd = trimmed.IndexOf('\n');
        var title = (lineEnd < 0 ? trimmed[2..] : trimmed[2..lineEnd]).Trim();
        var rest = lineEnd < 0 ? string.Empty : trimmed[(lineEnd + 1)..].TrimStart('\r', '\n');
        return (title.Length > 0 ? title : fallbackTitle, rest);
    }

    private static string? TryEmbedLocalImage(Note note, string sourceDirectory, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !absolute.IsFile) return null;

        try
        {
            var localPath = absolute is { IsFile: true }
                ? absolute.LocalPath
                : Path.GetFullPath(Path.Combine(sourceDirectory, Uri.UnescapeDataString(url)));

            var info = new FileInfo(localPath);
            if (!info.Exists || info.Length > MaxImportedImageSize) return null;

            return note.AddAsset(info.Name, File.ReadAllBytes(localPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
