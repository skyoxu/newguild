using Game.Core.Contracts;

namespace Game.Core.Ports;

public interface IEventBus
{
    Task PublishAsync(DomainEvent evt);
    IDisposable Subscribe(Func<DomainEvent, Task> handler);
}

