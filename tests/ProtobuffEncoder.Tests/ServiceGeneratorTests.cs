using System.Reflection;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;

namespace ProtobuffEncoder.Tests;

// ─── Test-local dummies for schema generation ─────────────

[ProtoContract(Version = 2, Name = "TestEntity", Metadata = "Domain entity for testing")]
public class SchemaTestEntity
{
    [ProtoField(FieldNumber = 1)]
    public int Id { get; set; }

    [ProtoField(FieldNumber = 2)]
    public string Name { get; set; } = "";
}

[ProtoContract]
public class SchemaEmptyMessage { }

[ProtoService("SchemaTestService", Metadata = "Test service for schema gen")]
public interface ISchemaTestService
{
    [ProtoMethod(ProtoMethodType.Unary, Name = "FetchEntity")]
    Task<SchemaTestEntity> GetEntityAsync(int id);

    [ProtoMethod(ProtoMethodType.Unary)]
    Task ExecuteAsync(SchemaTestEntity request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<SchemaTestEntity> StreamAll(SchemaEmptyMessage request);
}

[ProtoContract]
public class SchemaMapModel
{
    [ProtoMap]
    [ProtoField(1)]
    public Dictionary<string, string> Labels { get; set; } = new();
}

[ProtoContract]
public class SchemaOneOfModel
{
    [ProtoOneOf("payload")]
    [ProtoField(1)]
    public string? TextPayload { get; set; }

    [ProtoOneOf("payload")]
    [ProtoField(2)]
    public int? BinaryPayload { get; set; }
}

// ─── Cross-file import test models ─────────────

[ProtoContract(Name = "ImportTargetA")]
public class ImportTargetA
{
    [ProtoField(1)] public string Value { get; set; } = "";
}

[ProtoContract(Name = "ImportTargetB")]
public class ImportTargetB
{
    [ProtoField(1)] public ImportTargetA Ref { get; set; } = new();
}

[ProtoService("ImportTestService")]
public interface IImportTestService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<ImportTargetA> GetA(ImportTargetB request);
}

/// <summary>
/// Comprehensive tests for ProtoSchemaGenerator covering versioning, metadata,
/// services, enums, maps, oneofs, wrapper generation, cross-file imports, and file-boundary isolation.
/// </summary>
public class SchemaGeneratorTests
{
    #region Simple-Test Pattern — basic schema generation

    [Fact]
    public void GenerateForType_ContainsSyntaxProto3()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(SimpleMessage));
        Assert.Contains("syntax = \"proto3\"", schema);
    }

    [Fact]
    public void GenerateForType_SimpleMessage_EmitsAllFields()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(SimpleMessage));
        Assert.Contains("message SimpleMessage {", schema);
        Assert.Contains("int32 Id = 1;", schema);
        Assert.Contains("string Name = 2;", schema);
        Assert.Contains("bool IsActive = 3;", schema);
    }

    [Fact]
    public void GenerateForType_NestedMessage_IncludesReferencedType()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(NestedOuter));
        Assert.Contains("message NestedOuter {", schema);
        Assert.Contains("NestedInner Inner = 2;", schema);
    }

    [Fact]
    public void GenerateForType_ListField_EmitsRepeated()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(ListMessage));
        Assert.Contains("repeated int32 Numbers = 1;", schema);
        Assert.Contains("repeated string Tags = 2;", schema);
    }

    [Fact]
    public void GenerateForType_MapField_EmitsMapSyntax()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(SchemaMapModel));
        Assert.Contains("map<string, string>", schema);
    }

    [Fact]
    public void GenerateForType_OneOfGroup_EmitsOneOfBlock()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(SchemaOneOfModel));
        Assert.Contains("oneof payload {", schema);
    }

    #endregion

    #region Code-Path Pattern — versioning and metadata

    [Fact]
    public void GenerateAll_VersionedType_OutputsToVersionDir()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        Assert.True(results.ContainsKey("v2/TestEntity.proto"),
            "Should generate to versioned directory using Version and Name.");
    }

    [Fact]
    public void GenerateAll_MetadataPresent_EmitsComment()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var entityProto = results["v2/TestEntity.proto"];
        Assert.Contains("Domain entity for testing", entityProto);
    }

    #endregion

    #region Service generation — services get own file

    [Fact]
    public void GenerateAll_ServiceGetsOwnFile()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        Assert.True(results.ContainsKey("SchemaTestService.proto"),
            "Service should get its own file named after the service.");
    }

    [Fact]
    public void GenerateAll_DetectsService()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];
        Assert.Contains("service SchemaTestService {", content);
    }

    [Fact]
    public void GenerateAll_ServiceHasRpcMethods()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];
        Assert.Contains("rpc FetchEntity (FetchEntityRequest) returns (FetchEntityResponse);", content);
        Assert.Contains("rpc ExecuteAsync (ExecuteAsyncRequest) returns (ExecuteAsyncResponse);", content);
    }

    [Fact]
    public void GenerateAll_ServiceHasStreamingAnnotation()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];
        Assert.Contains("stream", content);
    }

    [Fact]
    public void GenerateAll_CreatesRequestWrapperForPrimitive()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];
        Assert.Contains("message FetchEntityRequest {", content);
        Assert.Contains("int32 data = 1;", content);
    }

    [Fact]
    public void GenerateAll_CreatesResponseWrapper()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];
        Assert.Contains("message FetchEntityResponse {", content);
    }

    [Fact]
    public void GenerateAll_ServiceMetadata_EmitsComment()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];
        Assert.Contains("Test service for schema gen", content);
    }

    #endregion

    #region Cross-file imports — auto-import when types span files

    [Fact]
    public void GenerateAll_ServiceFileImportsMessageFile()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
        var content = results["SchemaTestService.proto"];

        // Service references SchemaTestEntity (in v2/TestEntity.proto) and SchemaEmptyMessage
        // The service file should import the files where those types are defined
        Assert.Contains("import", content);
    }

    [Fact]
    public void GenerateAll_ImportTargetB_ImportsTargetA()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);

        // ImportTargetB references ImportTargetA, which is in its own file
        var bFileKey = results.Keys.FirstOrDefault(k =>
        {
            var content = results[k];
            return content.Contains("message ImportTargetB {");
        });
        Assert.NotNull(bFileKey);

        var aFileKey = results.Keys.FirstOrDefault(k =>
        {
            var content = results[k];
            return content.Contains("message ImportTargetA {") && !content.Contains("message ImportTargetB {");
        });

        // If they're in different files, the B file should import the A file
        if (bFileKey != aFileKey && aFileKey is not null)
        {
            var bContent = results[bFileKey];
            Assert.Contains($"import \"{aFileKey}\"", bContent);
        }
    }

    [Fact]
    public void GenerateAll_ServiceFile_DoesNotDuplicateExternalTypes()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);

        // ImportTestService references ImportTargetA and ImportTargetB
        // If they're in separate files, the service file should NOT contain their message definitions
        if (results.ContainsKey("ImportTestService.proto"))
        {
            var svcContent = results["ImportTestService.proto"];
            Assert.Contains("service ImportTestService {", svcContent);

            // If ImportTargetA has its own file, it should NOT be duplicated in the service file
            if (results.Keys.Any(k => k != "ImportTestService.proto" && results[k].Contains("message ImportTargetA {")))
            {
                // Count occurrences of ImportTargetA message definition across ALL files
                int defCount = results.Values.Count(v => v.Contains("message ImportTargetA {"));
                // Should be exactly 1 definition (in its own file)
                Assert.Equal(1, defCount);
            }
        }
    }

    [Fact]
    public void GenerateAll_NoCircularImports()
    {
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);

        foreach (var (fileKey, content) in results)
        {
            // A file should never import itself
            Assert.DoesNotContain($"import \"{fileKey}\"", content);
        }
    }

    #endregion

    #region ProtoFile model — Imports property

    [Fact]
    public void ProtoFile_ImportsProperty_DefaultsToEmpty()
    {
        var file = new ProtoFile();
        Assert.NotNull(file.Imports);
        Assert.Empty(file.Imports);
    }

    [Fact]
    public void ProtoFile_FilePath_CanBeSet()
    {
        var file = new ProtoFile { FilePath = "v1/Order.proto" };
        Assert.Equal("v1/Order.proto", file.FilePath);
    }

    #endregion

    #region ResolveFileKey — file key resolution

    [Fact]
    public void ResolveFileKey_VersionedType_IncludesVersionDir()
    {
        var key = ProtoSchemaGenerator.ResolveFileKey(typeof(SchemaTestEntity));
        Assert.Equal("v2/TestEntity.proto", key);
    }

    [Fact]
    public void ResolveFileKey_ServiceInterface_UsesServiceName()
    {
        var key = ProtoSchemaGenerator.ResolveFileKey(typeof(ISchemaTestService));
        Assert.Equal("SchemaTestService.proto", key);
    }

    [Fact]
    public void ResolveFileKey_PlainContract_UsesNamespace()
    {
        var key = ProtoSchemaGenerator.ResolveFileKey(typeof(SimpleMessage));
        Assert.Contains("protobuffencoder_tests", key);
        Assert.EndsWith(".proto", key);
    }

    [Fact]
    public void ResolveFileKey_NamedContract_UsesName()
    {
        var key = ProtoSchemaGenerator.ResolveFileKey(typeof(ImportTargetA));
        Assert.Equal("ImportTargetA.proto", key);
    }

    #endregion

    #region Render — import statements in output

    [Fact]
    public void Render_FileWithImports_EmitsImportStatements()
    {
        // Generate a full assembly to get rendered output with imports
        var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);

        // Find any file that has imports
        var fileWithImports = results.Values.FirstOrDefault(v => v.Contains("import \""));
        Assert.NotNull(fileWithImports);

        // Verify import syntax
        Assert.Matches(@"import ""[^""]+\.proto"";", fileWithImports);
    }

    [Fact]
    public void Render_FileWithoutImports_NoImportStatements()
    {
        // Single type generation should have no imports
        var schema = ProtoSchemaGenerator.Generate(typeof(SimpleMessage));
        Assert.DoesNotContain("import", schema);
    }

    #endregion

    #region Generate single type — unchanged behavior

    [Fact]
    public void Generate_SingleType_IncludesDependencies()
    {
        // Generate for NestedOuter should include NestedInner definition too
        var schema = ProtoSchemaGenerator.Generate(typeof(NestedOuter));
        Assert.Contains("message NestedOuter {", schema);
        Assert.Contains("message NestedInner {", schema);
    }

    [Fact]
    public void Generate_SingleType_IncludesEnum()
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(EnumMessage));
        Assert.Contains("enum Priority {", schema);
        Assert.Contains("message EnumMessage {", schema);
    }

    #endregion

    #region GenerateToDirectory — file system output

    [Fact]
    public void GenerateToDirectory_CreatesVersionedSubdirs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ProtoTest_{Guid.NewGuid():N}");
        try
        {
            var paths = ProtoSchemaGenerator.GenerateToDirectory(typeof(SchemaGeneratorTests).Assembly, tempDir);
            Assert.NotEmpty(paths);

            // Check that versioned subdirectory was created
            var v2File = paths.FirstOrDefault(p => p.Contains("v2"));
            Assert.NotNull(v2File);
            Assert.True(File.Exists(v2File));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateToDirectory_AllFilesWritten()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ProtoTest_{Guid.NewGuid():N}");
        try
        {
            var paths = ProtoSchemaGenerator.GenerateToDirectory(typeof(SchemaGeneratorTests).Assembly, tempDir);
            var dict = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);

            Assert.Equal(dict.Count, paths.Count);
            foreach (var path in paths)
            {
                Assert.True(File.Exists(path), $"Expected file to exist: {path}");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Concurrency — thread safety

    [Fact]
    public async Task GenerateForType_ConcurrentCalls_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var schema = ProtoSchemaGenerator.Generate(typeof(SimpleMessage));
            Assert.Contains("message SimpleMessage {", schema);
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task GenerateAll_ConcurrentCalls_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var results = ProtoSchemaGenerator.GenerateAll(typeof(SchemaGeneratorTests).Assembly);
            Assert.NotEmpty(results);
        }));

        await Task.WhenAll(tasks);
    }

    #endregion
}
