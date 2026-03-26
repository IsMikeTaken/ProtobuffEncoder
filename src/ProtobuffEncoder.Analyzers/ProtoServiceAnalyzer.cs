using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// Analyzes [ProtoService] interfaces and [ProtoMethod] declarations.
/// Reports: PROTO011, PROTO012.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProtoServiceAnalyzer : DiagnosticAnalyzer
{
    private const string ProtoServiceAttributeName = "ProtobuffEncoder.Attributes.ProtoServiceAttribute";
    private const string ProtoMethodAttributeName = "ProtobuffEncoder.Attributes.ProtoMethodAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.ServiceWithoutMethods,
        DiagnosticDescriptors.StreamingReturnTypeMismatch
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        var serviceAttr = namedType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ProtoServiceAttributeName);

        if (serviceAttr is null)
            return;

        var methods = namedType.GetMembers().OfType<IMethodSymbol>().ToList();

        var protoMethods = methods
            .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ProtoMethodAttributeName))
            .ToList();

        // PROTO011: No methods with [ProtoMethod]
        if (protoMethods.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ServiceWithoutMethods,
                namedType.Locations[0],
                namedType.Name));
        }

        // PROTO012: Validate return types match method type
        foreach (var method in protoMethods)
        {
            var methodAttr = method.GetAttributes()
                .First(a => a.AttributeClass?.ToDisplayString() == ProtoMethodAttributeName);

            if (methodAttr.ConstructorArguments.Length == 0)
                continue;

            var methodTypeValue = methodAttr.ConstructorArguments[0].Value;
            if (methodTypeValue is not int methodType)
                continue;

            // ServerStreaming = 1, DuplexStreaming = 3
            bool isStreaming = methodType == 1 || methodType == 3;
            bool returnsAsyncEnumerable = IsAsyncEnumerable(method.ReturnType);

            if (isStreaming && !returnsAsyncEnumerable)
            {
                string methodTypeName = methodType == 1 ? "ServerStreaming" : "DuplexStreaming";
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.StreamingReturnTypeMismatch,
                    method.Locations[0],
                    method.Name,
                    namedType.Name,
                    methodTypeName));
            }
        }
    }

    private static bool IsAsyncEnumerable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var fullName = named.ConstructedFrom.ToDisplayString();
            return fullName == "System.Collections.Generic.IAsyncEnumerable<T>";
        }
        return false;
    }
}
