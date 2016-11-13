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
        public async Task<IActionResult> Index()
        {
            var vm = await createViewModel();
            return View(vm);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RecordMileage(MileageViewModel viewModel)
        {
            MileageFormViewModel mileage = viewModel.Form;

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            Mileage mileageContent = new Mileage()
            {
                FilledAt = new DateTime(mileage.FilledDate.Year, mileage.FilledDate.Month, mileage.FilledDate.Day, mileage.FilledTime.Hour, mileage.FilledTime.Minute, mileage.FilledTime.Second),
                StartingMileage = mileage.StartingMileage,
                EndingMileage = mileage.EndingMileage,
                Gallons = mileage.Gallons,
                Cost = mileage.Cost,
                Station = mileage.Station
            };


            var client = getHttpClient();
            var content = new StringContent(JsonConvert.SerializeObject(mileageContent), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(mileageApiUrl, content);

            return RedirectToAction("Success");
        }

        public async Task<IActionResult> Success()
        {
            var vm = await createViewModel();
            return View("Index", vm);
        }

        private async Task<MileageViewModel> createViewModel()
        {
            ModelState.Clear();
            var vm = new MileageViewModel();
            vm.MileageRecords = await getMileageRecords();
            return vm;
        }

        private async Task<List<Mileage>> getMileageRecords()
        {
            var client = getHttpClient();
            var response = await client.GetStringAsync(mileageApiUrl);
            return JsonConvert.DeserializeObject<List<Mileage>>(response);
        }

        private HttpClient getHttpClient()
        {
            var accessToken = HttpContext.Authentication.GetTokenAsync("access_token").Result;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
