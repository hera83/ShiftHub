using Microsoft.EntityFrameworkCore;
using ShiftHub.Data;
using ShiftHub.Data.Entities;
using ShiftHub.Models;
using ShiftHub.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

public class BgServiceMailHandler : BackgroundService
{
    #region Init. Global Variables
    readonly ILogger<BgServiceMailHandler> _logger;
    readonly IConfiguration _configuration;
    readonly IServiceScopeFactory _scopeFactory;

    public BgServiceMailHandler(ILogger<BgServiceMailHandler> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }
    #endregion

    #region Init. Global Services
    CodeServices code = new CodeServices();
    MailServices mail = new MailServices();

    #endregion

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        #region Log Start
        _logger.LogInformation("BgServiceMailHandler: Started");
        #endregion

        #region Init. Variables
        List<MailModel> jMessages = new List<MailModel>();
        DateTime KeepAliveTime = DateTime.Now;
        int Counter;
        String JsonSenders;
        String JsonRecivers;
        String UiD;

        #endregion

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                #region Init. Loop Variables
                jMessages = new List<MailModel>();

                #endregion

                #region Validate if active
                if ($"{_configuration["MailConfig:Active"]}" == "0")
                {
                    #region Log Exception
                    _logger.LogTrace("Mail Service er ikke aktiv...");
                    #endregion

                    #region Delay Task
                    await Task.Delay((60 * 1000), stoppingToken);
                    #endregion

                    continue;
                }
                #endregion

                #region Get Data
                MailConfigModel MailConfig = new MailConfigModel
                {
                    Server_Smtp = $"{_configuration["MailConfig:Server_Smtp"]}",
                    Port_Smtp = Convert.ToInt32($"{_configuration["MailConfig:Port_Smtp"]}"),
                    Server_Imap = $"{_configuration["MailConfig:Server_Imap"]}",
                    Port_Imap = Convert.ToInt32($"{_configuration["MailConfig:Port_Imap"]}"),
                    Username = $"{_configuration["MailConfig:Username"]}",
                    Password = $"{_configuration["MailConfig:Password"]}",
                    Mail = $"{_configuration["MailConfig:Mail"]}"
                };

                #endregion

                #region Get Jobs
                jMessages = mail.GetMessage(MailConfig);

                #endregion

                #region Log Jobs Found (if any)
                if (jMessages.Count > 0)
                {
                    _logger.LogInformation("Found: {mails} Mail(s)", jMessages.Count);
                }
                #endregion

                #region Handel Jobs
                foreach (MailModel item in jMessages)
                {
                    try
                    {
                        if (Regex.IsMatch(item.Message.Subject!, @"\[.*?\]$") == true)
                        {
                            #region Log Process
                            _logger.LogInformation("Message found: {subject}", item.Message.Subject);
                            #endregion

                            #region Init. Loop Variables
                            UiD = Regex.Match(item.Message.Subject!, @"\[(.*?)\]$").Groups[1].Value;
                            JsonSenders = String.Empty;
                            JsonRecivers = String.Empty;
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            
                            #endregion

                            #region Get Data
                            var originalMessage = await db.SubGroupMessages
                                .Where(message => message.UiD == UiD)
                                .OrderBy(message => message.Id)
                                .FirstOrDefaultAsync(message => message.SubGroupId != 0, stoppingToken);
                            #endregion

                            #region Handel Message
                            if (originalMessage != null)
                            {
                                /// Gem besked til modtager

                                #region Generate Json Sender
                                Counter = 0;
                                JsonSenders = "[";
                                foreach (var mail in item.Message.From.Mailboxes)
                                {
                                    if (Counter == 0)
                                    {
                                        JsonSenders += $"\"{mail.Address}\"";
                                    }
                                    else
                                    {
                                        JsonSenders += $",\"{mail.Address}\"";
                                    }

                                    Counter++;
                                }
                                JsonSenders += "]";
                                #endregion

                                #region Generate Json Recivers
                                Counter = 0;
                                JsonRecivers = "[";
                                foreach (var mail in item.Message.To.Mailboxes)
                                {
                                    if (Counter == 0)
                                    {
                                        JsonRecivers += $"\"{mail.Address}\"";
                                    }
                                    else
                                    {
                                        JsonRecivers += $",\"{mail.Address}\"";
                                    }

                                    Counter++;
                                }
                                JsonRecivers += "]";
                                #endregion

                                #region Register Mail
                                var messageBody = item.Message.TextBody != null
                                    ? item.Message.TextBody
                                    : Regex.IsMatch(item.Message.HtmlBody ?? string.Empty, @"^<html>")
                                        ? item.Message.HtmlBody
                                        : $"<html>{item.Message.HtmlBody}</html>";

                                db.SubGroupMessages.Add(new SubGroupMessage
                                {
                                    UiD = UiD,
                                    SubGroupId = originalMessage.SubGroupId,
                                    StaffId = originalMessage.StaffId,
                                    Base64Sender = code.Base64Encode(JsonSenders),
                                    Base64Reciver = code.Base64Encode(JsonRecivers),
                                    Base64Title = code.Base64Encode(item.Message.Subject ?? string.Empty),
                                    Base64Message = code.Base64Encode(messageBody ?? string.Empty),
                                    iType = "Mail",
                                    Sendt = 1,
                                    InOut = 2,
                                    State = 0
                                });
                                await db.SaveChangesAsync(stoppingToken);

                                #endregion
                            }
                            else
                            {
                                /// UiD eksistere ikke (eks. hvis besked er slettet)
                                
                                #region Log Process
                                _logger.LogInformation("Message id not found: {subject}", item.Message.Subject);
                                #endregion

                                #region Init. Message
                                MailContentModel MailContent = new MailContentModel
                                {
                                    Base64To = $"{code.Base64Encode($"{item.Message.From.Mailboxes.First().Address}")}",
                                    Base64Cc = $"",
                                    Base64Bcc = $"",
                                    Base64From = $"{code.Base64Encode($"{_configuration["MailConfig:Mail"]}")}",
                                    Base64Subject = $"{code.Base64Encode($"Re: {item.Message.Subject}")}",
                                    Base64Message = $"{code.Base64Encode($"Det er ikke muligt at besvare denne mail")}",
                                    Priority = System.Net.Mail.MailPriority.Normal
                                };
                                #endregion

                                #region Reply Message
                                mail.Send(MailConfig, MailContent);
                                #endregion
                            }
                            #endregion

                            #region Delete Message
                            mail.DeleteMessage(MailConfig, item);
                            #endregion
                        }
                        else
                        {
                            /// Hvis den ikke har noget UiD (Man kan ikke skrive til mail direkte)

                            #region Log Process
                            _logger.LogInformation("Message deleted (messege subject do not match pattern)");
                            #endregion

                            #region Init. Message
                            MailContentModel MailContent = new MailContentModel
                            {
                                Base64To = $"{code.Base64Encode($"{item.Message.From.Mailboxes.First().Address}")}",
                                Base64Cc = $"",
                                Base64Bcc = $"",
                                Base64From = $"{code.Base64Encode($"{_configuration["MailConfig:Mail"]}")}",
                                Base64Subject = $"{code.Base64Encode($"Re: {item.Message.Subject}")}",
                                Base64Message = $"{code.Base64Encode($"Det er ikke godkendt at skrive direkte til denne mail!")}",
                                Priority = System.Net.Mail.MailPriority.Normal
                            };
                            #endregion

                            #region Reply Message
                            mail.Send(MailConfig, MailContent);
                            #endregion

                            #region Delete Message
                            mail.DeleteMessage(MailConfig, item);
                            #endregion
                        }
                    }
                    catch (Exception e)
                    {
                        #region Log Exception
                        _logger.LogError("Job Failed! Time: {time}, Message: {message}", DateTime.Now, e.Message);
                        #endregion
                    }
                    finally
                    {
                        #region Log Status
                        _logger.LogInformation("Status: {active} of {total}", (jMessages.IndexOf(item) + 1), jMessages.Count);
                        #endregion
                    }
                }
                #endregion

                #region Log KeepAlive
                if (KeepAliveTime < DateTime.Now.AddMinutes(-60))
                {
                    KeepAliveTime = DateTime.Now;
                    _logger.LogInformation("BgServiceMailHandler running {time}", DateTime.Now);
                }
                #endregion
            }
            catch (Exception e)
            {
                #region Log Exception
                _logger.LogError("BgServiceMailHandler failed! Time: {time}, Message: {message}", DateTime.Now, e.Message);
                #endregion

                #region Extra Delay
                await Task.Delay((25 * 1000), stoppingToken);
                #endregion
            }
            finally
            {
                #region Delay Task
                await Task.Delay((60 * 1000), stoppingToken);
                #endregion
            }
        }

        #region Log End
        _logger.LogInformation("BgServiceMailHandler: Ended");
        #endregion
    }
}