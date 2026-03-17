using System.IO;
using Microsoft.Win32;

namespace Peeklet.Services;

public static class PreviewHandlerRegistry
{
    private const string PreviewHandlerKey = @"shellex\{8895b1c6-b41f-4c1c-a562-0d564250836f}";

    public static bool TryGetHandlerClsid(string filePath, out Guid clsid)
    {
        clsid = Guid.Empty;
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return TryReadClsid($@"{extension}\{PreviewHandlerKey}", out clsid)
            || TryReadFromProgId(extension, out clsid)
            || TryReadClsid($@"SystemFileAssociations\{extension}\{PreviewHandlerKey}", out clsid);
    }

    private static bool TryReadFromProgId(string extension, out Guid clsid)
    {
        clsid = Guid.Empty;

        using var extensionKey = Registry.ClassesRoot.OpenSubKey(extension);
        var progId = extensionKey?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(progId))
        {
            return false;
        }

        return TryReadClsid($@"{progId}\{PreviewHandlerKey}", out clsid);
    }

    private static bool TryReadClsid(string subKeyPath, out Guid clsid)
    {
        clsid = Guid.Empty;

        using var key = Registry.ClassesRoot.OpenSubKey(subKeyPath);
        var value = key?.GetValue(null) as string;
        return Guid.TryParse(value, out clsid);
    }
}