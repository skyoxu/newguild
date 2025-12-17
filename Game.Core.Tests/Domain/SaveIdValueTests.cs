using System;
using FluentAssertions;
using Game.Core.Domain.Turn;
using Xunit;

namespace Game.Core.Tests.Domain;

public class SaveIdValueTests
{
    [Theory]
    [InlineData("save-1")]
    [InlineData("save_2")]
    [InlineData("save123")]
    [InlineData("SLOT_01")]
    public void Constructor_Accepts_Valid_Ids(string value)
    {
        var id = new SaveIdValue(value);

        id.Value.Should().Be(value);
        id.ToString().Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("save/../etc")]
    [InlineData("save@invalid")]
    [InlineData("'; DROP TABLE;--")]
    public void Constructor_rejects_invalid_ids(string value)
    {
        Action act = () => _ = new SaveIdValue(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Rejects_Ids_Longer_Than_T64_Characters()
    {
        var tooLong = new string('a', 65);

        Action act = () => _ = new SaveIdValue(tooLong);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryCreate_Returns_True_For_Valid_Value()
    {
        var ok = SaveIdValue.TryCreate("save-1", out var value);

        ok.Should().BeTrue();
        value.Should().NotBeNull();
        value!.Value.Should().Be("save-1");
    }

    [Fact]
    public void TryCreate_Returns_False_For_Invalid_Value()
    {
        var ok = SaveIdValue.TryCreate("invalid/value", out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }
}

