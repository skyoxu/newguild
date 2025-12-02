using System;
using FluentAssertions;
using Game.Core.Contracts;
using Xunit;

namespace Game.Core.Tests.Contracts;

public class DomainEventTests
{
    [Fact]
    public void Constructor_WithAllParameters_CreatesValidEvent()
    {
        // Arrange
        var type = "test.event.created";
        var source = "TestSource";
        var data = new { Value = "test-data" };
        var timestamp = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString();

        // Act
        var domainEvent = new DomainEvent(
            Type: type,
            Source: source,
            Data: data,
            Timestamp: timestamp,
            Id: id
        );

        // Assert
        domainEvent.Type.Should().Be(type);
        domainEvent.Source.Should().Be(source);
        domainEvent.Data.Should().Be(data);
        domainEvent.Timestamp.Should().Be(timestamp);
        domainEvent.Id.Should().Be(id);
        domainEvent.SpecVersion.Should().Be("1.0");
        domainEvent.DataContentType.Should().Be("application/json");
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesDefaultValues()
    {
        // Arrange & Act
        var domainEvent = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: "test-id"
        );

        // Assert
        domainEvent.SpecVersion.Should().Be("1.0");
        domainEvent.DataContentType.Should().Be("application/json");
    }

    [Fact]
    public void Constructor_WithCustomSpecVersion_UsesCustomValue()
    {
        // Arrange & Act
        var domainEvent = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: "test-id",
            SpecVersion: "2.0"
        );

        // Assert
        domainEvent.SpecVersion.Should().Be("2.0");
    }

    [Fact]
    public void Constructor_WithCustomDataContentType_UsesCustomValue()
    {
        // Arrange & Act
        var domainEvent = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: "test-id",
            DataContentType: "application/xml"
        );

        // Assert
        domainEvent.DataContentType.Should().Be("application/xml");
    }

    [Fact]
    public void Constructor_WithNullData_AllowsNullData()
    {
        // Arrange & Act
        var domainEvent = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: null,
            Timestamp: DateTime.UtcNow,
            Id: "test-id"
        );

        // Assert
        domainEvent.Data.Should().BeNull();
    }

    [Fact]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var event1 = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: "test-data",
            Timestamp: timestamp,
            Id: "test-id"
        );

        var event2 = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: "test-data",
            Timestamp: timestamp,
            Id: "test-id"
        );

        // Act & Assert
        event1.Should().Be(event2);
        (event1 == event2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var event1 = new DomainEvent(
            Type: "test.event.one",
            Source: "TestSource",
            Data: null,
            Timestamp: timestamp,
            Id: "test-id"
        );

        var event2 = new DomainEvent(
            Type: "test.event.two",
            Source: "TestSource",
            Data: null,
            Timestamp: timestamp,
            Id: "test-id"
        );

        // Act & Assert
        event1.Should().NotBe(event2);
        (event1 == event2).Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsReadableRepresentation()
    {
        // Arrange
        var domainEvent = new DomainEvent(
            Type: "test.event",
            Source: "TestSource",
            Data: "test-data",
            Timestamp: DateTime.UtcNow,
            Id: "test-id"
        );

        // Act
        var result = domainEvent.ToString();

        // Assert
        result.Should().Contain("test.event");
        result.Should().Contain("TestSource");
        result.Should().Contain("test-id");
    }
}
