using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using gabMileage.WebApi.Models;

namespace gabMileage.WebApi.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class MileageController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value });
            return new JsonResult(claims);
        }

        [HttpGet("{Id}")]
        public IActionResult GetById(int Id)
        {
            var mileage = new Mileage();
            return new JsonResult(mileage);
        }

        [HttpPost]
        public IActionResult Post([FromBody] Mileage mileage)
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value });

            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Data");
            }

            var url = Url.RouteUrl("GetById", new { Id = mileage.Id }, Request.Scheme, Request.Host.ToUriComponent());
            return Created(url, mileage);
        }
    }
}
