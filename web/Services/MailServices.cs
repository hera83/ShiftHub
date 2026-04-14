using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using ShiftHub.Models;
using System.Net.Mail;
using MimeKit;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace ShiftHub.Services
{
    public class MailServices
    {
        #region Init. Glabal Services
        CodeServices code = new CodeServices();

        #endregion

        public void Send(MailConfigModel config, MailContentModel mail)
        {
            MailMessage msg = new MailMessage();
            msg.From = new MailAddress(config.Mail);
            msg.To.Add(new MailAddress(code.Base64Decode(mail.Base64To)));

            if (String.IsNullOrEmpty(mail.Base64Cc) == false)
            {
                msg.CC.Add(new MailAddress(code.Base64Decode(mail.Base64Cc)));
            }
            if (String.IsNullOrEmpty(mail.Base64Bcc) == false)
            {
                msg.Bcc.Add(new MailAddress(code.Base64Decode(mail.Base64Bcc)));
            }
            if (String.IsNullOrEmpty(mail.Base64From) == false)
            {
                msg.ReplyToList.Add(new MailAddress(code.Base64Decode(mail.Base64From)));
            }

            msg.Subject = code.Base64Decode(mail.Base64Subject);
            msg.Body = code.Base64Decode(mail.Base64Message);
            msg.IsBodyHtml = true;
            msg.Priority = mail.Priority;
            SmtpClient client = new SmtpClient(config.Server_Smtp, config.Port_Smtp);
            client.Credentials = new System.Net.NetworkCredential(config.Username, config.Password);
            client.EnableSsl = true;
            client.Send(msg);
        }
        
        public List<MailModel> GetMessage(MailConfigModel config)
        {
            #region Init. Variables
            List<MailModel> Messages = new List<MailModel>();

            #endregion

            #region Get Messages
            using (var imapClient = new ImapClient())
            {
                imapClient.Connect(config.Server_Imap, config.Port_Imap, true);
                imapClient.Authenticate(config.Username, config.Password);
                imapClient.Inbox.Open(FolderAccess.ReadOnly);
                IList<UniqueId> uids = imapClient.Inbox.Search(SearchQuery.All);
                foreach (var uid in uids)
                {
                    Messages.Add(new MailModel { uid = uid, Message = imapClient.Inbox.GetMessage(uid) });
                }
                imapClient.Disconnect(true);
            }
            #endregion

            #region Return Data
            return Messages;
            #endregion
        }
        public void DeleteMessage(MailConfigModel config, MailModel item)
        {
            #region Delete Message
            using (var imapClient = new ImapClient())
            {
                imapClient.Connect(config.Server_Imap, config.Port_Imap, true);
                imapClient.Authenticate(config.Username, config.Password);
                imapClient.Inbox.Open(FolderAccess.ReadWrite);
                imapClient.Inbox.AddFlags(item.uid, MessageFlags.Deleted, true);
                imapClient.Inbox.Expunge();
                imapClient.Disconnect(true);
            }
            #endregion
        }
    }

}