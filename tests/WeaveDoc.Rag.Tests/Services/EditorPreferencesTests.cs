using System;
using System.IO;
using WeaveDoc.Rag.Services;
using Xunit;

namespace WeaveDoc.Rag.Tests.Services;

public sealed class EditorPreferencesTests
{
    [Fact]
    public void Load_FileMissing_DefaultsToWordWrapDisabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weavedoc-editor-prefs-{Guid.NewGuid()}.json");

        Assert.False(File.Exists(path));

        var prefs = EditorPreferences.Load(path);

        Assert.False(prefs.WordWrapEnabled);
    }

    [Fact]
    public void SetWordWrap_PersistsAcrossReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weavedoc-editor-prefs-{Guid.NewGuid()}.json");
        try
        {
            var prefs = EditorPreferences.Load(path);
            Assert.False(prefs.WordWrapEnabled);

            prefs.SetWordWrap(true);

            var reloaded = EditorPreferences.Load(path);
            Assert.True(reloaded.WordWrapEnabled);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SetWordWrap_SameValue_DoesNotRewriteFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weavedoc-editor-prefs-{Guid.NewGuid()}.json");
        try
        {
            var prefs = EditorPreferences.Load(path);
            prefs.SetWordWrap(true);
            Assert.True(File.Exists(path));

            File.Delete(path);

            // 相同值（true == true）幂等：不应触发写盘，磁盘文件不应被重建。
            prefs.SetWordWrap(true);

            Assert.False(File.Exists(path));
            Assert.True(prefs.WordWrapEnabled);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weavedoc-editor-prefs-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json");

            var prefs = EditorPreferences.Load(path);

            Assert.False(prefs.WordWrapEnabled);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
