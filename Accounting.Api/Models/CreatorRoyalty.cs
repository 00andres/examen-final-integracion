using System;

namespace Accounting.Api.Models
{
    public class CreatorRoyalty
    {
        public Guid Id { get; set; }
        public string CreatorId { get; set; } = string.Empty;
        public int TotalViews { get; set; }
        public decimal EstimatedRevenue { get; set; }
    }
}
