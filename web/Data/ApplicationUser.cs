using Microsoft.AspNetCore.Identity;

namespace ShiftHub.Data
{
    public class ApplicationUser : IdentityUser<int>
    {
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int GroupId { get; set; }
        public string? DisplayName { get; set; }
    }
}
