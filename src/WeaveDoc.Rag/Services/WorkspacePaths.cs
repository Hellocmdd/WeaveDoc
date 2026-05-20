namespace WeaveDoc.Rag.Services;

public static class WorkspacePaths
{
    public static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (LooksLikeWorkspaceRoot(directory.FullName))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the workspace root.");
    }

    private static bool LooksLikeWorkspaceRoot(string path)
    {
        if (File.Exists(Path.Combine(path, "WeaveDoc.slnx")))
        {
            return true;
        }

        if (Directory.Exists(Path.Combine(path, "src"))
            && Directory.Exists(Path.Combine(path, "tests")))
        {
            return true;
        }

        return Directory.Exists(Path.Combine(path, "models"));
    }
}
