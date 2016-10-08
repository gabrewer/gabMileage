using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace gabMileage.AspNetCoreMVC.Controllers
{
    public class AccountController : Controller
    {
        [Authorize]
        public ActionResult Login()
        {
            return RedirectToAction("Index", "Mileage");
        }

        public async Task Logout()
        {
            await HttpContext.Authentication.SignOutAsync("Cookies");
            await HttpContext.Authentication.SignOutAsync("oidc");
        }
    }
}
