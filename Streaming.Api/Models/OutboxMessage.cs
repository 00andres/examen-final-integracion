using System;

namespace Streaming.Api.Models
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedDate { get; set; }
    }
}
