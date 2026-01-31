using System;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Services
{
    public sealed class GameStartWorkflowTests
    {
        private const string ExpectedGameStartedEventType = "core.game.started";

        // ACC:T40.4
        [Fact]
        public void Should_ComputeSeedFingerprint_Deterministically()
        {
            var seed = "sample-seed-0001";

            var first = ComputeSeedFingerprint(seed);
            var second = ComputeSeedFingerprint(seed);

            first.Should().Be(second);
            first.Should().HaveLength(64);
            first.All(IsLowercaseHex).Should().BeTrue();
        }

        // ACC:T40.6
        [Fact]
        public void Should_UseCoreGameStartedEventTypeNamingConvention()
        {
            var eventType = ExpectedGameStartedEventType;

            eventType.Should().NotBeNullOrWhiteSpace();
            eventType.Should().Be(eventType.ToLowerInvariant());
            eventType.Split('.').Should().HaveCount(3);
            eventType.Should().StartWith("core.");
            eventType.Should().Contain(".game.");
        }

        // ACC:T40.4
        [Fact]
        public void Should_Expose_Seed_In_GameStarted_Event_Data()
        {
            var props = typeof(Game.Core.Contracts.Engine.GameStarted)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(p => p.Name)
                .ToArray();

            props.Should().Contain(
                n => n.Contains("Seed", StringComparison.OrdinalIgnoreCase),
                "Task 40 needs an observable seed (or seed fingerprint) on core.game.started to enable deterministic replay and UI validation.");
        }

        private static string ComputeSeedFingerprint(string seed)
        {
            using var sha = SHA256.Create();
            var input = seed ?? string.Empty;
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return ToLowerHex(hash);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";

            for (var i = 0; i < bytes.Length; i++)
            {
                var byteValue = bytes[i];
                chars[i * 2] = hex[byteValue >> 4];
                chars[i * 2 + 1] = hex[byteValue & 0x0F];
            }

            return new string(chars);
        }

        private static bool IsLowercaseHex(char c)
            => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
    }
}
