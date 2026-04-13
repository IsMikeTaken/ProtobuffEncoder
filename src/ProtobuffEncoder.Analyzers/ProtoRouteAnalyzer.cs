using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// Validates assembly-level [ProtoRoute] attributes.
/// Reports: PROTO016 (empty folder name), PROTO017 (no tokens).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProtoRouteAnalyzer : DiagnosticAnalyzer
{
    private const string ProtoRouteAttributeName = "ProtobuffEncoder.Attributes.ProtoRouteAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.ProtoRouteEmptyFolder,
        DiagnosticDescriptors.ProtoRouteNoTokens,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var assemblyName = context.Compilation.AssemblyName ?? "<unknown>";

        foreach (var attr in context.Compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != ProtoRouteAttributeName)
                continue;

            var location = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                           ?? Location.None;

            // First constructor argument is the folder name.
            string? folder = attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value as string
                : null;

            // PROTO016: empty or whitespace folder
            if (string.IsNullOrWhiteSpace(folder))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ProtoRouteEmptyFolder,
                    location,
                    assemblyName));
                continue;
            }

            // PROTO017: params string[] tokens is second argument (array)
            bool hasTokens = attr.ConstructorArguments.Length > 1 &&
                             attr.ConstructorArguments[1] is { Kind: TypedConstantKind.Array } arr &&
                             !arr.Values.IsDefaultOrEmpty;

            if (!hasTokens)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ProtoRouteNoTokens,
                    location,
                    folder,
                    assemblyName));
            }
        }
    }
}
