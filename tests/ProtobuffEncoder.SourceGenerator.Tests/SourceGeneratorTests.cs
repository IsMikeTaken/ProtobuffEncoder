using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProtobuffEncoder.SourceGenerator;
using Xunit;

namespace ProtobuffEncoder.SourceGenerator.Tests
{
    public class SourceGeneratorTests
    {
        [Fact]
        public void Generator_Creates_Status_Class_For_ProtoContracts()
        {
            // Arrange
            string source = @"
using ProtobuffEncoder.Attributes;

namespace TestNamespace
{
    [ProtoContract]
    public class MyMessage
    {
        [ProtoField(1)] public int Id { get; set; }
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ProtobuffEncoder.Attributes.ProtoContractAttribute).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create("TestComp",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ProtoSchemaGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult();

            // Assert
            Assert.Single(result.Diagnostics);
            Assert.Equal("PROTO018", result.Diagnostics[0].Id);
            Assert.Single(result.GeneratedTrees);

            var generatedSource = result.GeneratedTrees[0].GetText().ToString();
            Assert.Contains("SchemaGenerationStatus", generatedSource);
            Assert.Contains("Found 1 types", generatedSource);
            Assert.Contains("TestNamespace.MyMessage", generatedSource);
        }

        [Fact]
        public void Generator_Creates_Status_Class_For_ProtoServices()
        {
            // Arrange
            string source = @"
using ProtobuffEncoder.Attributes;

namespace TestNamespace
{
    [ProtoService(""MyService"")]
    public interface IMyService
    {
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ProtobuffEncoder.Attributes.ProtoServiceAttribute).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create("TestComp",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ProtoSchemaGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult();

            // Assert
            Assert.Single(result.Diagnostics);
            Assert.Equal("PROTO018", result.Diagnostics[0].Id);
            Assert.Single(result.GeneratedTrees);

            var generatedSource = result.GeneratedTrees[0].GetText().ToString();
            Assert.Contains("SchemaGenerationStatus", generatedSource);
            Assert.Contains("Found 1 types", generatedSource);
            Assert.Contains("TestNamespace.IMyService", generatedSource);
        }
    }
}
