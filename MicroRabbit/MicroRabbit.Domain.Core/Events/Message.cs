using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace MicroRabbit.Domain.Core.Events
{
    // Any request to MediateR expects a boolean value back
    public abstract class Message: IRequest<bool>
    {
        public string MessageType { get; protected set; }

        protected Message()
        {
            // using reflection to get the type name
            MessageType = GetType().Name;
        }
    }
}
