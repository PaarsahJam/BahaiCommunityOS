using CommunityOS.Contracts.Identity;
using FluentAssertions;
using MassTransit;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Integration;

/// <summary>
/// ADR-036 Q4 consumer contract, locked from the downstream side. No in-repo
/// service yet maintains account-scoped session-revocation state (the only
/// Identity-event consumers today are append-only audit ledgers, which hold no
/// epoch state), so there is no production consumer to wire yet. These tests
/// pin the required semantics that any future consumer must implement:
/// idempotent under duplicate delivery, monotonic across out-of-order arrivals,
/// a no-op for stale or equal epochs, and an advance for newer epochs.
/// </summary>
public sealed class SessionRevocationEpochConsumerSemanticsTests
{
    [Fact]
    public async Task Duplicate_DeliveryIsIdempotent()
    {
        var state = new SessionRevocationEpochReplicaState();
        var consumer = new SessionRevocationEpochReplicaConsumer(state);
        var accountId = Guid.NewGuid();
        var message = new SessionRevocationEpochAdvanced(accountId, 3, DateTime.UtcNow);

        await consumer.Consume(Context(message));
        await consumer.Consume(Context(message));

        state.Current(accountId).Should().Be(3);
        state.TransitionCount.Should().Be(1,
            "re-delivering the same epoch is a harmless, idempotent no-op");
    }

    [Fact]
    public async Task Older_EpochCanNeverOverwriteNewerLocalState()
    {
        var state = new SessionRevocationEpochReplicaState();
        var consumer = new SessionRevocationEpochReplicaConsumer(state);
        var accountId = Guid.NewGuid();

        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 5, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 2, DateTime.UtcNow)));

        state.Current(accountId).Should().Be(5,
            "a stale delivery must not roll local state backward");
        state.TransitionCount.Should().Be(1);
    }

    [Fact]
    public async Task Equal_EpochDoesNotCauseASecondStateTransition()
    {
        var state = new SessionRevocationEpochReplicaState();
        var consumer = new SessionRevocationEpochReplicaConsumer(state);
        var accountId = Guid.NewGuid();

        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 3, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 3, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 3, DateTime.UtcNow)));

        state.Current(accountId).Should().Be(3);
        state.TransitionCount.Should().Be(1);
    }

    [Fact]
    public async Task Newer_EpochAdvancesLocalStateExactlyOnce()
    {
        var state = new SessionRevocationEpochReplicaState();
        var consumer = new SessionRevocationEpochReplicaConsumer(state);
        var accountId = Guid.NewGuid();

        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 1, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 4, DateTime.UtcNow)));

        state.Current(accountId).Should().Be(4);
        state.TransitionCount.Should().Be(2);
    }

    [Fact]
    public async Task OutOfOrder_ArrivalsNeverRegressLocalState()
    {
        var state = new SessionRevocationEpochReplicaState();
        var consumer = new SessionRevocationEpochReplicaConsumer(state);
        var accountId = Guid.NewGuid();

        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 7, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 3, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 2, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(accountId, 7, DateTime.UtcNow)));

        state.Current(accountId).Should().Be(7,
            "out-of-order and duplicate deliveries converge on the highest epoch");
        state.TransitionCount.Should().Be(1);
    }

    [Fact]
    public async Task Accounts_AreTrackedIndependently()
    {
        var state = new SessionRevocationEpochReplicaState();
        var consumer = new SessionRevocationEpochReplicaConsumer(state);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(first, 2, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(second, 9, DateTime.UtcNow)));
        await consumer.Consume(Context(new SessionRevocationEpochAdvanced(first, 5, DateTime.UtcNow)));

        state.Current(first).Should().Be(5);
        state.Current(second).Should().Be(9);
        state.TransitionCount.Should().Be(3);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    /// <summary>Reference downstream replica: the minimal monotonic, per-account
    /// epoch keeper a consumer of <see cref="SessionRevocationEpochAdvanced"/> is
    /// required to maintain. Test infrastructure only (ADR-036 Q4).</summary>
    private sealed class SessionRevocationEpochReplicaState
    {
        private readonly Dictionary<Guid, long> _epochs = [];

        public long TransitionCount { get; private set; }

        public long? Current(Guid userAccountId) =>
            _epochs.TryGetValue(userAccountId, out var value) ? value : null;

        public bool Apply(Guid userAccountId, long announcedEpoch)
        {
            if (!_epochs.TryGetValue(userAccountId, out var current))
            {
                _epochs[userAccountId] = announcedEpoch;
                TransitionCount++;
                return true;
            }

            if (announcedEpoch <= current)
                return false;

            _epochs[userAccountId] = announcedEpoch;
            TransitionCount++;
            return true;
        }
    }

    private sealed class SessionRevocationEpochReplicaConsumer(
        SessionRevocationEpochReplicaState state) : IConsumer<SessionRevocationEpochAdvanced>
    {
        public Task Consume(ConsumeContext<SessionRevocationEpochAdvanced> context)
        {
            state.Apply(context.Message.UserAccountId, context.Message.SessionRevocationEpoch);
            return Task.CompletedTask;
        }
    }
}