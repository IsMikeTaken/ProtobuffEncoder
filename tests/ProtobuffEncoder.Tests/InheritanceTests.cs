namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for inheritance via [ProtoInclude] and [IncludeBaseFields].
/// </summary>
public class InheritanceTests
{
    [Fact]
    public void Encode_Decode_DerivedType_IncludesBaseFields()
    {
        // Arrange
        var original = new DogModel { Name = "Rex", Breed = "Labrador" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<DogModel>(bytes);

        // Assert
        Assert.Equal("Rex", decoded.Name);
        Assert.Equal("Labrador", decoded.Breed);
    }

    [Fact]
    public void Encode_Decode_AnotherDerivedType_IncludesBaseFields()
    {
        // Arrange
        var original = new CatModel { Name = "Whiskers", IsIndoor = true };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<CatModel>(bytes);

        // Assert
        Assert.Equal("Whiskers", decoded.Name);
        Assert.True(decoded.IsIndoor);
    }

    [Fact]
    public void Encode_Decode_BaseTypeOnly_RoundTrips()
    {
        // Arrange
        var original = new AnimalModel { Name = "Generic" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AnimalModel>(bytes);

        // Assert
        Assert.Equal("Generic", decoded.Name);
    }

    [Fact]
    public void Encode_Decode_DerivedWithEmptyBaseFields_RoundTrips()
    {
        // Arrange
        var original = new DogModel { Name = "", Breed = "Unknown" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<DogModel>(bytes);

        // Assert
        Assert.Equal("", decoded.Name);
        Assert.Equal("Unknown", decoded.Breed);
    }

    [Fact]
    public void Encode_Decode_AdminProfile_TwoLevelInheritance()
    {
        // Arrange
        var original = new AdminProfile
        {
            DisplayName = "John Admin",
            Age = 35,
            Department = "Engineering"
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AdminProfile>(bytes);

        // Assert
        Assert.Equal("John Admin", decoded.DisplayName);
        Assert.Equal(35, decoded.Age);
        Assert.Equal("Engineering", decoded.Department);
    }

    [Fact]
    public async Task Encode_Decode_InheritanceConcurrently_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            if (i % 2 == 0)
            {
                var dog = new DogModel { Name = $"Dog-{i}", Breed = $"Breed-{i}" };

                // Act
                var bytes = ProtobufEncoder.Encode(dog);
                var decoded = ProtobufEncoder.Decode<DogModel>(bytes);

                // Assert
                Assert.Equal($"Dog-{i}", decoded.Name);
                Assert.Equal($"Breed-{i}", decoded.Breed);
            }
            else
            {
                var cat = new CatModel { Name = $"Cat-{i}", IsIndoor = i % 3 == 0 };

                // Act
                var bytes = ProtobufEncoder.Encode(cat);
                var decoded = ProtobufEncoder.Decode<CatModel>(bytes);

                // Assert
                Assert.Equal($"Cat-{i}", decoded.Name);
            }
        }));

        // Act & Assert
        await Task.WhenAll(tasks);
    }
}
