using ShiftHub.Data;
using ShiftHub.Data.Entities;
using ShiftHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ShiftHub.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly AppDbContext _context;

        public AccountController(
            ILogger<AccountController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole<int>> roleManager,
            AppDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        public IActionResult Login()
        {
            if (!_context.Users.Any())
                return RedirectToAction("NoDb", "Account");

            return View(new AccountModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(AccountModel iData)
        {
            AccountModel model = new AccountModel
            {
                Username = iData.Username,
                Password = iData.Password
            };

            if (string.IsNullOrWhiteSpace(iData.Username) || string.IsNullOrWhiteSpace(iData.Password))
            {
                model.ErrorMessage = "Både Brugernavn og Adgangskode skal udfyldes!";
                return View(model);
            }

            var user = await _userManager.FindByNameAsync(iData.Username.Trim().ToLower());

            if (user == null)
            {
                _context.LogLogins.Add(new LogLogin { UserId = 0, Username = iData.Username, Status = "Bruger findes ikke!" });
                await _context.SaveChangesAsync();
                model.ErrorMessage = "Bruger findes ikke!";
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, iData.Password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _context.LogLogins.Add(new LogLogin { UserId = user.Id, Username = user.UserName, Status = "Login Ok" });
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }

            _context.LogLogins.Add(new LogLogin { UserId = user.Id, Username = iData.Username, Status = "Adgangskode ikke korrekt!" });
            await _context.SaveChangesAsync();
            model.ErrorMessage = "Adgangskode ikke korrekt!";
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                _context.LogLogins.Add(new LogLogin { UserId = user.Id, Username = user.UserName, Status = "Logger ud" });
                await _context.SaveChangesAsync();
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult NoAccess() => View();

        [HttpGet]
        public IActionResult NoDb()
        {
            if (_context.Users.Any())
                return RedirectToAction("Login", "Account");

            return View(new AccountModel());
        }

        [HttpPost]
        public async Task<IActionResult> NoDb(AccountModel iData)
        {
            if (string.IsNullOrWhiteSpace(iData.Username) || string.IsNullOrWhiteSpace(iData.Password))
                return RedirectToAction("Login", "Account");

            if (!await _roleManager.RoleExistsAsync("Administrator"))
                await _roleManager.CreateAsync(new IdentityRole<int>("Administrator"));

            var user = new ApplicationUser
            {
                UserName = iData.Username.Trim().ToLower(),
                GroupId = 0,
                CreatedDate = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, iData.Password);
            if (!createResult.Succeeded)
            {
                iData.ErrorMessage = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return View(iData);
            }

            await _userManager.AddToRoleAsync(user, "Administrator");

            _context.AppMessages.Add(new AppMessage { UserId = user.Id, Module = "System", Type = "success", Message = "Database er oprettet", AutoClose = 5000, State = 0 });
            _context.AppMessages.Add(new AppMessage { UserId = user.Id, Module = "System", Type = "success", Message = "Administrator bruger er oprettet", AutoClose = 5000, State = 0 });
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", "Account");
        }
    }
}