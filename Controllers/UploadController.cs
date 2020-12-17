using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SCS.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        [HttpPost("files"), DisableRequestSizeLimit]
        public IActionResult Upload()
        {
            try
            {
                var files = Request.Form.Files;
             
                var folderName = Path.Combine("StaticFiles", "Images");
                var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);
                if (files.Any(f => f.Length == 0))
                {
                    return BadRequest();
                }
                foreach (var file in files)
                {
                    var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    var fullPath = Path.Combine(pathToSave, fileName);
                    var dbPath = Path.Combine(folderName, fileName); //you can add this path to a list and then return all dbPaths to the client if require
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error : {ex}");
            }
        }
        [HttpPost("data")]
        public IActionResult Data()
        {
            var name = Request.Form["name"];
            var email = Request.Form["email"];

            return Ok(new
            {
                name,
                email
            }) ;
        }

    }
}
