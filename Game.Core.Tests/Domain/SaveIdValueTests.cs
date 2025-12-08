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
    public void Constructor_accepts_valid_ids(string value)
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
    public void Constructor_rejects_ids_longer_than_64_characters()
    {
        var tooLong = new string('a', 65);

        Action act = () => _ = new SaveIdValue(tooLong);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryCreate_returns_true_for_valid_value()
    {
        var ok = SaveIdValue.TryCreate("save-1", out var value);

        ok.Should().BeTrue();
        value.Should().NotBeNull();
        value!.Value.Should().Be("save-1");
    }

    [Fact]
    public void TryCreate_returns_false_for_invalid_value()
    {
        var ok = SaveIdValue.TryCreate("invalid/value", out var value);

        ok.Should().BeFalse();
        value.Should().BeNull();
    }
}

