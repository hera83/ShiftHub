using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftHub.Data.Entities
{
    public class Group
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        public int InChargeId { get; set; }
        public string? Note { get; set; }
    }

    public class LogLogin
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int UserId { get; set; }
        [MaxLength(255)]
        public string? Username { get; set; }
        [MaxLength(1024)]
        public string Status { get; set; } = string.Empty;
    }

    public class Mail
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int? JobId { get; set; }
        public int UserId { get; set; }
        public int GroupId { get; set; }
        public string MailConfigJson { get; set; } = string.Empty;
        public string MailContentJson { get; set; } = string.Empty;
        public DateTime? SendTime { get; set; }
        [MaxLength(255)]
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public int? Count { get; set; }
    }

    public class AppMessage
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int UserId { get; set; }
        [MaxLength(255)]
        public string Module { get; set; } = string.Empty;
        [MaxLength(255)]
        public string Type { get; set; } = string.Empty;
        [MaxLength(2048)]
        public string Message { get; set; } = string.Empty;
        public int AutoClose { get; set; }
        public int State { get; set; }
    }

    public class Staff
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int GroupId { get; set; }
        [MaxLength(255)]
        public string UiD { get; set; } = string.Empty;
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(255)]
        public string? Address { get; set; }
        [MaxLength(255)]
        public string? ZipCode { get; set; }
        [MaxLength(255)]
        public string? City { get; set; }
        public DateTime? Birthday { get; set; }
        [MaxLength(255)]
        public string? Mail { get; set; }
        [MaxLength(255)]
        public string? Phone { get; set; }
        [MaxLength(255)]
        public string? Key1 { get; set; }
        [MaxLength(255)]
        public string? Key2 { get; set; }
        [MaxLength(255)]
        public string? Key3 { get; set; }
        public int? iType { get; set; }
    }

    public class SubGroup
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int GroupId { get; set; }
        [MaxLength(255)]
        public string UiD { get; set; } = string.Empty;
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Activated { get; set; }
    }

    public class SubGroupAlert
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int StaffId { get; set; }
        public int ShiftId { get; set; }
        public int State { get; set; }
        public DateTime? Sleep { get; set; }
    }

    public class SubGroupDay
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public DateTime Day { get; set; }
        [MaxLength(255)]
        public string? Key1 { get; set; }
    }

    public class SubGroupForm
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public string Base64FormRaw { get; set; } = string.Empty;
        public string? Base64FormData { get; set; }
        public string? Base64FormHtml { get; set; }
    }

    public class SubGroupKey
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        [MaxLength(50)]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class SubGroupMessage
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(255)]
        public string UiD { get; set; } = string.Empty;
        public int SubGroupId { get; set; }
        public int StaffId { get; set; }
        public int? UserId { get; set; }
        [MaxLength(255)]
        public string? Base64Sender { get; set; }
        [MaxLength(255)]
        public string? Base64Reciver { get; set; }
        [MaxLength(512)]
        public string Base64Title { get; set; } = string.Empty;
        public string Base64Message { get; set; } = string.Empty;
        [MaxLength(255)]
        public string iType { get; set; } = string.Empty;
        public int? Sendt { get; set; }
        public int InOut { get; set; }
        public int State { get; set; }
    }

    public class SubGroupNeed
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int ShiftId { get; set; }
        public DateTime Time { get; set; }
        public int StaffCount { get; set; }
    }

    public class SubGroupPostGroup
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int GroupPosision { get; set; }
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        public int MaxTime { get; set; }
    }

    public class SubGroupPostMember
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int PostId { get; set; }
        public int StaffId { get; set; }
        public DateTime? CheckOut { get; set; }
        public int AlarmState { get; set; }
    }

    public class SubGroupRegistration
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int ShiftId { get; set; }
        public int StaffId { get; set; }
    }

    public class SubGroupShift
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int DayId { get; set; }
        [MaxLength(255)]
        public string? Name { get; set; }
        [MaxLength(255)]
        public string Type { get; set; } = string.Empty;
        public DateTime SubStartTime { get; set; }
        public DateTime SubEndTime { get; set; }
        public int? StaffNeeds { get; set; }
        [MaxLength(255)]
        public string? Key1 { get; set; }
    }

    public class SubGroupStaff
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int SubGroupId { get; set; }
        public int StaffId { get; set; }
    }
}
