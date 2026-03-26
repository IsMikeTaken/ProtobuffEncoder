using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// Analyzes individual [ProtoField] usages for correctness.
/// Reports: PROTO005, PROTO006, PROTO007, PROTO010.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProtoFieldAnalyzer : DiagnosticAnalyzer
{
    private const string ProtoContractAttributeName = "ProtobuffEncoder.Attributes.ProtoContractAttribute";
    private const string ProtoFieldAttributeName = "ProtobuffEncoder.Attributes.ProtoFieldAttribute";

    private static readonly HashSet<string> KnownEncodings = new(StringComparer.OrdinalIgnoreCase)
    {
        "utf-8", "utf8", "utf-16", "utf16", "unicode",
        "utf-16be", "utf16be", "utf-32", "utf32",
        "ascii", "us-ascii", "latin-1", "latin1", "iso-8859-1"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.FieldWithoutContract,
            DiagnosticDescriptors.InvalidFieldNumber,
            DiagnosticDescriptors.ReservedFieldNumber,
            DiagnosticDescriptors.UnrecognisedEncoding
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;

        var fieldAttr = GetAttribute(property, ProtoFieldAttributeName);
        if (fieldAttr is null)
            return;

        var containingType = property.ContainingType;
        if (containingType is null)
            return;

        // PROTO005: [ProtoField] without [ProtoContract] on the type
        var contractAttr = GetAttribute(containingType, ProtoContractAttributeName);
        if (contractAttr is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.FieldWithoutContract,
                property.Locations[0],
                property.Name,
                containingType.Name));
        }

        // Extract field number
        int fieldNumber = GetFieldNumber(fieldAttr);

        // PROTO006: Invalid field number (less than 1 or exceeds max)
        if (fieldNumber < 0 || fieldNumber > 536_870_911)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidFieldNumber,
                property.Locations[0],
                fieldNumber,
                property.Name));
        }

        // PROTO007: Reserved range 19000–19999
        if (fieldNumber is >= 19_000 and <= 19_999)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ReservedFieldNumber,
                property.Locations[0],
                fieldNumber,
                property.Name));
        }

        // PROTO010: Unrecognized encoding on a field
        var encodingArg = GetNamedStringArg(fieldAttr, "Encoding");
        if (encodingArg is not null && !KnownEncodings.Contains(encodingArg))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnrecognisedEncoding,
                property.Locations[0],
                encodingArg,
                property.Name));
        }

        // Also check the contract-level default encoding if present
        if (contractAttr is not null)
        {
            var defaultEncoding = GetNamedStringArg(contractAttr, "DefaultEncoding");
            if (defaultEncoding is not null && !KnownEncodings.Contains(defaultEncoding))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnrecognisedEncoding,
                    containingType.Locations[0],
                    defaultEncoding,
                    containingType.Name));
            }
        }
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string fullName)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == fullName);
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

    private static string? GetNamedStringArg(AttributeData attr, string name)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is string s)
                return s;
        }
        return null;
    }
}
