using System.Data;

namespace ShiftHub.Models
{
    public class AdminModels
    {
        public MessageModel Messages { get; set; } = new MessageModel();
        public UserDetailsModel UserDetails { get; set; } = new UserDetailsModel();
        public DataModel Data { get; set; } = new DataModel();

        public GroupModel Group { get; set; } = new GroupModel();
        public UserModel User { get; set; } = new UserModel();

    }

    public class GroupModel
    {
        public int GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int InChargeId { get; set; }
        public bool Notification { get; set; } = false;
    }

    public class UserModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool Notification { get; set; } = false;
    }

}
