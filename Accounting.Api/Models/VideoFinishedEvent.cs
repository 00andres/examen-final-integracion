using System;

namespace Accounting.Api.Models
{
    public class VideoFinishedEvent
    {
        public Guid ViewId { get; set; }
        public string VideoId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string CreatorId { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public DateTime WatchedAt { get; set; }
    }
}
