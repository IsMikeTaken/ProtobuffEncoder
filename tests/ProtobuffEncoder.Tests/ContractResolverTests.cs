using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for ContractResolver: field resolution, auto-numbering,
/// ExplicitFields, ImplicitFields, ProtoIgnore, IncludeBaseFields,
/// reserved number avoidance, and caching behavior.
/// </summary>
public class ContractResolverTests
{
    [Fact]
    public void Resolve_SimpleMessage_ReturnsCorrectFieldCount()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(SimpleMessage));

        // Assert
        Assert.Equal(3, descriptors.Length);
        Assert.Contains(descriptors, d => d.Name == "Id" && d.FieldNumber == 1);
        Assert.Contains(descriptors, d => d.Name == "Name" && d.FieldNumber == 2);
        Assert.Contains(descriptors, d => d.Name == "IsActive" && d.FieldNumber == 3);
    }

    [Fact]
    public void Resolve_SameType_ReturnsCachedResult()
    {
        // Arrange & Act
        var first = ContractResolver.Resolve(typeof(SimpleMessage));
        var second = ContractResolver.Resolve(typeof(SimpleMessage));

        // Assert — same reference (cached)
        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_AutoNumbering_AssignsSequentially()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(AllScalarsMessage));

        // Assert — explicit numbers 1-13/field numbers are assigned
        for (int i = 0; i < descriptors.Length; i++)
        {
            Assert.Equal(i + 1, descriptors[i].FieldNumber);
        }
    }

    [Fact]
    public void Resolve_ExplicitFields_OnlyReturnsMarkedProperties()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(ExplicitMessage));

        // Assert
        Assert.Equal(2, descriptors.Length); // Included and AlsoIncluded
        Assert.Contains(descriptors, d => d.Name == "Included");
        Assert.Contains(descriptors, d => d.Name == "AlsoIncluded");
        Assert.DoesNotContain(descriptors, d => d.Name == "Excluded");
    }

    [Fact]
    public void Resolve_IgnoredField_IsExcluded()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(IgnoredFieldMessage));

        // Assert
        Assert.Single(descriptors);
        Assert.Equal("Visible", descriptors[0].Name);
    }

    [Fact]
    public void Resolve_IncludeBaseFields_IncludesParentProperties()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(DogModel));

        // Assert — should include Name from base + Breed
        Assert.True(descriptors.Length >= 2);
        Assert.Contains(descriptors, d => d.Name == "Name");
        Assert.Contains(descriptors, d => d.Name == "Breed");
    }

    [Fact]
    public void GetIncludes_BaseType_ReturnsDerivedMappings()
    {
        // Arrange & Act
        var includes = ContractResolver.GetIncludes(typeof(AnimalModel));

        // Assert
        Assert.Equal(2, includes.Length);
        Assert.Contains(includes, i => i.DerivedType == typeof(DogModel) && i.FieldNumber == 10);
        Assert.Contains(includes, i => i.DerivedType == typeof(CatModel) && i.FieldNumber == 11);
    }

    [Fact]
    public void Resolve_ReservedNumbers_AutoAssignmentSkipsReserved()
    {
        // Arrange & Act — AnimalModel has ProtoInclude at 10 and 11
        var descriptors = ContractResolver.Resolve(typeof(AnimalModel));

        // Assert — field numbers should not include 10 or 11
        foreach (var descriptor in descriptors)
        {
            Assert.NotEqual(10, descriptor.FieldNumber);
            Assert.NotEqual(11, descriptor.FieldNumber);
        }
    }

    [Fact]
    public void Resolve_ImplicitParent_ResolvesChildWithoutAttribute()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(ImplicitParent));

        // Assert — should include the Child property
        Assert.Contains(descriptors, d => d.Name == "Child");
    }

    [Fact]
    public void ResolveImplicit_UnattributedType_Succeeds()
    {
        // Arrange & Act — ImplicitChild has no [ProtoContract]
        var descriptors = ContractResolver.ResolveImplicit(typeof(ImplicitChild));

        // Assert
        Assert.True(descriptors.Length >= 2);
        Assert.Contains(descriptors, d => d.Name == "Value");
        Assert.Contains(descriptors, d => d.Name == "Label");
    }

    [Fact]
    public void IsContractType_DecoratedType_ReturnsTrue()
    {
        // Arrange & Act & Assert
        Assert.True(ContractResolver.IsContractType(typeof(SimpleMessage)));
    }

    [Fact]
    public void IsContractType_UnDecoratedType_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.False(ContractResolver.IsContractType(typeof(ImplicitChild)));
    }

    [Fact]
    public void Resolve_TypeWithoutAttribute_Throws()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => ContractResolver.Resolve(typeof(System.Text.StringBuilder)));
    }

    [Fact]
    public void Resolve_IntProperty_HasVarintWireType()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(SimpleMessage));
        var idField = descriptors.First(d => d.Name == "Id");

        // Assert
        Assert.Equal(WireType.Varint, idField.WireType);
    }

    [Fact]
    public void Resolve_StringProperty_HasLengthDelimitedWireType()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(SimpleMessage));
        var nameField = descriptors.First(d => d.Name == "Name");

        // Assert
        Assert.Equal(WireType.LengthDelimited, nameField.WireType);
    }

    [Fact]
    public void Resolve_BoolProperty_HasVarintWireType()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(SimpleMessage));
        var activeField = descriptors.First(d => d.Name == "IsActive");

        // Assert
        Assert.Equal(WireType.Varint, activeField.WireType);
    }

    [Fact]
    public void Resolve_FloatProperty_HasFixed32WireType()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(AllScalarsMessage));
        var floatField = descriptors.First(d => d.Name == "FloatValue");

        // Assert
        Assert.Equal(WireType.Fixed32, floatField.WireType);
    }

    [Fact]
    public void Resolve_DoubleProperty_HasFixed64WireType()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(AllScalarsMessage));
        var doubleField = descriptors.First(d => d.Name == "DoubleValue");

        // Assert
        Assert.Equal(WireType.Fixed64, doubleField.WireType);
    }

    [Fact]
    public void Resolve_ListProperty_IsCollection()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(ListMessage));
        var numbersField = descriptors.First(d => d.Name == "Numbers");

        // Assert
        Assert.True(numbersField.IsCollection);
        Assert.Equal(typeof(int), numbersField.ElementType);
    }

    [Fact]
    public void Resolve_ArrayProperty_IsCollection()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(ArrayMessage));
        var scoresField = descriptors.First(d => d.Name == "Scores");

        // Assert
        Assert.True(scoresField.IsCollection);
    }

    [Fact]
    public void Resolve_MapProperty_IsMap()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(MapMessage));
        var tagsField = descriptors.First(d => d.Name == "Tags");

        // Assert
        Assert.True(tagsField.IsMap);
        Assert.Equal(typeof(string), tagsField.MapKeyType);
        Assert.Equal(typeof(string), tagsField.MapValueType);
    }

    [Fact]
    public void Resolve_OneOfProperty_HasGroupName()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(OneOfMessage));
        var emailField = descriptors.First(d => d.Name == "Email");
        var phoneField = descriptors.First(d => d.Name == "Phone");

        // Assert
        Assert.Equal("contact", emailField.OneOfGroup);
        Assert.Equal("contact", phoneField.OneOfGroup);
    }

    [Fact]
    public void Resolve_RegularProperty_HasNoOneOfGroup()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(OneOfMessage));
        var nameField = descriptors.First(d => d.Name == "Name");

        // Assert
        Assert.Null(nameField.OneOfGroup);
    }

    [Fact]
    public void Resolve_RequiredField_IsMarkedRequired()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(RequiredFieldMessage));
        var requiredField = descriptors.First(d => d.Name == "MustHaveValue");

        // Assert
        Assert.True(requiredField.IsRequired);
    }

    [Fact]
    public void Resolve_WriteDefaultField_HasWriteDefaultTrue()
    {
        // Arrange & Act
        var descriptors = ContractResolver.Resolve(typeof(WriteDefaultMessage));
        var alwaysField = descriptors.First(d => d.Name == "AlwaysWritten");
        var normalField = descriptors.First(d => d.Name == "SkippedWhenDefault");

        // Assert
        Assert.True(alwaysField.WriteDefault);
        Assert.False(normalField.WriteDefault);
    }

    [Fact]
    public async Task Resolve_ConcurrentCalls_AllReturnSameInstance()
    {
        // Arrange
        var results = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
                ContractResolver.Resolve(typeof(EnumMessage)))));

        // Act & Assert
        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));
    }
}
