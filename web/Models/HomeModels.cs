using System.Data;

namespace ShiftHub.Models
{
    public class HomeModels
    {
        public string HostName { get; set; } = string.Empty;
        public MessageModel Messages { get; set; } = new MessageModel();
        public UserDetailsModel UserDetails { get; set; } = new UserDetailsModel();
        public DataModel Data { get; set; } = new DataModel();
        public String JsonObj { get; set; } = String.Empty;

        public StaffModel Staff { get; set; } = new StaffModel();
        public InportModel Inport { get; set; } = new InportModel();
        public SubGroupsModel SubGroups { get; set; } = new SubGroupsModel();

        public SubGroupDays SubDays { get; set; } = new SubGroupDays();
        public SubGroupShifts SubShifts { get; set; } = new SubGroupShifts();
        public SubGroupNeeds SubNeeds { get; set; } = new SubGroupNeeds();
        public List<NeedsRuleKeysModel> NeedsRuleKeys { get; set; } = new List<NeedsRuleKeysModel>();

        public SubPostModel SubPost { get; set; } = new SubPostModel();

        public SubGroupFormModel SubForm { get; set; } = new SubGroupFormModel();

        public TilmeldingsModel Tilmelding { get; set; } = new TilmeldingsModel();

        public MessageViewModel MessageView { get; set; } = new MessageViewModel();

        public UserModel User { get; set; } = new UserModel();

    }

    public class StaffModel
    {
        public int StaffId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.MinValue;
        public int GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; } = string.Empty;
        public string? ZipCode { get; set; } = string.Empty;
        public string? City { get; set; } = string.Empty;
        public DateTime Birthday { get; set; } = new DateTime(1900, 1, 1);
        public string? Mail { get; set; } = string.Empty;
        public string? Phone { get; set; } = string.Empty;
        public string? Key1 { get; set; } = string.Empty;
        public string? Key2 { get; set; } = string.Empty;
        public string? Key3 { get; set; } = string.Empty;
        public bool Notification { get; set; } = false;
    }
    public class InportModel
    {
        public IFormFile File { get; set; } = null!;
    }

    public class SubGroupsModel
    {
        public int SubGroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Notification { get; set; } = false;

    }

    public class SubGroupDays
    {
        public int DaysId { get; set; }
        public DateTime DayStart { get; set; } = DateTime.Now;
        public DateTime DayEnd { get; set; } = DateTime.Now;
        public string Key1 { get; set; } = string.Empty;

    }

    public class SubGroupShifts
    {
        public int ShiftsId { get; set; }
        public int DayId { get; set; }
        public int NeedsId { get; set; }
        public string Name { get; set; } = string.Empty;
        public Types type { get; set; } = Types.Fixed;
        public string typeInput { get; set; } = string.Empty; // ??
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int StaffNeeds { get; set; }
        public string Key1 { get; set; } = string.Empty;
        public enum Types
        {
            Fixed, Flex
        }
    }

    public class SubGroupNeeds
    {
        public string jsonNeeds { get; set; } = string.Empty;
        public TimeModel Time { get; set; } = new TimeModel();

        public class TimeModel
        {
            public List<string> labels { get; set; } = new List<string>();
            public List<int> data { get; set; } = new List<int>();

        }
    }

    public class SubGroupFormModel
    {
        public int Activated { get; set; }
        public string Form { get; set; } = string.Empty;
        public string Staff { get; set; } = string.Empty;

    }

    public class TilmeldingsModel
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;

    }

    public class NeedsRuleKeysModel
    {
        public string Title { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;

    }

    public class RegistrationModel
    {
        public Overview overview { get; set; } = new Overview();
        public Shifts shifts { get; set; } = new Shifts();
        public TimeUsage timeUsage { get; set; } = new TimeUsage();
        public Messages messages { get; set; } = new Messages();

        public class Overview
        {
            public int Counter { get; set; }
        }
        public class Shifts
        {
            public int Counter { get; set; }
        }
        public class TimeUsage
        {
            public int Counter { get; set; }
        }
        public class Messages
        {
            public int Counter { get; set; }
        }
    }

    public class MessageViewModel
    {
        public int SubGroupId { get; set; }
        public int MessageId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int State { get; set; }

    }

    public class SubPostModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MaxTime { get; set; }
        public bool Notification { get; set; } = false;

    }

}