using System;
using System.ComponentModel.DataAnnotations;

namespace EdCo.Core.Entities
{
    /// <summary>
    /// Tracks the conversational state for a WhatsApp guardian bot session.
    /// Each unique phone number has one active session.
    /// </summary>
    public class WhatsAppSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string CurrentState { get; set; } = "Initial";

        /// <summary>
        /// JSON serialized context data (e.g. selected student ID, pending action, auth step)
        /// </summary>
        public string? ContextData { get; set; }

        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    }
}
