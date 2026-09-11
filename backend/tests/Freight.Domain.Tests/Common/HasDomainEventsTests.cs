using Freight.Domain.Common;

namespace Freight.Domain.Tests.Common;

public class HasDomainEventsTests
{
    private sealed class TestEvent(DateTime occurredAt) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = occurredAt;
    }

    private sealed class TestAggregate : HasDomainEvents
    {
        public void Raise(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);
    }

    [Fact]
    public void DomainEvents_Initially_IsEmpty()
    {
        var aggregate = new TestAggregate();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void AddDomainEvent_Null_Throws()
    {
        var aggregate = new TestAggregate();

        Assert.Throws<ArgumentNullException>(() => aggregate.Raise(null!));
    }

    [Fact]
    public void AddDomainEvent_MultipleEvents_AreKeptInOrder()
    {
        var aggregate = new TestAggregate();
        var first = new TestEvent(DateTime.UtcNow);
        var second = new TestEvent(DateTime.UtcNow.AddMinutes(1));

        aggregate.Raise(first);
        aggregate.Raise(second);

        Assert.Equal([first, second], aggregate.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.Raise(new TestEvent(DateTime.UtcNow));

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_WhenAlreadyEmpty_DoesNotThrow()
    {
        var aggregate = new TestAggregate();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }
}
