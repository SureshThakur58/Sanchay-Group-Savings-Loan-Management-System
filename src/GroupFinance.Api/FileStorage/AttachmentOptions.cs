namespace GroupFinance.Api.FileStorage;

public sealed class AttachmentOptions
{
    public string RootPath { get; set; } = "App_Data/Attachments";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx"];
}
