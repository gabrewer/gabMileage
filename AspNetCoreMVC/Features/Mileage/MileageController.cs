using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using gabMileage.AspNetCoreMVC.Features.Mileage.Models;
using NodaTime;
using System.Text;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace gabMileage.AspNetCoreMVC.Controllers
{
    public class MileageController : Controller
    {
        const string mileageApiUrl = "http://localhost:47853/mileage";

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RecordMileage(MileageViewModel mileage)
        {
            Mileage mileageContent = new Mileage()
            {
                FilledAt = new DateTime(mileage.FilledDate.Year, mileage.FilledDate.Month, mileage.FilledDate.Day, mileage.FilledTime.Hour, mileage.FilledTime.Minute, mileage.FilledTime.Second),
                StartingMileage = mileage.StartingMileage,
                EndingMileage = mileage.EndingMileage,
                Gallons = mileage.Gallons,
                Cost = mileage.Cost,
                Station = mileage.Station
            };

            var accessToken = await HttpContext.Authentication.GetTokenAsync("access_token");
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var content = new StringContent(JsonConvert.SerializeObject(mileageContent), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(mileageApiUrl, content);

            return View("Index");
        }

        [Authorize]
        public async Task<IActionResult> CallApi()
        {
            var accessToken = await HttpContext.Authentication.GetTokenAsync("access_token");
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetStringAsync("http://localhost:47853/mileage");
            ViewBag.Json = JArray.Parse(response).ToString();

            return View("Index");
        }
    }
}
