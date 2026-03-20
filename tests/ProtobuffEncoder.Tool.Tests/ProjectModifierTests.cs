using System.Xml.Linq;
using ProtobuffEncoder.Tool;

namespace ProtobuffEncoder.Tool.Tests;

/// <summary>
/// Comprehensive tests for <see cref="ProjectModifier"/> — csproj file modification.
/// </summary>
public class ProjectModifierTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectModifierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ProtobufToolTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateCsproj(string xml)
    {
        var path = Path.Combine(_tempDir, "test.csproj");
        File.WriteAllText(path, xml);
        return path;
    }

    private static string MinimalCsproj => @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>";

    #region Simple-Test Pattern — basic append

    [Fact]
    public void AppendToCsproj_AddsNewProtoItems()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var protoPath = Path.Combine(_tempDir, "model.proto");
        File.WriteAllText(protoPath, "syntax = \"proto3\";");

        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var xml = File.ReadAllText(csproj);
        Assert.Contains("model.proto", xml);
        Assert.Contains("CopyToOutputDirectory", xml);
        Assert.Contains("PreserveNewest", xml);
    }

    [Fact]
    public void AppendToCsproj_CreatesContentElement()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var protoPath = Path.Combine(_tempDir, "service.proto");

        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var doc = XDocument.Load(csproj);
        var content = doc.Descendants("Content").FirstOrDefault();
        Assert.NotNull(content);
        Assert.Equal("service.proto", content!.Attribute("Include")?.Value);
    }

    #endregion

    #region Collection-Constraint Pattern — no duplicates

    [Fact]
    public void AppendToCsproj_DoesNotDuplicateExistingContent()
    {
        var xml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <Content Include=""existing.proto"">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>";
        var csproj = CreateCsproj(xml);
        var protoPath = Path.Combine(_tempDir, "existing.proto");

        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var result = File.ReadAllText(csproj);
        var count = System.Text.RegularExpressions.Regex.Matches(result, "existing\\.proto").Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void AppendToCsproj_DoesNotDuplicateExistingNone()
    {
        var xml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <None Include=""legacy.proto"" />
  </ItemGroup>
</Project>";
        var csproj = CreateCsproj(xml);

        // Add a different proto
        var protoPath = Path.Combine(_tempDir, "new.proto");
        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var result = File.ReadAllText(csproj);
        Assert.Contains("new.proto", result);
    }

    #endregion

    #region Collection-Order Pattern — multiple files

    [Fact]
    public void AppendToCsproj_MultipleFiles_AllAdded()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var paths = new List<string>
        {
            Path.Combine(_tempDir, "a.proto"),
            Path.Combine(_tempDir, "b.proto"),
            Path.Combine(_tempDir, "c.proto")
        };

        ProjectModifier.AppendToCsproj(csproj, _tempDir, paths);

        var result = File.ReadAllText(csproj);
        Assert.Contains("a.proto", result);
        Assert.Contains("b.proto", result);
        Assert.Contains("c.proto", result);
    }

    [Fact]
    public void AppendToCsproj_MixedNewAndExisting_OnlyAddsNew()
    {
        var xml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <Content Include=""old.proto"">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>";
        var csproj = CreateCsproj(xml);
        var paths = new List<string>
        {
            Path.Combine(_tempDir, "old.proto"),
            Path.Combine(_tempDir, "new.proto")
        };

        ProjectModifier.AppendToCsproj(csproj, _tempDir, paths);

        var doc = XDocument.Load(csproj);
        var contentElements = doc.Descendants("Content").ToList();
        Assert.Equal(2, contentElements.Count);
    }

    #endregion

    #region Constraint-Data Pattern — empty input

    [Fact]
    public void AppendToCsproj_EmptyList_DoesNotModifyFile()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var originalContent = File.ReadAllText(csproj);

        ProjectModifier.AppendToCsproj(csproj, _tempDir, []);

        var afterContent = File.ReadAllText(csproj);
        Assert.Equal(originalContent, afterContent);
    }

    #endregion

    #region Process-Rule Pattern — ItemGroup creation

    [Fact]
    public void AppendToCsproj_NoExistingItemGroup_CreatesNewOne()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var protoPath = Path.Combine(_tempDir, "new.proto");

        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var doc = XDocument.Load(csproj);
        var itemGroups = doc.Root!.Elements("ItemGroup").ToList();
        Assert.True(itemGroups.Count >= 1);
    }

    [Fact]
    public void AppendToCsproj_ExistingProtoItemGroup_ReusesIt()
    {
        var xml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <Content Include=""first.proto"">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>";
        var csproj = CreateCsproj(xml);
        var protoPath = Path.Combine(_tempDir, "second.proto");

        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var doc = XDocument.Load(csproj);
        // Both protos should be in the same ItemGroup
        var itemGroupsWithProto = doc.Root!.Elements("ItemGroup")
            .Where(ig => ig.Elements("Content")
                .Any(e => e.Attribute("Include")?.Value.EndsWith(".proto") == true))
            .ToList();
        Assert.Single(itemGroupsWithProto);
        Assert.Equal(2, itemGroupsWithProto[0].Elements("Content").Count());
    }

    #endregion

    #region Bit-Error-Simulation Pattern — edge cases

    [Fact]
    public void AppendToCsproj_SubdirectoryPath_UsesRelativePath()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var subDir = Path.Combine(_tempDir, "protos", "v1");
        Directory.CreateDirectory(subDir);
        var protoPath = Path.Combine(subDir, "model.proto");

        ProjectModifier.AppendToCsproj(csproj, _tempDir, [protoPath]);

        var doc = XDocument.Load(csproj);
        var include = doc.Descendants("Content").First().Attribute("Include")?.Value;
        Assert.NotNull(include);
        Assert.Contains("protos", include);
        Assert.Contains("model.proto", include);
    }

    #endregion

    #region Signalled Pattern — concurrent modifications

    [Fact]
    public void AppendToCsproj_SequentialCalls_AllPersist()
    {
        var csproj = CreateCsproj(MinimalCsproj);

        for (int i = 0; i < 5; i++)
        {
            ProjectModifier.AppendToCsproj(csproj, _tempDir,
                [Path.Combine(_tempDir, $"proto_{i}.proto")]);
        }

        var doc = XDocument.Load(csproj);
        var contentElements = doc.Descendants("Content")
            .Where(e => e.Attribute("Include")?.Value.EndsWith(".proto") == true)
            .ToList();
        Assert.Equal(5, contentElements.Count);
    }

    #endregion

    #region Performance-Test Pattern

    [Fact]
    public void AppendToCsproj_ManyFiles_CompletesEfficiently()
    {
        var csproj = CreateCsproj(MinimalCsproj);
        var paths = Enumerable.Range(0, 100)
            .Select(i => Path.Combine(_tempDir, $"gen_{i}.proto"))
            .ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        ProjectModifier.AppendToCsproj(csproj, _tempDir, paths);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"100 proto files took {sw.ElapsedMilliseconds}ms");

        var doc = XDocument.Load(csproj);
        var count = doc.Descendants("Content")
            .Count(e => e.Attribute("Include")?.Value.EndsWith(".proto") == true);
        Assert.Equal(100, count);
    }

    #endregion
}
