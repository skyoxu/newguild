using System;
using FluentAssertions;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class AsyncInteractionStateTests
{
    // ACC:T32.3
    [Fact]
    public void Should_ClearErrorAndEnterLoading_When_RetryFromError()
    {
        var errorState = AsyncInteractionState.Error("boom");

        errorState.IsLoading.Should().BeFalse("Error mode is not loading");
        errorState.CanRetry.Should().BeTrue("retry must be available in Error mode");
        errorState.ErrorMessage.Should().Be("boom", "Error mode must carry an error message");

        var retriedState = errorState.Retry();

        retriedState.IsLoading.Should().BeTrue("retry must start a new loading attempt");
        retriedState.CanRetry.Should().BeFalse("retry must not remain enabled while loading");
        retriedState.ErrorMessage.Should().BeNull("retry must clear the previous error before starting a new attempt");
    }

    // ACC:T32.3
    [Fact]
    public void Should_RefuseRetryOutsideErrorMode_When_InLoadingOrDisabled()
    {
        var loadingState = AsyncInteractionState.Loading();

        loadingState.CanRetry.Should().BeFalse("retry must be refused outside Error mode");
        loadingState.Retry().Should().BeSameAs(loadingState, "retry should be a no-op outside Error mode");

        var disabledState = AsyncInteractionState.Disabled();

        disabledState.CanRetry.Should().BeFalse("retry must be refused outside Error mode");
        disabledState.IsLoading.Should().BeFalse("disabled mode must not be treated as loading");
        disabledState.Retry().Should().BeSameAs(disabledState, "retry should be a no-op outside Error mode");
    }

    // ACC:T32.3
    [Fact]
    public void Should_UseExceptionMessage_When_CreatedFromException()
    {
        var state = AsyncInteractionState.Error(new InvalidOperationException("boom"));

        state.CanRetry.Should().BeTrue();
        state.ErrorMessage.Should().Be("boom");
    }

    // ACC:T32.3
    [Fact]
    public void Should_FallbackToExceptionTypeName_When_ExceptionMessageIsBlank()
    {
        var state = AsyncInteractionState.Error(new InvalidOperationException(""));

        state.CanRetry.Should().BeTrue();
        state.ErrorMessage.Should().Be(nameof(InvalidOperationException));
    }

    // ACC:T32.3
    [Fact]
    public void Should_ThrowArgumentNullException_When_CreatingErrorFromNullException()
    {
        Action act = () => AsyncInteractionState.Error((Exception)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ex");
    }

    // ACC:T32.3
    [Fact]
    public void Should_ThrowArgumentException_When_CreatingErrorFromBlankMessage()
    {
        Action act = () => AsyncInteractionState.Error("  ");
        act.Should().Throw<ArgumentException>().WithParameterName("errorMessage");
    }

    // ACC:T32.3
    [Fact]
    public void Should_ThrowArgumentNullException_When_CreatingErrorFromNullMessage()
    {
        Action act = () => AsyncInteractionState.Error((string)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("errorMessage");
    }
}
