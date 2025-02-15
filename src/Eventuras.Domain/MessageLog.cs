using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using NodaTime;

namespace Eventuras.Domain
{
    [Obsolete("See NotificationLog - the new data scheme for SMS/Email notification history")]
    public class MessageLog
    {
        public int MessageLogId { get; set; }
        public int EventInfoId { get; set; }
        public EventInfo EventInfo { get; set; }

        public string Recipients { get; set; }
        public string MessageContent { get; set; }
        public string MessageType { get; set; }
        public Instant TimeStamp { get; set; } = SystemClock.Instance.Now();

        public string Provider { get; set; }
        public string Result { get; set; }

    }
}
