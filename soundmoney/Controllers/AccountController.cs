using Microsoft.AspNetCore.Mvc;
using SoundMoney.Models;

namespace SoundMoney.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in, redirect straight to Screener
            if (HttpContext.Session.GetString("UserEmail") != null)
            {
                return RedirectToAction("Index", "Screener");
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Authentication Check
            if (model.Username == "founder.technops@gmail.com" && model.Password == "admin")
            {
                // Store User details in Session
                HttpContext.Session.SetString("UserEmail", model.Username);
                HttpContext.Session.SetString("IsLoggedIn", "true");

                return RedirectToAction("Index", "Screener");
            }

            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            // Clear and erase session data completely
            HttpContext.Session.Clear();
            return RedirectToAction("Index","Screener");
        }

        [HttpGet]
        public IActionResult Users()
        {
            // Sample Mock Data
            var users = new List<UserViewModel>
            {
                new UserViewModel { Id = 1, Name = "Alex Morgan", Email = "alex.morgan@soundmoney.com", Role = "Administrator", Status = "Active", LastLogin = DateTime.Now.AddHours(-2) },
                new UserViewModel { Id = 2, Name = "Sarah Jenkins", Email = "s.jenkins@soundmoney.com", Role = "Analyst", Status = "Active", LastLogin = DateTime.Now.AddDays(-1) },
                new UserViewModel { Id = 3, Name = "Michael Chen", Email = "m.chen@soundmoney.com", Role = "Subscriber", Status = "Pending", LastLogin = DateTime.Now.AddDays(-5) },
                new UserViewModel { Id = 4, Name = "Elena Rostova", Email = "e.rostova@soundmoney.com", Role = "Analyst", Status = "Inactive", LastLogin = DateTime.Now.AddMonths(-1) }
            };

            return View(new AccountDashboardViewModel { Users = users });
        }
    }
}