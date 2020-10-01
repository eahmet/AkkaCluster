using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Entities.Messages
{
    public class GetUserGroupMessage:BaseMessage
    {
        public int UserGroupId { get; }
        public GetUserGroupMessage(int userGroupId)
        {
            UserGroupId = userGroupId;
        }
    }
}
