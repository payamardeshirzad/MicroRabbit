using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MicroRabbit.Domain.Core.Bus
{
    public interface IEventBus
    {
        // Send Commands to MediatR
        Task SendCommand<T>(T command) where T : Command;

        // Publish an event. as event is reserve keyword @event is declared
        void Publish<T>(T @event) where T : Event;

        // Subscribe to published events. EventType and EventHandler
        void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>;

    }
}
