using System.ComponentModel.DataAnnotations;

namespace ShoppingWebApi.Models
{
    public class LogEntry : BaseEntity
    {
        [Required, MaxLength(20)]
        public string Level { get; set; } = "Error"; // Info/Warn/Error

        [Required]
        public string Message { get; set; } = null!;

        public string? Exception { get; set; }
        public string? StackTrace { get; set; }

        [MaxLength(200)]
        public string? Source { get; set; }   // e.g., OrdersService
        public int? EventId { get; set; }

        [MaxLength(100)]
        public string? CorrelationId { get; set; }

        [MaxLength(300)]
        public string? RequestPath { get; set; }
    }
}
