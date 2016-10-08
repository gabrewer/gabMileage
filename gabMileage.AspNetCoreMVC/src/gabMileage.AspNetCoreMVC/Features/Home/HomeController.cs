using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace gabMileage.AspNetCoreMVC.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "gabMileage - A testing ground for new development technology.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "GAB Software";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
