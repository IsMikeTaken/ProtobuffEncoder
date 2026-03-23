using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for auto-discovery, ProtoRegistry, and field numbering strategies.
/// Each test uses ProtoRegistry.Reset() to ensure isolation.
/// </summary>
public class AutoDiscoveryTests : IDisposable
{
    public AutoDiscoveryTests()
    {
        ProtoRegistry.Reset();
    }

    public void Dispose()
    {
        ProtoRegistry.Reset();
    }

    #region ProtoRegistry — Registration

    [Fact]
    public void Register_GenericType_IsRegistered()
    {
        // Arrange & Act
        ProtoRegistry.Register<PlainDto>();

        // Assert
        Assert.True(ProtoRegistry.IsRegistered(typeof(PlainDto)));
    }

    [Fact]
    public void Register_TypeOverload_IsRegistered()
    {
        // Arrange & Act
        ProtoRegistry.Register(typeof(PlainDto));

        // Assert
        Assert.True(ProtoRegistry.IsRegistered(typeof(PlainDto)));
    }

    [Fact]
    public void IsRegistered_UnregisteredType_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.False(ProtoRegistry.IsRegistered(typeof(PlainDto)));
    }

    [Fact]
    public void RegisteredTypes_ReturnsAllRegistered()
    {
        // Arrange
        ProtoRegistry.Register<PlainDto>();
        ProtoRegistry.Register<AnotherPlainDto>();

        // Act
        var types = ProtoRegistry.RegisteredTypes;

        // Assert
        Assert.Contains(typeof(PlainDto), types);
        Assert.Contains(typeof(AnotherPlainDto), types);
    }

    [Fact]
    public void Reset_ClearsRegistrations()
    {
        // Arrange
        ProtoRegistry.Register<PlainDto>();
        Assert.True(ProtoRegistry.IsRegistered(typeof(PlainDto)));

        // Act
        ProtoRegistry.Reset();

        // Assert
        Assert.False(ProtoRegistry.IsRegistered(typeof(PlainDto)));
    }

    #endregion

    #region ProtoRegistry — Configuration

    [Fact]
    public void Configure_SetsOptions()
    {
        // Arrange & Act
        ProtoRegistry.Configure(opts =>
        {
            opts.AutoDiscover = true;
            opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
        });

        // Assert
        Assert.True(ProtoRegistry.Options.AutoDiscover);
        Assert.Equal(FieldNumbering.Alphabetical, ProtoRegistry.Options.DefaultFieldNumbering);
    }

    [Fact]
    public void Configure_NullAction_Throws()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProtoRegistry.Configure(null!));
    }

    [Fact]
    public void Options_DefaultValues()
    {
        // Assert
        Assert.False(ProtoRegistry.Options.AutoDiscover);
        Assert.Equal(FieldNumbering.DeclarationOrder, ProtoRegistry.Options.DefaultFieldNumbering);
        Assert.Null(ProtoRegistry.Options.DefaultEncoding);
    }

    #endregion

    #region ProtoRegistry — IsResolvable

    [Fact]
    public void IsResolvable_RegisteredType_ReturnsTrue()
    {
        // Arrange
        ProtoRegistry.Register<PlainDto>();

        // Assert
        Assert.True(ProtoRegistry.IsResolvable(typeof(PlainDto)));
    }

    [Fact]
    public void IsResolvable_ContractType_ReturnsTrue()
    {
        // Assert — SimpleMessage has [ProtoContract]
        Assert.True(ProtoRegistry.IsResolvable(typeof(SimpleMessage)));
    }

    [Fact]
    public void IsResolvable_AutoDiscover_ReturnsTrue()
    {
        // Arrange
        ProtoRegistry.Configure(opts => opts.AutoDiscover = true);

        // Assert — any type is resolvable when auto-discover is on
        Assert.True(ProtoRegistry.IsResolvable(typeof(PlainDto)));
    }

    [Fact]
    public void IsResolvable_NoRegistrationNoAutoDiscover_ReturnsFalse()
    {
        // Assert
        Assert.False(ProtoRegistry.IsResolvable(typeof(PlainDto)));
    }

    #endregion

    #region ProtoRegistry — RegisterAssembly

    [Fact]
    public void RegisterAssembly_RegistersPublicTypes()
    {
        // Arrange & Act
        int count = ProtoRegistry.RegisterAssembly(typeof(PlainDto).Assembly);

        // Assert — at least PlainDto and AnotherPlainDto should be registered
        Assert.True(count > 0);
        Assert.True(ProtoRegistry.IsRegistered(typeof(PlainDto)));
    }

    [Fact]
    public void RegisterAssembly_SkipsContractTypes()
    {
        // Arrange & Act
        ProtoRegistry.RegisterAssembly(typeof(SimpleMessage).Assembly);

        // Assert — SimpleMessage has [ProtoContract], should NOT be registered
        Assert.False(ProtoRegistry.IsRegistered(typeof(SimpleMessage)));
    }

    [Fact]
    public void RegisterAssembly_NullAssembly_Throws()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProtoRegistry.RegisterAssembly(null!));
    }

    #endregion

    #region ContractResolver — Auto-Discovery Resolution

    [Fact]
    public void Resolve_RegisteredType_Succeeds()
    {
        // Arrange
        ProtoRegistry.Register<PlainDto>();

        // Act
        var descriptors = ContractResolver.ResolveImplicit(typeof(PlainDto));

        // Assert
        Assert.True(descriptors.Length >= 2);
        Assert.Contains(descriptors, d => d.Name == "Name");
        Assert.Contains(descriptors, d => d.Name == "Age");
    }

    [Fact]
    public void Resolve_AutoDiscover_ResolvesUnregisteredType()
    {
        // Arrange
        ProtoRegistry.Configure(opts => opts.AutoDiscover = true);

        // Act
        var descriptors = ContractResolver.ResolveImplicit(typeof(AnotherPlainDto));

        // Assert
        Assert.True(descriptors.Length >= 2);
        Assert.Contains(descriptors, d => d.Name == "Title");
        Assert.Contains(descriptors, d => d.Name == "Count");
    }

    [Fact]
    public void ResolveImplicit_AutoAssigns_SequentialFieldNumbers()
    {
        // Arrange & Act
        var descriptors = ContractResolver.ResolveImplicit(typeof(PlainDto));

        // Assert — fields should be 1, 2 (declaration order, +1+1)
        Assert.Equal(1, descriptors[0].FieldNumber);
        Assert.Equal(2, descriptors[1].FieldNumber);
    }

    #endregion

    #region Field Numbering — Declaration Order (default +1+1+1)

    [Fact]
    public void FieldNumbering_DeclarationOrder_AssignsSequentially()
    {
        // Arrange & Act — DeclarationOrderModel has no explicit field numbers
        var descriptors = ContractResolver.ResolveImplicit(typeof(DeclarationOrderModel));

        // Assert — fields should be sequential in declaration order
        Assert.Equal("First", descriptors[0].Name);
        Assert.Equal(1, descriptors[0].FieldNumber);
        Assert.Equal("Second", descriptors[1].Name);
        Assert.Equal(2, descriptors[1].FieldNumber);
        Assert.Equal("Third", descriptors[2].Name);
        Assert.Equal(3, descriptors[2].FieldNumber);
    }

    #endregion

    #region Field Numbering — Alphabetical

    [Fact]
    public void FieldNumbering_Alphabetical_SortsByName()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(AlphabeticalModel));

        // Assert — Alpha, Beta, Gamma in alphabetical order
        Assert.Equal("Alpha", descriptors[0].Name);
        Assert.Equal(1, descriptors[0].FieldNumber);
        Assert.Equal("Beta", descriptors[1].Name);
        Assert.Equal(2, descriptors[1].FieldNumber);
        Assert.Equal("Gamma", descriptors[2].Name);
        Assert.Equal(3, descriptors[2].FieldNumber);
    }

    [Fact]
    public void FieldNumbering_Alphabetical_ViaRegistry()
    {
        // Arrange
        ProtoRegistry.Register<AlphabeticalViaRegistryModel>(FieldNumbering.Alphabetical);

        // Act
        var descriptors = ContractResolver.ResolveImplicit(typeof(AlphabeticalViaRegistryModel));

        // Assert — Charlie, Alpha, Bravo → Alpha(1), Bravo(2), Charlie(3)
        Assert.Equal("Alpha", descriptors[0].Name);
        Assert.Equal(1, descriptors[0].FieldNumber);
        Assert.Equal("Bravo", descriptors[1].Name);
        Assert.Equal(2, descriptors[1].FieldNumber);
        Assert.Equal("Charlie", descriptors[2].Name);
        Assert.Equal(3, descriptors[2].FieldNumber);
    }

    [Fact]
    public void FieldNumbering_Alphabetical_GlobalDefault()
    {
        // Arrange
        ProtoRegistry.Configure(opts =>
        {
            opts.AutoDiscover = true;
            opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
        });

        // Act
        var descriptors = ContractResolver.ResolveImplicit(typeof(GlobalDefaultModel));

        // Assert — Zebra, Mango, Apple → Apple(1), Mango(2), Zebra(3)
        Assert.Equal("Apple", descriptors[0].Name);
        Assert.Equal(1, descriptors[0].FieldNumber);
        Assert.Equal("Mango", descriptors[1].Name);
        Assert.Equal(2, descriptors[1].FieldNumber);
        Assert.Equal("Zebra", descriptors[2].Name);
        Assert.Equal(3, descriptors[2].FieldNumber);
    }

    #endregion

    #region Field Numbering — TypeThenAlphabetical

    [Fact]
    public void FieldNumbering_TypeThenAlphabetical_GroupsByType()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(TypeGroupedModel));

        // Assert — scalars first (alphabetical), then collections, then messages
        // Scalars: Age (int), Name (string) → Age(1), Name(2)
        // Collections: Items (List<string>) → Items(3)
        // Messages: Details (NestedInner) → Details(4)
        Assert.Equal("Age", descriptors[0].Name);
        Assert.Equal(1, descriptors[0].FieldNumber);
        Assert.Equal("Name", descriptors[1].Name);
        Assert.Equal(2, descriptors[1].FieldNumber);
        Assert.Equal("Items", descriptors[2].Name);
        Assert.Equal(3, descriptors[2].FieldNumber);
        Assert.Equal("Details", descriptors[3].Name);
        Assert.Equal(4, descriptors[3].FieldNumber);
    }

    #endregion

    #region Field Numbering — Explicit numbers preserved

    [Fact]
    public void FieldNumbering_ExplicitNumbers_NotReordered()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(MixedNumberingModel));

        // Assert — ExplicitField keeps field number 10 regardless of ordering
        var explicitField = descriptors.First(d => d.Name == "ExplicitField");
        Assert.Equal(10, explicitField.FieldNumber);

        // Auto-assigned fields skip 10
        var autoFields = descriptors.Where(d => d.Name != "ExplicitField").ToArray();
        foreach (var field in autoFields)
        {
            Assert.NotEqual(10, field.FieldNumber);
        }
    }

    [Fact]
    public void FieldNumbering_Alphabetical_ExplicitNumbers_Preserved()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(AlphabeticalWithExplicitModel));

        // Assert — FixedField has explicit number 5
        var fixedField = descriptors.First(d => d.Name == "FixedField");
        Assert.Equal(5, fixedField.FieldNumber);

        // Alpha and Zulu are auto-assigned alphabetically, skipping 5
        var alphaField = descriptors.First(d => d.Name == "Alpha");
        var zuluField = descriptors.First(d => d.Name == "Zulu");
        Assert.True(alphaField.FieldNumber < zuluField.FieldNumber);
        Assert.NotEqual(5, alphaField.FieldNumber);
        Assert.NotEqual(5, zuluField.FieldNumber);
    }

    #endregion

    #region Field Numbering — Priority (per-type > attribute > global)

    [Fact]
    public void GetFieldNumbering_PerTypeRegistration_TakesPriority()
    {
        // Arrange — register with Alphabetical, attribute says DeclarationOrder (default)
        ProtoRegistry.Register<PriorityContractModel>(FieldNumbering.Alphabetical);

        // Act — GetFieldNumbering is internal, test through ContractResolver behavior
        var descriptors = ContractResolver.ResolveImplicit(typeof(PriorityContractModel));

        // Assert — should use Alphabetical from registration
        // Properties: Zebra, Apple → Alphabetical → Apple(1), Zebra(2)
        Assert.Equal("Apple", descriptors[0].Name);
        Assert.Equal("Zebra", descriptors[1].Name);
    }

    [Fact]
    public void GetFieldNumbering_AttributeOverridesGlobal()
    {
        // Arrange
        ProtoRegistry.Configure(opts =>
            opts.DefaultFieldNumbering = FieldNumbering.DeclarationOrder);

        // Act — AlphabeticalModel has [ProtoContract(FieldNumbering = Alphabetical)]
        var descriptors = ContractResolver.Resolve(typeof(AlphabeticalModel));

        // Assert — should use Alphabetical from attribute, not DeclarationOrder from global
        Assert.Equal("Alpha", descriptors[0].Name);
    }

    #endregion

    #region Auto-Discovery — Encode/Decode round-trip

    [Fact]
    public void Encode_Decode_RegisteredType_RoundTrips()
    {
        // Arrange
        ProtoRegistry.Register<RoundTripDto>();
        var original = new RoundTripDto { Id = 42, Label = "test" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<RoundTripDto>(bytes);

        // Assert
        Assert.Equal(42, decoded.Id);
        Assert.Equal("test", decoded.Label);
    }

    [Fact]
    public void Encode_Decode_AutoDiscoveredType_RoundTrips()
    {
        // Arrange
        ProtoRegistry.Configure(opts => opts.AutoDiscover = true);
        var original = new AutoDiscoverDto { Value = 99, Tag = "auto" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AutoDiscoverDto>(bytes);

        // Assert
        Assert.Equal(99, decoded.Value);
        Assert.Equal("auto", decoded.Tag);
    }

    [Fact]
    public void Encode_Decode_AlphabeticalNumbering_RoundTrips()
    {
        // Arrange
        var original = new AlphabeticalRoundTripModel
        {
            Zebra = "last",
            Apple = "first",
            Mango = "middle"
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AlphabeticalRoundTripModel>(bytes);

        // Assert
        Assert.Equal("last", decoded.Zebra);
        Assert.Equal("first", decoded.Apple);
        Assert.Equal("middle", decoded.Mango);
    }

    #endregion

    #region Concurrency

    [Fact]
    public async Task Registry_ConcurrentRegistrations_AllSucceed()
    {
        // Arrange & Act
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            ProtoRegistry.Register(typeof(PlainDto));
            Assert.True(ProtoRegistry.IsRegistered(typeof(PlainDto)));
        }));

        await Task.WhenAll(tasks);

        // Assert
        Assert.True(ProtoRegistry.IsRegistered(typeof(PlainDto)));
    }

    #endregion
}

// ═══════════════════════════════════════════════════
// Test models for auto-discovery and field numbering
// ═══════════════════════════════════════════════════

/// <summary>Plain DTO without [ProtoContract] — for auto-discovery tests.</summary>
public class PlainDto
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

/// <summary>Another plain DTO for registration tests.</summary>
public class AnotherPlainDto
{
    public string Title { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Model for declaration order test — fields declared as First, Second, Third.</summary>
public class DeclarationOrderModel
{
    public string First { get; set; } = "";
    public int Second { get; set; }
    public bool Third { get; set; }
}

/// <summary>Model with [ProtoContract] specifying Alphabetical ordering.</summary>
[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]
public class AlphabeticalModel
{
    // Declared: Gamma, Alpha, Beta — Alphabetical → Alpha(1), Beta(2), Gamma(3)
    public string Gamma { get; set; } = "";
    public string Alpha { get; set; } = "";
    public string Beta { get; set; } = "";
}

/// <summary>Model for testing alphabetical via registry registration.</summary>
public class AlphabeticalViaRegistryModel
{
    // Declared: Charlie, Alpha, Bravo
    public string Charlie { get; set; } = "";
    public string Alpha { get; set; } = "";
    public string Bravo { get; set; } = "";
}

/// <summary>Model for testing global default field numbering.</summary>
public class GlobalDefaultModel
{
    public string Zebra { get; set; } = "";
    public string Mango { get; set; } = "";
    public string Apple { get; set; } = "";
}

/// <summary>Model with TypeThenAlphabetical grouping.</summary>
[ProtoContract(FieldNumbering = FieldNumbering.TypeThenAlphabetical)]
public class TypeGroupedModel
{
    // Declared: Details(message), Items(collection), Name(scalar), Age(scalar)
    // Expected: Age(1), Name(2), Items(3), Details(4)
    public NestedInner Details { get; set; } = new();
    public List<string> Items { get; set; } = [];
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

/// <summary>Model mixing explicit and auto-assigned field numbers.</summary>
[ProtoContract]
public class MixedNumberingModel
{
    public string AutoOne { get; set; } = "";
    [ProtoField(10)] public int ExplicitField { get; set; }
    public string AutoTwo { get; set; } = "";
}

/// <summary>Model with alphabetical ordering and an explicit field number.</summary>
[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]
public class AlphabeticalWithExplicitModel
{
    public string Zulu { get; set; } = "";
    [ProtoField(5)] public int FixedField { get; set; }
    public string Alpha { get; set; } = "";
}

/// <summary>Model for priority test — registered with override.</summary>
public class PriorityContractModel
{
    public string Zebra { get; set; } = "";
    public string Apple { get; set; } = "";
}

/// <summary>DTO for encode/decode round-trip with registry.</summary>
public class RoundTripDto
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>DTO for encode/decode round-trip with auto-discover.</summary>
public class AutoDiscoverDto
{
    public int Value { get; set; }
    public string Tag { get; set; } = "";
}

/// <summary>Model for alphabetical round-trip test.</summary>
[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]
public class AlphabeticalRoundTripModel
{
    public string Zebra { get; set; } = "";
    public string Apple { get; set; } = "";
    public string Mango { get; set; } = "";
}
