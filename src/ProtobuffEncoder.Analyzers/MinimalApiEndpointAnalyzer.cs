using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// Analyzes MapProtobufSender, MapProtobufReceiver, MapProtobufDuplex, and MapProtobufWebSocket invocations.
/// Reports PROTO016 if the type parameters do not have [ProtoContract].
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MinimalApiEndpointAnalyzer : DiagnosticAnalyzer
{
    private const string ProtoContractAttributeName = "ProtobuffEncoder.Attributes.ProtoContractAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.MinimalApiTypeMustBeContract
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        var methodName = method.Name;
        if (methodName != "MapProtobufSender" &&
            methodName != "MapProtobufReceiver" &&
            methodName != "MapProtobufDuplex" &&
            methodName != "MapProtobufWebSocket")
        {
            return;
        }

        var containingType = method.ContainingType?.ToDisplayString();
        if (containingType != "ProtobuffEncoder.AspNetCore.Setup.ProtobufMinimalApiExtensions" &&
            containingType != "ProtobuffEncoder.WebSockets.WebSocketEndpointRouteBuilderExtensions")
        {
            return;
        }

        foreach (var typeArg in method.TypeArguments)
        {
            if (typeArg.TypeKind == TypeKind.TypeParameter)
                continue;

            // Optional: If the type arg is a framework type we map automatically (like string, int)
            // wait, we only allow reference types in these endpoints `class, new()`.

            var hasContract = typeArg.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == ProtoContractAttributeName);

            if (!hasContract && typeArg.TypeKind == TypeKind.Class)
            {
                // Emit warning PROTO016
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MinimalApiTypeMustBeContract,
                    invocation.Syntax.GetLocation(),
                    typeArg.Name,
                    methodName));
            }
        }
    }
}
