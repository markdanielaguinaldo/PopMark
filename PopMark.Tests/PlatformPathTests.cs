using PopMark.Helpers;
using PopMark.Services;
using Xunit;

namespace PopMark.Tests;

public sealed class PlatformPathTests
{
    [Fact]
    public void PopMarkHomeOverride_ControlsEveryStatePath()
    {
        var previous = Environment.GetEnvironmentVariable("POPMARK_HOME");
        var root = Path.Combine(Path.GetTempPath(), $"popmark-state-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable("POPMARK_HOME", root);

            Assert.Equal(Path.GetFullPath(root), PopMarkState.Root);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "queue.json"), PopMarkState.QueueFile);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "sessions.json"), PopMarkState.SessionsFile);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "theme.json"), PopMarkState.ThemeFile);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "tools"), ToolLocator.ToolRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "tools.json"), ToolLocator.OverrideFilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POPMARK_HOME", previous);
        }
    }

    [Fact]
    public void PopMarkHomeOverride_ExpandsTilde()
    {
        var previous = Environment.GetEnvironmentVariable("POPMARK_HOME");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        try
        {
            Environment.SetEnvironmentVariable("POPMARK_HOME", "~/popmark-tilde-test");

            Assert.Equal(
                Path.GetFullPath(Path.Combine(home, "popmark-tilde-test")),
                PopMarkState.Root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POPMARK_HOME", previous);
        }
    }
}
