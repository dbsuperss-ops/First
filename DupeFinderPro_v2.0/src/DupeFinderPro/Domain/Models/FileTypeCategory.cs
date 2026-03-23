namespace DupeFinderPro.Domain.Models;

public enum FileTypeCategory
{
    Documents,
    Images,
    Videos,
    Audio,
    Archives,
    Installers,
    Other
}

public static class FileTypeCategoryExtensions
{
    private static readonly Dictionary<FileTypeCategory, string[]> Mappings = new()
    {
        [FileTypeCategory.Documents] = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".csv"],
        [FileTypeCategory.Images]    = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".svg", ".ico", ".heic"],
        [FileTypeCategory.Videos]    = [".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".m4v", ".mpeg", ".webm"],
        [FileTypeCategory.Audio]     = [".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus"],
        [FileTypeCategory.Archives]  = [".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".cab"],
        [FileTypeCategory.Installers]= [".exe", ".msi", ".dmg", ".pkg", ".deb", ".rpm", ".appx"],
        [FileTypeCategory.Other]     = []
    };

    public static IReadOnlyList<string> GetExtensions(FileTypeCategory category) =>
        Mappings.TryGetValue(category, out var exts) ? exts : [];

    public static string GetLabel(FileTypeCategory category) => category switch
    {
        FileTypeCategory.Documents  => "문서",
        FileTypeCategory.Images     => "이미지",
        FileTypeCategory.Videos     => "동영상",
        FileTypeCategory.Audio      => "오디오",
        FileTypeCategory.Archives   => "압축 파일",
        FileTypeCategory.Installers => "설치 파일",
        FileTypeCategory.Other      => "기타",
        _ => category.ToString()
    };

    public static string GetDescription(FileTypeCategory category) => category switch
    {
        FileTypeCategory.Documents  => "PDF, DOC, TXT 등",
        FileTypeCategory.Images     => "JPG, PNG, GIF 등",
        FileTypeCategory.Videos     => "MP4, AVI, MKV 등",
        FileTypeCategory.Audio      => "MP3, WAV, FLAC 등",
        FileTypeCategory.Archives   => "ZIP, RAR, 7Z 등",
        FileTypeCategory.Installers => "EXE, MSI, DMG 등",
        FileTypeCategory.Other      => "기타 모든 파일",
        _ => string.Empty
    };
}
