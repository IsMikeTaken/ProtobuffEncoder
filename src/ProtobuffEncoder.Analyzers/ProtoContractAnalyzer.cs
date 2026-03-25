using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// Analyzes types marked with [ProtoContract] for common configuration mistakes.
/// Reports: PROTO001, PROTO002, PROTO003, PROTO004, PROTO008, PROTO009.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProtoContractAnalyzer : DiagnosticAnalyzer
{
    private const string ProtoContractAttributeName = "ProtobuffEncoder.Attributes.ProtoContractAttribute";
    private const string ProtoFieldAttributeName = "ProtobuffEncoder.Attributes.ProtoFieldAttribute";
    private const string ProtoIgnoreAttributeName = "ProtobuffEncoder.Attributes.ProtoIgnoreAttribute";
    private const string ProtoOneOfAttributeName = "ProtobuffEncoder.Attributes.ProtoOneOfAttribute";
    private const string ProtoMapAttributeName = "ProtobuffEncoder.Attributes.ProtoMapAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.ProtoContractWithoutFields,
            DiagnosticDescriptors.DuplicateFieldNumber,
            DiagnosticDescriptors.MissingParameterlessConstructor,
            DiagnosticDescriptors.PropertyWithoutSetter,
            DiagnosticDescriptors.MutableStructContract,
            DiagnosticDescriptors.SingleMemberOneOf
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

        // Only analyze types with [ProtoContract]
        var contractAttr = GetAttribute(namedType, ProtoContractAttributeName);
        if (contractAttr is null)
            return;

        var isExplicitFields = GetNamedBoolArg(contractAttr, "ExplicitFields");

        // PROTO008: Struct warning
        if (namedType is { IsValueType: true, TypeKind: TypeKind.Struct })
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MutableStructContract,
                namedType.Locations[0],
                namedType.Name));
        }

        // PROTO003: Check for parameterless constructor
        if (namedType.TypeKind == TypeKind.Class)
        {
            bool hasParameterless = false;
            foreach (var ctor in namedType.Constructors)
            {
                if (ctor.IsImplicitlyDeclared || (ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility >= Accessibility.Internal))
                {
                    hasParameterless = true;
                    break;
                }
            }

            if (!hasParameterless)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingParameterlessConstructor,
                    namedType.Locations[0],
                    namedType.Name));
            }
        }

        // Collect properties
        var properties = namedType.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToList();

        // PROTO001: No serializable properties
        var serialisableProps = properties
            .Where(p => p.GetMethod is not null && p.SetMethod is not null)
            .Where(p => GetAttribute(p, ProtoIgnoreAttributeName) is null)
            .ToList();

        if (isExplicitFields)
        {
            serialisableProps = serialisableProps
                .Where(p => GetAttribute(p, ProtoFieldAttributeName) is not null
                            || GetAttribute(p, ProtoMapAttributeName) is not null)
                .ToList();
        }

        if (serialisableProps.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ProtoContractWithoutFields,
                namedType.Locations[0],
                namedType.Name));
        }

        // PROTO004: Properties without a setter (only non-ignored, non-explicit-fields)
        if (!isExplicitFields)
        {
            foreach (var prop in properties)
            {
                if (GetAttribute(prop, ProtoIgnoreAttributeName) is not null)
                    continue;

                if (prop.GetMethod is not null && prop.SetMethod is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PropertyWithoutSetter,
                        prop.Locations[0],
                        prop.Name,
                        namedType.Name));
                }
            }
        }

        // PROTO002: Duplicate field numbers
        var fieldNumbers = new Dictionary<int, string>();
        foreach (var prop in serialisableProps)
        {
            var fieldAttr = GetAttribute(prop, ProtoFieldAttributeName);
            if (fieldAttr is null)
                continue;

            int fieldNumber = GetFieldNumber(fieldAttr);
            if (fieldNumber <= 0)
                continue;

            if (fieldNumbers.TryGetValue(fieldNumber, out var existingName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateFieldNumber,
                    prop.Locations[0],
                    fieldNumber,
                    existingName,
                    prop.Name,
                    namedType.Name));
            }
            else
            {
                fieldNumbers[fieldNumber] = prop.Name;
            }
        }

        // PROTO009: OneOf groups with single member
        var oneOfGroups = new Dictionary<string, List<string>>();
        foreach (var prop in serialisableProps)
        {
            var oneOfAttr = GetAttribute(prop, ProtoOneOfAttributeName);
            if (oneOfAttr is null)
                continue;

            string? groupName = oneOfAttr.ConstructorArguments.Length > 0
                ? oneOfAttr.ConstructorArguments[0].Value as string
                : null;

            if (groupName is null)
                continue;

            if (!oneOfGroups.TryGetValue(groupName, out var members))
            {
                members = new List<string>();
                oneOfGroups[groupName] = members;
            }
            members.Add(prop.Name);
        }

        foreach (var kvp in oneOfGroups)
        {
            if (kvp.Value.Count == 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.SingleMemberOneOf,
                    namedType.Locations[0],
                    kvp.Key,
                    namedType.Name));
            }
        }
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string fullName)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == fullName);
    }

    private static bool GetNamedBoolArg(AttributeData attr, string name)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is bool b)
                return b;
        }
        return false;
    }

    private static int GetFieldNumber(AttributeData fieldAttr)
    {
        if (fieldAttr.ConstructorArguments.Length > 0 && fieldAttr.ConstructorArguments[0].Value is int n)
            return n;

        foreach (var arg in fieldAttr.NamedArguments)
        {
            if (arg is { Key: "FieldNumber", Value.Value: int fn })
                return fn;
        }

        return 0;
    }
}
