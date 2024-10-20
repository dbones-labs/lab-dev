namespace Dev.Infrastructure;

using System.Reflection;
using System.Security.Cryptography;

public static class FolderHelpers 
{
    public static string CalculateMD5FromFile(string filename)
    {
        using var stream = File.OpenRead(filename);
        
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    public static string CalculateMD5FromString(string content)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static void EnsureFile(string basePath, string filePath, string content)
    {
        var fullPath = Path.Combine(basePath, filePath);

        //file exists
        var newFile = File.Exists(fullPath);
        if (newFile)
        {
            //content is the same
            var onFileMd5 = CalculateMD5FromFile(fullPath);
            var desiredFileMd5 = CalculateMD5FromString(content);
            if (onFileMd5 == desiredFileMd5)
            {
                //no change
                return;
            }
            
            File.Delete(fullPath);
        }
        else
        {
            //ensure direct exists
            var parent = Directory.GetParent(fullPath);
            Directory.CreateDirectory(parent.FullName);
        }
        
        using var fileStream = File.OpenWrite(fullPath);
        using var streamWriter = new StreamWriter(fileStream);
        
        streamWriter.Write(content);
        streamWriter.Flush();
    }
    
    public static bool HasDirectoryHaveFiles(string path)
    {
        return Directory.EnumerateFileSystemEntries(path).Any();
    }

    public static string BaseDirectory { get; } = Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName;

    public static string PathFormat(this string filePath)
    {
        var os = Environment.OSVersion;
        return os.Platform == PlatformID.Win32NT ? filePath.Replace("/", "\\") : filePath;
    }

}