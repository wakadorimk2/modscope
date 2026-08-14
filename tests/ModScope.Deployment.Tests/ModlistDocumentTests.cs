using System.Text;
using ModScope.Deployment;
using Xunit;

namespace ModScope.Deployment.Tests;

public sealed class ModlistDocumentTests
{
    [Fact]
    public void RewritePreservesCommentsBlankLinesSeparatorsAndUnknownLines()
    {
        const string original = "# keep this comment\r\n\r\n-Alpha Mod\r\n_separator\r\n+Beta Mod\r\nunknown line\r\n";
        var document = ModlistDocument.Read(Encoding.UTF8.GetBytes(original));

        var rewritten = document.Rewrite(new[]
        {
            new DeploymentEntryDraft("Beta Mod", true, 0),
            new DeploymentEntryDraft("Alpha Mod", false, 1)
        });

        var text = Encoding.UTF8.GetString(rewritten);
        Assert.Contains("# keep this comment\r\n", text, StringComparison.Ordinal);
        Assert.Contains("\r\n\r\n", text, StringComparison.Ordinal);
        Assert.Contains("_separator\r\n", text, StringComparison.Ordinal);
        Assert.Contains("unknown line\r\n", text, StringComparison.Ordinal);
        Assert.Contains("+Beta Mod\r\n", text, StringComparison.Ordinal);
        Assert.Contains("-Alpha Mod\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EditableLinesKeepDocumentOrderAndEnabledState()
    {
        var document = ModlistDocument.Read(Encoding.UTF8.GetBytes("+High\n-Low\n"));

        Assert.Equal(new[] { "High", "Low" }, document.EditableLines.Select(line => line.ModKey));
        Assert.True(document.EditableLines[0].IsEnabled);
        Assert.False(document.EditableLines[1].IsEnabled);

        var rewritten = document.Rewrite(new[]
        {
            new DeploymentEntryDraft("Low", true, 0),
            new DeploymentEntryDraft("High", false, 1)
        });

        Assert.Equal("-High\n+Low\n", Encoding.UTF8.GetString(rewritten));
    }
}
