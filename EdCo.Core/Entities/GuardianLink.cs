using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EdCo.Core.Entities
{
    /// <summary>
    /// Links a guardian's WhatsApp phone number to a student AppUser account.
    /// A guardian can link to multiple students.
    /// </summary>
    public class GuardianLink : ISoftDelete
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Guardian's WhatsApp phone number in E.164 format (digits only, e.g. "263771234567")
        /// </summary>
        [Required]
        [StringLength(50)]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// FK to the student's AppUser.Id
        /// </summary>
        [Required]
        public string StudentUserId { get; set; } = string.Empty;

        [ForeignKey("StudentUserId")]
        public AppUser Student { get; set; } = null!;

        public bool IsPrimary { get; set; }

        public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

        // ISoftDelete
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
