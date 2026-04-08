using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClaudeMaximus.Models;
using Serilog;

namespace ClaudeMaximus.Services;

/// <remarks>Created by Claude</remarks>
public sealed class AttachmentService : IAttachmentService
{
    private static readonly ILogger _log = Log.ForContext<AttachmentService>();

    public async Task<PendingAttachment?> LoadFileAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                _log.Warning("Attachment file not found: {Path}", path);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path);
            var name = Path.GetFileName(path);
            var mediaType = DetectMediaType(path, bytes);

            return new PendingAttachment
            {
                DisplayName = name,
                MediaType = mediaType,
                Data = bytes,
            };
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to load attachment from {Path}", path);
            return null;
        }
    }

    public PendingAttachment WrapBytes(string displayName, string mediaType, byte[] data) =>
        new()
        {
            DisplayName = displayName,
            MediaType = mediaType,
            Data = data,
        };

    public List<TessynContentBlock> ToContentBlocks(IEnumerable<PendingAttachment> attachments, string text)
    {
        var blocks = new List<TessynContentBlock>();

        foreach (var att in attachments)
        {
            if (att.IsImage)
            {
                blocks.Add(TessynContentBlock.FromBase64Image(
                    att.MediaType,
                    Convert.ToBase64String(att.Data)));
            }
            else
            {
                // Non-image attachments: inline as a text block describing the file.
                // Daemon stream-json mode currently only supports text + image content blocks.
                // For other file types, we send the file content inline as text (with truncation guard).
                blocks.Add(TessynContentBlock.FromText(BuildFileBlockText(att)));
            }
        }

        if (!string.IsNullOrWhiteSpace(text))
            blocks.Add(TessynContentBlock.FromText(text));

        return blocks;
    }

    private static string BuildFileBlockText(PendingAttachment att)
    {
        // Try to inline as text if the bytes are valid UTF-8 and reasonably sized.
        const int maxInlineBytes = 256 * 1024; // 256 KB hard cap
        if (att.Data.Length <= maxInlineBytes)
        {
            try
            {
                var text = System.Text.Encoding.UTF8.GetString(att.Data);
                // Reject if it contains too many control characters (likely binary)
                var controlCount = 0;
                foreach (var c in text)
                {
                    if (c < 32 && c != '\n' && c != '\r' && c != '\t')
                    {
                        controlCount++;
                        if (controlCount > 16) break;
                    }
                }
                if (controlCount <= 16)
                    return $"<attachment name=\"{att.DisplayName}\" mediaType=\"{att.MediaType}\" size=\"{att.SizeText}\">\n{text}\n</attachment>";
            }
            catch
            {
                // Fall through to placeholder
            }
        }

        return $"[Attachment: {att.DisplayName} ({att.MediaType}, {att.SizeText}) — content not inlined]";
    }

    private static string DetectMediaType(string path, byte[] bytes)
    {
        // Magic byte detection for common image formats first
        if (bytes.Length >= 8)
        {
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "image/png";
            // JPEG: FF D8 FF
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";
            // GIF: 47 49 46 38
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
                return "image/gif";
            // WebP: RIFF....WEBP
            if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return "image/webp";
            // PDF: 25 50 44 46
            if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
                return "application/pdf";
        }

        // Fallback to extension
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"  => "image/gif",
            ".webp" => "image/webp",
            ".pdf"  => "application/pdf",
            ".txt" or ".md" or ".log" => "text/plain",
            ".json" => "application/json",
            ".xml"  => "application/xml",
            ".html" or ".htm" => "text/html",
            ".csv"  => "text/csv",
            ".cs" or ".js" or ".ts" or ".py" or ".go" or ".rs" or ".java" or ".rb" or ".sh"
                    => "text/plain",
            _       => "application/octet-stream",
        };
    }
}
