using MailKit;
using MimeKit;
using System.Data;
using System.Net.Mail;

namespace ShiftHub.Models
{
    public class MessageModel
    {
        public List<Message> aList { get; set; } = new List<Message>();
        public List<Message> bList { get; set; } = new List<Message>();
        public List<Message> cList { get; set; } = new List<Message>();

        public class Message
        {
            public string UiD { get; set; } = Convert.ToString($"{Guid.NewGuid()}").Replace("-", "");
            public string Content { get; set; } = string.Empty;
            public Types Type { get; set; } = Types.Success;
            public int AutoClose { get; set; } = 30000;
        }

        public enum Types
        {
            Info,
            Success,
            Warning,
            Danger
        }
    }

    public class DataModel
    {
        public DataTable aTable { get; set; } = new DataTable();
        public DataTable bTable { get; set; } = new DataTable();
        public DataTable cTable { get; set; } = new DataTable();
        public DataTable dTable { get; set; } = new DataTable();
        public DataTable eTable { get; set; } = new DataTable();
        public DataTable fTable { get; set; } = new DataTable();
        public DataTable gTable { get; set; } = new DataTable();

    }

    public class UserDetailsModel
    {
        public string? Username { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public string? DisplayName { get; set; } = string.Empty;
        public string? Mail { get; set; } = string.Empty;
        public string? Phone { get; set; } = string.Empty;
    }

    public class AccountModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public class MailContentModel
    {
        public string Base64To { get; set; } = string.Empty;
        public string Base64Cc { get; set; } = string.Empty;
        public string Base64Bcc { get; set; } = string.Empty;
        public string Base64From { get; set; } = string.Empty;
        public string Base64Subject { get; set; } = string.Empty;
        public string Base64Message { get; set; } = string.Empty;
        public MailPriority Priority { get; set; } = MailPriority.Normal;

    }
    public class MailConfigModel
    {
        public string Server_Smtp { get; set; } = string.Empty;
        public int Port_Smtp { get; set; }
        public string Server_Imap { get; set; } = string.Empty;
        public int Port_Imap { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
    }

    public class MailModel
    {
        public UniqueId uid { get; set; }
        public MimeMessage Message { get; set; } = null!;
    }
}