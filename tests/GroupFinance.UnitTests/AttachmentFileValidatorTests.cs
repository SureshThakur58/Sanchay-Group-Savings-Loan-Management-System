using GroupFinance.Api.FileStorage;

namespace GroupFinance.UnitTests;

public sealed class AttachmentFileValidatorTests
{
    private static readonly string[] Extensions =
        [".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx"];

    [Fact]
    public void PdfWithMatchingContentType_IsAccepted()
    {
        var name = AttachmentFileValidator.Validate(
            @"C:\temp\statement.pdf",
            "application/pdf",
            1024,
            10 * 1024 * 1024,
            Extensions);

        Assert.Equal("statement.pdf", name);
    }

    [Fact]
    public void PathTraversalName_IsReducedToFileName()
    {
        var name = AttachmentFileValidator.Validate(
            @"..\..\statement.pdf",
            "application/pdf",
            1024,
            10 * 1024 * 1024,
            Extensions);

        Assert.Equal("statement.pdf", name);
    }

    [Fact]
    public void UnsupportedExtension_IsRejected()
    {
        var ex = Assert.Throws<AttachmentValidationException>(() =>
            AttachmentFileValidator.Validate(
                "malware.exe",
                "application/octet-stream",
                100,
                10 * 1024 * 1024,
                Extensions));

        Assert.Contains("Unsupported file type", ex.Message);
    }

    [Fact]
    public void MismatchedContentType_IsRejected()
    {
        Assert.Throws<AttachmentValidationException>(() =>
            AttachmentFileValidator.Validate(
                "statement.pdf",
                "image/png",
                100,
                10 * 1024 * 1024,
                Extensions));
    }

    [Fact]
    public void OversizedFile_IsRejected()
    {
        Assert.Throws<AttachmentValidationException>(() =>
            AttachmentFileValidator.Validate(
                "statement.pdf",
                "application/pdf",
                10 * 1024 * 1024 + 1,
                10 * 1024 * 1024,
                Extensions));
    }

    [Fact]
    public void PdfSignature_IsAccepted()
    {
        Assert.True(AttachmentFileValidator.HasValidSignature(
            [0x25, 0x50, 0x44, 0x46, 0x2D],
            ".pdf"));
    }

    [Fact]
    public void PdfExtensionWithPngSignature_IsRejected()
    {
        Assert.False(AttachmentFileValidator.HasValidSignature(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            ".pdf"));
    }
}
