using FluentAssertions;
using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Domain;

public class SafeResourcePathTests
{
    [Fact]
    public void FromString_WithValidResPath_ReturnsReadOnlyPath()
    {
        // Act
        var path = SafeResourcePath.FromString("res://scenes/Main.tscn");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("res://scenes/Main.tscn");
        path.Type.Should().Be(PathType.ReadOnly);
    }

    [Fact]
    public void FromString_WithValidUserPath_ReturnsReadWritePath()
    {
        // Act
        var path = SafeResourcePath.FromString("user://saves/save1.json");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("user://saves/save1.json");
        path.Type.Should().Be(PathType.ReadWrite);
    }

    [Fact]
    public void FromString_WithPathTraversal_ReturnsNull()
    {
        // Act
        var path = SafeResourcePath.FromString("res://../../../etc/passwd");

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void FromString_WithAbsolutePath_ReturnsNull()
    {
        // Act
        var path = SafeResourcePath.FromString("C:\\Windows\\System32\\config");

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void FromString_WithRelativePath_ReturnsNull()
    {
        // Act
        var path = SafeResourcePath.FromString("../config/settings.json");

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void FromString_WithHttpUrl_ReturnsNull()
    {
        // Act
        var path = SafeResourcePath.FromString("http://evil.com/malware");

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void FromString_WithEmptyString_ReturnsNull()
    {
        // Act
        var path = SafeResourcePath.FromString("");

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void FromString_WithWhitespace_ReturnsNull()
    {
        // Act
        var path = SafeResourcePath.FromString("   ");

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void ResPath_WithRelativePath_CreatesValidResPath()
    {
        // Act
        var path = SafeResourcePath.ResPath("assets/icon.png");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("res://assets/icon.png");
        path.Type.Should().Be(PathType.ReadOnly);
    }

    [Fact]
    public void ResPath_WithLeadingSlash_CreatesValidResPath()
    {
        // Act
        var path = SafeResourcePath.ResPath("/assets/icon.png");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("res://assets/icon.png");
    }

    [Fact]
    public void ResPath_WithResPrefix_PreservesPrefix()
    {
        // Act
        var path = SafeResourcePath.ResPath("res://scenes/Main.tscn");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("res://scenes/Main.tscn");
    }

    [Fact]
    public void UserPath_WithRelativePath_CreatesValidUserPath()
    {
        // Act
        var path = SafeResourcePath.UserPath("saves/save1.json");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("user://saves/save1.json");
        path.Type.Should().Be(PathType.ReadWrite);
    }

    [Fact]
    public void UserPath_WithUserPrefix_PreservesPrefix()
    {
        // Act
        var path = SafeResourcePath.UserPath("user://config/settings.cfg");

        // Assert
        path.Should().NotBeNull();
        path!.Value.Should().Be("user://config/settings.cfg");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var path = SafeResourcePath.ResPath("scenes/Main.tscn")!;

        // Act
        string pathString = path;

        // Assert
        pathString.Should().Be("res://scenes/Main.tscn");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var path = SafeResourcePath.UserPath("saves/save1.json")!;

        // Act
        var result = path.ToString();

        // Assert
        result.Should().Be("user://saves/save1.json");
    }

    [Fact]
    public void Equality_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var path1 = SafeResourcePath.ResPath("scenes/Main.tscn")!;
        var path2 = SafeResourcePath.ResPath("scenes/Main.tscn")!;

        // Act & Assert
        path1.Should().Be(path2);
        (path1 == path2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var path1 = SafeResourcePath.ResPath("scenes/Main.tscn")!;
        var path2 = SafeResourcePath.ResPath("scenes/Other.tscn")!;

        // Act & Assert
        path1.Should().NotBe(path2);
        (path1 == path2).Should().BeFalse();
    }

    [Fact]
    public void FromString_CaseInsensitive_RecognizesResPrefixes()
    {
        // Act
        var lower = SafeResourcePath.FromString("res://file.txt");
        var upper = SafeResourcePath.FromString("RES://file.txt");
        var mixed = SafeResourcePath.FromString("Res://file.txt");

        // Assert
        lower.Should().NotBeNull();
        upper.Should().NotBeNull();
        mixed.Should().NotBeNull();
    }

    [Fact]
    public void FromString_CaseInsensitive_RecognizesUserPrefixes()
    {
        // Act
        var lower = SafeResourcePath.FromString("user://file.txt");
        var upper = SafeResourcePath.FromString("USER://file.txt");
        var mixed = SafeResourcePath.FromString("User://file.txt");

        // Assert
        lower.Should().NotBeNull();
        upper.Should().NotBeNull();
        mixed.Should().NotBeNull();
    }
}
