using ShiftHub.Data;
using ShiftHub.Data.Entities;
using ShiftHub.Models;
using EntityGroup = global::ShiftHub.Data.Entities.Group;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShiftHub.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            ILogger<AdminController> logger,
            IConfiguration configuration,
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _userManager = userManager;
        }

        private async Task<ApplicationUser> GetCurrentUserAsync() =>
            (await _userManager.GetUserAsync(User))!;

        private static UserDetailsModel ToUserDetails(ApplicationUser u) => new UserDetailsModel
        {
            UserId = u.Id,
            GroupId = u.GroupId,
            Username = u.UserName,
            DisplayName = u.DisplayName,
            Mail = u.Email,
            Phone = u.PhoneNumber
        };

        private async Task AddMessageAsync(int userId, string module, string type, string message, int autoClose = 3000)
        {
            _context.AppMessages.Add(new AppMessage
            {
                UserId = userId,
                Module = module,
                Type = type,
                Message = message,
                AutoClose = autoClose,
                State = 0
            });
            await _context.SaveChangesAsync();
        }

        private async Task<MessageModel> GetPendingMessagesAsync(int userId, string module)
        {
            var msgModel = new MessageModel();
            var pending = await _context.AppMessages
                .Where(m => m.State == 0 && m.Module == module && m.UserId == userId)
                .ToListAsync();

            foreach (var m in pending)
            {
                var mType = m.Type.ToLower() switch
                {
                    "info" => MessageModel.Types.Info,
                    "warning" => MessageModel.Types.Warning,
                    "danger" => MessageModel.Types.Danger,
                    _ => MessageModel.Types.Success
                };
                msgModel.aList.Add(new MessageModel.Message { Content = m.Message, Type = mType, AutoClose = m.AutoClose });
                m.State = 1;
            }
            await _context.SaveChangesAsync();
            return msgModel;
        }

        private static DataTable BuildGroupsTable(List<(ShiftHub.Data.Entities.Group g, ApplicationUser? u)> data)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id"); dt.Columns.Add("Name"); dt.Columns.Add("InChargeId");
            dt.Columns.Add("Note"); dt.Columns.Add("DisplayName"); dt.Columns.Add("Mail"); dt.Columns.Add("Phone");
            foreach (var (g, u) in data)
                dt.Rows.Add(g.Id, g.Name, g.InChargeId, g.Note, u?.DisplayName, u?.Email, u?.PhoneNumber);
            return dt;
        }

        private static DataTable BuildUsersTable(List<(ApplicationUser u, string groupName)> data)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id"); dt.Columns.Add("Username"); dt.Columns.Add("DisplayName");
            dt.Columns.Add("Mail"); dt.Columns.Add("Phone"); dt.Columns.Add("GroupId"); dt.Columns.Add("GroupName");
            foreach (var (u, gn) in data)
                dt.Rows.Add(u.Id, u.UserName, u.DisplayName, u.Email, u.PhoneNumber, u.GroupId, gn);
            return dt;
        }

        private static DataTable BuildGroupSelectTable(List<ShiftHub.Data.Entities.Group> groups)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id"); dt.Columns.Add("Name");
            foreach (var g in groups)
                dt.Rows.Add(g.Id, g.Name);
            return dt;
        }

        private static DataTable BuildGroupUsersTable(List<ApplicationUser> users)
        {
            var dt = new DataTable();
            dt.Columns.Add("Id"); dt.Columns.Add("Username"); dt.Columns.Add("DisplayName");
            dt.Columns.Add("Mail"); dt.Columns.Add("Phone");
            foreach (var u in users)
                dt.Rows.Add(u.Id, u.UserName, u.DisplayName, u.Email, u.PhoneNumber);
            return dt;
        }

        /// Main

        #region Groups
        public async Task<IActionResult> aGroups()
        {
            var currentUser = await GetCurrentUserAsync();
            ViewData["DisplayName"] = currentUser.DisplayName;

            var groups = await _context.Groups.ToListAsync();
            var userIds = groups.Select(g => g.InChargeId).Distinct().ToList();
            var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
            var userMap = users.ToDictionary(u => u.Id);

            var model = new AdminModels
            {
                UserDetails = ToUserDetails(currentUser),
                Data = new DataModel
                {
                    aTable = BuildGroupsTable(groups.Select(g => (g, (ApplicationUser?)userMap.GetValueOrDefault(g.InChargeId))).ToList())
                },
                Messages = await GetPendingMessagesAsync(currentUser.Id, "aGroups")
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult AddGroup() => PartialView("_AddGroup", new AdminModels());

        [HttpPost]
        public async Task<IActionResult> AddGroup(AdminModels iData)
        {
            var currentUser = await GetCurrentUserAsync();

            var newUser = new ApplicationUser
            {
                UserName = iData.User.Username.Trim().ToLower(),
                DisplayName = iData.User.DisplayName,
                Email = iData.User.Mail,
                PhoneNumber = iData.User.Phone,
                GroupId = 0,
                CreatedDate = DateTime.UtcNow
            };
            await _userManager.CreateAsync(newUser, iData.User.Password);

            var groupModel = iData.GetType().GetProperty("Group")?.GetValue(iData) as GroupModel ?? new GroupModel();
            var group = new EntityGroup { Name = groupModel.Name, InChargeId = newUser.Id, Note = groupModel.Note };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            newUser.GroupId = group.Id;
            await _userManager.UpdateAsync(newUser);
            await _userManager.AddToRoleAsync(newUser, "InCharge");

            if (iData.User.Notification)
            {
                var mailConfig = new MailConfigModel
                {
                    Server_Smtp = _configuration["MailConfig:Server_Smtp"] ?? "",
                    Port_Smtp = int.TryParse(_configuration["MailConfig:Port_Smtp"], out var p) ? p : 587,
                    Username = _configuration["MailConfig:Username"] ?? "",
                    Password = _configuration["MailConfig:Password"] ?? "",
                    Mail = _configuration["MailConfig:Mail"] ?? ""
                };
                var mailContent = new MailContentModel
                {
                    Base64To = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{iData.User.DisplayName} <{iData.User.Mail}>")),
                    Base64From = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ShiftHub <{_configuration["MailConfig:Mail"]}>")),
                    Base64Subject = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("ShiftHub, Ny Gruppe")),
                    Base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"Gruppe: {groupModel.Name}, Bruger: {iData.User.Username}"))
                };
                _context.Mails.Add(new Mail
                {
                    UserId = currentUser.Id,
                    GroupId = group.Id,
                    MailConfigJson = JsonSerializer.Serialize(mailConfig),
                    MailContentJson = JsonSerializer.Serialize(mailContent),
                    Status = "New"
                });
                await _context.SaveChangesAsync();
            }

            await AddMessageAsync(currentUser.Id, "aGroups", "success", "Gruppe er oprettet");
            return PartialView("_AddGroup", iData);
        }

        [HttpGet]
        public async Task<IActionResult> EditGroup(int GroupId)
        {
            var group = await _context.Groups.FindAsync(GroupId);
            var users = await _context.Users.Where(u => u.GroupId == GroupId).ToListAsync();

            var model = new AdminModels();
            model.Group.GroupId = GroupId;
            model.Group.Name = group?.Name ?? "";
            model.Group.Note = group?.Note ?? "";
            model.Group.InChargeId = group?.InChargeId ?? 0;
            model.Data.aTable = BuildGroupUsersTable(users);

            return PartialView("_EditGroup", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditGroup(AdminModels iData)
        {
            var currentUser = await GetCurrentUserAsync();
            var group = await _context.Groups.FindAsync(iData.Group.GroupId);
            if (group != null)
            {
                var oldInChargeId = group.InChargeId;
                group.Name = iData.Group.Name;
                group.InChargeId = iData.Group.InChargeId;
                group.Note = iData.Group.Note;
                await _context.SaveChangesAsync();

                if (oldInChargeId != iData.Group.InChargeId)
                {
                    var oldInCharge = await _userManager.FindByIdAsync(oldInChargeId.ToString());
                    if (oldInCharge != null) await _userManager.RemoveFromRoleAsync(oldInCharge, "InCharge");

                    var newInCharge = await _userManager.FindByIdAsync(iData.Group.InChargeId.ToString());
                    if (newInCharge != null && !await _userManager.IsInRoleAsync(newInCharge, "InCharge"))
                        await _userManager.AddToRoleAsync(newInCharge, "InCharge");
                }
            }
            await AddMessageAsync(currentUser.Id, "aGroups", "success", "Gruppe er opdateret");
            return PartialView("_EditGroup", iData);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteGroup(int GroupId)
        {
            var group = await _context.Groups.FindAsync(GroupId);
            var model = new AdminModels();
            model.Group.GroupId = GroupId;
            model.Group.Name = group?.Name ?? "";
            model.Group.Note = group?.Note ?? "";
            return PartialView("_DeleteGroup", model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGroup(AdminModels iData)
        {
            var currentUser = await GetCurrentUserAsync();

            var usersToDelete = await _context.Users.Where(u => u.GroupId == iData.Group.GroupId).ToListAsync();
            foreach (var u in usersToDelete)
                await _userManager.DeleteAsync(u);

            var group = await _context.Groups.FindAsync(iData.Group.GroupId);
            if (group != null) _context.Groups.Remove(group);

            var mails = await _context.Mails.Where(m => m.GroupId == iData.Group.GroupId).ToListAsync();
            _context.Mails.RemoveRange(mails);

            var staff = await _context.Staff.Where(s => s.GroupId == iData.Group.GroupId).ToListAsync();
            _context.Staff.RemoveRange(staff);

            await _context.SaveChangesAsync();
            await AddMessageAsync(currentUser.Id, "aGroups", "success", "Gruppen er slettet");
            return PartialView("_DeleteGroup", iData);
        }

        #endregion

        #region Users
        public async Task<IActionResult> aUsers()
        {
            var currentUser = await GetCurrentUserAsync();
            ViewData["DisplayName"] = currentUser.DisplayName;

            var users = await _context.Users.ToListAsync();
            var groupIds = users.Select(u => u.GroupId).Distinct().ToList();
            var groups = await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
            var groupMap = groups.ToDictionary(g => g.Id, g => g.Name);

            var tableData = users
                .OrderBy(u => u.GroupId)
                .Select(u => (u, groupMap.GetValueOrDefault(u.GroupId, "Master")))
                .ToList();

            var model = new AdminModels
            {
                UserDetails = ToUserDetails(currentUser),
                Data = new DataModel { aTable = BuildUsersTable(tableData) },
                Messages = await GetPendingMessagesAsync(currentUser.Id, "aUsers")
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AddUser()
        {
            var groups = await _context.Groups.ToListAsync();
            var model = new AdminModels();
            model.Data.aTable = BuildGroupSelectTable(groups);
            return PartialView("_AddUser", model);
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(AdminModels iData)
        {
            var currentUser = await GetCurrentUserAsync();

            var newUser = new ApplicationUser
            {
                UserName = iData.User.Username.Trim().ToLower(),
                DisplayName = iData.User.DisplayName,
                Email = iData.User.Mail,
                PhoneNumber = iData.User.Phone,
                GroupId = iData.User.GroupId,
                CreatedDate = DateTime.UtcNow
            };
            await _userManager.CreateAsync(newUser, iData.User.Password);

            if (iData.User.Notification)
            {
                var mailConfig = new MailConfigModel
                {
                    Server_Smtp = _configuration["MailConfig:Server_Smtp"] ?? "",
                    Port_Smtp = int.TryParse(_configuration["MailConfig:Port_Smtp"], out var p) ? p : 587,
                    Username = _configuration["MailConfig:Username"] ?? "",
                    Password = _configuration["MailConfig:Password"] ?? "",
                    Mail = _configuration["MailConfig:Mail"] ?? ""
                };
                var mailContent = new MailContentModel
                {
                    Base64To = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{iData.User.DisplayName} <{iData.User.Mail}>")),
                    Base64From = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ShiftHub <{_configuration["MailConfig:Mail"]}>")),
                    Base64Subject = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("ShiftHub, Ny Bruger")),
                    Base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"Bruger: {iData.User.Username}"))
                };
                _context.Mails.Add(new Mail
                {
                    UserId = currentUser.Id,
                    GroupId = currentUser.GroupId,
                    MailConfigJson = JsonSerializer.Serialize(mailConfig),
                    MailContentJson = JsonSerializer.Serialize(mailContent),
                    Status = "New"
                });
                await _context.SaveChangesAsync();
            }

            await AddMessageAsync(currentUser.Id, "aUsers", "success", "Bruger er oprettet");
            return PartialView("_AddUser", iData);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int UserId)
        {
            var groups = await _context.Groups.ToListAsync();
            var user = await _userManager.FindByIdAsync(UserId.ToString());

            var model = new AdminModels();
            model.Data.aTable = BuildGroupSelectTable(groups);
            if (user != null)
            {
                model.User.UserId = user.Id;
                model.User.Username = user.UserName ?? "";
                model.User.GroupId = user.GroupId;
                model.User.DisplayName = user.DisplayName ?? "";
                model.User.Mail = user.Email ?? "";
                model.User.Phone = user.PhoneNumber ?? "";
            }
            return PartialView("_EditUser", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(AdminModels iData)
        {
            var currentUser = await GetCurrentUserAsync();
            var user = await _userManager.FindByIdAsync(iData.User.UserId.ToString());
            if (user != null)
            {
                user.GroupId = iData.User.GroupId;
                user.DisplayName = iData.User.DisplayName;
                user.Email = iData.User.Mail;
                user.PhoneNumber = iData.User.Phone;
                await _userManager.UpdateAsync(user);

                if (!string.IsNullOrEmpty(iData.User.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, token, iData.User.Password);
                }
            }
            await AddMessageAsync(currentUser.Id, "aUsers", "success", "Bruger er opdateret");
            return PartialView("_EditUser", iData);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteUser(int UserId)
        {
            var user = await _userManager.FindByIdAsync(UserId.ToString());
            var model = new AdminModels();
            if (user != null)
            {
                model.User.UserId = user.Id;
                model.User.Username = user.UserName ?? "";
                model.User.GroupId = user.GroupId;
                model.User.DisplayName = user.DisplayName ?? "";
                model.User.Mail = user.Email ?? "";
                model.User.Phone = user.PhoneNumber ?? "";
            }
            return PartialView("_DeleteUser", model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(AdminModels iData)
        {
            var currentUser = await GetCurrentUserAsync();
            var groupUsers = await _context.Users.Where(u => u.GroupId == iData.User.GroupId).ToListAsync();

            if (groupUsers.Count > 1)
            {
                var group = await _context.Groups.FindAsync(iData.User.GroupId);
                if (group != null && group.InChargeId == iData.User.UserId)
                {
                    var newInCharge = groupUsers.FirstOrDefault(u => u.Id != iData.User.UserId);
                    if (newInCharge != null) group.InChargeId = newInCharge.Id;
                    await _context.SaveChangesAsync();
                }
                var userToDelete = await _userManager.FindByIdAsync(iData.User.UserId.ToString());
                if (userToDelete != null) await _userManager.DeleteAsync(userToDelete);
                await AddMessageAsync(currentUser.Id, "aUsers", "success", "Bruger er slettet");
            }
            else
            {
                await AddMessageAsync(currentUser.Id, "aUsers", "danger", "Kan ikke slette eneste gruppe bruger!");
            }
            return PartialView("_DeleteUser", iData);
        }

        #endregion

        #region Shared
        public async Task<JsonResult> _GenerateUsername(string DisplayName)
        {
            var matches = Regex.Matches(DisplayName, @"\w+");
            string username = matches.Count switch
            {
                1 => DisplayName.ToLower()[..Math.Min(4, DisplayName.Length)],
                2 => matches[0].Value.ToLower()[..Math.Min(2, matches[0].Value.Length)] + matches[1].Value.ToLower()[..Math.Min(2, matches[1].Value.Length)],
                _ => matches[0].Value.ToLower()[..Math.Min(2, matches[0].Value.Length)] + matches[2].Value.ToLower()[..Math.Min(2, matches[2].Value.Length)]
            };

            while (username.Length < 4) username += "0";
            if (Regex.IsMatch(username, @"(test|hest|fest)")) username = username[2..] + username[..2];

            int count = 0;
            while (count == 0 || await _context.Users.AnyAsync(u => u.UserName == username))
            {
                if (count > 0)
                {
                    string suffix = count.ToString();
                    username = username[..Math.Max(0, 4 - suffix.Length)] + suffix;
                }
                count++;
                if (count > 9999) break;
            }

            return Json(username);
        }
        #endregion
    }
}
