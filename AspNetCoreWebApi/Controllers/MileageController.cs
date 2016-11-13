using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using gabMileage.WebApi.Models;
using gabMileage.WebApi.Data;
using AutoMapper;
using System.Security.Claims;

namespace gabMileage.WebApi.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class MileageController : ControllerBase
    {
        private MileageContext context;

        public MileageController(MileageContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public IActionResult GetAllMileageRecords()
        {
            var sub = getUserFromClaims(User.Claims);

            var milageRecords = context.MileageRecords.Where(m => m.User == sub).ToList();

            var mileageList = Mapper.Map<List<MileageRecord>, List<Mileage>>(milageRecords);

            return new JsonResult(mileageList);
        }

        [HttpGet("{Id}", Name ="GetMileageRecord")]
        public IActionResult GetMileageRecord(int Id)
        {
            var sub = getUserFromClaims(User.Claims);
            var milageRecord = context.MileageRecords.Where(m => m.User == sub && m.Id == Id).FirstOrDefault();

            var mileage = Mapper.Map<MileageRecord, Mileage>(milageRecord);
            return new JsonResult(mileage);
        }

        [HttpPost]
        public IActionResult RecordMileage([FromBody] Mileage mileage)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Data");
            }

            var sub = getUserFromClaims(User.Claims);
            var mileageRecord = Mapper.Map<Mileage, MileageRecord>(mileage);
            mileageRecord.User = sub;

            context.MileageRecords.Add(mileageRecord);
            context.SaveChanges();

            mileage.Id = mileageRecord.Id;

            return CreatedAtRoute("GetMileageRecord", new { controller = "Mileage", id = mileage.Id }, mileage);
        }

        private Guid getUserFromClaims(IEnumerable<Claim> claims)
        {
            var sub = claims.FirstOrDefault(c => c.Type == "sub").Value;
            return new Guid(sub);
        }
    }
}
