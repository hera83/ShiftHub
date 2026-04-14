using Microsoft.EntityFrameworkCore;
using ShiftHub.Data;
using ShiftHub.Data.Entities;
using ShiftHub.Models;
using ShiftHub.Services;

public class BgServiceMailSender : BackgroundService
{
    #region Init. Global Variables
    readonly ILogger<BgServiceMailSender> _logger;
    readonly IConfiguration _configuration;
    readonly IServiceScopeFactory _scopeFactory;

    public BgServiceMailSender(ILogger<BgServiceMailSender> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
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
        _logger.LogInformation("BgServiceMailSender: Started");
        #endregion

        #region Init. Variables
        DateTime KeepAliveTime = DateTime.Now;

        #endregion

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                #region Get Jobs
                var jobs = await db.SubGroupMessages
                    .Where(message => message.iType == "Mail" && message.Sendt == 0)
                    .OrderBy(message => message.Id)
                    .ToListAsync(stoppingToken);

                #endregion

                #region Log Jobs Found (if any)
                if (jobs.Count > 0)
                {
                    _logger.LogInformation("Found: {mails} Mail(s)", jobs.Count);
                }
                #endregion

                #region Handel Jobs
                foreach (var job in jobs)
                {
                    try
                    {
                        #region Get Data
                        var staff = await db.Staff
                            .FirstOrDefaultAsync(item => item.Id == job.StaffId, stoppingToken);

                        if (staff == null || string.IsNullOrWhiteSpace(staff.Mail))
                        {
                            job.Sendt = 2;
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }


                        MailConfigModel MailConfig = new MailConfigModel {
                            Server_Smtp = $"{_configuration["MailConfig:Server_Smtp"]}",
                            Port_Smtp = Convert.ToInt32($"{_configuration["MailConfig:Port_Smtp"]}"),
                            Username = $"{_configuration["MailConfig:Username"]}",
                            Password = $"{_configuration["MailConfig:Password"]}",
                            Mail = $"{_configuration["MailConfig:Mail"]}"
                        };
                        MailContentModel MailContent = new MailContentModel {
                            Base64To = $"{code.Base64Encode(staff.Mail)}",
                            Base64Cc = $"",
                            Base64Bcc = $"",
                            Base64From = $"{code.Base64Encode($"{_configuration["MailConfig:Mail"]}")}",
                            Base64Subject = job.Base64Title,
                            Base64Message = job.Base64Message,
                            Priority = System.Net.Mail.MailPriority.Normal
                        };

                        #endregion

                        #region Log Mail Reciver
                        _logger.LogTrace("Sending mail to: {mail}", staff.Mail);
                        #endregion

                        #region Send Mail
                        mail.Send(MailConfig, MailContent);

                        #endregion

                        #region Set Job Status
                        job.Sendt = 1;
                        await db.SaveChangesAsync(stoppingToken);
                        #endregion
                    }
                    catch (Exception e)
                    {
                        #region Log Exception
                        _logger.LogError("Job Failed! Time: {time}, Message: {message}", DateTime.Now, e.Message);
                        #endregion

                        #region Set Failed Message
                        db.SubGroupMessages.Add(new SubGroupMessage
                        {
                            UiD = job.UiD,
                            SubGroupId = job.SubGroupId,
                            StaffId = job.StaffId,
                            UserId = job.UserId,
                            Base64Title = code.Base64Encode("Mail fejlede i afsendelse!"),
                            Base64Message = code.Base64Encode(e.Message),
                            iType = "Note",
                            Sendt = 1,
                            InOut = 1,
                            State = 0
                        });
                        #endregion

                        #region Set Job Status
                        job.Sendt = 2;
                        await db.SaveChangesAsync(stoppingToken);
                        #endregion
                    }
                    finally
                    {
                        #region Log Status
                        _logger.LogInformation("Status: {active} of {total}", (jobs.IndexOf(job) + 1), jobs.Count);
                        #endregion
                    }
                }
                #endregion

                #region Log KeepAlive
                if (KeepAliveTime < DateTime.Now.AddMinutes(-60))
                {
                    KeepAliveTime = DateTime.Now;
                    _logger.LogInformation("BgServiceMailSender running {time}", DateTime.Now);
                }
                #endregion
            }
            catch (Exception e)
            {
                #region Log Exception
                _logger.LogError("BgServiceMailSender failed! Time: {time}, Message: {message}", DateTime.Now, e.Message);
                #endregion

                #region Extra Delay
                await Task.Delay((25 * 1000), stoppingToken);
                #endregion
            }
            finally
            {
                #region Delay Task
                await Task.Delay((5 * 1000), stoppingToken);
                #endregion
            }
        }

        #region Log End
        _logger.LogInformation("BgServiceMailSender: Ended");
        #endregion
    }
}