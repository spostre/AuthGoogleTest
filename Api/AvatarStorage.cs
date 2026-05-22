namespace Api;

public static class AvatarStorage
{
    public const string PublicUrlPrefix = "/avatars";

    public static string GetDirectory(string contentRootPath) =>
        Path.Combine(contentRootPath, "App_Data", "avatars");

    public static void EnsureDirectoryExists(string contentRootPath)
    {
        Directory.CreateDirectory(GetDirectory(contentRootPath));
    }

    public static void MigrateLegacyClientAvatarsIfPresent(string clientPath, string contentRootPath)
    {
        var legacyDir = Path.Combine(clientPath, "avatars");
        if (!Directory.Exists(legacyDir))
        {
            return;
        }

        var targetDir = GetDirectory(contentRootPath);
        Directory.CreateDirectory(targetDir);

        foreach (var legacyFile in Directory.GetFiles(legacyDir))
        {
            var fileName = Path.GetFileName(legacyFile);
            if (string.IsNullOrEmpty(fileName) || fileName.StartsWith('.'))
            {
                continue;
            }

            var destination = Path.Combine(targetDir, fileName);
            if (File.Exists(destination))
            {
                continue;
            }

            File.Move(legacyFile, destination);
        }
    }
}
