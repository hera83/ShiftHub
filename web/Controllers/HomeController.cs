using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftHub.Data;
using ShiftHub.Data.Entities;
using ShiftHub.Models;
using ShiftHub.Services;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Web;
using EntityStaff = ShiftHub.Data.Entities.Staff;

namespace ShiftHub.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        /// Init.

        #region Init. Log & Config
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _userManager = userManager;
        }
        #endregion

        #region Init. Glabal Services
        CodeServices code = new CodeServices();

        #endregion

        #region Init. Global Models
        HomeModels model = new HomeModels();

        #endregion

        private static string GetDanishWeekDay(DateTime value) =>
            value.ToString("dddd", CultureInfo.GetCultureInfo("da-DK"));

        private static int GetDanishWeekNumber(DateTime value)
        {
            var culture = CultureInfo.GetCultureInfo("da-DK");
            return culture.Calendar.GetWeekOfYear(value, culture.DateTimeFormat.CalendarWeekRule, culture.DateTimeFormat.FirstDayOfWeek);
        }

        private UserDetailsModel GetCurrentUserDetails()
        {
            var username = User.Identity?.Name ?? string.Empty;
            var user = _context.Users.AsNoTracking().First(item => item.UserName == username);

            return new UserDetailsModel
            {
                UserId = user.Id,
                GroupId = user.GroupId,
                Username = user.UserName,
                DisplayName = user.DisplayName,
                Mail = user.Email,
                Phone = user.PhoneNumber
            };
        }

        private MessageModel GetPendingMessages(int userId, string module)
        {
            var result = new MessageModel();
            var pendingMessages = _context.AppMessages
                .Where(message => message.UserId == userId && message.Module == module && message.State == 0)
                .OrderBy(message => message.Id)
                .ToList();

            foreach (var pendingMessage in pendingMessages)
            {
                result.aList.Add(new MessageModel.Message
                {
                    Content = pendingMessage.Message,
                    Type = pendingMessage.Type.ToLower() switch
                    {
                        "info" => MessageModel.Types.Info,
                        "warning" => MessageModel.Types.Warning,
                        "danger" => MessageModel.Types.Danger,
                        _ => MessageModel.Types.Success
                    },
                    AutoClose = pendingMessage.AutoClose
                });

                pendingMessage.State = 1;
            }

            if (pendingMessages.Count > 0)
            {
                _context.SaveChanges();
            }

            return result;
        }

        private void AddAppMessage(int userId, string module, string type, string message, int autoClose = 3000)
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
            _context.SaveChanges();
        }

        private static StaffModel ToStaffModel(EntityStaff staff) => new StaffModel
        {
            StaffId = staff.Id,
            CreatedDate = staff.CreatedDate,
            GroupId = staff.GroupId,
            Name = staff.Name,
            Address = staff.Address ?? string.Empty,
            ZipCode = staff.ZipCode ?? string.Empty,
            City = staff.City ?? string.Empty,
            Birthday = staff.Birthday ?? new DateTime(1900, 1, 1),
            Mail = staff.Mail ?? string.Empty,
            Phone = staff.Phone ?? string.Empty,
            Key1 = staff.Key1 ?? string.Empty,
            Key2 = staff.Key2 ?? string.Empty,
            Key3 = staff.Key3 ?? string.Empty
        };

        private static DataTable BuildStaffTable(IEnumerable<EntityStaff> staffItems, bool includeRowId = false)
        {
            var table = new DataTable();
            if (includeRowId)
            {
                table.Columns.Add("RowId");
            }

            table.Columns.Add("Id");
            table.Columns.Add("CreatedDate");
            table.Columns.Add("GroupId");
            table.Columns.Add("UiD");
            table.Columns.Add("Name");
            table.Columns.Add("Address");
            table.Columns.Add("ZipCode");
            table.Columns.Add("City");
            table.Columns.Add("Birthday");
            table.Columns.Add("Mail");
            table.Columns.Add("Phone");
            table.Columns.Add("Key1");
            table.Columns.Add("Key2");
            table.Columns.Add("Key3");
            table.Columns.Add("iType");

            var orderedItems = staffItems.ToList();
            for (var index = 0; index < orderedItems.Count; index++)
            {
                var staff = orderedItems[index];
                var rowValues = new List<object?>();
                if (includeRowId)
                {
                    rowValues.Add(index + 1);
                }

                rowValues.Add(staff.Id);
                rowValues.Add(staff.CreatedDate);
                rowValues.Add(staff.GroupId);
                rowValues.Add(staff.UiD);
                rowValues.Add(staff.Name);
                rowValues.Add(staff.Address);
                rowValues.Add(staff.ZipCode);
                rowValues.Add(staff.City);
                rowValues.Add(staff.Birthday);
                rowValues.Add(staff.Mail);
                rowValues.Add(staff.Phone);
                rowValues.Add(staff.Key1);
                rowValues.Add(staff.Key2);
                rowValues.Add(staff.Key3);
                rowValues.Add(staff.iType);
                table.Rows.Add(rowValues.ToArray());
            }

            return table;
        }

        private static DataTable CreateTable(params string[] columnNames)
        {
            var table = new DataTable();
            foreach (var columnName in columnNames)
            {
                table.Columns.Add(columnName);
            }

            return table;
        }

        private static string GetFirstName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return string.Empty;
            }

            var match = Regex.Match(fullName, @"^(.*?)\s");
            return match.Success ? match.Groups[1].Value : fullName;
        }

        private IQueryable<EntityStaff> GetStaffQuery(int groupId, string? filterValue = null)
        {
            var query = _context.Staff.Where(staff => staff.GroupId == groupId);

            if (string.IsNullOrWhiteSpace(filterValue))
            {
                return query.OrderBy(staff => staff.Name);
            }

            var filter = filterValue.Trim();
            return query
                .Where(staff =>
                    EF.Functions.Like(staff.Name, $"%{filter}%") ||
                    EF.Functions.Like(staff.Key1 ?? string.Empty, $"%{filter}%") ||
                    EF.Functions.Like(staff.Key2 ?? string.Empty, $"%{filter}%") ||
                    EF.Functions.Like(staff.Key3 ?? string.Empty, $"%{filter}%"))
                .OrderBy(staff => staff.Name);
        }

        private List<SubGroupDay> GetSubGroupDayItems(int subGroupId, string? filterValue = null)
        {
            var dayItems = _context.SubGroupDays
                .Where(day => day.SubGroupId == subGroupId)
                .OrderBy(day => day.Day)
                .ToList();

            if (string.IsNullOrWhiteSpace(filterValue))
            {
                return dayItems;
            }

            var filter = filterValue.Trim();
            return dayItems
                .Where(day =>
                    GetDanishWeekNumber(day.Day).ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    GetDanishWeekDay(day.Day).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (day.Key1 ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private List<SubGroupShift> GetSubGroupShiftItems(int subGroupId, string? filterValue = null, string? typeFilter = null)
        {
            var query = _context.SubGroupShifts
                .Where(shift => shift.SubGroupId == subGroupId);

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(shift => shift.Type == typeFilter);
            }

            if (!string.IsNullOrWhiteSpace(filterValue))
            {
                var filter = filterValue.Trim();
                query = query.Where(shift =>
                    EF.Functions.Like(shift.Name ?? string.Empty, $"%{filter}%") ||
                    EF.Functions.Like(shift.Type, $"%{filter}%") ||
                    EF.Functions.Like((shift.StaffNeeds ?? 0).ToString(), $"%{filter}%") ||
                    EF.Functions.Like(shift.Key1 ?? string.Empty, $"%{filter}%"));
            }

            return query.OrderBy(shift => shift.SubStartTime).ToList();
        }

        private string GetOrCreateSubGroupKeyValue(int subGroupId, string key, string defaultValue)
        {
            var subGroupKey = _context.SubGroupKeys.FirstOrDefault(item => item.SubGroupId == subGroupId && item.Key == key);
            if (subGroupKey != null)
            {
                return subGroupKey.Value;
            }

            _context.SubGroupKeys.Add(new SubGroupKey
            {
                SubGroupId = subGroupId,
                Key = key,
                Value = defaultValue
            });
            _context.SaveChanges();

            return defaultValue;
        }

        private static SubGroupShifts.Types MapShiftType(string? type) =>
            string.Equals(type, "Flex", StringComparison.OrdinalIgnoreCase)
            ? SubGroupShifts.Types.Flex
            : SubGroupShifts.Types.Fixed;

        private void SetShiftModel(HomeModels model, SubGroupShift shift)
        {
            model.SubShifts.ShiftsId = shift.Id;
            model.SubShifts.DayId = shift.DayId;
            model.SubShifts.Name = shift.Name ?? string.Empty;
            model.SubShifts.type = MapShiftType(shift.Type);
            model.SubShifts.StartTime = shift.SubStartTime;
            model.SubShifts.EndTime = shift.SubEndTime;
            model.SubShifts.StaffNeeds = shift.StaffNeeds ?? 0;
            model.SubShifts.Key1 = shift.Key1 ?? string.Empty;
        }

        private (DateTime StartTime, DateTime EndTime)? GetShiftDayWindow(int subGroupId, int subDayId)
        {
            var day = _context.SubGroupDays.AsNoTracking().FirstOrDefault(item => item.Id == subDayId && item.SubGroupId == subGroupId);
            if (day == null)
            {
                return null;
            }

            var cycleValue = GetOrCreateSubGroupKeyValue(subGroupId, "DaysCycle", "00:00");
            var startTime = DateTime.ParseExact(
                $"{day.Day:yyyy-MM-dd} {cycleValue}:00",
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture);

            return (startTime, startTime.AddDays(1).AddMinutes(-1));
        }

        private static string BuildNeedsChartJson(SubGroupShift shift, IEnumerable<SubGroupNeed> needs)
        {
            var needsByTime = needs.ToDictionary(item => item.Time, item => item.StaffCount);
            var current = shift.SubStartTime;
            var labels = new List<string>();
            var values = new List<int>();

            while (current < shift.SubEndTime)
            {
                labels.Add(current.ToString("HH:mm"));
                values.Add(needsByTime.TryGetValue(current, out var staffCount) ? staffCount : 0);
                current = current.AddHours(1);
            }

            return JsonSerializer.Serialize(new { labels, data = values });
        }

        private sealed class TimeUsageRow
        {
            public int StaffId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Mail { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Key1 { get; set; } = string.Empty;
            public string Key2 { get; set; } = string.Empty;
            public string Key3 { get; set; } = string.Empty;
            public int Timer { get; set; }
            public int Minutter { get; set; }
            public int TotalMinutes { get; set; }
        }

        private List<TimeUsageRow> GetTimeUsageRows(int subGroupId, int groupId, string? filterValue = null)
        {
            var totals = _context.SubGroupPostMembers
                .AsNoTracking()
                .Where(item => item.SubGroupId == subGroupId && item.CheckOut != null)
                .ToList()
                .GroupBy(item => item.StaffId)
                .Select(group => new
                {
                    StaffId = group.Key,
                    TotalMinutes = group.Sum(item => (int)(item.CheckOut!.Value - item.CreatedDate).TotalMinutes)
                })
                .ToList();

            var staffLookup = _context.Staff
                .AsNoTracking()
                .Where(staff => staff.GroupId == groupId)
                .ToDictionary(staff => staff.Id);

            var rows = totals
                .Where(item => staffLookup.ContainsKey(item.StaffId))
                .Select(item =>
                {
                    var staff = staffLookup[item.StaffId];
                    return new TimeUsageRow
                    {
                        StaffId = staff.Id,
                        Name = staff.Name,
                        Mail = staff.Mail ?? string.Empty,
                        Phone = staff.Phone ?? string.Empty,
                        Key1 = staff.Key1 ?? string.Empty,
                        Key2 = staff.Key2 ?? string.Empty,
                        Key3 = staff.Key3 ?? string.Empty,
                        Timer = item.TotalMinutes / 60,
                        Minutter = item.TotalMinutes - ((item.TotalMinutes / 60) * 60),
                        TotalMinutes = item.TotalMinutes
                    };
                })
                .OrderByDescending(item => item.TotalMinutes)
                .ThenBy(item => item.Name)
                .ToList();

            if (string.IsNullOrWhiteSpace(filterValue))
            {
                return rows;
            }

            var filter = filterValue.Trim();
            return rows
                .Where(item =>
                    item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Key1.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Key2.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Key3.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private List<(DateTime DateTime, int Needs, int Registered)> GetShiftViewTimeline(int subGroupId, DateTime date)
        {
            var cycleValue = GetOrCreateSubGroupKeyValue(subGroupId, "DaysCycle", "00:00");
            var startTime = DateTime.ParseExact(
                $"{date:yyyy-MM-dd} {cycleValue}:00",
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture);

            var shifts = _context.SubGroupShifts
                .AsNoTracking()
                .Where(shift => shift.SubGroupId == subGroupId)
                .ToList();

            var registrationCounts = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(registration => registration.SubGroupId == subGroupId)
                .GroupBy(registration => registration.ShiftId)
                .ToDictionary(group => group.Key, group => group.Count());

            var points = new List<(DateTime DateTime, int Needs, int Registered)>();
            for (var hour = 0; hour < 24; hour++)
            {
                var current = startTime.AddHours(hour);
                var overlappingShifts = shifts
                    .Where(shift => shift.SubStartTime <= current && shift.SubEndTime >= current.AddMinutes(59))
                    .ToList();

                points.Add((
                    current,
                    overlappingShifts.Sum(shift => shift.StaffNeeds ?? 0),
                    overlappingShifts.Sum(shift => registrationCounts.TryGetValue(shift.Id, out var count) ? count : 0)));
            }

            return points;
        }

        #region Handel Errors
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #endregion

        /// Main

        #region Dashboard
        public IActionResult Index()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Init. Variables
            String jsonObj = String.Empty;

            String jsonDateList = String.Empty;
            String jsonWeekDayList = String.Empty;
            String jsonCountList = String.Empty;
            String jsonActiveSubGroups = String.Empty;
            var activeSubGroups = _context.SubGroups
                .Where(item => item.GroupId == UserDetails.GroupId && item.Activated == 1)
                .OrderBy(item => item.Name)
                .ToList();
            var activeSubGroupIds = activeSubGroups.Select(item => item.Id).ToList();
            var nowDate = DateTime.Today;

            #endregion

            var arrangementsActive = _context.SubGroups.Count(item => item.GroupId == UserDetails.GroupId && item.Activated == 1);
            var arrangementsPassive = _context.SubGroups.Count(item => item.GroupId == UserDetails.GroupId && item.Activated == 0);
            var shiftsNeeds = _context.SubGroupShifts
                .Where(item => activeSubGroupIds.Contains(item.SubGroupId))
                .Sum(item => (int?)item.StaffNeeds) ?? 0;
            var registrationsPerSubGroup = _context.SubGroupRegistrations
                .Where(item => activeSubGroupIds.Contains(item.SubGroupId))
                .GroupBy(item => item.SubGroupId)
                .Select(group => group.Count())
                .ToList()
                .DefaultIfEmpty(0)
                .Max();
            var readMessages = _context.SubGroupMessages
                .Where(item => item.InOut == 2 && item.State == 1 && activeSubGroupIds.Contains(item.SubGroupId))
                .Count();
            var unreadMessages = _context.SubGroupMessages
                .Where(item => item.InOut == 2 && item.State == 0 && activeSubGroupIds.Contains(item.SubGroupId))
                .Count();
            var registrationsByDay = Enumerable.Range(-6, 7)
                .Select(offset => nowDate.AddDays(offset))
                .Select(date => new
                {
                    Date = date,
                    WeekDay = GetDanishWeekDay(date),
                    Count = _context.SubGroupRegistrations
                        .Where(item => activeSubGroupIds.Contains(item.SubGroupId) && item.CreatedDate.Date == date.Date)
                        .Select(item => item.StaffId)
                        .Distinct()
                        .Count()
                })
                .ToList();

            #region Generate Json String
            jsonObj = "{";

            /// Arrangementer
            jsonObj += $"\"Arrangements\": [{arrangementsActive},{arrangementsPassive}],";

            /// Vagter
            jsonObj += $"\"Shifts\": [{registrationsPerSubGroup},{shiftsNeeds - registrationsPerSubGroup}],";

            /// Beskeder
            jsonObj += $"\"Messages\": [{readMessages},{unreadMessages}],";

            /// Tilmeldinger
            foreach (var registration in registrationsByDay)
            {
                if (registrationsByDay.IndexOf(registration) == 0)
                {
                    jsonDateList += $"\"{registration.Date:yyyy-MM-dd}\"";
                    jsonWeekDayList += $"\"{registration.WeekDay}\"";
                    jsonCountList += $"{registration.Count}";
                }
                else
                {
                    jsonDateList += $",\"{registration.Date:yyyy-MM-dd}\"";
                    jsonWeekDayList += $",\"{registration.WeekDay}\"";
                    jsonCountList += $",{registration.Count}";
                }
            }
            jsonObj += "\"Registrations\": {"; jsonObj += $"\"Date\": [{jsonDateList}], \"WeekDay\": [{jsonWeekDayList}], \"Count\": [{jsonCountList}]"; jsonObj += "},";

            /// Active SubGroups
            foreach (var subGroup in activeSubGroups)
            {
                if (activeSubGroups.IndexOf(subGroup) == 0)
                {
                    jsonActiveSubGroups = "{\"Id\": " + $"{subGroup.Id}" + ", \"Name\": \"" + $"{subGroup.Name}" + "\"}";
                }
                else
                {
                    jsonActiveSubGroups += ",{\"Id\": " + $"{subGroup.Id}" + ", \"Name\": \"" + $"{subGroup.Name}" + "\"}";
                }
            }
            jsonObj += "\"SubGroups\": [" + jsonActiveSubGroups + "]";

            jsonObj += "}";
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.JsonObj = jsonObj;
            model.Messages = GetPendingMessages(UserDetails.UserId, "Dashboard");

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }

        [HttpGet]
        public IActionResult SubPostCheckIn()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return PartialView("_SubPostCheckIn", model);
            #endregion
        }

        [HttpGet]
        public IActionResult SubPostCheckOut(int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();
            var staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.GroupId == UserDetails.GroupId && item.Id == StaffId);

            if (staff == null)
            {
                return NotFound();
            }

            #endregion

            #region Set Data
            data.aTable = BuildStaffTable(new[] { staff });

            model.UserDetails = UserDetails;
            model.Data = data;

            #endregion

            #region Return Data
            return PartialView("_SubPostCheckOut", model);
            #endregion
        }
        [HttpGet]
        public IActionResult SubPostCreatePost()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return PartialView("_SubPostCreatePost", model);
            #endregion
        }
        [HttpGet]
        public IActionResult SubPostEditPost(string Id)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            int postGroupId = Convert.ToInt32(Id);
            var postGroup = _context.SubGroupPostGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == postGroupId);

            if (postGroup == null)
            {
                return NotFound();
            }

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.SubPost = new SubPostModel { Id = postGroup.Id, Name = postGroup.Name, MaxTime = postGroup.MaxTime };

            #endregion

            #region Return Data
            return PartialView("_SubPostEditPost", model);
            #endregion
        }
        [HttpGet]
        public IActionResult SubPostDeletePost(string Id)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            int postGroupId = Convert.ToInt32(Id);
            var postGroup = _context.SubGroupPostGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == postGroupId);

            if (postGroup == null)
            {
                return NotFound();
            }

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.SubPost = new SubPostModel { Id = postGroup.Id, Name = postGroup.Name };

            #endregion

            #region Return Data
            return PartialView("_SubPostDeletePost", model);
            #endregion
        }
        [HttpGet]
        public IActionResult AlertView(string ShiftId, string StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();
            int shiftId = Convert.ToInt32(ShiftId);
            int staffId = Convert.ToInt32(StaffId);

            var staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.GroupId == UserDetails.GroupId && item.Id == staffId);
            var shift = (from subShift in _context.SubGroupShifts.AsNoTracking()
                         join subGroup in _context.SubGroups.AsNoTracking() on subShift.SubGroupId equals subGroup.Id
                         where subGroup.GroupId == UserDetails.GroupId && subShift.Id == shiftId
                         select subShift)
                .FirstOrDefault();

            if (staff == null || shift == null)
            {
                return NotFound();
            }

            var minuteDifference = Convert.ToInt32(Math.Truncate((shift.SubStartTime - DateTime.Now).TotalMinutes));
            #endregion

            #region Get Data
            data.aTable = BuildStaffTable(new[] { staff });
            data.bTable = CreateTable("Id", "Min");
            data.bTable.Rows.Add(shift.Id, minuteDifference);

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Data = data;

            #endregion

            #region Return Data
            return PartialView("_AlertView", model);
            #endregion
        }

        public JsonResult _SubPostUpdate(int SubGroupId, string SearchValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            var now = DateTime.Now;

            #endregion

            #region Get Data
            var activePostCounts = _context.SubGroupPostMembers
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId && item.CheckOut == null)
                .GroupBy(item => item.PostId)
                .ToDictionary(group => group.Key, group => group.Count());

            var posts = _context.SubGroupPostGroups
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .OrderBy(item => item.GroupPosision)
                .ToList()
                .Select(item => new
                {
                    Id = item.Id,
                    Group = item.GroupPosision,
                    Name = item.Name,
                    MaxTime = item.MaxTime,
                    StaffCount = activePostCounts.TryGetValue(item.Id, out var staffCount) ? staffCount : 0
                })
                .ToList();

            var staffQuery = from postMember in _context.SubGroupPostMembers.AsNoTracking()
                             join staff in _context.Staff.AsNoTracking() on postMember.StaffId equals staff.Id
                             join postGroup in _context.SubGroupPostGroups.AsNoTracking() on postMember.PostId equals postGroup.Id
                             where postMember.SubGroupId == SubGroupId && postMember.CheckOut == null
                             select new
                             {
                                 postGroup.GroupPosision,
                                 GroupName = postGroup.Name,
                                 postMember.PostId,
                                 staff.Id,
                                 staff.Name,
                                 postMember.CreatedDate
                             };

            if (!string.IsNullOrWhiteSpace(SearchValue))
            {
                var search = SearchValue.Trim();
                staffQuery = staffQuery.Where(item => EF.Functions.Like(item.GroupName, $"%{search}%"));
            }

            var staffItems = staffQuery
                .OrderBy(item => item.GroupPosision)
                .ThenBy(item => item.Name)
                .ToList()
                .Select(item => new
                {
                    Group = item.GroupPosision,
                    GroupName = item.GroupName,
                    item.PostId,
                    StaffId = item.Id,
                    FirstName = GetFirstName(item.Name),
                    item.Name,
                    CheckIn = item.CreatedDate.ToString("dd MMM HH:mm"),
                    Minutes = Convert.ToInt32(Math.Truncate((now - item.CreatedDate).TotalMinutes))
                })
                .ToList();

            var alerts = (from registration in _context.SubGroupRegistrations.AsNoTracking()
                          join shift in _context.SubGroupShifts.AsNoTracking() on registration.ShiftId equals shift.Id
                          join staff in _context.Staff.AsNoTracking() on registration.StaffId equals staff.Id
                          join subGroup in _context.SubGroups.AsNoTracking() on registration.SubGroupId equals subGroup.Id
                          where registration.SubGroupId == SubGroupId
                              && subGroup.GroupId == UserDetails.GroupId
                              && now >= shift.SubStartTime.AddMinutes(10)
                              && now <= shift.SubEndTime
                              && !_context.SubGroupPostMembers.Any(postMember => postMember.StaffId == registration.StaffId && postMember.CheckOut == null)
                              && !_context.SubGroupAlerts.Any(alert =>
                                  alert.StaffId == registration.StaffId &&
                                  alert.ShiftId == registration.ShiftId &&
                                  (alert.State == 1 || (alert.State == 0 && alert.Sleep != null && now <= alert.Sleep.Value)))
                          select new
                          {
                              registration.ShiftId,
                              registration.StaffId,
                              staff.Name,
                              staff.Mail,
                              staff.Phone,
                              ShiftName = shift.Name,
                              ShiftStart = shift.SubStartTime,
                              ShiftEnd = shift.SubEndTime
                          })
                .ToList()
                .Select(item => new
                {
                    item.ShiftId,
                    item.StaffId,
                    FirstName = GetFirstName(item.Name),
                    item.Name,
                    item.Mail,
                    item.Phone,
                    item.ShiftName,
                    item.ShiftStart,
                    item.ShiftEnd
                })
                .ToList();

            var moveToItems = _context.SubGroupPostGroups
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId && item.GroupPosision == 0)
                .OrderBy(item => item.CreatedDate)
                .Select(item => new
                {
                    PostId = item.Id,
                    GroupName = item.Name
                })
                .ToList();

            #endregion

            #region Generate Json
            string json = JsonSerializer.Serialize(new
            {
                Posts = posts,
                Staff = staffItems,
                Alerts = alerts,
                MoveTo = moveToItems
            });
            #endregion

            #region Return Data
            return Json(json);
            #endregion
        }
        public JsonResult _SubPostSave(int SubGroupId, string Name, int MaxTime)
        {
            #region Set Data
            _context.SubGroupPostGroups.Add(new SubGroupPostGroup
            {
                SubGroupId = SubGroupId,
                GroupPosision = 0,
                Name = Name,
                MaxTime = MaxTime
            });
            _context.SaveChanges();

            #endregion

            #region Return Data
            return Json(string.Empty);
            #endregion
        }
        public JsonResult _SubPostEdit(int Id, string Name, int MaxTime)
        {
            #region Set Data
            var postGroup = _context.SubGroupPostGroups.FirstOrDefault(item => item.Id == Id);
            if (postGroup != null)
            {
                postGroup.Name = Name;
                postGroup.MaxTime = MaxTime;
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(string.Empty);
            #endregion
        }
        public JsonResult _SubPostDelete(int Id)
        {
            string json = $"{Id}";

            #region Set Data
            var postGroup = _context.SubGroupPostGroups.FirstOrDefault(item => item.Id == Id);
            if (postGroup != null)
            {
                _context.SubGroupPostGroups.Remove(postGroup);
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(json);
            #endregion
        }
        public JsonResult _SubPostStaffCheckOut(int StaffId)
        {
            String json = String.Empty;

            #region Set Data
            List<SubGroupPostMember> activePosts = _context.SubGroupPostMembers
                .Where(item => item.StaffId == StaffId && item.CheckOut == null)
                .ToList();

            foreach (SubGroupPostMember activePost in activePosts)
            {
                activePost.CheckOut = DateTime.Now;
                activePost.AlarmState = 0;
            }

            if (activePosts.Count > 0)
            {
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(json);
            #endregion
        }
        public JsonResult _SubPostChangePosision(int SubGroupId, string Group, string Target)
        {
            String json = String.Empty;
            String GroupId = String.Empty;
            String TargetTrimed = String.Empty;
            String TargetId = String.Empty;

            #region Get Data
            GroupId = Regex.Match(Group.Trim(), @"^(.*)_([0-9]+)$").Groups[2].Value;
            TargetTrimed = Regex.Match(Target.Trim(), @"^(.*)_([0-9]+)$").Groups[1].Value;
            TargetId = Regex.Match(Target.Trim(), @"^(.*)_([0-9]+)$").Groups[2].Value;

            #endregion

            #region Set Data
            switch (TargetTrimed)
            {
                case "Groups":
                    int postGroupId = Convert.ToInt32(GroupId);
                    int groupTargetId = Convert.ToInt32(TargetId);
                    var postGroup = _context.SubGroupPostGroups.FirstOrDefault(item => item.Id == postGroupId);
                    if (postGroup != null)
                    {
                        postGroup.GroupPosision = groupTargetId;
                        _context.SaveChanges();
                    }
                    break;
                case "Staffs":
                    int staffId = Convert.ToInt32(GroupId);
                    int targetPostId = Convert.ToInt32(TargetId);
                    var currentPost = _context.SubGroupPostMembers
                        .AsNoTracking()
                        .FirstOrDefault(item => item.StaffId == staffId && item.CheckOut == null);

                    if (currentPost != null)
                    {
                        if (currentPost.PostId == targetPostId)
                        {
                            break;
                        }
                    }

                    List<SubGroupPostMember> activePosts = _context.SubGroupPostMembers
                        .Where(item => item.StaffId == staffId && item.CheckOut == null)
                        .ToList();

                    foreach (SubGroupPostMember activePost in activePosts)
                    {
                        activePost.CheckOut = DateTime.Now;
                    }

                    _context.SubGroupPostMembers.Add(new SubGroupPostMember
                    {
                        SubGroupId = SubGroupId,
                        PostId = targetPostId,
                        StaffId = staffId,
                        AlarmState = 0
                    });
                    _context.SaveChanges();
                    break;
            }
            #endregion

            #region Return Data
            return Json(json);
            #endregion
        }
        public JsonResult _SubPostGetPosts(int SubGroupId)
        {
            String html = String.Empty;

            #region Get Data
            List<SubGroupPostGroup> postGroups = _context.SubGroupPostGroups
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .OrderBy(item => item.GroupPosision)
                .ToList();
            #endregion

            #region Generate Html
            foreach (SubGroupPostGroup postGroup in postGroups)
            {
                html += $"<option value=\"{postGroup.Id}\">{postGroup.Name}</option>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubPostGetList(int SubGroupId)
        {
            String html = String.Empty;
            DateTime now = DateTime.Now;

            #region Get Data
            var staffList = (from registration in _context.SubGroupRegistrations.AsNoTracking()
                             join shift in _context.SubGroupShifts.AsNoTracking() on registration.ShiftId equals shift.Id
                             join staff in _context.Staff.AsNoTracking() on registration.StaffId equals staff.Id
                             where registration.SubGroupId == SubGroupId
                                 && shift.SubStartTime >= now.AddMinutes(-60)
                                 && shift.SubStartTime <= now.AddMinutes(60)
                                 && !_context.SubGroupPostMembers.Any(postMember =>
                                     postMember.SubGroupId == registration.SubGroupId &&
                                     postMember.StaffId == registration.StaffId &&
                                     postMember.CheckOut == null)
                             select new
                             {
                                 registration.StaffId,
                                 staff.Name
                             })
                .ToList()
                .GroupBy(item => new { item.StaffId, item.Name })
                .Select(group => group.Key)
                .OrderBy(item => item.Name)
                .Take(10)
                .ToList();
            #endregion

            #region Generate Html
            html = $"<table width=\"100%\">";

            foreach (var staff in staffList)
            {
                html += $"<tr>" +
                    $"<td align=\"left\" valign=\"middle\">" +
                        $"<b>{staff.Name}</b>" +
                    $"</td>" +
                    $"<td align=\"right\" valign=\"middle\">" +
                        $"<button type=\"button\" class=\"btn\" onclick=\"CheckIn({staff.StaffId})\" title=\"CheckIn\"><i class=\"bi bi-person-down\"></i></button>" +
                    $"</td>" +
                $"</tr>";
            }
            if (staffList.Count == 0)
            {
                html += $"<tr><td align=\"center\"><i>Ingen fundet</i></td></tr>";
            }

            html += $"</table>";
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubPostGetStaffSearch(int SubGroupId, string Filter)
        {
            String html = String.Empty;
            DateTime today = DateTime.Today;
            string filter = Filter?.Trim() ?? string.Empty;

            #region Get Data
            DateTime todayEnd = today.AddDays(1);

            // Fetch all staff registered for a shift in this subgroup who are not already checked in
            // Bring to memory first to avoid SQLite translation issues with .Date and nested subqueries
            var allRegistered = (from registration in _context.SubGroupRegistrations.AsNoTracking()
                                 join shift in _context.SubGroupShifts.AsNoTracking() on registration.ShiftId equals shift.Id
                                 join staff in _context.Staff.AsNoTracking() on registration.StaffId equals staff.Id
                                 where registration.SubGroupId == SubGroupId
                                 select new
                                 {
                                     registration.StaffId,
                                     staff.Name,
                                     staff.Key1,
                                     staff.Key2,
                                     staff.Key3,
                                     ShiftStart = shift.SubStartTime
                                 })
                .ToList();

            // Exclude already checked-in staff
            var checkedInStaffIds = _context.SubGroupPostMembers.AsNoTracking()
                .Where(pm => pm.SubGroupId == SubGroupId && pm.CheckOut == null)
                .Select(pm => pm.StaffId)
                .ToHashSet();

            // Group per staff, apply text filter and compute ToDay — all in memory
            string f = filter.ToLower();
            bool showAll = f == "%";
            var staffResult = allRegistered
                .Where(item => !checkedInStaffIds.Contains(item.StaffId))
                .GroupBy(item => new { item.StaffId, item.Name })
                .Select(group => new
                {
                    group.Key.StaffId,
                    group.Key.Name,
                    Key1 = group.First().Key1,
                    Key2 = group.First().Key2,
                    Key3 = group.First().Key3,
                    ToDay = group.Any(item => item.ShiftStart >= today && item.ShiftStart < todayEnd) ? 1 : 0
                })
                .Where(item => showAll ||
                    item.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                    (item.Key1 != null && item.Key1.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                    (item.Key2 != null && item.Key2.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                    (item.Key3 != null && item.Key3.Contains(f, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.Name)
                .Take(10)
                .ToList();
            #endregion

            #region Generate Html
            html = $"<table width=\"100%\">";

            foreach (var staff in staffResult)
            {
                html += $"<tr>" +
                    $"<td align=\"left\" valign=\"middle\">" +
                        $"<b>{staff.Name}</b>" +
                    $"</td>" +
                    $"<td align=\"right\" valign=\"middle\">" +
                        $"<button type=\"button\" class=\"btn\" onclick=\"CheckIn({staff.StaffId})\"><i class=\"bi bi-person-down\" style=\"color: "; if (staff.ToDay == 1) { html += "green"; } else { html += "red"; } html += ";\" title=\""; if (staff.ToDay == 1) { html += "Vagt fundet"; } else { html += "Bruger har ingen vagt som starter i dag?"; } html += "\"></i></button>" +
                    $"</td>" +
                $"</tr>";
            }
            if (staffResult.Count == 0)
            {
                html += $"<tr><td align=\"center\"><i>Ingen fundet</i></td></tr>";
            }

            html += $"</table>";
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubPostCheckIn(int SubGroupId, string PostId, string StaffId)
        {
            #region Set Data
            _context.SubGroupPostMembers.Add(new SubGroupPostMember
            {
                SubGroupId = SubGroupId,
                PostId = Convert.ToInt32(PostId),
                StaffId = Convert.ToInt32(StaffId),
                AlarmState = 0
            });
            _context.SaveChanges();
            #endregion

            #region Return Data
            return Json(string.Empty);
            #endregion
        }
        public JsonResult _AlertSnooz(int SubGroupId, int ShiftId, int StaffId, int Min)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String html = String.Empty;

            #endregion

            #region Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                List<SubGroupAlert> alerts = _context.SubGroupAlerts
                    .Where(alert => alert.SubGroupId == SubGroupId && alert.StaffId == StaffId && alert.ShiftId == ShiftId)
                    .ToList();

                if (alerts.Count > 0)
                {
                    _context.SubGroupAlerts.RemoveRange(alerts);
                }

                _context.SubGroupAlerts.Add(new SubGroupAlert
                {
                    SubGroupId = SubGroupId,
                    StaffId = StaffId,
                    ShiftId = ShiftId,
                    State = 0,
                    Sleep = DateTime.Now.AddMinutes(Min)
                });

                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _AlertAbsent(int SubGroupId, int ShiftId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String html = String.Empty;

            #endregion

            #region Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                List<SubGroupAlert> alerts = _context.SubGroupAlerts
                    .Where(alert => alert.SubGroupId == SubGroupId && alert.StaffId == StaffId && alert.ShiftId == ShiftId)
                    .ToList();

                if (alerts.Count > 0)
                {
                    _context.SubGroupAlerts.RemoveRange(alerts);
                }

                _context.SubGroupAlerts.Add(new SubGroupAlert
                {
                    SubGroupId = SubGroupId,
                    StaffId = StaffId,
                    ShiftId = ShiftId,
                    State = 1
                });

                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubPostStaffSwop(int SubGroupId, int PostId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String html = String.Empty;

            #endregion

            #region Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                List<SubGroupPostMember> activePosts = _context.SubGroupPostMembers
                    .Where(postMember => postMember.SubGroupId == SubGroupId && postMember.StaffId == StaffId && postMember.CheckOut == null)
                    .ToList();

                foreach (SubGroupPostMember activePost in activePosts)
                {
                    activePost.CheckOut = DateTime.Now;
                }

                _context.SubGroupPostMembers.Add(new SubGroupPostMember
                {
                    SubGroupId = SubGroupId,
                    PostId = PostId,
                    StaffId = StaffId,
                    AlarmState = 0
                });

                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        #endregion

        #region Staff
        public IActionResult Staff()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Data.aTable = BuildStaffTable(GetStaffQuery(UserDetails.GroupId).ToList(), includeRowId: true);
            model.Messages = GetPendingMessages(UserDetails.UserId, "Staff");

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }

        [HttpGet]
        public IActionResult AddStaff()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return PartialView("_AddStaff", model);
            #endregion
        }
        [HttpPost]
        public IActionResult AddStaff(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            _context.Staff.Add(new EntityStaff
            {
                GroupId = UserDetails.GroupId,
                UiD = Guid.NewGuid().ToString("N"),
                Name = iData.Staff.Name,
                Address = iData.Staff.Address,
                ZipCode = iData.Staff.ZipCode,
                City = iData.Staff.City,
                Birthday = iData.Staff.Birthday,
                Mail = iData.Staff.Mail,
                Phone = iData.Staff.Phone,
                Key1 = iData.Staff.Key1,
                Key2 = iData.Staff.Key2,
                Key3 = iData.Staff.Key3,
                iType = 1
            });
            _context.SaveChanges();

            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "Staff", "success", "Bruger er oprettet");

            #endregion

            #region Return Data
            return PartialView("_AddStaff", iData);
            #endregion
        }

        [HttpGet]
        public IActionResult EditStaff(int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var staff = _context.Staff.AsNoTracking().First(item => item.Id == StaffId);

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Staff = ToStaffModel(staff);

            #endregion

            #region Return Data
            return PartialView("_EditStaff", model);
            #endregion
        }
        [HttpPost]
        public IActionResult EditStaff(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            var staff = _context.Staff.First(item => item.Id == iData.Staff.StaffId);
            staff.Name = iData.Staff.Name;
            staff.Address = iData.Staff.Address;
            staff.ZipCode = iData.Staff.ZipCode;
            staff.City = iData.Staff.City;
            staff.Birthday = iData.Staff.Birthday;
            staff.Mail = iData.Staff.Mail;
            staff.Phone = iData.Staff.Phone;
            staff.Key1 = iData.Staff.Key1;
            staff.Key2 = iData.Staff.Key2;
            staff.Key3 = iData.Staff.Key3;
            _context.SaveChanges();

            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "Staff", "success", "Bruger er ændret");

            #endregion

            #region Return Data
            return PartialView("_EditStaff", iData);
            #endregion
        }

        [HttpGet]
        public IActionResult DeleteStaff(int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var staff = _context.Staff.AsNoTracking().First(item => item.Id == StaffId);

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Staff = ToStaffModel(staff);

            #endregion

            #region Return Data
            return PartialView("_DeleteStaff", model);
            #endregion
        }
        [HttpPost]
        public IActionResult DeleteStaff(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            var relatedMessages = _context.SubGroupMessages.Where(item => item.StaffId == iData.Staff.StaffId).ToList();
            var relatedRegistrations = _context.SubGroupRegistrations.Where(item => item.StaffId == iData.Staff.StaffId).ToList();
            var relatedSubGroupStaff = _context.SubGroupStaff.Where(item => item.StaffId == iData.Staff.StaffId).ToList();
            var staff = _context.Staff.FirstOrDefault(item => item.Id == iData.Staff.StaffId);

            _context.SubGroupMessages.RemoveRange(relatedMessages);
            _context.SubGroupRegistrations.RemoveRange(relatedRegistrations);
            _context.SubGroupStaff.RemoveRange(relatedSubGroupStaff);
            if (staff != null)
            {
                _context.Staff.Remove(staff);
            }
            _context.SaveChanges();

            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "Staff", "success", "Bruger er slettet");

            #endregion

            #region Return Data
            return PartialView("_DeleteStaff", iData);
            #endregion
        }

        [HttpGet]
        public IActionResult InportStaff()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return PartialView("_InportStaff", model);
            #endregion
        }
        [HttpPost]
        public IActionResult InportStaff(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataTable aTable = new DataTable();
            DataRow row;
            MatchCollection aMatch;
            Encoding encoding;
            int aCount;
            int bCount;
            var importedStaff = new List<EntityStaff>();

            #endregion

            #region Config Table
            aTable.Columns.Add($"Id", typeof(int));
            aTable.Columns.Add($"CreatedDate", typeof(DateTime));
            aTable.Columns.Add($"GroupId", typeof(int));
            aTable.Columns.Add($"UiD", typeof(string));

            #endregion

            #region Check Inport File
            if (iData.Inport.File == null)
            {
                #region Set Message
                AddAppMessage(UserDetails.UserId, "InportStaff", "danger", "Ingen file fundet!?", 0);

                #endregion

                #region Return Data
                return RedirectToAction("Users", "Home");
                #endregion
            }

            #endregion

            #region Get File Encoding
            string detectedEncoding;
            var Utf8EncodingVerifier = Encoding.GetEncoding("utf-8", new EncoderExceptionFallback(), new DecoderExceptionFallback());
            using (StreamReader reader = new StreamReader(iData.Inport.File.OpenReadStream(), Utf8EncodingVerifier, detectEncodingFromByteOrderMarks: true, leaveOpen: true, bufferSize: 1024))
            {
                try
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                    }
                    detectedEncoding = reader.CurrentEncoding.BodyName;
                }
                catch (Exception)
                {
                    detectedEncoding = "ISO-8859-1";
                }
            }
            encoding = Encoding.GetEncoding(detectedEncoding);
            #endregion

            #region Generate Table
            aCount = 0;
            using (StreamReader reader = new StreamReader(iData.Inport.File.OpenReadStream(), encoding))
            {
                while (reader.Peek() >= 0)
                {
                    if (aCount == 0)
                    {
                        /// Generate Columns
                        aMatch = Regex.Matches($"{reader.ReadLine()}", @"(.*?)(?:;|$)");

                        foreach (Match match in aMatch)
                        {
                            if (match.Groups[1].Success && string.IsNullOrEmpty(match.Groups[1].Value) == false)
                            {
                                aTable.Columns.Add($"{match.Groups[1].Value}", typeof(string));
                            }
                        }
                    }
                    else
                    {
                        /// Set Content
                        aMatch = Regex.Matches($"{reader.ReadLine()}", @"(.*?)(?:;|$)");
                        row = aTable.NewRow();

                        bCount = 4;
                        foreach (Match match in aMatch)
                        {
                            if (bCount <= aTable.Columns.Count)
                            {
                                if (match.Groups[1].Success && string.IsNullOrEmpty(match.Groups[1].Value) == false)
                                {
                                    row[bCount] = $"{match.Groups[1].Value}";
                                }
                                else
                                {
                                    row[bCount] = DBNull.Value;
                                }
                            }

                            bCount++;
                        }

                        aTable.Rows.Add(row);
                    }

                    aCount++;
                }
            }

            #endregion

            #region Set Table Defaults
            foreach (DataRow aRow in aTable.Rows)
            {
                importedStaff.Add(new EntityStaff
                {
                    CreatedDate = DateTime.Now,
                    GroupId = UserDetails.GroupId,
                    UiD = Guid.NewGuid().ToString("N"),
                    Name = Convert.ToString(aRow.Table.Columns.Contains("Name") ? aRow["Name"] : string.Empty) ?? string.Empty,
                    Address = aRow.Table.Columns.Contains("Address") ? Convert.ToString(aRow["Address"]) : null,
                    ZipCode = aRow.Table.Columns.Contains("ZipCode") ? Convert.ToString(aRow["ZipCode"]) : null,
                    City = aRow.Table.Columns.Contains("City") ? Convert.ToString(aRow["City"]) : null,
                    Birthday = aRow.Table.Columns.Contains("Birthday") && DateTime.TryParse(Convert.ToString(aRow["Birthday"]), out var birthday) ? birthday : null,
                    Mail = aRow.Table.Columns.Contains("Mail") ? Convert.ToString(aRow["Mail"]) : null,
                    Phone = aRow.Table.Columns.Contains("Phone") ? Convert.ToString(aRow["Phone"]) : null,
                    Key1 = aRow.Table.Columns.Contains("Key1") ? Convert.ToString(aRow["Key1"]) : null,
                    Key2 = aRow.Table.Columns.Contains("Key2") ? Convert.ToString(aRow["Key2"]) : null,
                    Key3 = aRow.Table.Columns.Contains("Key3") ? Convert.ToString(aRow["Key3"]) : null,
                    iType = 1
                });
            }
            #endregion

            #region Set Data
            if (importedStaff.Count > 0)
            {
                _context.Staff.AddRange(importedStaff);
                _context.SaveChanges();
            }

            #endregion

            #region Set Message
            if (importedStaff.Count > 0)
            {
                AddAppMessage(UserDetails.UserId, "Staff", "success", $"File er indlæst ({importedStaff.Count})");
            }
            else
            {
                AddAppMessage(UserDetails.UserId, "Staff", "success", "File er indlæst, ingen brugere fundet?");
            }
            #endregion

            #region Return Data
            return RedirectToAction("Staff","Home");
            #endregion
        }

        public IActionResult ExportStaff()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            string ExportContent = string.Empty;

            #endregion

            #region Get Data
            var staffItems = GetStaffQuery(UserDetails.GroupId)
                .Select(staff => new
                {
                    staff.Name,
                    staff.Address,
                    staff.ZipCode,
                    staff.City,
                    staff.Birthday,
                    staff.Mail,
                    staff.Phone,
                    staff.Key1,
                    staff.Key2,
                    staff.Key3
                })
                .ToList();

            #endregion

            #region Generate CSV
            ExportContent += "Name;Address;ZipCode;City;Birthday;Mail;Phone;Key1;Key2;Key3";
            ExportContent += Environment.NewLine;
            foreach (var staff in staffItems)
            {
                ExportContent += string.Join(";", new[]
                {
                    staff.Name,
                    staff.Address,
                    staff.ZipCode,
                    staff.City,
                    staff.Birthday?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    staff.Mail,
                    staff.Phone,
                    staff.Key1,
                    staff.Key2,
                    staff.Key3
                });
                ExportContent += Environment.NewLine;
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return File(Encoding.Unicode.GetBytes(ExportContent), "text/plain", $"BrugerRapport_{DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss")}.csv",true);
            #endregion
        }

        public JsonResult _StaffUpdate(string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            var staffItems = GetStaffQuery(UserDetails.GroupId, FilterValue).ToList();

            #endregion

            #region Generate Html
            if (staffItems.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                foreach (var aRow in staffItems)
                {
                    if (staffItems.IndexOf(aRow) == 10)
                    {
                        break;
                    }

                    html += $"<tr class=\"light\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{(staffItems.IndexOf(aRow) + 1)}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            $"{aRow.Name}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{aRow.Mail}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{aRow.Phone}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{aRow.Key1}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{aRow.Key2}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{aRow.Key3}" +
                        "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            "<ul class=\"navbar-nav flex-grow-1\">" +
                                "<li class=\"nav-item dropdown\">" +
                                    "<a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"navbarDropdown\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">" +
                                        "<i class=\"bi bi-list\" style=\"width: 100%; text-align: left;\" style=\"font-size: 1rem; color: gray;\"></i>" +
                                    "</a>" +
                                    "<ul class=\"dropdown-menu\" aria-labelledby=\"navbarDropdown\">" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"staffUpdate-ajax-modal\" data-bs-url=\"/Home/EditStaff?StaffId={aRow.Id}\" style=\"width: 100%; text-align: left;\" title=\"Rediger bruger\">" +
                                                "<i class=\"bi bi-person-vcard\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Rediger</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"staffUpdate-ajax-modal\" data-bs-url=\"/Home/DeleteStaff?StaffId={aRow.Id}\" style=\"width: 100%; text-align: left;\" title=\"Slet bruger\">" +
                                                "<i class=\"bi bi-person-dash\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Slet</i>" +
                                            "</button>" +
                                        "</li>" +
                                    "</ul>" +
                                "</li>" +
                            "</ul>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"8\">" +
                            "<div>";
                if (staffItems.Count > 10)
                {
                    html += $"<i>10 af {staffItems.Count}</i>";
                }
                else
                {
                    html += $"<i>{staffItems.Count} af {staffItems.Count}</i>";
                }

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"8\">" +
                            "<center>Ingen brugere fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        
        #endregion

        #region Planner
        public IActionResult Planner()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Set Data
            model.HostName = $"{_configuration["URL:HostName"]}";
            model.UserDetails = UserDetails;
            model.Data.aTable = new DataTable();
            model.Data.aTable.Columns.Add("Id");
            model.Data.aTable.Columns.Add("CreatedDate");
            model.Data.aTable.Columns.Add("GroupId");
            model.Data.aTable.Columns.Add("UiD");
            model.Data.aTable.Columns.Add("Name");
            model.Data.aTable.Columns.Add("Description");
            model.Data.aTable.Columns.Add("Activated");

            var subGroups = _context.SubGroups
                .Where(item => item.GroupId == UserDetails.GroupId)
                .OrderBy(item => item.Name)
                .ToList();

            foreach (var subGroup in subGroups)
            {
                model.Data.aTable.Rows.Add(subGroup.Id, subGroup.CreatedDate, subGroup.GroupId, subGroup.UiD, subGroup.Name, subGroup.Description, subGroup.Activated);
            }
            model.Messages = GetPendingMessages(UserDetails.UserId, "Planner");

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }

        [HttpGet]
        public IActionResult AddSubGroup()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return PartialView("_AddSubGroup", model);
            #endregion
        }
        [HttpPost]
        public IActionResult AddSubGroup(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            _context.SubGroups.Add(new SubGroup
            {
                GroupId = UserDetails.GroupId,
                UiD = Guid.NewGuid().ToString("N"),
                Name = iData.SubGroups.Name,
                Description = iData.SubGroups.Description,
                Activated = 0
            });
            _context.SaveChanges();

            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "Planner", "success", "Gruppen er oprettet");

            #endregion

            #region Return Data
            return PartialView("_AddSubGroup", iData);
            #endregion
        }

        [HttpGet]
        public IActionResult DeleteSubGroup(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var subGroup = _context.SubGroups.AsNoTracking().First(item => item.Id == SubGroupId);

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.SubGroups.SubGroupId = SubGroupId;
            model.SubGroups.Name = subGroup.Name;
            model.SubGroups.Description = subGroup.Description ?? string.Empty;

            #endregion

            #region Return Data
            return PartialView("_DeleteSubGroup", model);
            #endregion
        }
        [HttpPost]
        public IActionResult DeleteSubGroup(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            var subGroup = _context.SubGroups.FirstOrDefault(item => item.GroupId == UserDetails.GroupId && item.Id == iData.SubGroups.SubGroupId);
            if (subGroup != null)
            {
                _context.SubGroupDays.RemoveRange(_context.SubGroupDays.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupForms.RemoveRange(_context.SubGroupForms.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupKeys.RemoveRange(_context.SubGroupKeys.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupMessages.RemoveRange(_context.SubGroupMessages.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupNeeds.RemoveRange(_context.SubGroupNeeds.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupRegistrations.RemoveRange(_context.SubGroupRegistrations.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupShifts.RemoveRange(_context.SubGroupShifts.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroupStaff.RemoveRange(_context.SubGroupStaff.Where(item => item.SubGroupId == iData.SubGroups.SubGroupId));
                _context.SubGroups.Remove(subGroup);
                _context.SaveChanges();
            }

            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "Planner", "success", "Gruppen er slettet");

            #endregion

            #region Return Data
            return PartialView("_DeleteSubGroup", iData);
            #endregion
        }

        public JsonResult _SubGroupData_Planner(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            string form = string.Empty;
            string tValue = string.Empty;
            var defaultKeys = new Dictionary<string, string>
            {
                ["DaysCycle"] = "00:00",
                ["DaysNote"] = string.Empty,
                ["ShiftsNote"] = string.Empty,
                ["NeedsRuleWeekend"] = string.Empty,
                ["NeedsRuleHours"] = string.Empty,
                ["NeedsRuleKeys"] = string.Empty,
                ["NeedsNote"] = string.Empty
            };

            #endregion

            #region Get Data
            var subGroup = _context.SubGroups.AsNoTracking()
                .FirstOrDefault(item => item.Id == SubGroupId && item.GroupId == UserDetails.GroupId);
            var subGroupKeys = _context.SubGroupKeys.AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .ToList();
            var subGroupForm = _context.SubGroupForms.AsNoTracking()
                .FirstOrDefault(item => item.SubGroupId == SubGroupId);

            #endregion

            #region Set Data
            if (subGroup != null)
            {
                var outputValues = new Dictionary<string, string>
                {
                    ["Activated"] = subGroup.Activated.ToString(),
                    ["UiD"] = subGroup.UiD
                };

                foreach (var key in subGroupKeys)
                {
                    outputValues[key.Key] = key.Value;
                }

                foreach (var defaultKey in defaultKeys)
                {
                    if (!outputValues.ContainsKey(defaultKey.Key))
                    {
                        outputValues[defaultKey.Key] = defaultKey.Value;
                    }
                }

                foreach (var outputValue in outputValues.OrderBy(item => item.Key))
                {
                    switch (outputValue.Key)
                    {
                        case "NeedsRuleKeys":
                            html += $"<div style=\"display: none;\" id=\"SubGroup_{outputValue.Key}\">{outputValue.Value}</div>";
                            break;

                        default:
                            html += $"<input type=\"hidden\" id=\"SubGroup_{outputValue.Key}\" value=\"{outputValue.Value}\" />";
                            break;

                    }
                }
            }
            #endregion

            #region Set From
            if (subGroupForm != null)
            {
                /// Generate From with data
                form = $"<div id=\"SubGroup_Form\" style=\"display: none;\">{code.Base64Decode(subGroupForm.Base64FormRaw)}</div>";

                List<string> formData = JsonSerializer.Deserialize<List<string>>(code.Base64Decode(subGroupForm.Base64FormData ?? string.Empty))!;
                MatchCollection formMatch = Regex.Matches(code.Base64Decode(subGroupForm.Base64FormRaw), @"(<textarea.*?>)(?:.*?)?(<\/textarea>)");

                int textareaCount = 0;
                foreach (String item in formData)
                {
                    if (Regex.IsMatch(item, @"^(textarea|link)$") == true)
                    {
                        tValue = item;
                    }
                    else
                    {
                        switch (tValue)
                        {
                            case "textarea":
                                if ((textareaCount + 1) <= formMatch.Count)
                                {
                                    form = Regex.Replace(form, $"{formMatch[textareaCount].Groups[0].Value.Replace("/", "\\/")}", $"{formMatch[textareaCount].Groups[1].Value}{item}{formMatch[textareaCount].Groups[2].Value}");
                                }
                                textareaCount++;
                                break;
                            case "link":
                                //
                                break;
                        }
                    }
                }

                /// Add Form to dataset
                html += form;
            }
            else
            {
                html += "<div id=\"SubGroup_Form\" style=\"display: none;\"></div>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        /// Dage (Days)
        public IActionResult SubDaysAdd()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            #endregion

            #region Return Data
            return PartialView("_SubDaysAdd", model);
            #endregion
        }
        public IActionResult SubDaysEdit(int DaysId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            SubGroupDay? day = _context.SubGroupDays
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == DaysId && _context.SubGroups.Any(group => group.Id == item.SubGroupId && group.GroupId == UserDetails.GroupId));

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            if (day != null)
            {
                model.SubDays.DaysId = day.Id;
                model.SubDays.Key1 = day.Key1 ?? string.Empty;
            }

            #endregion

            #region Return Data
            return PartialView("_SubDaysEdit", model);
            #endregion
        }
        
        public JsonResult _SubDaysUpdate(int SubGroupId, string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            List<SubGroupDay> dayItems = new();

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                dayItems = GetSubGroupDayItems(SubGroupId, FilterValue);
            }
            #endregion

            #region Generate Html
            if (dayItems.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Dato" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Ugenr" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Ugedag" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                for (int index = 0; index < dayItems.Count && index < 10; index++)
                {
                    SubGroupDay day = dayItems[index];

                    html += $"<tr class=\"light\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{index + 1}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            day.Day.ToString("yyyy-MM-dd") +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{GetDanishWeekNumber(day.Day)}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{GetDanishWeekDay(day.Day)}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{day.Key1}" +
                        "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            "<ul class=\"navbar-nav flex-grow-1\">" +
                                "<li class=\"nav-item dropdown\">" +
                                    "<a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"navbarDropdown\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">" +
                                        "<i class=\"bi bi-list\" style=\"width: 100%; text-align: left;\" style=\"font-size: 1rem; color: gray;\"></i>" +
                                    "</a>" +
                                    "<ul class=\"dropdown-menu\" aria-labelledby=\"navbarDropdown\">" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton Activated LederAct\" data-bs-toggle=\"daysUpdate-ajax-modal\" data-bs-url=\"/Home/SubDaysEdit?DaysId={day.Id}\" style=\"width: 100%; text-align: left;\" title=\"Rediger dag\">" +
                                                "<i class=\"bi bi-pencil\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Rediger</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton Activated\" onclick=\"daysRemove({SubGroupId},{day.Id})\" style=\"width: 100%; text-align: left;\" title=\"Slet dag\">" +
                                                "<i class=\"bi bi-trash2\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Slet</i>" +
                                            "</button>" +
                                        "</li>" +
                                    "</ul>" +
                                "</li>" +
                            "</ul>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"6\">" +
                            "<div>";
                if (dayItems.Count > 10)
                {
                    html += $"<i>10 af {dayItems.Count}</i>";
                }
                else
                {
                    html += $"<i>{dayItems.Count} af {dayItems.Count}</i>";
                }

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Dato" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Ugenr" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Ugedag" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"6\">" +
                            "<center>Ingen dage fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubDaysAdd(int SubGroupId, DateTime DayStart, DateTime DayEnd, string Key1)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                var newDays = new List<SubGroupDay>();

                while (DayStart <= DayEnd)
                {
                    newDays.Add(new SubGroupDay
                    {
                        SubGroupId = SubGroupId,
                        Day = DayStart.Date,
                        Key1 = Key1
                    });

                    DayStart = DayStart.AddDays(1);
                }

                if (newDays.Count > 0)
                {
                    _context.SubGroupDays.AddRange(newDays);
                    _context.SaveChanges();
                }
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubDaysAddValidate(int SubGroupId, DateTime DayStart, DateTime DayEnd)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            bool hasConflicts = false;

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                hasConflicts = _context.SubGroupDays.Any(day =>
                    day.SubGroupId == SubGroupId &&
                    day.Day >= DayStart.Date &&
                    day.Day <= DayEnd.Date);
            }

            #endregion

            #region Validate Data
            if (!hasConflicts)
            {
                html = "OK";
            }
            else
            {
                html = "Fejl";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubDaysEdit(int DayId, string Key1)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            SubGroupDay? day = _context.SubGroupDays
                .FirstOrDefault(item => item.Id == DayId && _context.SubGroups.Any(group => group.Id == item.SubGroupId && group.GroupId == UserDetails.GroupId));

            if (day != null)
            {
                day.Key1 = Key1;
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubDaysRemove(int SubGroupId, int DaysId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            SubGroupDay? day = _context.SubGroupDays
                .FirstOrDefault(item => item.Id == DaysId && item.SubGroupId == SubGroupId && _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId));

            if (day != null)
            {
                List<int> shiftIds = _context.SubGroupShifts
                    .Where(shift => shift.DayId == DaysId)
                    .Select(shift => shift.Id)
                    .ToList();

                if (shiftIds.Count > 0)
                {
                    _context.SubGroupNeeds.RemoveRange(_context.SubGroupNeeds.Where(need => need.SubGroupId == SubGroupId && shiftIds.Contains(need.ShiftId)));
                }

                _context.SubGroupShifts.RemoveRange(_context.SubGroupShifts.Where(shift => shift.DayId == DaysId));
                _context.SubGroupDays.Remove(day);
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _DaysCycleUpdate(string DaysCycleStart)
        {
            #region Init. Config
            string LoDb = $"{_configuration["ConnectionStrings:LoDb"]}";

            #endregion

            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            Match time = Regex.Match(DaysCycleStart, @"(\d{2}):(\d{2})");
            #endregion

            #region Set Data
            html = (new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, Convert.ToInt32($"{time.Groups[1].Value}"), Convert.ToInt32($"{time.Groups[2].Value}"),0)).AddMinutes(-1).ToString("HH:mm");
            
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _DaysCycleCleenUp(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            #endregion

            #region Set Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                _context.SubGroupNeeds.RemoveRange(_context.SubGroupNeeds.Where(need => need.SubGroupId == SubGroupId));
                _context.SubGroupShifts.RemoveRange(_context.SubGroupShifts.Where(shift => shift.SubGroupId == SubGroupId));
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        /// Vagter (Shifts)
        public IActionResult SubShiftsAdd()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            #endregion

            #region Return Data
            return PartialView("_SubShiftsAdd", model);
            #endregion
        }
        public IActionResult SubShiftsEdit(int ShiftsId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var shift = _context.SubGroupShifts
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == ShiftsId && _context.SubGroups.Any(group => group.Id == item.SubGroupId && group.GroupId == UserDetails.GroupId));
            
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            if (shift != null)
            {
                SetShiftModel(model, shift);
            }

            #endregion

            #region Return Data
            return PartialView("_SubShiftsEdit", model);
            #endregion
        }
        public IActionResult SubShiftsView()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return PartialView("_SubShiftsView", model);
            #endregion
        }

        public JsonResult _SubShiftsUpdate(int SubGroupId, string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            List<SubGroupShift> shiftItems = new();

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                shiftItems = GetSubGroupShiftItems(SubGroupId, FilterValue);
            }
            #endregion

            #region Generate Html
            if (shiftItems.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Dato" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Type" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                for (int index = 0; index < shiftItems.Count && index < 10; index++)
                {
                    var shift = shiftItems[index];

                    html += $"<tr class=\"light\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{index + 1}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            shift.SubStartTime.ToString("yyyy-MM-dd") +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{shift.Name}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{shift.Type}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{shift.Key1}" +
                        "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            "<ul class=\"navbar-nav flex-grow-1\">" +
                                "<li class=\"nav-item dropdown\">" +
                                    "<a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"navbarDropdown\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">" +
                                        "<i class=\"bi bi-list\" style=\"width: 100%; text-align: left;\" style=\"font-size: 1rem; color: gray;\"></i>" +
                                    "</a>" +
                                    "<ul class=\"dropdown-menu\" aria-labelledby=\"navbarDropdown\">" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton Activated LederAct\" data-bs-toggle=\"shiftsUpdate-ajax-modal\" data-bs-url=\"/Home/SubShiftsEdit?ShiftsId={shift.Id}\" style=\"width: 100%; text-align: left;\" title=\"Rediger vagt\">" +
                                                "<i class=\"bi bi-pencil\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Rediger</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton Activated\" onclick=\"shiftsRemove({SubGroupId},{shift.Id})\" style=\"width: 100%; text-align: left;\" title=\"Slet vagt\">" +
                                                "<i class=\"bi bi-trash2\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Slet</i>" +
                                            "</button>" +
                                        "</li>" +
                                    "</ul>" +
                                "</li>" +
                            "</ul>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"6\">" +
                            "<div>";
                if (shiftItems.Count > 10)
                {
                    html += $"<i>10 af {shiftItems.Count}</i>";
                }
                else
                {
                    html += $"<i>{shiftItems.Count} af {shiftItems.Count}</i>";
                }

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Dato" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Type" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"6\">" +
                            "<center>Ingen dage fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsAdd(int SubGroupId, int SubDayId, string SubName, string SubType, DateTime SubStartTime, DateTime SubEndTime, int SubStaffNeeds, string Key)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId) &&
                _context.SubGroupDays.Any(day => day.Id == SubDayId && day.SubGroupId == SubGroupId))
            {
                _context.SubGroupShifts.Add(new SubGroupShift
                {
                    SubGroupId = SubGroupId,
                    DayId = SubDayId,
                    Name = SubName,
                    Type = SubType,
                    SubStartTime = SubStartTime,
                    SubEndTime = SubEndTime,
                    StaffNeeds = SubStaffNeeds,
                    Key1 = Key
                });
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsEdit(int SubGroupId, int SubDayId, int SubShiftId, string SubName, string SubType, DateTime SubStartTime, DateTime SubEndTime, int SubStaffNeeds, string Key)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Det Data
            var shift = _context.SubGroupShifts
                .FirstOrDefault(item => item.Id == SubShiftId && item.SubGroupId == SubGroupId && _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId));

            #endregion

            #region Set Data
            if (shift != null)
            {
                if (SubType == "Fixed" || (SubType == "Flex" && (shift.SubStartTime != SubStartTime || shift.SubEndTime != SubEndTime)))
                {
                    _context.SubGroupNeeds.RemoveRange(_context.SubGroupNeeds.Where(need => need.SubGroupId == SubGroupId && need.ShiftId == SubShiftId));
                }

                shift.DayId = SubDayId;
                shift.Name = SubName;
                shift.Type = SubType;
                shift.SubStartTime = SubStartTime;
                shift.SubEndTime = SubEndTime;
                shift.StaffNeeds = SubStaffNeeds;
                shift.Key1 = Key;
                _context.SaveChanges();

            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsRemove(int SubGroupId, int ShiftsId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            var shift = _context.SubGroupShifts
                .FirstOrDefault(item => item.Id == ShiftsId && item.SubGroupId == SubGroupId && _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId));

            if (shift != null)
            {
                _context.SubGroupNeeds.RemoveRange(_context.SubGroupNeeds.Where(need => need.SubGroupId == SubGroupId && need.ShiftId == ShiftsId));
                _context.SubGroupShifts.Remove(shift);
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsDaysUpdate(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            List<SubGroupDay> dayItems = new();

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                dayItems = GetSubGroupDayItems(SubGroupId);
            }

            #endregion

            #region Generate Html
            html = "<option></option>";
            foreach (var day in dayItems)
            {
                html += $"<option value=\"{day.Id}\">{day.Day:yyyy-MM-dd} ({GetDanishWeekNumber(day.Day)}) ({GetDanishWeekDay(day.Day)})</option>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsTimeUpdate(int SubGroupId, int SubDayId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            DateTime startTime = DateTime.Now;
            DateTime endTime = DateTime.Now;

            #endregion

            #region Get Data
            var timeWindow = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId)
                ? GetShiftDayWindow(SubGroupId, SubDayId)
                : null;

            if (timeWindow != null)
            {
                startTime = timeWindow.Value.StartTime;
                endTime = timeWindow.Value.EndTime;
            }

            #endregion

            #region Generate Html
            if (timeWindow != null)
            {
                html = $"<input type=\"hidden\" value=\"{startTime:yyyy-MM-dd HH:mm}\" id=\"StartTimeMin\" /><input type=\"hidden\" value=\"{endTime:yyyy-MM-dd HH:mm}\" id=\"EndTimeMax\" />";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsDaysCheck(int SubGroupId, int SubDayId, DateTime SubStartTime, DateTime SubEndTime)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";
            DateTime startTime = DateTime.Now;
            DateTime endTime = DateTime.Now;

            #endregion

            #region Get Data
            var timeWindow = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId)
                ? GetShiftDayWindow(SubGroupId, SubDayId)
                : null;

            if (timeWindow == null)
            {
                return Json("Fejl: Dag er ikke valid!");
            }

            startTime = timeWindow.Value.StartTime;
            endTime = timeWindow.Value.EndTime;
            #endregion

            #region Validate Dates
            if (SubStartTime < startTime || SubStartTime >= SubEndTime)
            {
                html = "Fejl: StartTime er ikke valid!";
            }
            if (SubEndTime > endTime || SubEndTime <= startTime)
            {
                html = "Fejl: EndTime er ikke valid!";
            }
            if (SubStartTime >= SubEndTime)
            {
                html = "Fejl: Start & EndTime er ikke valid!";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewGetDays(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string jsonObj = string.Empty;
            var dateItems = new List<DateTime>();

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                dateItems = _context.SubGroupShifts
                    .Where(shift => shift.SubGroupId == SubGroupId)
                    .Select(shift => shift.SubStartTime.Date)
                    .Distinct()
                    .OrderBy(date => date)
                    .ToList();
            }

            #endregion

            #region Generate Json
            jsonObj = "[";

            foreach (var date in dateItems)
            {
                if (dateItems.IndexOf(date) == 0)
                {
                    jsonObj += "{\"WeekDay\": \"" + GetDanishWeekDay(date) + "\", \"Date\": \"" + date.ToString("yyyy-MM-dd") + "\"}";
                }
                else
                {
                    jsonObj += ",{\"WeekDay\": \"" + GetDanishWeekDay(date) + "\", \"Date\": \"" + date.ToString("yyyy-MM-dd") + "\"}";
                }
            }

            jsonObj += "]";
            #endregion

            #region Return Data
            return Json(jsonObj);
            #endregion
        }
        public JsonResult _SubShiftsViewGetDetails(int SubGroupId, string Date)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string jsonObj = string.Empty;
            string jsonLables = string.Empty;
            string jsonValues = string.Empty;
            var chartRows = new List<(DateTime TimeSlot, int Needs)>();

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId) && DateTime.TryParse(Date, out var selectedDate))
            {
                var shifts = _context.SubGroupShifts
                    .Where(shift => shift.SubGroupId == SubGroupId && shift.SubStartTime.Date == selectedDate.Date)
                    .OrderBy(shift => shift.SubStartTime)
                    .ToList();

                if (shifts.Count > 0)
                {
                    var minTime = shifts.Min(shift => shift.SubStartTime);
                    var maxTime = shifts.Max(shift => shift.SubEndTime);
                    var timeSlot = minTime;

                    while (timeSlot < maxTime)
                    {
                        var needs = shifts
                            .Where(shift => shift.SubStartTime <= timeSlot && shift.SubEndTime > timeSlot)
                            .Sum(shift => shift.StaffNeeds ?? 0);

                        chartRows.Add((timeSlot, needs));
                        timeSlot = timeSlot.AddHours(1);
                    }
                }
            }

            #endregion

            #region Generate Json
            foreach (var row in chartRows)
            {
                if (String.IsNullOrEmpty(jsonLables) == true)
                {
                    jsonLables = $"\"{row.TimeSlot:HH:mm}\"";
                    jsonValues = $"{row.Needs}";
                }
                else
                {
                    jsonLables += $",\"{row.TimeSlot:HH:mm}\"";
                    jsonValues += $",{row.Needs}";
                }
            }

            jsonObj = "{\"labels\": [" + jsonLables + "], \"data\": [" + jsonValues +"]}";
            #endregion

            #region Return Data
            return Json(jsonObj);
            #endregion
        }

        /// Behov (Needs)
        public IActionResult SubNeedsEdit(int ShiftsId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string json = string.Empty;

            #endregion

            #region Get Data
            var shift = _context.SubGroupShifts
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == ShiftsId && _context.SubGroups.Any(group => group.Id == item.SubGroupId && group.GroupId == UserDetails.GroupId));

            #endregion

            #region Generate Json Data
            if (shift != null)
            {
                var needs = _context.SubGroupNeeds
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == shift.SubGroupId && item.ShiftId == ShiftsId)
                    .OrderBy(item => item.Time)
                    .ToList();

                json = BuildNeedsChartJson(shift, needs);
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            if (shift != null)
            {
                SetShiftModel(model, shift);
            }
            model.SubNeeds.jsonNeeds = json;

            #endregion

            #region Return Data
            return PartialView("_SubNeedsEdit", model);
            #endregion
        }

        public JsonResult _SubNeedsUpdate(int SubGroupId, string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            List<SubGroupShift> shiftItems = new();
            Dictionary<int, int> needCounts = new();

            #endregion

            #region Get Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                shiftItems = GetSubGroupShiftItems(SubGroupId, FilterValue, "Flex");
                needCounts = _context.SubGroupNeeds
                    .Where(item => item.SubGroupId == SubGroupId)
                    .GroupBy(item => item.ShiftId)
                    .ToDictionary(group => group.Key, group => group.Sum(item => item.StaffCount));
            }
            #endregion

            #region Generate Html
            if (shiftItems.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Dato" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Timer" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                for (int index = 0; index < shiftItems.Count && index < 10; index++)
                {
                    var shift = shiftItems[index];

                    html += $"<tr class=\"light\" title=\"Flex vagt\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{index + 1}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            shift.SubStartTime.ToString("yyyy-MM-dd") +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{shift.Name}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{(needCounts.TryGetValue(shift.Id, out var count) ? count : 0)}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{shift.Key1}" +
                        "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            $"<button type=\"button\" class=\"btn lightButton Activated LederAct\" data-bs-toggle=\"needsUpdate-ajax-modal\" data-bs-url=\"/Home/SubNeedsEdit?ShiftsId={shift.Id}\" title=\"Rediger behov\">" +
                                "<i class=\"bi bi-pencil\" style=\"font-size: 1rem; color: gray;\"></i>" +
                            "</button>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"6\">" +
                            "<div>";
                if (shiftItems.Count > 10)
                {
                    html += $"<i>10 af {shiftItems.Count}</i>";
                }
                else
                {
                    html += $"<i>{shiftItems.Count} af {shiftItems.Count}</i>";
                }

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Dato" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Timer" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"6\">" +
                            "<center>Ingen dage fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubNeedsSave(int SubGroupId, int SubShiftId, string JsonData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            var shift = _context.SubGroupShifts
                .FirstOrDefault(item => item.Id == SubShiftId && item.SubGroupId == SubGroupId && _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId));

            #endregion

            #region Convert Json to Model
            SubGroupNeeds.TimeModel timeModel = JsonSerializer.Deserialize<SubGroupNeeds.TimeModel>(JsonData)!;

            #endregion

            #region CleenUp
            if (shift != null)
            {
                _context.SubGroupNeeds.RemoveRange(_context.SubGroupNeeds.Where(item => item.SubGroupId == SubGroupId && item.ShiftId == SubShiftId));

                var newNeeds = new List<SubGroupNeed>();
                var startTime = shift.SubStartTime;
                var counter = 0;

                while (startTime < shift.SubEndTime && counter < timeModel.data.Count)
                {
                    newNeeds.Add(new SubGroupNeed
                    {
                        SubGroupId = SubGroupId,
                        ShiftId = SubShiftId,
                        Time = startTime,
                        StaffCount = timeModel.data[counter]
                    });

                    counter++;
                    startTime = startTime.AddHours(1);
                }

                if (newNeeds.Count > 0)
                {
                    _context.SubGroupNeeds.AddRange(newNeeds);
                }

                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubNeedsRuleKeysSave(int SubGroupId, string Keys)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Set Data
            if (_context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId))
            {
                var subGroupKey = _context.SubGroupKeys.FirstOrDefault(item => item.SubGroupId == SubGroupId && item.Key == "NeedsRuleKeys");
                if (subGroupKey == null)
                {
                    _context.SubGroupKeys.Add(new SubGroupKey
                    {
                        SubGroupId = SubGroupId,
                        Key = "NeedsRuleKeys",
                        Value = Keys
                    });
                }
                else
                {
                    subGroupKey.Value = Keys;
                }

                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        /// Brugere (Staff)
        [HttpGet]
        public IActionResult SubStaffAdd()
        {
            #region Init. Config
            string LoDb = $"{_configuration["ConnectionStrings:LoDb"]}";

            #endregion

            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();

            #endregion

            #region Return Data
            return PartialView("_SubStaffAdd", model);
            #endregion
        }
        public IActionResult SubStaffDetails(int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            EntityStaff? staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(entity => entity.Id == StaffId && entity.GroupId == UserDetails.GroupId);

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            if (staff != null)
            {
                model.Staff = ToStaffModel(staff);
            }

            #endregion

            #region Return Data
            return PartialView("_SubStaffDetails", model);
            #endregion
        }
        
        public JsonResult _SubStaffUpdate(int SubGroupId, string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            List<EntityStaff> staffItems = new();
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                IQueryable<int> subGroupStaffIds = _context.SubGroupStaff
                    .Where(link => link.SubGroupId == SubGroupId)
                    .Select(link => link.StaffId);

                staffItems = GetStaffQuery(UserDetails.GroupId, FilterValue)
                    .Where(staff => subGroupStaffIds.Contains(staff.Id))
                    .ToList();
            }
            #endregion

            #region Generate Html
            if (staffItems.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                for (int index = 0; index < staffItems.Count && index < 10; index++)
                {
                    EntityStaff staff = staffItems[index];

                    html += $"<tr class=\"light\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{index + 1}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Name}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Mail}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Phone}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Key1}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Key2}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Key3}" +
                        "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            "<ul class=\"navbar-nav flex-grow-1\">" +
                                "<li class=\"nav-item dropdown\">" +
                                    "<a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"navbarDropdown\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">" +
                                        "<i class=\"bi bi-list\" style=\"width: 100%; text-align: left;\" style=\"font-size: 1rem; color: gray;\"></i>" +
                                    "</a>" +
                                    "<ul class=\"dropdown-menu\" aria-labelledby=\"navbarDropdown\">" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"staffUpdate-ajax-modal\" data-bs-url=\"/Home/SubStaffDetails?StaffId={staff.Id}\" style=\"width: 100%; text-align: left;\" title=\"Bruger detaljer\">" +
                                                "<i class=\"bi bi-person-vcard\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Bruger</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton Activated\" onclick=\"staffRemove({SubGroupId},{staff.Id})\" style=\"width: 100%; text-align: left;\" title=\"Fjern bruger\">" +
                                                "<i class=\"bi bi-person-dash\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Fjern</i>" +
                                            "</button>" +
                                        "</li>" +
                                    "</ul>" +
                                "</li>" +
                            "</ul>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"8\">" +
                            "<div>";
                if (staffItems.Count > 10)
                {
                    html += $"<i>10 af {staffItems.Count}</i>";
                }
                else
                {
                    html += $"<i>{staffItems.Count} af {staffItems.Count}</i>";
                }
                                
                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>"+
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"8\">" +
                            "<center>Ingen brugere fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubStaffSearch(int SubGroupId, string SearchValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            string UiD = string.Empty;

            #endregion

            #region Get Data
            List<EntityStaff> staffItems = new();
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                staffItems = GetStaffQuery(UserDetails.GroupId, SearchValue).ToList();
            }

            #endregion

            #region Generate Html
            if (staffItems.Count > 0)
            {
                foreach (EntityStaff staff in staffItems)
                {
                    UiD = Guid.NewGuid().ToString().Replace("-", "");

                    html += $"<tr id=\"row_{UiD}\">" +
                        "<td>" +
                            "<table width=\"100%\">" +
                                "<tr>" +
                                    "<td align=\"left\" valign=\"middle\">" +
                                        $"{staff.Name}" +
                                    "</td>" +
                                    "<td align=\"right\" valign=\"middle\">" +
                                        $"<input type=\"hidden\" id=\"staffId_{UiD}\" value=\"{staff.Id}\">" +
                                        $"<button type=\"button\" class=\"btn lightButton\" onclick=\"staffAdd('{UiD}')\" title=\"Tilføj bruger\">" +
                                            "<i class=\"bi bi-plus-circle\" style=\"font-size: 1.2rem; color: gray;\"></i>" +
                                        "</button>" +
                                    "</td>" +
                                "</tr>" +
                            "</table>" +
                        "</td>" +
                    "</tr>";
                }
            }
            else
            {
                html = "<tr><td><center><i>Ingen fundet</i></center></td></tr>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubStaffAdd(int SubGroupId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);
            bool staffBelongsToGroup = _context.Staff.Any(staff => staff.Id == StaffId && staff.GroupId == UserDetails.GroupId);
            bool alreadyExists = _context.SubGroupStaff.Any(link => link.SubGroupId == SubGroupId && link.StaffId == StaffId);

            if (ownsSubGroup && staffBelongsToGroup && !alreadyExists)
            {
                _context.SubGroupStaff.Add(new SubGroupStaff
                {
                    SubGroupId = SubGroupId,
                    StaffId = StaffId
                });

                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubStaffAddBulk(int SubGroupId, string SearchValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                IQueryable<EntityStaff> query = _context.Staff
                    .Where(staff => staff.GroupId == UserDetails.GroupId);

                if (!string.IsNullOrWhiteSpace(SearchValue))
                {
                    query = query.Where(staff =>
                        (staff.Name ?? string.Empty).Contains(SearchValue) ||
                        (staff.Key1 ?? string.Empty).Contains(SearchValue) ||
                        (staff.Key2 ?? string.Empty).Contains(SearchValue) ||
                        (staff.Key3 ?? string.Empty).Contains(SearchValue));
                }

                List<int> existingIds = _context.SubGroupStaff
                    .Where(link => link.SubGroupId == SubGroupId)
                    .Select(link => link.StaffId)
                    .ToList();

                List<SubGroupStaff> newLinks = query
                    .Where(staff => !existingIds.Contains(staff.Id))
                    .Select(staff => new SubGroupStaff
                    {
                        SubGroupId = SubGroupId,
                        StaffId = staff.Id
                    })
                    .ToList();

                if (newLinks.Count > 0)
                {
                    _context.SubGroupStaff.AddRange(newLinks);
                    _context.SaveChanges();
                }
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubStaffAddAll(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                List<int> existingIds = _context.SubGroupStaff
                    .Where(link => link.SubGroupId == SubGroupId)
                    .Select(link => link.StaffId)
                    .ToList();

                List<SubGroupStaff> newLinks = _context.Staff
                    .Where(staff => staff.GroupId == UserDetails.GroupId && !existingIds.Contains(staff.Id))
                    .Select(staff => new SubGroupStaff
                    {
                        SubGroupId = SubGroupId,
                        StaffId = staff.Id
                    })
                    .ToList();

                if (newLinks.Count > 0)
                {
                    _context.SubGroupStaff.AddRange(newLinks);
                    _context.SaveChanges();
                }
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubStaffRemove(int SubGroupId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                List<SubGroupStaff> links = _context.SubGroupStaff
                    .Where(link => link.SubGroupId == SubGroupId && link.StaffId == StaffId)
                    .ToList();

                if (links.Count > 0)
                {
                    _context.SubGroupStaff.RemoveRange(links);
                    _context.SaveChanges();
                }
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        /// Tilmelding (From)
        public IActionResult SubFormElements()
        {
            #region Return Data
            return PartialView("_SubFormElements", model);
            #endregion
        }
        [HttpGet]
        public IActionResult SubFormActivate(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var subGroup = _context.SubGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == SubGroupId);
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            if (subGroup != null)
            {
                model.SubGroups.SubGroupId = subGroup.Id;
                model.SubGroups.Name = subGroup.Name;
            }
            #endregion

            #region Return Data
            return PartialView("_SubFormActivate", model);
            #endregion
        }
        [HttpPost]
        public IActionResult SubFormActivate(HomeModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            Boolean validation = true;
            String HtmlForm = String.Empty;
            String FormTemplate = String.Empty;
            String UserTemplate = String.Empty;
            String UiD = String.Empty;
            int subGroupId = iData.SubGroups.SubGroupId;

            #endregion

            #region Validate OwnerShip
            var subGroup = _context.SubGroups.FirstOrDefault(item => item.GroupId == UserDetails.GroupId && item.Id == subGroupId);
            if (subGroup == null)
            {
                AddAppMessage(UserDetails.UserId, "Planner", "danger", "Gruppen er ikke ejert af dig!?");
                
                return PartialView("_SubFormActivate", iData);
            }
            #endregion

            #region Validate Days Data
            List<SubGroupDay> dayItems = _context.SubGroupDays
                .Where(item => item.SubGroupId == subGroupId)
                .ToList();

            if (dayItems.Count == 0)
            {
                AddAppMessage(UserDetails.UserId, "Planner", "danger", "Ingen dage er tilføjet!?");

                validation = false;
            }
            #endregion

            #region Validate Shifts Data
            List<SubGroupDay> daysWithoutShifts = dayItems
                .Where(day => !_context.SubGroupShifts.Any(shift => shift.DayId == day.Id))
                .OrderBy(day => day.Day)
                .ToList();

            foreach (SubGroupDay day in daysWithoutShifts)
            {
                AddAppMessage(UserDetails.UserId, "Planner", "danger", $"Vagt mangler på dag: {day.Day:yyyy-MM-dd}!?");

                validation = false;
            }
            #endregion

            #region Validate Needs Data
            List<int> shiftIdsWithNeeds = _context.SubGroupNeeds
                .Where(item => item.SubGroupId == subGroupId)
                .Select(item => item.ShiftId)
                .Distinct()
                .ToList();

            List<SubGroupShift> flexShiftsWithoutNeeds = _context.SubGroupShifts
                .Where(shift => shift.SubGroupId == subGroupId && shift.Type == "Flex" && !shiftIdsWithNeeds.Contains(shift.Id))
                .OrderBy(shift => shift.SubStartTime)
                .ToList();

            foreach (SubGroupShift shift in flexShiftsWithoutNeeds)
            {
                AddAppMessage(UserDetails.UserId, "Planner", "danger", $"Behov mangler på dag: {shift.SubStartTime:yyyy-MM-dd}!?");

                validation = false;
            }
            #endregion

            #region Validate Staff Data
            bool hasStaffLinks = _context.SubGroupStaff.Any(item => item.SubGroupId == subGroupId);

            if (!hasStaffLinks)
            {
                AddAppMessage(UserDetails.UserId, "Planner", "danger", "Ingen brugere er tilføjet!?");

                validation = false;
            }
            #endregion

            #region Validate Form Data
            List<SubGroupForm> forms = _context.SubGroupForms
                .Where(item => item.SubGroupId == subGroupId)
                .ToList();

            if (forms.Count == 0)
            {
                AddAppMessage(UserDetails.UserId, "Planner", "danger", "Ingen tilmelding er lavet!?");

                validation = false;
            }
            else
            {
                foreach (SubGroupForm form in forms)
                {
                    HtmlForm = code.Base64Decode(form.Base64FormHtml ?? string.Empty);

                    if (Regex.IsMatch(HtmlForm, "href=\"") == false)
                    {
                        AddAppMessage(UserDetails.UserId, "Planner", "danger", "Ingen tilmeldingslink er tilføjet!?");

                        validation = false;
                    }
                }
            }
            #endregion

            #region Check Validation
            if (validation == false)
            {
                return PartialView("_SubFormActivate", iData);
            }
            #endregion

            #region Get Data
            SubGroupForm activeForm = forms.First();
            List<EntityStaff> subGroupStaff = (from link in _context.SubGroupStaff.AsNoTracking()
                                               join staff in _context.Staff.AsNoTracking() on link.StaffId equals staff.Id
                                               where link.SubGroupId == subGroupId
                                               select staff)
                .ToList();

            FormTemplate = code.Base64Decode(activeForm.Base64FormHtml ?? string.Empty);

            #endregion

            #region Send Mails
            foreach (EntityStaff staff in subGroupStaff)
            {
                #region Init. Loop Variables
                UiD = Guid.NewGuid().ToString().Replace("-", "");

                #endregion

                #region Custom Template
                // Copy Template
                UserTemplate = FormTemplate;

                // #navn
                UserTemplate = UserTemplate.Replace($"#navn", staff.Name ?? string.Empty);
                // #adresse
                UserTemplate = UserTemplate.Replace($"#adresse", staff.Address ?? string.Empty);
                // #postnr
                UserTemplate = UserTemplate.Replace($"#postnr", staff.ZipCode ?? string.Empty);
                // #by
                UserTemplate = UserTemplate.Replace($"#by", staff.City ?? string.Empty);
                // #fødselsdag
                UserTemplate = UserTemplate.Replace($"#foedselsdag", staff.Birthday?.ToString() ?? string.Empty);
                // #mail
                UserTemplate = UserTemplate.Replace($"#mail", staff.Mail ?? string.Empty);
                // #telefonnr
                UserTemplate = UserTemplate.Replace($"#telefonnr", staff.Phone ?? string.Empty);
                // #key1
                UserTemplate = UserTemplate.Replace($"#key1", staff.Key1 ?? string.Empty);
                // #key2
                UserTemplate = UserTemplate.Replace($"#key2", staff.Key2 ?? string.Empty);
                // #key3
                UserTemplate = UserTemplate.Replace($"#key3", staff.Key3 ?? string.Empty);
                // Set Link
                UserTemplate = UserTemplate.Replace(subGroup.UiD, $"{subGroup.UiD}&staff={staff.UiD}");
                
                #endregion

                #region Set Data
                if (Regex.IsMatch((staff.Mail ?? string.Empty).Replace(" ", ""), @"^.*@.*\..*$") == true)
                {
                    _context.SubGroupMessages.Add(new SubGroupMessage
                    {
                        UiD = UiD,
                        SubGroupId = subGroupId,
                        StaffId = staff.Id,
                        UserId = UserDetails.UserId,
                        Base64Sender = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                        Base64Reciver = code.Base64Encode($"[\"{staff.Mail}\"]"),
                        Base64Title = code.Base64Encode($"{subGroup.Name} - Tilmelding [{UiD}]"),
                        Base64Message = code.Base64Encode(UserTemplate),
                        iType = "Mail",
                        Sendt = 0,
                        InOut = 1,
                        State = 1
                    });
                }
                else
                {
                    _context.SubGroupMessages.Add(new SubGroupMessage
                    {
                        UiD = UiD,
                        SubGroupId = subGroupId,
                        StaffId = staff.Id,
                        UserId = UserDetails.UserId,
                        Base64Title = code.Base64Encode("Tilmelding: Mail ikke gyldig"),
                        Base64Message = code.Base64Encode($"Bruger: {staff.Name}{Environment.NewLine}Mail: {staff.Mail}{Environment.NewLine}{Environment.NewLine}Maile opfylder ikke krav om mail sammensætning!"),
                        iType = "Note",
                        InOut = 1,
                        State = 0
                    });
                }
                #endregion
            }
            #endregion

            #region Set Data
            subGroup.Activated = 1;
            _context.SaveChanges();
                        
            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "Planner", "success", "Gruppen er aktiveret og mails er sendt");

            #endregion

            #region Return Data
            return PartialView("_SubFormActivate", iData);
            #endregion
        }
        
        public JsonResult _SubFormSave(int SubGroupId, string SubGroupForm, string SubGroupData)
        {
            #region Init. Variables
            string html = "OK";
            string formHtml = string.Empty;
            string tValue = string.Empty;
            List<string> data = new List<string>();

            MatchCollection formMatch = Regex.Matches($"{SubGroupForm}", @"(<textarea.*?>)(?:.*?)?(<\/textarea>)");

            foreach (Match match in formMatch)
            {
                SubGroupForm = Regex.Replace(SubGroupForm, $"{match.Groups[0].Value}", $"{match.Groups[1].Value}{match.Groups[2].Value}");
            }

            if (String.IsNullOrEmpty(SubGroupData) == false)
            {
                data = JsonSerializer.Deserialize<List<string>>(SubGroupData)!;
            }

            if (String.IsNullOrEmpty(SubGroupForm) == true)
            {
                SubGroupForm = "";
            }

            #endregion

            #region Generate Form Html
            formHtml = "<html>" +
                    "<head>" +
                        "<div id=\"DeleteZone\">" +
                            "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN\" crossorigin=\"anonymous\">" +
                        "</div>" +
                    "</head>" +
                    "<body>" +
                        "<table width=\"100%\">";
            foreach (String item in data)
            {
                if (Regex.IsMatch(item, @"^(textarea|link)$") == true)
                {
                    tValue = item;
                }
                else
                {
                    switch (tValue)
                    {
                        case "textarea":
                            var text = item;
                            text = text.Replace("\r\n", "\r");
                            text = text.Replace("\n", "\r");
                            text = text.Replace("\r", "<br>");
                            text = text.Replace(" ", "&nbsp;");

                            formHtml += $"<tr><td>{text}</td></tr>";
                            break;
                        case "link":
                            formHtml += $"<tr><td height=\"15px\"></td></tr>" +
                                $"<tr><td>{Regex.Match(SubGroupForm, @"<a .*?" + $"{item}" + @".*?>.*?</a>").Groups[0].Value}</td></tr>" +
                                $"<tr><td height=\"15px\"></td></tr>";
                            break;
                    }
                }
            }

            formHtml += "</table>" +
                        "<div id=\"DeleteZone\">" +
                            "<script src=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js\" integrity=\"sha384-C6RzsynM9kWDrMNeT87bh95OGNyZPhcTNXj1NW7RuBCsyN/o0jlpcV8Qyq46cDfL\" crossorigin=\"anonymous\"></script>" +
                        "</div>" +
                    "</body>" +
                "</html>";
            #endregion

            #region Set Data
            try
            {
                SubGroupForm? subGroupForm = _context.SubGroupForms.FirstOrDefault(item => item.SubGroupId == SubGroupId);
                string encodedForm = code.Base64Encode(SubGroupForm);
                string encodedData = code.Base64Encode(SubGroupData ?? string.Empty);
                string encodedHtml = code.Base64Encode(formHtml);

                if (subGroupForm == null)
                {
                    _context.SubGroupForms.Add(new SubGroupForm
                    {
                        SubGroupId = SubGroupId,
                        Base64FormRaw = encodedForm,
                        Base64FormData = encodedData,
                        Base64FormHtml = encodedHtml
                    });
                }
                else
                {
                    subGroupForm.Base64FormRaw = encodedForm;
                    subGroupForm.Base64FormData = encodedData;
                    subGroupForm.Base64FormHtml = encodedHtml;
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                html = ex.Message;
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        #endregion

        #region Registration
        public IActionResult Registration()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Get Data
            Dictionary<int, int> pendingMessageCounts = _context.SubGroupMessages
                .AsNoTracking()
                .Where(message => message.State == 0)
                .GroupBy(message => message.SubGroupId)
                .ToDictionary(group => group.Key, group => group.Count());

            List<SubGroup> activeSubGroups = _context.SubGroups
                .AsNoTracking()
                .Where(group => group.GroupId == UserDetails.GroupId && group.Activated == 1)
                .OrderBy(group => group.Name)
                .ToList();

            DataTable aTable = CreateTable("Id", "CreatedDate", "GroupId", "UiD", "Name", "Description", "Activated", "Messages");
            foreach (SubGroup subGroup in activeSubGroups)
            {
                aTable.Rows.Add(
                    subGroup.Id,
                    subGroup.CreatedDate,
                    subGroup.GroupId,
                    subGroup.UiD,
                    subGroup.Name,
                    subGroup.Description,
                    subGroup.Activated,
                    pendingMessageCounts.TryGetValue(subGroup.Id, out var messageCount) ? messageCount : 0);
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Data.aTable = aTable;
            model.Messages = GetPendingMessages(UserDetails.UserId, "Registration");

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }

        public JsonResult _SubGroupData_Registration(int SubGroupId)
        {
            RegistrationModel localModel = new RegistrationModel();

            #region Get Data (Messages)
            localModel.messages.Counter = _context.SubGroupMessages.Count(message => message.SubGroupId == SubGroupId && message.State == 0);
            #endregion

            #region Set Data
            string html = JsonSerializer.Serialize(localModel);
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        /// Overblik (Overview)
        public JsonResult _SubOverviewViewUpdate(int SubGroupId)
        {
            String json = String.Empty;
            DateTime today = DateTime.Today;

            #region Get Data
            Dictionary<int, int> registrationCounts = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(registration => registration.SubGroupId == SubGroupId)
                .GroupBy(registration => registration.ShiftId)
                .ToDictionary(group => group.Key, group => group.Count());

            List<SubGroupShift> shifts = _context.SubGroupShifts
                .AsNoTracking()
                .Where(shift => shift.SubGroupId == SubGroupId)
                .OrderBy(shift => shift.SubStartTime)
                .ToList();

            DateTime startDate = today.AddDays(-13);
            Dictionary<DateTime, int> registrationsPerDay = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(registration => registration.SubGroupId == SubGroupId && registration.CreatedDate.Date >= startDate && registration.CreatedDate.Date <= today)
                .ToList()
                .GroupBy(registration => registration.CreatedDate.Date)
                .ToDictionary(group => group.Key, group => group.Select(item => item.StaffId).Distinct().Count());

            #endregion

            #region Generate Json
            var dayGroups = new List<object>();
            foreach (var group in shifts.GroupBy(shift => GetDanishWeekDay(shift.SubStartTime)))
            {
                dayGroups.Add(new
                {
                    WeekDay = group.Key,
                    Shifts = group.Select(shift => new
                    {
                        Name = shift.Name,
                        StartTime = shift.SubStartTime,
                        EndTime = shift.SubEndTime,
                        StaffNeeds = shift.StaffNeeds ?? 0,
                        Registrations = registrationCounts.TryGetValue(shift.Id, out var count) ? count : 0
                    }).ToList()
                });
            }

            var chartDates = Enumerable.Range(0, 14)
                .Select(offset => startDate.AddDays(offset))
                .ToList();

            json = JsonSerializer.Serialize(new
            {
                Days = dayGroups,
                Registrations = new
                {
                    labels = chartDates.Select(date => date.ToString("MMM dd")).ToList(),
                    data = chartDates.Select(date => registrationsPerDay.TryGetValue(date, out var count) ? count : 0).ToList()
                }
            });
            #endregion

            #region Return Data
            return Json(json);
            #endregion
        }

        /// Vagter (Shifts)
        [HttpGet]
        public IActionResult SubShiftsViewAdd()
        {
            #region Init. Config
            string LoDb = $"{_configuration["ConnectionStrings:LoDb"]}";

            #endregion

            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewAdd", model);
            #endregion
        }
        public IActionResult SubShiftsViewRegistrations()
        {
            #region Init. Config
            string LoDb = $"{_configuration["ConnectionStrings:LoDb"]}";

            #endregion

            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewRegistrations", model);
            #endregion
        }
        public IActionResult SubShiftsViewView()
        {
            #region Init. Config
            string LoDb = $"{_configuration["ConnectionStrings:LoDb"]}";

            #endregion

            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewView", model);
            #endregion
        }
        public IActionResult SubShiftsViewEdit(int SubGroupId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();
            String mail = String.Empty;
            #endregion

            #region Get Data
            Dictionary<int, int> registrationCounts = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .GroupBy(item => item.ShiftId)
                .ToDictionary(group => group.Key, group => group.Count());

            HashSet<int> selectedShiftIds = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId && item.StaffId == StaffId)
                .Select(item => item.ShiftId)
                .ToHashSet();

            List<SubGroupShift> shifts = _context.SubGroupShifts
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .OrderBy(item => item.SubStartTime)
                .ToList();

            EntityStaff? staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == StaffId);

            if (staff == null)
            {
                return NotFound();
            }

            data.aTable = CreateTable("ShiftId", "WeekDay", "Name", "Type", "SubStartTime", "SubEndTime", "StaffNeeds", "Key1", "Selected");
            foreach (SubGroupShift shift in shifts)
            {
                data.aTable.Rows.Add(
                    shift.Id,
                    GetDanishWeekDay(shift.SubStartTime),
                    shift.Name,
                    shift.Type,
                    shift.SubStartTime,
                    shift.SubEndTime,
                    (shift.StaffNeeds ?? 0) - (registrationCounts.TryGetValue(shift.Id, out var count) ? count : 0),
                    shift.Key1,
                    selectedShiftIds.Contains(shift.Id) ? 1 : 0);
            }

            #endregion

            #region Validate Mail
            if (Regex.IsMatch((staff.Mail ?? string.Empty).Replace(" ", ""), @"^.*@.*\..*$") == true)
            {
                mail = staff.Mail ?? string.Empty;
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Data = data;
            model.Staff.StaffId = StaffId;
            model.Staff.Mail = mail;

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewEdit", model);
            #endregion
        }
        public IActionResult SubShiftsViewSend(int SubGroupId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String mail = String.Empty;
            EntityStaff? staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == StaffId);

            if (staff == null)
            {
                return NotFound();
            }

            #endregion

            #region Validate Mail
            if (Regex.IsMatch((staff.Mail ?? string.Empty).Replace(" ", ""), @"^.*@.*\..*$") == true)
            {
                mail = staff.Mail ?? string.Empty;
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Staff.StaffId = StaffId;
            model.Staff.Mail = mail;

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewSend", model);
            #endregion
        }
        public IActionResult SubShiftsViewDetails(int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            EntityStaff? staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == StaffId);

            if (staff == null)
            {
                return NotFound();
            }

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Staff = ToStaffModel(staff);

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewDetails", model);
            #endregion
        }

        public JsonResult _SubShiftsViewUpdate(int SubGroupId, string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            Dictionary<int, int> registrationCounts = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .GroupBy(item => item.StaffId)
                .ToDictionary(group => group.Key, group => group.Count());

            List<int> subGroupStaffIds = _context.SubGroupStaff
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .Select(item => item.StaffId)
                .ToList();

            List<EntityStaff> staffItems = GetStaffQuery(UserDetails.GroupId, FilterValue)
                .Where(staff => subGroupStaffIds.Contains(staff.Id))
                .ToList();
            #endregion

            #region Generate Html
            if (staffItems.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                foreach (EntityStaff staff in staffItems.Take(10))
                {
                    html += $"<tr class=\"light\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{(staffItems.IndexOf(staff) + 1)}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Name}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Mail}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Phone}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Key1}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Key2}" +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            $"{staff.Key3}" +
                        "</td>" +
                        "<td valign=\"middle\">";
                    if (registrationCounts.TryGetValue(staff.Id, out var registrations) && registrations > 0)
                    {
                        html += "<i class=\"bi bi-check2-circle\" style=\"font-size: 1.2rem; color: green;\" title=\"Har registreret timer\"></i>";
                    }
                        html += "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            "<ul class=\"navbar-nav flex-grow-1\">" +
                                "<li class=\"nav-item dropdown\">" +
                                    "<a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"navbarDropdown\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">" +
                                        "<i class=\"bi bi-list\" style=\"width: 100%; text-align: left;\" style=\"font-size: 1rem; color: gray;\"></i>" +
                                    "</a>" +
                                    "<ul class=\"dropdown-menu\" aria-labelledby=\"navbarDropdown\">" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"staffUpdate-ajax-modal\" data-bs-url=\"/Home/SubShiftsViewEdit?SubGroupId={SubGroupId}&StaffId={staff.Id}\" style=\"width: 100%; text-align: left;\" title=\"Rediger vagter\">" +
                                                "<i class=\"bi bi-pencil\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Rediger</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"staffUpdate-ajax-modal\" data-bs-url=\"/Home/SubShiftsViewSend?SubGroupId={SubGroupId}&StaffId={staff.Id}\" style=\"width: 100%; text-align: left;\" title=\"Send tilmeldingsmail\">" +
                                                "<i class=\"bi bi-envelope-at\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Send</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"staffUpdate-ajax-modal\" data-bs-url=\"/Home/SubShiftsViewDetails?StaffId={staff.Id}\" style=\"width: 100%; text-align: left;\" title=\"Bruger detaljer\">" +
                                                "<i class=\"bi bi-person-vcard\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Bruger</i>" +
                                            "</button>" +
                                        "</li>" +
                                        $"<li>" +
                                            $"<button type=\"button\" class=\"btn lightButton\" onclick=\"staffRemove({SubGroupId},{staff.Id})\" style=\"width: 100%; text-align: left;\" title=\"Fjern bruger\">" +
                                                "<i class=\"bi bi-person-dash\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Fjern</i>" +
                                            "</button>" +
                                        "</li>" +
                                    "</ul>" +
                                "</li>" +
                            "</ul>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"9\">" +
                            "<div>";
                if (staffItems.Count > 10)
                {
                    html += $"<i>10 af {staffItems.Count}</i>";
                }
                else
                {
                    html += $"<i>{staffItems.Count} af {staffItems.Count}</i>";
                }

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"9\">" +
                            "<center>Ingen brugere fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewSearch(int SubGroupId, string SearchValue)
        {
            #region Init. Variables
            string html = string.Empty;
            string UiD = string.Empty;

            #endregion

            #region Get Data
            int? groupId = _context.SubGroups
                .AsNoTracking()
                .Where(item => item.Id == SubGroupId)
                .Select(item => (int?)item.GroupId)
                .FirstOrDefault();

            List<EntityStaff> matches = groupId == null
                ? new List<EntityStaff>()
                : GetStaffQuery(groupId.Value, SearchValue).ToList();

            #endregion

            #region Generate Html
            if (matches.Count > 0)
            {
                foreach (EntityStaff staff in matches)
                {
                    UiD = Guid.NewGuid().ToString().Replace("-", "");

                    html += $"<tr id=\"row_{UiD}\">" +
                        "<td>" +
                            "<table width=\"100%\">" +
                                "<tr>" +
                                    "<td align=\"left\" valign=\"middle\">" +
                                        $"{staff.Name}" +
                                    "</td>" +
                                    "<td align=\"right\" valign=\"middle\">" +
                                        $"<input type=\"hidden\" id=\"staffId_{UiD}\" value=\"{staff.Id}\">" +
                                        $"<button type=\"button\" class=\"btn lightButton\" onclick=\"staffAdd('{UiD}')\" title=\"Tilføj bruger\">" +
                                            "<i class=\"bi bi-plus-circle\" style=\"font-size: 1.2rem; color: gray;\"></i>" +
                                        "</button>" +
                                    "</td>" +
                                "</tr>" +
                            "</table>" +
                        "</td>" +
                    "</tr>";
                }
            }
            else
            {
                html = "<tr><td><center><i>Ingen fundet</i></center></td></tr>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewAdd(int SubGroupId, int StaffId)
        {
            #region Set Data
            _context.SubGroupStaff.Add(new SubGroupStaff
            {
                SubGroupId = SubGroupId,
                StaffId = StaffId
            });
            _context.SaveChanges();

            #endregion

            #region Return Data
            return Json(string.Empty);
            #endregion
        }
        public JsonResult _SubShiftsViewAddBulk(int SubGroupId, string SearchValue)
        {
            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            int? groupId = _context.SubGroups
                .AsNoTracking()
                .Where(item => item.Id == SubGroupId)
                .Select(item => (int?)item.GroupId)
                .FirstOrDefault();

            List<EntityStaff> matches = groupId == null
                ? new List<EntityStaff>()
                : GetStaffQuery(groupId.Value, SearchValue).ToList();

            #endregion

            #region Set Data
            foreach (EntityStaff staff in matches)
            {
                _context.SubGroupStaff.Add(new SubGroupStaff
                {
                    SubGroupId = SubGroupId,
                    StaffId = staff.Id
                });
            }

            if (matches.Count > 0)
            {
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewAddAll(int SubGroupId)
        {
            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            int? groupId = _context.SubGroups
                .AsNoTracking()
                .Where(item => item.Id == SubGroupId)
                .Select(item => (int?)item.GroupId)
                .FirstOrDefault();

            List<EntityStaff> matches = groupId == null
                ? new List<EntityStaff>()
                : _context.Staff
                    .AsNoTracking()
                    .Where(item => item.GroupId == groupId.Value)
                    .OrderBy(item => item.Name)
                    .ToList();

            #endregion

            #region Set Data
            foreach (EntityStaff staff in matches)
            {
                _context.SubGroupStaff.Add(new SubGroupStaff
                {
                    SubGroupId = SubGroupId,
                    StaffId = staff.Id
                });
            }

            if (matches.Count > 0)
            {
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewRemove(int SubGroupId, int StaffId)
        {
            #region Init. Variables
            string html = "OK";

            #endregion

            #region Set Data
            List<SubGroupMessage> messages = _context.SubGroupMessages
                .Where(item => item.SubGroupId == SubGroupId && item.StaffId == StaffId)
                .ToList();
            List<SubGroupRegistration> registrations = _context.SubGroupRegistrations
                .Where(item => item.SubGroupId == SubGroupId && item.StaffId == StaffId)
                .ToList();
            List<SubGroupStaff> staffLinks = _context.SubGroupStaff
                .Where(item => item.SubGroupId == SubGroupId && item.StaffId == StaffId)
                .ToList();

            if (messages.Count > 0)
            {
                _context.SubGroupMessages.RemoveRange(messages);
            }

            if (registrations.Count > 0)
            {
                _context.SubGroupRegistrations.RemoveRange(registrations);
            }

            if (staffLinks.Count > 0)
            {
                _context.SubGroupStaff.RemoveRange(staffLinks);
            }

            if (messages.Count > 0 || registrations.Count > 0 || staffLinks.Count > 0)
            {
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewEditSave(int SubGroupId, int StaffId, string Shifts, bool Notification)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String html = "OK";
            String htmlMessage = string.Empty;
            String UiD = Guid.NewGuid().ToString().Replace("-", "");
            List<String> shiftsList = JsonSerializer.Deserialize<List<String>>(Shifts)!;
            String ShiftsView = String.Empty;
            List<int> shiftIds = shiftsList.Select(item => Convert.ToInt32(item)).ToList();

            #endregion

            #region Cleen Data
            List<SubGroupRegistration> registrations = _context.SubGroupRegistrations
                .Where(item => item.SubGroupId == SubGroupId && item.StaffId == StaffId)
                .ToList();

            if (registrations.Count > 0)
            {
                _context.SubGroupRegistrations.RemoveRange(registrations);
            }

            #endregion

            #region Set Data
            foreach (int shiftId in shiftIds)
            {
                _context.SubGroupRegistrations.Add(new SubGroupRegistration
                {
                    SubGroupId = SubGroupId,
                    ShiftId = shiftId,
                    StaffId = StaffId
                });
            }
            #endregion

            #region Notification
            if (Notification == true)
            {
                #region Get Data
                EntityStaff? staff = _context.Staff
                    .AsNoTracking()
                    .FirstOrDefault(item => item.Id == StaffId);
                List<SubGroupShift> shiftItems = _context.SubGroupShifts
                    .AsNoTracking()
                    .Where(item => shiftIds.Contains(item.Id))
                    .OrderBy(item => item.SubStartTime)
                    .ToList();
                SubGroup? subGroup = _context.SubGroups
                    .AsNoTracking()
                    .FirstOrDefault(item => item.Id == SubGroupId);

                if (staff == null || subGroup == null)
                {
                    html = "Bruger eller gruppe blev ikke fundet";
                }
                else
                {

                    #endregion

                    #region Generate Html Message
                    ShiftsView = "<table>";

                    foreach (SubGroupShift shift in shiftItems)
                    {
                        ShiftsView += "<tr>" +
                            "<td align=\"left\" valign=\"middle\">" +
                                $"<b>{shift.Name}</b>&nbsp;" +
                            "</td>" +
                            "<td align=\"center\" valign=\"middle\">" +
                                $"{shift.SubStartTime:yyyy-MM-dd HH:mm} # {shift.SubEndTime.AddMinutes(1):yyyy-MM-dd HH:mm}" +
                            "</td>" +
                        "</tr>";
                    }

                    ShiftsView += "</table>";

                    /// Merge to template
                    htmlMessage = System.IO.File.ReadAllText(@"wwwroot\templates\RegistrationConfirm.htm").Replace("#shifts", $"{ShiftsView}");
                    #endregion

                    #region Send Confirm to staff
                    _context.SubGroupMessages.Add(new SubGroupMessage
                    {
                        UiD = UiD,
                        SubGroupId = SubGroupId,
                        StaffId = StaffId,
                        Base64Sender = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                        Base64Reciver = code.Base64Encode($"[\"{staff.Mail}\"]"),
                        Base64Title = code.Base64Encode($"{subGroup.Name} - Ændring af vagter [{UiD}]"),
                        Base64Message = code.Base64Encode(htmlMessage),
                        iType = "Mail",
                        Sendt = 0,
                        InOut = 1,
                        State = 1
                    });
                    #endregion
                }
            }
            #endregion

            if (html == "OK")
            {
                _context.SaveChanges();
            }

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewSendConfirm(int SubGroupId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String html = "OK";
            String UiD = String.Empty;
            String FormTemplate = String.Empty;
            String UserTemplate = String.Empty;

            #endregion

            #region Get Data
            SubGroup? subGroup = _context.SubGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == SubGroupId);
            SubGroupForm? form = _context.SubGroupForms
                .AsNoTracking()
                .FirstOrDefault(item => item.SubGroupId == SubGroupId);
            EntityStaff? staff = (from link in _context.SubGroupStaff.AsNoTracking()
                                  join item in _context.Staff.AsNoTracking() on link.StaffId equals item.Id
                                  where link.SubGroupId == SubGroupId && item.Id == StaffId
                                  select item)
                .FirstOrDefault();

            if (subGroup == null || form == null || staff == null)
            {
                return Json("Gruppe, formular eller bruger blev ikke fundet");
            }

            FormTemplate = code.Base64Decode(form.Base64FormHtml ?? string.Empty);

            #endregion

            #region Send Flow
            UiD = Guid.NewGuid().ToString().Replace("-", "");

            UserTemplate = FormTemplate;
            UserTemplate = UserTemplate.Replace("#navn", staff.Name ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#adresse", staff.Address ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#postnr", staff.ZipCode ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#by", staff.City ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#foedselsdag", staff.Birthday?.ToString() ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#mail", staff.Mail ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#telefonnr", staff.Phone ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#key1", staff.Key1 ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#key2", staff.Key2 ?? string.Empty);
            UserTemplate = UserTemplate.Replace("#key3", staff.Key3 ?? string.Empty);
            UserTemplate = UserTemplate.Replace(subGroup.UiD, $"{subGroup.UiD}&staff={staff.UiD}");

            _context.SubGroupMessages.Add(new SubGroupMessage
            {
                UiD = UiD,
                SubGroupId = SubGroupId,
                StaffId = staff.Id,
                UserId = UserDetails.UserId,
                Base64Sender = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                Base64Reciver = code.Base64Encode($"[\"{staff.Mail}\"]"),
                Base64Title = code.Base64Encode($"{subGroup.Name} - Tilmelding [{UiD}]"),
                Base64Message = code.Base64Encode(UserTemplate),
                iType = "Mail",
                Sendt = 0,
                InOut = 1,
                State = 1
            });
            _context.SaveChanges();
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewUpdateRegistrations(int SubGroupId)
        {
            String html = String.Empty;

            #endregion

            #region Get Data
            var shiftLookup = _context.SubGroupShifts
                .AsNoTracking()
                .Where(shift => shift.SubGroupId == SubGroupId)
                .ToDictionary(shift => shift.Id, shift => shift.Name ?? string.Empty);

            var staffLookup = _context.Staff
                .AsNoTracking()
                .ToDictionary(staff => staff.Id, staff => staff.Name);

            var registrations = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(registration => registration.SubGroupId == SubGroupId)
                .ToList()
                .GroupBy(registration => registration.StaffId)
                .Select(group => new
                {
                    CreatedDate = group.Max(item => item.CreatedDate),
                    Name = staffLookup.TryGetValue(group.Key, out var name) ? name : string.Empty,
                    ShiftNames = group
                        .OrderBy(item => shiftLookup.TryGetValue(item.ShiftId, out var shiftName) ? shiftName : string.Empty)
                        .Select(item => shiftLookup.TryGetValue(item.ShiftId, out var shiftName) ? shiftName : string.Empty)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct()
                        .ToList()
                })
                .OrderByDescending(item => item.CreatedDate)
                .ToList();

            #endregion

            #region Generate html
            html += "<tr>" +
                    "<th></th>" +
                    "<th>" +
                        $"Registreret ({registrations.Count} Stk)" +
                    "</th>" +
                    "<th>" +
                        "Navn" +
                    "</th>" +
                    "<th>" +
                        "Vagter" +
                    "</th>" +
                "</tr>";

            foreach (var registration in registrations)
            {
                html += "<tr>" +
                        "<td align=\"center\" valign=\"middle\">" +
                            "<i class=\"bi bi-check2-circle\" style=\"font-size: 1.2rem; color: green;\"></i>" +
                        "</td>" +
                        "<td align=\"left\" valign=\"middle\">" +
                            $"<i>{registration.CreatedDate:yyyy-MM-dd HH:mm}</i>" +
                        "</td>" +
                        "<td align=\"left\" valign=\"middle\">" +
                            registration.Name +
                        "</td>" +
                        "<td align=\"left\" valign=\"middle\">" +
                            string.Join(", ", registration.ShiftNames) +
                        "</td>" +
                    "</tr>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubShiftsViewViewGetDays(int SubGroupId)
        {
            string jsonObj = string.Empty;

            #region Get Data
            var dayItems = _context.SubGroupShifts
                .AsNoTracking()
                .Where(shift => shift.SubGroupId == SubGroupId)
                .OrderBy(shift => shift.SubStartTime)
                .Select(shift => shift.SubStartTime.Date)
                .Distinct()
                .ToList()
                .Select(date => new
                {
                    WeekDay = GetDanishWeekDay(date),
                    Date = date.ToString("yyyy-MM-dd")
                })
                .ToList();

            #endregion

            #region Generate Json
            jsonObj = JsonSerializer.Serialize(dayItems);

            #endregion

            #region Return Data
            return Json(jsonObj);
            #endregion
        }
        public JsonResult _SubShiftsViewViewGetDetails(int SubGroupId, string Date)
        {
            string jsonObj = string.Empty;

            #region Get Data
            DateTime selectedDate = DateTime.Parse(Date, CultureInfo.InvariantCulture);
            var timeline = GetShiftViewTimeline(SubGroupId, selectedDate);

            #endregion

            #region Generate Json
            jsonObj = JsonSerializer.Serialize(new
            {
                labels = timeline.Select(item => item.DateTime.ToString("HH:mm")).ToList(),
                needs = timeline.Select(item => item.Needs).ToList(),
                registered = timeline.Select(item => item.Registered).ToList()
            });
            #endregion

            #region Return Data
            return Json(jsonObj);
            #endregion
        }

        /// Tidsforbrug (TimeUsage)
        public IActionResult SubShiftsViewHoursExport(int SubGroupId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            string ExportContent = string.Empty;

            #endregion

            #region Get Data
            List<TimeUsageRow> rows = GetTimeUsageRows(SubGroupId, UserDetails.GroupId);

            #endregion

            #region Generate CSV
            ExportContent += "Name;Mail;Phone;Key1;Key2;Key3;Timer;Minutter";
            ExportContent += Environment.NewLine;
            foreach (TimeUsageRow row in rows)
            {
                ExportContent += $"{row.Name};{row.Mail};{row.Phone};{row.Key1};{row.Key2};{row.Key3};{row.Timer};{row.Minutter}{Environment.NewLine}";
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;

            #endregion

            #region Return Data
            return File(Encoding.Unicode.GetBytes(ExportContent), "text/plain", $"TimeRapport_{DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss")}.csv", true);
            #endregion
        }
        public IActionResult SubShiftsViewHoursDetails(int SubGroupId, int StaffId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();
            EntityStaff? staff = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == StaffId);

            if (staff == null)
            {
                return NotFound();
            }

            #endregion

            #region Get Data
            data.aTable = BuildStaffTable(new[] { staff });
            data.bTable = CreateTable("Date", "CheckInTime", "CheckOutTime", "PostName", "Min");

            List<SubGroupPostMember> postMembers = (from postMember in _context.SubGroupPostMembers.AsNoTracking()
                                                    join subGroup in _context.SubGroups.AsNoTracking() on postMember.SubGroupId equals subGroup.Id
                                                    where postMember.SubGroupId == SubGroupId
                                                        && postMember.StaffId == StaffId
                                                        && subGroup.GroupId == UserDetails.GroupId
                                                    orderby postMember.CreatedDate
                                                    select postMember)
                .ToList();

            Dictionary<int, string> postNames = _context.SubGroupPostGroups
                .AsNoTracking()
                .Where(group => group.SubGroupId == SubGroupId)
                .ToDictionary(group => group.Id, group => group.Name);

            foreach (SubGroupPostMember postMember in postMembers)
            {
                data.bTable.Rows.Add(
                    postMember.CreatedDate.Date,
                    postMember.CreatedDate,
                    postMember.CheckOut,
                    postNames.TryGetValue(postMember.PostId, out var postName) ? postName : string.Empty,
                    postMember.CheckOut == null ? 0 : (int)(postMember.CheckOut.Value - postMember.CreatedDate).TotalMinutes);
            }

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Data = data;

            #endregion

            #region Return Data
            return PartialView("_SubShiftsViewHoursDetails", model);
            #endregion
        }

        public JsonResult _SubTimeUsageViewUpdate(int SubGroupId, string FilterValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            List<TimeUsageRow> rows = GetTimeUsageRows(SubGroupId, UserDetails.GroupId, FilterValue);
            #endregion

            #region Generate Html
            if (rows.Count > 0)
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Timer" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>";

                foreach (TimeUsageRow row in rows.Take(10))
                {
                    html += $"<tr class=\"light\">" +
                        "<th scope=\"row\" valign=\"middle\">" +
                            $"{(rows.IndexOf(row) + 1)}" +
                        "</th>" +
                        "<td valign=\"middle\">" +
                            row.Name +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            row.Mail +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            row.Phone +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            row.Key1 +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            row.Key2 +
                        "</td>" +
                        "<td valign=\"middle\">" +
                            row.Key3 +
                        "</td>" +
                        $"<td valign=\"middle\" style=\"cursor: help;\" title=\"{row.Timer} Timer {row.Minutter} Minutter\">" +
                            $"{row.Timer}" +
                        "</td>" +
                        "<td align=\"right\" valign=\"middle\">" +
                            $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"timeUpdate-ajax-modal\" data-bs-url=\"/Home/SubShiftsViewHoursDetails?SubGroupId={SubGroupId}&StaffId={row.StaffId}\" style=\"width: 100%; text-align: left;\" title=\"Se detaljer om timer\">" +
                                "<i class=\"bi bi-journals\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Detaljer</i>" +
                            "</button>" +
                        "</td>" +
                    "</tr>";
                }

                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"9\">" +
                            "<div>";
                if (rows.Count > 10)
                {
                    html += $"<i>10 af {rows.Count}</i>";
                }
                else
                {
                    html += $"<i>{rows.Count} af {rows.Count}</i>";
                }

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mail" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Mobil" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key1" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key2" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Key3" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Timer" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"9\">" +
                            "<center>Ingen registreringer fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        
        /// Beskeder (Messages)
        [HttpGet]
        public IActionResult SubMessageViewMessage(int MessageId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            SubGroupMessage? message = (from item in _context.SubGroupMessages
                                        join subGroup in _context.SubGroups on item.SubGroupId equals subGroup.Id
                                        where subGroup.GroupId == UserDetails.GroupId && item.Id == MessageId
                                        select item)
                .FirstOrDefault();

            if (message == null)
            {
                return NotFound();
            }

            #endregion

            #region Change State
            message.State = 1;
            _context.SaveChanges();

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.MessageView = new MessageViewModel
            {
                SubGroupId = message.SubGroupId,
                MessageId = MessageId,
                Subject = code.Base64Decode(message.Base64Title),
                Message = Regex.Replace(code.Base64Decode(message.Base64Message), @"<div id=""DeleteZone"">(?:(?:.*?|\n)+)+<\/div>", ""),
                State = message.State
            };

            #endregion

            #region Return Data
            return PartialView("_SubMessageView", model);
            #endregion
        }
        [HttpGet]
        public IActionResult SubMessageSendMessage()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Return Data
            model.UserDetails = UserDetails;
            return PartialView("_SubMessageSend", model);
            #endregion
        }

        [HttpGet]
        public IActionResult SubMessageReplyMessage(int MessageId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            SubGroupMessage? message = (from item in _context.SubGroupMessages
                                        join subGroup in _context.SubGroups on item.SubGroupId equals subGroup.Id
                                        where subGroup.GroupId == UserDetails.GroupId && item.Id == MessageId
                                        select item)
                .FirstOrDefault();

            if (message == null)
            {
                return NotFound();
            }

            #endregion

            #region Change State
            message.State = 1;
            _context.SaveChanges();

            #endregion

            #region ReGenerate Message
            String Message = code.Base64Decode(message.Base64Message);

            var text = HttpUtility.HtmlDecode(Message);
            text = Regex.Replace(text, @"(<br(\s/)?>|<hr(\s/)?>|<tr>)", "\r\n");
            text = Regex.Replace(text, "&nbsp;", " ");
            text = Regex.Replace(text, "<.*?>", "");

            Message = $"\r\n\r\n\r\nMed Venlig Hilsen\r\n{UserDetails.DisplayName}\r\n>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>\r\n" + text;
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.MessageView = new MessageViewModel
            {
                SubGroupId = message.SubGroupId,
                MessageId = MessageId,
                Subject = "Re: " + code.Base64Decode(message.Base64Title),
                Message = Message,
                State = message.State
            };

            #endregion

            #region Return Data
            return PartialView("_SubMessageReply", model);
            #endregion
        }

        public JsonResult _SubMessagesViewUpdate(int SubGroupId, string Sort, string Filter)
        {
            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Get Data
            var query = from message in _context.SubGroupMessages.AsNoTracking()
                        join staff in _context.Staff.AsNoTracking() on message.StaffId equals staff.Id into staffJoin
                        from staff in staffJoin.DefaultIfEmpty()
                        where message.SubGroupId == SubGroupId
                        select new
                        {
                            message.StaffId,
                            Name = staff != null ? staff.Name : string.Empty,
                            MessageId = message.Id,
                            message.UiD,
                            message.Base64Title,
                            message.iType,
                            message.Sendt,
                            message.InOut,
                            message.State,
                            message.CreatedDate
                        };

            if (Sort == "0")
            {
                query = query.Where(item => item.State == 0);
            }

            if (!string.IsNullOrWhiteSpace(Filter))
            {
                var filter = Filter.Trim();
                query = query.Where(item => EF.Functions.Like(item.Name, $"%{filter}%"));
            }

            var messages = query
                .OrderBy(item => item.StaffId)
                .ThenBy(item => item.UiD)
                .ThenByDescending(item => item.CreatedDate)
                .ToList();
            #endregion

            #region Generate Html
            if (messages.Count > 0)
            {
                /// Generate Head
                html = "<thead>" +
                        "<tr>" +
                            "<th scope=\"col\" style=\"width: 10px;\">" +
                                "#" +
                            "</th>" +
                            "<th scope=\"col\">" +
                                "Navn" +
                            "</th>" +
                            "<th scope=\"col\">" +
                                "Emne" +
                            "</th>" +
                            "<th scope=\"col\">" +
                            "</th>" +
                        "</tr>" +
                    "</thead>" +
                    "<tbody>";

                /// Generate Rows
                foreach (var item in messages)
                {
                    html += "<tr class=\"light\">" +
                        $"<td align=\"left\" valign=\"middle\" id=\"{item.MessageId}_Icon\">";
                            switch ($"{item.State}")
                            {
                                case "0":
                                    html += "<i class=\"bi bi-envelope\" style=\"font-size: 1.2rem; color: gray;\"></i>";
                                    break;
                                case "1":
                                    html += "<i class=\"bi bi-envelope-open\" style=\"font-size: 1.2rem; color: gray;\"></i>";
                                    break;
                                case "2":
                                    html += "<i class=\"bi bi-envelope-exclamation\" style=\"font-size: 1.2rem; color: gray;\"></i>";
                                    break;

                                default:
                                    html += $"<i class=\"bi bi-exclamation-diamond\" style=\"font-size: 1.2rem; color: red;\" title=\"State: {item.State}\"></i>";
                                    break;
                            }
                        html += "</td>" +
                        "<td align=\"left\" valign=\"middle\">" +
                            $"<b>{item.Name}</b>" +
                        "</td>" +
                        "<td align=\"left\" valign=\"middle\">";
                            string decodedTitle = code.Base64Decode(item.Base64Title);
                            if (decodedTitle.Length < 60)
                            {
                                html += $"<i>{decodedTitle}</i>";
                            }
                            else
                            {
                                html += $"<i>{decodedTitle.Substring(0, 60)}...</i>";
                            }
                            html += "</td>" +
                            "<td align=\"right\" valign=\"middle\">" +
                                "<ul class=\"navbar-nav flex-grow-1\">" +
                                    "<li class=\"nav-item dropdown\">" +
                                        "<a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"navbarDropdown\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">" +
                                            "<i class=\"bi bi-list\" style=\"width: 100%; text-align: left;\" style=\"font-size: 1rem; color: gray;\"></i>" +
                                        "</a>" +
                                        "<ul class=\"dropdown-menu\" aria-labelledby=\"navbarDropdown\">";
                                            if ($"{item.InOut}" == "2")
                                            {
                                                html += $"<li>" +
                                                    $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"messageView-ajax-modal\" data-bs-url=\"/Home/SubMessageReplyMessage?MessageId={item.MessageId}\" style=\"width: 100%; text-align: left;\" title=\"Besvar mail\">" +
                                                        "<i class=\"bi bi-reply\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Besvar</i>" +
                                                    "</button>" +
                                                "</li>";
                                            }
                                            html += $"<li>" +
                                                $"<button type=\"button\" class=\"btn lightButton\" data-bs-toggle=\"messageView-ajax-modal\" data-bs-url=\"/Home/SubMessageViewMessage?MessageId={item.MessageId}\" style=\"width: 100%; text-align: left;\" title=\"Åben mail\">" +
                                                    "<i class=\"bi bi-card-text\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Åben</i>" +
                                                "</button>" +
                                            "</li>" +
                                            $"<li>" +
                                                $"<button type=\"button\" class=\"btn lightButton\" onclick=\"messageDelete({item.MessageId})\" style=\"width: 100%; text-align: left;\" title=\"Slet mail\">" +
                                                    "<i class=\"bi bi-trash2\" style=\"font-size: 1rem; color: gray;\">&nbsp;&nbsp;Slet</i>" +
                                                "</button>" +
                                            "</li>" +
                                        "</ul>" +
                                    "</li>" +
                                "</ul>" +
                            "</td>" +
                        "</tr>";
                }

                /// Generate Foot
                html += "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"4\">" +
                            "<div>";

                html += $"<i>{messages.Count} af {messages.Count}</i>";

                html += "</div>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            else
            {
                html = "<thead>" +
                    "<tr>" +
                        "<th scope=\"col\" style=\"width: 10px;\">" +
                            "#" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Navn" +
                        "</th>" +
                        "<th scope=\"col\">" +
                            "Emne" +
                        "</th>" +
                        "<th scope=\"col\">" +
                        "</th>" +
                    "</tr>" +
                "</thead>" +
                "<tbody>" +
                "</tbody>" +
                "<tfoot>" +
                    "<tr style=\"border-bottom: 1px solid white;\">" +
                        "<td colspan=\"4\">" +
                            "<center>Ingen mails fundet</center>" +
                        "</td>" +
                    "</tr>" +
                "</tfoot>";
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubMessagesViewDelete(int MessageId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Set Data
            SubGroupMessage? message = (from item in _context.SubGroupMessages
                                        join subGroup in _context.SubGroups on item.SubGroupId equals subGroup.Id
                                        where subGroup.GroupId == UserDetails.GroupId && item.Id == MessageId
                                        select item)
                .FirstOrDefault();

            if (message != null)
            {
                _context.SubGroupMessages.Remove(message);
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubMessagesReply(int MessageId, string Message)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            List<string> Senders = new List<string>();
            #endregion

            #region Get Data
            SubGroupMessage? originalMessage = (from item in _context.SubGroupMessages
                                                join subGroup in _context.SubGroups on item.SubGroupId equals subGroup.Id
                                                where subGroup.GroupId == UserDetails.GroupId && item.Id == MessageId
                                                select item)
                .FirstOrDefault();

            if (originalMessage == null)
            {
                return Json("Beskeden blev ikke fundet");
            }

            Senders = JsonSerializer.Deserialize<List<string>>(code.Base64Decode(originalMessage.Base64Sender ?? string.Empty)) ?? new List<string>();

            #endregion

            #region Generate Message
            var text = "<html>" + Message + "</html>";
            text = text.Replace("\r\n", "\r");
            text = text.Replace("\n", "\r");
            text = text.Replace("\r", "<br />\r\n");
            text = text.Replace("  ", " &nbsp;");

            #endregion

            #region Set Data
            _context.SubGroupMessages.Add(new SubGroupMessage
            {
                UiD = originalMessage.UiD,
                SubGroupId = originalMessage.SubGroupId,
                StaffId = originalMessage.StaffId,
                UserId = UserDetails.UserId,
                Base64Sender = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                Base64Reciver = code.Base64Encode($"[\"{Senders.FirstOrDefault() ?? string.Empty}\"]"),
                Base64Title = code.Base64Encode($"Re: {code.Base64Decode(originalMessage.Base64Title)}"),
                Base64Message = code.Base64Encode(text),
                iType = "Mail",
                Sendt = 0,
                InOut = 1,
                State = 1
            });
            _context.SaveChanges();
            
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _SubMessagesGetStaff(int SubGroupId)
        {
            #region Init. Variables
            String json = String.Empty;

            #endregion

            #region Get Data
            List<EntityStaff> staffItems = (from link in _context.SubGroupStaff.AsNoTracking()
                                            join staff in _context.Staff.AsNoTracking() on link.StaffId equals staff.Id
                                            where link.SubGroupId == SubGroupId
                                            orderby staff.Name
                                            select staff)
                .Distinct()
                .ToList();

            #endregion

            #region Generate Json
            json = JsonSerializer.Serialize(new
            {
                Staff = staffItems.Select(staff => new
                {
                    StaffId = staff.Id,
                    Name = staff.Name,
                    Mail = Regex.IsMatch((staff.Mail ?? string.Empty).Replace(" ", ""), @"^.*@.*\..*$") ? staff.Mail ?? string.Empty : string.Empty
                }).ToList()
            });
            #endregion

            #region Return Data
            return Json(json);
            #endregion
        }
        public JsonResult _SubMessagesSend(int SubGroupId, string Reciver, string Subject, string Message)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            String html = "OK";
            String UiD = String.Empty;
            string templateMessage = Message;

            #endregion

            #region Get Data
            SubGroup? subGroup = _context.SubGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == SubGroupId);

            if (subGroup == null)
            {
                return Json("Gruppen blev ikke fundet");
            }

            List<int> registeredStaffIds = _context.SubGroupRegistrations
                .AsNoTracking()
                .Where(item => item.SubGroupId == SubGroupId)
                .Select(item => item.StaffId)
                .Distinct()
                .ToList();

            var subGroupStaffQuery = from link in _context.SubGroupStaff.AsNoTracking()
                                     join staff in _context.Staff.AsNoTracking() on link.StaffId equals staff.Id
                                     where link.SubGroupId == SubGroupId
                                     select staff;

            List<EntityStaff> recipients = Reciver switch
            {
                "Alle" => subGroupStaffQuery.ToList(),
                "Tilmeldte" => subGroupStaffQuery.Where(staff => registeredStaffIds.Contains(staff.Id)).ToList(),
                "Ikke tilmeldte" => subGroupStaffQuery.Where(staff => !registeredStaffIds.Contains(staff.Id)).ToList(),
                _ => _context.Staff.AsNoTracking().Where(staff => staff.Id == Convert.ToInt32(Reciver)).ToList()
            };

            #endregion

            #region Send Messages
            foreach (EntityStaff recipient in recipients)
            {
                #region Init. Loop Variables
                UiD = Guid.NewGuid().ToString().Replace("-", "");

                #endregion

                #region Custom Message
                Message = templateMessage;
                // #navn
                Message = Message.Replace($"#navn", recipient.Name ?? string.Empty);
                // #adresse
                Message = Message.Replace($"#adresse", recipient.Address ?? string.Empty);
                // #postnr
                Message = Message.Replace($"#postnr", recipient.ZipCode ?? string.Empty);
                // #by
                Message = Message.Replace($"#by", recipient.City ?? string.Empty);
                // #fødselsdag
                Message = Message.Replace($"#foedselsdag", recipient.Birthday?.ToString() ?? string.Empty);
                // #mail
                Message = Message.Replace($"#mail", recipient.Mail ?? string.Empty);
                // #telefonnr
                Message = Message.Replace($"#telefonnr", recipient.Phone ?? string.Empty);
                // #key1
                Message = Message.Replace($"#key1", recipient.Key1 ?? string.Empty);
                // #key2
                Message = Message.Replace($"#key2", recipient.Key2 ?? string.Empty);
                // #key3
                Message = Message.Replace($"#key3", recipient.Key3 ?? string.Empty);

                // Convert Message to HTML
                Message = "<html>" + Message + "</html>";
                Message = Message.Replace("\r\n", "\r");
                Message = Message.Replace("\n", "\r");
                Message = Message.Replace("\r", "<br />\r\n");
                Message = Message.Replace(" ", " &nbsp;");

                #endregion

                #region Set Data
                if (Regex.IsMatch((recipient.Mail ?? string.Empty).Replace(" ", ""), @"^.*@.*\..*$") == true)
                {
                    _context.SubGroupMessages.Add(new SubGroupMessage
                    {
                        UiD = UiD,
                        SubGroupId = SubGroupId,
                        StaffId = recipient.Id,
                        UserId = UserDetails.UserId,
                        Base64Sender = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                        Base64Reciver = code.Base64Encode($"[\"{recipient.Mail}\"]"),
                        Base64Title = code.Base64Encode($"{subGroup.Name} - {Subject} [{UiD}]"),
                        Base64Message = code.Base64Encode(Message),
                        iType = "Mail",
                        Sendt = 0,
                        InOut = 1,
                        State = 1
                    });
                }
                else
                {
                    _context.SubGroupMessages.Add(new SubGroupMessage
                    {
                        UiD = UiD,
                        SubGroupId = SubGroupId,
                        StaffId = recipient.Id,
                        UserId = UserDetails.UserId,
                        Base64Title = code.Base64Encode($"{Subject}: Mail ikke gyldig"),
                        Base64Message = code.Base64Encode($"Bruger: {recipient.Name}{Environment.NewLine}Mail: {recipient.Mail}{Environment.NewLine}{Environment.NewLine}Maile opfylder ikke krav om mail sammensætning!"),
                        iType = "Note",
                        InOut = 1,
                        State = 0
                    });
                }
                #endregion
            }

            if (recipients.Count > 0)
            {
                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        #region MyUser
        [HttpGet]
        public IActionResult MyUser()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Messages = GetPendingMessages(UserDetails.UserId, "MyUser");

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }
        [HttpPost]
        public async Task<IActionResult> MyUser(HomeModels iData)
        {
            var user = await _userManager.FindByIdAsync(iData.UserDetails.UserId.ToString());
            if (user == null)
            {
                return View(iData);
            }

            user.DisplayName = iData.UserDetails.DisplayName;
            user.Email = iData.UserDetails.Mail;
            user.PhoneNumber = iData.UserDetails.Phone;

            await _userManager.UpdateAsync(user);

            if (string.IsNullOrWhiteSpace(iData.UserDetails.Password) == false)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, iData.UserDetails.Password);

                if (!resetResult.Succeeded)
                {
                    foreach (var error in resetResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    iData.UserDetails = GetCurrentUserDetails();
                    iData.Messages = GetPendingMessages(iData.UserDetails.UserId, "MyUser");
                    return View(iData);
                }
            }

            AddAppMessage(user.Id, "MyUser", "success", "Bruger er opdateret");

            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Set Data
            iData.UserDetails = UserDetails;
            iData.Messages = GetPendingMessages(UserDetails.UserId, "MyUser");

            #endregion

            #region Return Data
            return View(iData);
            #endregion
        }

        #endregion

        #region Tilmelding
        [AllowAnonymous]
        [Route("/{Tilmelding}")]
        public IActionResult Tilmelding(string form, string staff)
        {
            #region Init. Variales
            DataModel data = new DataModel();
            data.aTable = CreateTable("Name", "Activated", "SubGroupId");
            data.bTable = CreateTable("Id", "UiD", "Name", "Phone", "Mail");
            data.cTable = CreateTable("ShiftId", "WeekDay", "Name", "Type", "SubStartTime", "SubEndTime", "StaffNeeds", "Key1");
            data.dTable = CreateTable("Id", "CreatedDate", "SubGroupId", "Key", "Value");
            data.eTable = CreateTable("Rule", "Value");
            data.gTable = CreateTable("Id");

            #endregion

            #region Get Data
            var subGroup = _context.SubGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.UiD == form);

            var staffItem = _context.Staff
                .AsNoTracking()
                .FirstOrDefault(item => item.UiD == staff);

            if (subGroup != null)
            {
                data.aTable.Rows.Add(subGroup.Name, subGroup.Activated, subGroup.Id);

                var registrationCounts = _context.SubGroupRegistrations
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == subGroup.Id)
                    .GroupBy(item => item.ShiftId)
                    .ToDictionary(group => group.Key, group => group.Count());

                var shifts = _context.SubGroupShifts
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == subGroup.Id)
                    .OrderBy(item => item.SubStartTime)
                    .ToList();

                foreach (var shift in shifts)
                {
                    var remainingNeeds = (shift.StaffNeeds ?? 0) - registrationCounts.GetValueOrDefault(shift.Id);
                    data.cTable.Rows.Add(
                        shift.Id,
                        GetDanishWeekDay(shift.SubStartTime),
                        shift.Name ?? string.Empty,
                        shift.Type,
                        shift.SubStartTime,
                        shift.SubEndTime,
                        remainingNeeds,
                        shift.Key1 ?? string.Empty);
                }

                var ruleKeys = _context.SubGroupKeys
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == subGroup.Id && EF.Functions.Like(item.Key, "NeedsRule%"))
                    .OrderBy(item => item.Id)
                    .ToList();

                foreach (var ruleKey in ruleKeys)
                {
                    data.dTable.Rows.Add(ruleKey.Id, ruleKey.CreatedDate, ruleKey.SubGroupId, ruleKey.Key, ruleKey.Value);
                }

                if (staffItem != null)
                {
                    var registrations = _context.SubGroupRegistrations
                        .AsNoTracking()
                        .Where(item => item.SubGroupId == subGroup.Id && item.StaffId == staffItem.Id)
                        .Select(item => item.Id)
                        .ToList();

                    foreach (var registrationId in registrations)
                    {
                        data.gTable.Rows.Add(registrationId);
                    }
                }

                #region Validate Rules
                foreach (var ruleKey in ruleKeys)
                {
                    var row = data.eTable.NewRow();

                    switch (ruleKey.Key)
                    {
                        case "NeedsRuleWeekend":
                            var availableWeekendShifts = shifts.Count(item =>
                            {
                                var remainingNeeds = (item.StaffNeeds ?? 0) - registrationCounts.GetValueOrDefault(item.Id);
                                return remainingNeeds > 0 && (item.SubStartTime.DayOfWeek == DayOfWeek.Saturday || item.SubStartTime.DayOfWeek == DayOfWeek.Sunday);
                            });

                            row["Rule"] = ruleKey.Key;
                            row["Value"] = availableWeekendShifts == 0 ? "0" : ruleKey.Value;
                            data.eTable.Rows.Add(row);
                            break;

                        case "NeedsRuleKeys":
                            if (string.IsNullOrWhiteSpace(ruleKey.Value) == false)
                            {
                                model.NeedsRuleKeys = JsonSerializer.Deserialize<List<NeedsRuleKeysModel>>(ruleKey.Value) ?? new List<NeedsRuleKeysModel>();

                                foreach (var needsRuleKey in model.NeedsRuleKeys)
                                {
                                    var availableCount = shifts.Count(item => string.Equals(item.Key1, needsRuleKey.Key, StringComparison.Ordinal));
                                    if (availableCount < needsRuleKey.Amount)
                                    {
                                        needsRuleKey.Amount = availableCount;
                                    }
                                }
                            }
                            break;

                        default:
                            row["Rule"] = ruleKey.Key;
                            row["Value"] = ruleKey.Value;
                            data.eTable.Rows.Add(row);
                            break;
                    }
                }
                #endregion
            }

            if (staffItem != null)
            {
                data.bTable.Rows.Add(staffItem.Id, staffItem.UiD, staffItem.Name, staffItem.Phone ?? string.Empty, staffItem.Mail ?? string.Empty);
            }
            #endregion

            #region Set Data
            model.Data = data;
            model.SubForm.Form = form;
            model.SubForm.Staff = staff;

            if (data.aTable.Rows.Count > 0)
            {
                if (data.gTable.Rows.Count > 0)
                {
                    /// Bruger har allerede meldt timer ind
                    model.SubForm.Activated = 2;
                }
                else
                {
                    /// Form findes og bruger har ikke meldt timer ind
                    model.SubForm.Activated = Convert.ToInt32($"{data.aTable.Rows[0]["Activated"]}");
                }
            }
            else
            {
                /// Form findes ikke
                model.SubForm.Activated = -1;
            }

            if (data.bTable.Rows.Count > 0)
            {
                /// Bruger findes
                model.Tilmelding.Name = $"{data.bTable.Rows[0]["Name"]}";
                model.Tilmelding.Phone = $"{data.bTable.Rows[0]["Phone"]}";
                model.Tilmelding.Mail = $"{data.bTable.Rows[0]["Mail"]}";
            }
            else
            {
                /// Bruger findes ikke
                model.SubForm.Activated = -1;
            }

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }

        [AllowAnonymous]
        public JsonResult _TilmeldingCheck(string Shifts)
        {
            #region Init. Variables
            List<NeedsRuleKeysModel> RuleKeys = new List<NeedsRuleKeysModel>();
            String html = string.Empty;
            String keys = string.Empty;
            int passValidation = 1;

            DateTime startTime;
            DateTime endTime;
            double hours = 0;

            List<String> shiftsList = JsonSerializer.Deserialize<List<String>>(Shifts) ?? new List<string>();
            List<int> shiftIds = shiftsList
                .Select(id => int.TryParse(id, out var parsedId) ? parsedId : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            #endregion

            #region Get Data
            if (shiftIds.Count > 0)
            {
                var shifts = _context.SubGroupShifts
                    .AsNoTracking()
                    .Where(item => shiftIds.Contains(item.Id))
                    .OrderBy(item => item.SubStartTime)
                    .ToList();

                if (shifts.Count == 0)
                {
                    return Json("[{\"HoursCount\": 0}]");
                }

                var subGroupId = shifts[0].SubGroupId;
                var hoursRuleValue = _context.SubGroupKeys
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == subGroupId && item.Key == "NeedsRuleHours")
                    .Select(item => item.Value)
                    .FirstOrDefault();

                var weekendRuleValue = _context.SubGroupKeys
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == subGroupId && item.Key == "NeedsRuleWeekend")
                    .Select(item => item.Value)
                    .FirstOrDefault();

                var keyRuleValue = _context.SubGroupKeys
                    .AsNoTracking()
                    .Where(item => item.SubGroupId == subGroupId && item.Key == "NeedsRuleKeys")
                    .Select(item => item.Value)
                    .FirstOrDefault();

                var hoursRule = int.TryParse(hoursRuleValue, out var parsedHoursRule) ? parsedHoursRule : 0;
                var weekendRule = int.TryParse(weekendRuleValue, out var parsedWeekendRule) ? parsedWeekendRule : 0;

                if (string.IsNullOrWhiteSpace(keyRuleValue) == false)
                {
                    RuleKeys = JsonSerializer.Deserialize<List<NeedsRuleKeysModel>>(keyRuleValue) ?? new List<NeedsRuleKeysModel>();
                }

                var selectedKeyCounts = shifts
                    .Where(item => string.IsNullOrWhiteSpace(item.Key1) == false)
                    .GroupBy(item => item.Key1!)
                    .ToDictionary(group => group.Key, group => group.Count());

                #region Get Hours
                foreach (var shift in shifts)
                {
                    startTime = shift.SubStartTime;
                    endTime = shift.SubEndTime;
                    hours += (endTime.AddMinutes(1) - startTime).TotalHours;
                }

                hours = Math.Round(hours, 0);
                #endregion

                var weekendCount = shifts.Count(item => item.SubStartTime.DayOfWeek == DayOfWeek.Saturday || item.SubStartTime.DayOfWeek == DayOfWeek.Sunday);

                #region Validate
                if (hoursRule > hours)
                {
                    passValidation = 0;
                }

                if (weekendRule > weekendCount)
                {
                    passValidation = 0;
                }

                foreach (var ruleKey in RuleKeys)
                {
                    var amountCount = selectedKeyCounts.GetValueOrDefault(ruleKey.Key, 0);
                    if (ruleKey.Amount > amountCount)
                    {
                        passValidation = 0;
                    }

                    if (string.IsNullOrEmpty(keys) == true)
                    {
                        keys = "{" +
                            "\"Key\": \"" + ruleKey.Key + "\", " +
                            "\"Amount\": " + ruleKey.Amount + ", " +
                            "\"AmountCount\": " + amountCount +
                            "}";
                    }
                    else
                    {
                        keys += ",{" +
                            "\"Key\": \"" + ruleKey.Key + "\", " +
                            "\"Amount\": " + ruleKey.Amount + ", " +
                            "\"AmountCount\": " + amountCount +
                            "}";
                    }
                }
                #endregion

                #region Set Data
                html = "[{";
                html += "\"Hours\": " + hoursRule + ", \"HoursCount\": " + Convert.ToInt32(hours);
                html += ", \"Weekend\": " + weekendRule + ", \"WeekendCount\": " + weekendCount;
                html += string.IsNullOrEmpty(keys)
                    ? ", \"RuleKeys\": []"
                    : ", \"RuleKeys\": [" + keys + "]";
                html += ", \"PassValidation\": " + passValidation;
                html += "}]";
                #endregion
            }
            else
            {
                return Json("[{\"HoursCount\": 0}]");
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        
        [AllowAnonymous]
        public JsonResult _TilmeldingSave(string Staff, string Shifts, string ShiftNote)
        {
            #region Init. Variables
            String html = string.Empty;
            String htmlMessage = string.Empty;
            String UiD = Guid.NewGuid().ToString().Replace("-","");
            List<String> shiftsList = JsonSerializer.Deserialize<List<String>>(Shifts) ?? new List<string>();
            List<int> shiftIds = shiftsList
                .Select(id => int.TryParse(id, out var parsedId) ? parsedId : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            String ShiftsView = String.Empty;

            #endregion

            #region Get Data
            var selectedShifts = _context.SubGroupShifts
                .AsNoTracking()
                .Where(item => shiftIds.Contains(item.Id))
                .OrderBy(item => item.SubStartTime)
                .ToList();

            var staffItem = _context.Staff.FirstOrDefault(item => item.UiD == Staff);
            var subGroupId = selectedShifts.Select(item => item.SubGroupId).FirstOrDefault();
            var subGroup = _context.SubGroups
                .AsNoTracking()
                .FirstOrDefault(item => item.Id == subGroupId);

            if (selectedShifts.Count == 0 || staffItem == null || subGroup == null)
            {
                return Json(html);
            }

            #endregion

            #region Generate Html Message
            ShiftsView = "<table>";

            foreach (var shift in selectedShifts)
            {
                ShiftsView += "<tr>" +
                    "<td align=\"left\" valign=\"middle\">" +
                        $"<b>{shift.Name}</b>&nbsp;" +
                    "</td>" +
                    "<td align=\"center\" valign=\"middle\">" +
                        $"{shift.SubStartTime:yyyy-MM-dd HH:mm} # {shift.SubEndTime.AddMinutes(1):yyyy-MM-dd HH:mm}" +
                    "</td>" +
                "</tr>";
            }

            ShiftsView += "</table>";

            /// Merge to template
            htmlMessage = System.IO.File
                .ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", "RegistrationConfirm.htm"))
                .Replace("#shifts", ShiftsView);
            #endregion

            #region Set Data
            foreach (var shiftId in shiftIds)
            {
                _context.SubGroupRegistrations.Add(new SubGroupRegistration
                {
                    CreatedDate = DateTime.Now,
                    SubGroupId = subGroupId,
                    ShiftId = shiftId,
                    StaffId = staffItem.Id
                });
            }

            /// Save note
            if (String.IsNullOrEmpty(ShiftNote) == false)
            {
                var text = HttpUtility.HtmlEncode(ShiftNote);
                text = text.Replace("\r\n", "\r");
                text = text.Replace("\n", "\r");
                text = text.Replace("\r", "<br />\r\n");
                text = text.Replace("  ", " &nbsp;");

                ShiftsView = "<html>" + text + "<br /><br /><hr /><center><h5>Vagter valgt</h5>" + ShiftsView + "</center></html>";

                _context.SubGroupMessages.Add(new SubGroupMessage
                {
                    CreatedDate = DateTime.Now,
                    UiD = UiD,
                    SubGroupId = subGroupId,
                    StaffId = staffItem.Id,
                    Base64Sender = code.Base64Encode($"[\"{staffItem.Mail}\"]"),
                    Base64Reciver = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                    Base64Title = code.Base64Encode("Besked fra tilmelding"),
                    Base64Message = code.Base64Encode(ShiftsView),
                    iType = "Note",
                    InOut = 2,
                    State = 0
                });
            }
            #endregion

            #region Send Confirm to staff
            _context.SubGroupMessages.Add(new SubGroupMessage
            {
                CreatedDate = DateTime.Now,
                UiD = UiD,
                SubGroupId = subGroupId,
                StaffId = staffItem.Id,
                Base64Sender = code.Base64Encode($"[\"{_configuration["MailConfig:Mail"]}\"]"),
                Base64Reciver = code.Base64Encode($"[\"{staffItem.Mail}\"]"),
                Base64Title = code.Base64Encode($"{subGroup.Name} - Bekræftigelse af vagter [{UiD}]"),
                Base64Message = code.Base64Encode(htmlMessage),
                iType = "Mail",
                Sendt = 0,
                InOut = 1,
                State = 1
            });

            _context.SaveChanges();
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        
        [AllowAnonymous]
        public JsonResult _TilUpdateStaff(string Staff, string Name, string Phone, string Mail)
        {
            #region Init. Variables
            string html = string.Empty;

            #endregion

            #region Set Data
            var staffItem = _context.Staff.FirstOrDefault(item => item.UiD == Staff);
            if (staffItem != null)
            {
                staffItem.Name = Name;
                staffItem.Mail = Mail;
                staffItem.Phone = Phone;
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        
        #endregion

        #region Shared
        public JsonResult _KeyValueRead(int SubGroupId, string KeyName)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            #endregion

            #region Get & Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                html = _context.SubGroupKeys
                    .Where(key => key.SubGroupId == SubGroupId && key.Key == KeyName)
                    .Select(key => key.Value)
                    .FirstOrDefault() ?? string.Empty;
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _KeyValueSave(int SubGroupId, string KeyName, string KeyValue)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            #endregion

            #region Set Data
            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup)
            {
                SubGroupKey? subGroupKey = _context.SubGroupKeys
                    .FirstOrDefault(key => key.SubGroupId == SubGroupId && key.Key == KeyName);

                if (subGroupKey == null)
                {
                    _context.SubGroupKeys.Add(new SubGroupKey
                    {
                        SubGroupId = SubGroupId,
                        Key = KeyName,
                        Value = KeyValue
                    });
                }
                else
                {
                    subGroupKey.Value = KeyValue;
                }

                _context.SaveChanges();
            }
            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }
        public JsonResult _KeyValueDelete(int SubGroupId, string KeyName)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Init. Variables
            string html = string.Empty;
            #endregion

            #region Set Data
            SubGroupKey? subGroupKey = _context.SubGroupKeys
                .FirstOrDefault(key => key.SubGroupId == SubGroupId && key.Key == KeyName);

            bool ownsSubGroup = _context.SubGroups.Any(group => group.Id == SubGroupId && group.GroupId == UserDetails.GroupId);

            if (ownsSubGroup && subGroupKey != null)
            {
                _context.SubGroupKeys.Remove(subGroupKey);
                _context.SaveChanges();
            }

            #endregion

            #region Return Data
            return Json(html);
            #endregion
        }

        #endregion

        #region GroupUsers
        [Authorize(Roles = "InCharge")]
        public IActionResult GroupUsers()
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();
            ViewData["DisplayName"] = UserDetails.DisplayName;

            #endregion

            #region Init. Variables
            DataModel data = new DataModel();

            #endregion

            #region Get Data
            data.aTable.Columns.Add("Id");
            data.aTable.Columns.Add("Username");
            data.aTable.Columns.Add("GroupId");
            data.aTable.Columns.Add("DisplayName");
            data.aTable.Columns.Add("Mail");
            data.aTable.Columns.Add("Phone");
            data.aTable.Columns.Add("GroupName");

            var groupName = _context.Groups
                .Where(group => group.Id == UserDetails.GroupId)
                .Select(group => group.Name)
                .FirstOrDefault() ?? "Master";

            var users = _context.Users
                .Where(user => user.GroupId == UserDetails.GroupId)
                .OrderBy(user => user.DisplayName)
                .ToList();

            foreach (var user in users)
            {
                data.aTable.Rows.Add(user.Id, user.UserName, user.GroupId, user.DisplayName, user.Email, user.PhoneNumber, groupName);
            }
            #endregion

            #region Set Data
            model.UserDetails = UserDetails;
            model.Data = data;
            model.Messages = GetPendingMessages(UserDetails.UserId, "GroupUsers");

            #endregion

            #region Return Data
            return View(model);
            #endregion
        }

        [Authorize(Roles = "InCharge")]
        [HttpGet]
        public IActionResult EditUser(int UserId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var user = _context.Users.AsNoTracking().First(item => item.Id == UserId);

            #endregion

            #region Set Data
            model.User.UserId = UserId;
            model.User.Username = user.UserName ?? string.Empty;
            model.User.GroupId = user.GroupId;
            model.User.DisplayName = user.DisplayName ?? string.Empty;
            model.User.Mail = user.Email ?? string.Empty;
            model.User.Phone = user.PhoneNumber ?? string.Empty;

            #endregion

            #region Return Data
            return PartialView("_EditUser", model);
            #endregion
        }
        [Authorize(Roles = "InCharge")]
        [HttpPost]
        public async Task<IActionResult> EditUser(AdminModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Set Data
            var user = await _userManager.FindByIdAsync(iData.User.UserId.ToString());
            if (user != null)
            {
                user.DisplayName = iData.User.DisplayName;
                user.Email = iData.User.Mail;
                user.PhoneNumber = iData.User.Phone;
                await _userManager.UpdateAsync(user);

                if (string.IsNullOrWhiteSpace(iData.User.Password) == false)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, token, iData.User.Password);
                }
            }

            #endregion

            #region Set Message
            AddAppMessage(UserDetails.UserId, "GroupUsers", "success", "Bruger er opdateret");

            #endregion

            #region Return Data
            return PartialView("_EditUser", iData);
            #endregion
        }

        [Authorize(Roles = "InCharge")]
        [HttpGet]
        public IActionResult DeleteUser(int UserId)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var user = _context.Users.AsNoTracking().First(item => item.Id == UserId);

            #endregion

            #region Set Data
            model.User.UserId = UserId;
            model.User.Username = user.UserName ?? string.Empty;
            model.User.GroupId = user.GroupId;
            model.User.DisplayName = user.DisplayName ?? string.Empty;
            model.User.Mail = user.Email ?? string.Empty;
            model.User.Phone = user.PhoneNumber ?? string.Empty;

            #endregion

            #region Return Data
            return PartialView("_DeleteUser", model);
            #endregion
        }
        [Authorize(Roles = "InCharge")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser(AdminModels iData)
        {
            #region Init. UserDetails
            UserDetailsModel UserDetails = GetCurrentUserDetails();

            #endregion

            #region Get Data
            var usersInGroup = _context.Users.Where(item => item.GroupId == iData.User.GroupId).OrderBy(item => item.Id).ToList();
            var group = _context.Groups.FirstOrDefault(item => item.Id == iData.User.GroupId);

            #endregion

            #region Set Data
            if (usersInGroup.Count > 1)
            {
                #region ReAsign InCharge
                if (group != null && iData.User.UserId == group.InChargeId)
                {
                    foreach (var groupUser in usersInGroup)
                    {
                        if (iData.User.UserId != groupUser.Id)
                        {
                            group.InChargeId = groupUser.Id;
                            break;
                        }
                    }
                }
                #endregion

                #region Set Data
                var user = await _userManager.FindByIdAsync(iData.User.UserId.ToString());
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }
                _context.SaveChanges();

                #endregion

                #region Set Message
                AddAppMessage(UserDetails.UserId, "GroupUsers", "success", "Bruger er slettet");

                #endregion
            }
            else
            {
                #region Set Message
                AddAppMessage(UserDetails.UserId, "GroupUsers", "danger", "Kan ikke slette eneste gruppe bruger!");

                #endregion
            }

            #endregion

            #region Return Data
            return PartialView("_DeleteUser", iData);
            #endregion
        }

        #endregion

    }
}