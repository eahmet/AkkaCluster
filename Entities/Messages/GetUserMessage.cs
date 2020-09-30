using System;
using System.Collections.Generic;
using System.Text;

namespace Entities.Messages
{
    public class GetUserMessage:BaseMessage
    {
        public int UserId { get; }
        public GetUserMessage(int userId)
        {
            UserId = userId;
        }
    }
}
