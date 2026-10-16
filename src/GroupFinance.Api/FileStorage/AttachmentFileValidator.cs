namespace GroupFinance.Api.FileStorage;

public static class AttachmentFileValidator
{
    public static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

    public static string Validate(
        string originalFileName,
        string contentType,
        long length,
        long maxFileSizeBytes,
        IEnumerable<string> configuredExtensions)
    {
        if (length <= 0)
            throw new AttachmentValidationException("A non-empty file is required.");

        if (length > maxFileSizeBytes)
            throw new AttachmentValidationException(
                $"File exceeds the maximum allowed size of {maxFileSizeBytes / (1024 * 1024)} MB.");

        var safeName = Path.GetFileName(originalFileName).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
            throw new AttachmentValidationException("The uploaded file must have a valid file name.");

        if (safeName.Length > 260)
            throw new AttachmentValidationException("File name cannot exceed 260 characters.");

        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        var configured = new HashSet<string>(
            configuredExtensions ?? [],
            StringComparer.OrdinalIgnoreCase);

        if (!configured.Contains(extension) || !AllowedContentTypes.TryGetValue(extension, out var expected))
            throw new AttachmentValidationException(
                "Unsupported file type. Allowed types are PDF, JPG, JPEG, PNG, DOCX and XLSX.");

        if (!string.Equals(contentType?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            throw new AttachmentValidationException(
                $"The uploaded content type does not match the {extension} file extension.");

        return safeName;
    }

    public static bool HasValidSignature(ReadOnlySpan<byte> header, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => header.Length >= 4 &&
                      header[0] == 0x25 && header[1] == 0x50 &&
                      header[2] == 0x44 && header[3] == 0x46,
            ".jpg" or ".jpeg" => header.Length >= 3 &&
                                 header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8 &&
                      header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".docx" or ".xlsx" => header.Length >= 4 &&
                                  header[0] == 0x50 && header[1] == 0x4B &&
                                  header[2] == 0x03 && header[3] == 0x04,
            _ => false
        };
    }
}
