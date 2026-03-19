using System.ComponentModel.DataAnnotations;

namespace CognitiveOverloadLMS.Models
{
    public class StartSessionRequest
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Range(1, 120)]
        public int Age { get; set; }

        [Required]
        public string Major { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public BehaviorData? IndexBehaviorData { get; set; }
    }
}
