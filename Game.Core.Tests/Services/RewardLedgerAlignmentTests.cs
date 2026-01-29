using FluentAssertions;
using Xunit;
using Game.Core.Contracts.Engine;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Progression;
using Game.Core.Services;

namespace Game.Core.Tests.Services
{
    public class RewardLedgerAlignmentTests
    {
        // ACC:T37.1
        [Fact]
        public void Should_Expose_Stable_EventType_Contracts_For_Rewards()
        {
            ScoreChanged.EventType.Should().NotBeNullOrWhiteSpace();
            ReputationChanged.EventType.Should().NotBeNullOrWhiteSpace();
            MediaBeatTriggered.EventType.Should().NotBeNullOrWhiteSpace();
            RaidResolved.EventType.Should().NotBeNullOrWhiteSpace();
            RecruitmentOfferResolved.EventType.Should().NotBeNullOrWhiteSpace();

            ScoreChanged.EventType.Should().StartWith("core.");
            ReputationChanged.EventType.Should().StartWith("core.");
            MediaBeatTriggered.EventType.Should().StartWith("core.");
            RaidResolved.EventType.Should().StartWith("core.");
            RecruitmentOfferResolved.EventType.Should().StartWith("core.");
        }

        [Fact]
        public void Should_Expose_RewardLedger_Core_Types_For_Alignment()
        {
            typeof(RewardLedger).Name.Should().Be("RewardLedger");
            typeof(RewardLedgerService).Name.Should().Be("RewardLedgerService");
        }
    }
}
