using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Peekerino.Services
{
    internal static class JsonSummarizer
    {
        public static string Summarize(string path, int maxOutputChars, CancellationToken ct = default)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = JsonDocument.Parse(fs, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = 256
                });

                ct.ThrowIfCancellationRequested();

                var buffer = new ArrayBufferWriter<byte>();
                using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
                {
                    Indented = true,
                    SkipValidation = false
                }))
                {
                    document.WriteTo(writer);
                }

                string pretty = Encoding.UTF8.GetString(buffer.WrittenSpan);
                if (pretty.Length > maxOutputChars)
                {
                    return pretty.Substring(0, maxOutputChars) + Environment.NewLine + $"... (truncated, total {pretty.Length:N0} characters)";
                }

                return pretty;
            }
            catch (JsonException jex)
            {
                return $"JSON parsing error: {jex.Message}";
            }
            catch (Exception ex)
            {
                return $"JSON summary failed: {ex.Message}";
            }
        }
    }
}

