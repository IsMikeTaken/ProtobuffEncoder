using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// Analyzes [ProtoInclude] and [ProtoMap] usage for correctness.
/// Reports: PROTO013, PROTO014, PROTO015.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProtoIncludeAnalyzer : DiagnosticAnalyzer
{
    private const string ProtoIncludeAttributeName = "ProtobuffEncoder.Attributes.ProtoIncludeAttribute";
    private const string ProtoFieldAttributeName = "ProtobuffEncoder.Attributes.ProtoFieldAttribute";
    private const string ProtoMapAttributeName = "ProtobuffEncoder.Attributes.ProtoMapAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.IncludeFieldNumberConflict,
        DiagnosticDescriptors.IncludeNotDerived,
        DiagnosticDescriptors.MapOnNonDictionary
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        var includeAttrs = namedType.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == ProtoIncludeAttributeName)
            .ToList();

        if (includeAttrs.Count == 0)
            return;

        // Collect existing ProtoField numbers on this type
        var fieldNumbers = new HashSet<int>();
        foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
        {
            var fieldAttr = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ProtoFieldAttributeName);
            if (fieldAttr is null)
                continue;

            int num = GetIntArg(fieldAttr);
            if (num > 0)
                fieldNumbers.Add(num);
        }

        foreach (var includeAttr in includeAttrs)
        {
            if (includeAttr.ConstructorArguments.Length < 2)
                continue;

            int includeFieldNumber = includeAttr.ConstructorArguments[0].Value is int fn ? fn : 0;
            var derivedTypeSymbol = includeAttr.ConstructorArguments[1].Value as INamedTypeSymbol;

            // PROTO013: Include field number conflicts with a ProtoField number
            if (includeFieldNumber > 0 && fieldNumbers.Contains(includeFieldNumber))
            {
                string derivedName = derivedTypeSymbol?.Name ?? "unknown";
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.IncludeFieldNumberConflict,
                    namedType.Locations[0],
                    includeFieldNumber,
                    derivedName,
                    namedType.Name));
            }

            // PROTO014: Derived type does not inherit from this type
            if (derivedTypeSymbol is not null)
            {
                bool isDerived = false;
                var baseType = derivedTypeSymbol.BaseType;
                while (baseType is not null)
                {
                    if (SymbolEqualityComparer.Default.Equals(baseType, namedType))
                    {
                        isDerived = true;
                        break;
                    }
                    baseType = baseType.BaseType;
                }

                if (!isDerived)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.IncludeNotDerived,
                        namedType.Locations[0],
                        namedType.Name,
                        derivedTypeSymbol.Name));
                }
            }
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;

        var mapAttr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ProtoMapAttributeName);

        if (mapAttr is null)
            return;

        // PROTO015: ProtoMap on non-Dictionary property
        if (!IsDictionaryType(property.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MapOnNonDictionary,
                property.Locations[0],
                property.Name,
                property.ContainingType?.Name ?? "unknown"));
        }
    }

    private static bool IsDictionaryType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var fullName = named.ConstructedFrom.ToDisplayString();
            return fullName == "System.Collections.Generic.Dictionary<TKey, TValue>"
                || fullName == "System.Collections.Generic.IDictionary<TKey, TValue>";
        }
        return false;
    }

    private static int GetIntArg(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int n)
            return n;

        foreach (var arg in attr.NamedArguments)
        {
            if (arg is { Key: "FieldNumber", Value.Value: int fn })
                return fn;
        }
        return 0;
    }
}
