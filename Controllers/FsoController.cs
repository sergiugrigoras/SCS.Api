using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SCS.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;


namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FsoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private string _storageUrl;

        public FsoController(AppDbContext userContext, IConfiguration configuration)
        {
            this._context = userContext ?? throw new ArgumentNullException(nameof(userContext));
            this._storageUrl = configuration.GetValue<string>("Storage:url");
        }

        [HttpGet("getuserdrive")]
        public async Task<IActionResult> GetUserDriveIdAsync()
        {
            var user = _context.Users.Find(GetJti());

            if (user == null)
            {
                return NotFound();
            }
            else
            {
                var fso = await _context.FileSystemObjects.FindAsync(user.DriveId);
                return Ok(new Fso(fso));
            }
        }
        [HttpGet("fullpath/{id}")]
        public async Task<IActionResult> GetFullPathAsync(int id)
        {
            FileSystemObject fso = await _context.FileSystemObjects.FindAsync(id);
            if (fso == null)
            {
                return NotFound();
            }
            if (!await IsOwnerAsync(id))
            {
                return Forbid();
            }

            var parser = fso;
            var fullPathList = new List<Fso>();
            while (parser != null)
            {
                fullPathList.Insert(0, new Fso(parser));
                parser = await _context.FileSystemObjects.FindAsync(parser.ParentId);
            }
            return new JsonResult(fullPathList);
        }
        [HttpGet("folder/{id}")]
        public async Task<IActionResult> GetFolderContentAsync(int id)
        {
            FileSystemObject fso = await _context.FileSystemObjects.FindAsync(id);
            if (fso == null)
            { 
                return NotFound();
            }

            if (! await IsOwnerAsync(id))
            {
                return Forbid();
            }
            
            if (fso.IsFolder)
            {
                var result = await _context.FileSystemObjects.Where(f => f.ParentId == fso.Id).ToListAsync();
                return new JsonResult(result.Select(f => new Fso(f)));
            }
            else
            {
                return BadRequest("Not a folder");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAsync(int id)
        {
            FileSystemObject fso = await _context.FileSystemObjects.FindAsync(id);
            if (fso == null)
            {
                return NotFound();
            }
            if (!await IsOwnerAsync(id))
            {
                return Forbid();
            }
            
            return Ok(new Fso(fso));

        }

        [HttpPost("addfolder")]
        public async Task<IActionResult> AddAsync([FromBody] FileSystemObject fso)
        {
            if (fso.IsFolder && await IsOwnerAsync((int)fso.ParentId))
            {
                fso.Date = DateTime.Now;
                await _context.FileSystemObjects.AddAsync(fso);
                await _context.SaveChangesAsync();
                return Ok(new Fso(fso));
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPut("rename")]
        public async Task<IActionResult> RenameAsync([FromBody] FileSystemObject newFso)
        {
            FileSystemObject fso = await _context.FileSystemObjects.FindAsync(newFso.Id);
            if (fso != null)
            {
                if (!await IsOwnerAsync(fso.Id))
                {
                    return Forbid();
                }
                fso.Name = newFso.Name;
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }


        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAsync(string fsoIdcsv)
        {
            if (fsoIdcsv == null || fsoIdcsv == "")
            {
                return BadRequest();
            }
            string[] fsoIdArr = fsoIdcsv.Split(',');
            foreach (var fsoId in fsoIdArr)
            {
                if (await IsOwnerAsync(int.Parse(fsoId)))
                {
                    await DeleteFsoAsync(int.Parse(fsoId));
                }
            }

            return Ok();
        }

        [HttpPost("upload"), DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAsync()
        {
            var parentId = Request.Form["rootId"];
            if (!await IsOwnerAsync(int.Parse(parentId)))
            {
                return Forbid();
            }
            try
            {
                var files = Request.Form.Files;
                /*                if (files.Any(f => f.Length == 0))
                                {
                                    return BadRequest();
                                }*/
                

                var pathToSave = Path.Combine(_storageUrl, GetJti());
                if (!Directory.Exists(pathToSave))
                {
                    Directory.CreateDirectory(pathToSave);
                }

                List<Fso> fsoList = new List<Fso>();
                foreach (var file in files)
                {
                    var name = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    var fileName = Guid.NewGuid().ToString();
                    var fullPath = Path.Combine(pathToSave, fileName);

                    using (var stream = System.IO.File.Create(fullPath))
                    {
                        await file.CopyToAsync(stream);
                        FileSystemObject fso = new FileSystemObject();
                        fso.Name = name;
                        fso.FileName = fileName;
                        fso.FileSize = file.Length;
                        fso.ParentId = int.Parse(parentId);
                        fso.Date = DateTime.Now;
                        fso.IsFolder = false;
                        await _context.FileSystemObjects.AddAsync(fso);
                        await _context.SaveChangesAsync();
                        fsoList.Add(new Fso(fso));
                    }
                }
                return new JsonResult(fsoList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error : {ex}");
            }
        }

        [HttpPost("download")]
        public async Task<Stream> DownloadAsync()
        {
            var fsoIdcsv = Request.Form["fsoIdcsv"].ToString();
            var rootId = int.Parse(Request.Form["rootId"].ToString());

            if (fsoIdcsv == null || fsoIdcsv == "" || !await IsOwnerAsync(rootId))
            {
                BadRequest();
            }

            string[] fsoIdArr = fsoIdcsv.Split(',');
            List<FileSystemObject> fsoList = new List<FileSystemObject>();
            foreach (var fsoId in fsoIdArr)
            {
                var fso = await _context.FileSystemObjects.FirstOrDefaultAsync(f => f.Id == int.Parse(fsoId));
                fsoList.Add(fso);
            }

            if (fsoList.Count == 1 && !fsoList[0].IsFolder)
            {
                var fullPath = Path.Combine(Path.Combine(_storageUrl, GetJti()), fsoList[0].FileName);
                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            }
            else
            {
                var ms = new MemoryStream();
                ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Create, true);
                foreach (var fso in fsoList)
                {
                    await AddFsoToArchiveAsync(archive, fso, rootId);
                }
                archive.Dispose();
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
        private async Task AddFsoToArchiveAsync(ZipArchive archive, FileSystemObject fso, int rootDirId)
        {
            string fsoPath = String.Empty;
            var parseObj = await _context.FileSystemObjects.FindAsync(fso.ParentId);
            while (parseObj.Id != rootDirId)
            {
                fsoPath = fsoPath.Insert(0, parseObj.Name + "/");
                parseObj.Parent = await _context.FileSystemObjects.FindAsync(parseObj.ParentId);
                parseObj = parseObj.Parent;
            }

            if (!fso.IsFolder)
            {
                var fullPath = Path.Combine(Path.Combine(_storageUrl, GetJti()), fso.FileName);
                archive.CreateEntryFromFile(fullPath, fsoPath + fso.Name, CompressionLevel.Optimal);
            }
            else
            {
                archive.CreateEntry(fsoPath + fso.Name + "/");
                foreach (var c in await GetFsoContentAsync(fso))
                {
                    await AddFsoToArchiveAsync(archive, c, rootDirId);
                }
            }
        }

        public class Fso
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int? ParentId { get; set; }
            public bool IsFolder { get; set; }
            public string FileName { get; set; }
            public long? FileSize { get; set; }
            public DateTime Date { get; set; }

            public Fso(FileSystemObject fso)
            {
                Id = fso.Id;
                Name = fso.Name;
                ParentId = fso.ParentId;
                IsFolder = fso.IsFolder;
                FileName = fso.FileName;
                FileSize = fso.FileSize;
                Date = fso.Date;
            }
        }
        private async Task DeleteFsoAsync(int id)
        {
            var fso = await _context.FileSystemObjects.FindAsync(id);
            if (!fso.IsFolder)
            {
                var fullPath = Path.Combine(Path.Combine(_storageUrl, GetJti()),fso.FileName);
                _context.FileSystemObjects.Remove(fso);
                await _context.SaveChangesAsync();
                DeleteFile(fullPath);
            }
            else
            {
                var fsoContent = await _context.FileSystemObjects.Where(f => f.ParentId == fso.Id).ToListAsync();
                foreach (var f in fsoContent)
                {
                    await DeleteFsoAsync(f.Id);
                }
                _context.FileSystemObjects.Remove(fso);
                await _context.SaveChangesAsync();
            }
        }

        private void DeleteFile(string fullPath)
        {
            if (System.IO.File.Exists(fullPath))
            {
                try
                {
                    System.IO.File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

        private async Task<bool> IsOwnerAsync(int id)
        {
            var user = await _context.Users.FindAsync(GetJti());

            var fso = await _context.FileSystemObjects.FindAsync(id);
            while (fso.ParentId != null)
            { 
                fso = await _context.FileSystemObjects.FindAsync(fso.ParentId);
            }
            if (fso.Id == user.DriveId)
                return true;

            return false;
        }

        private async Task<List<FileSystemObject>> GetFsoContentAsync(FileSystemObject fso)
        {
            return await _context.FileSystemObjects.Where(f => f.ParentId == fso.Id).ToListAsync();
        }

        private string GetJti()
        {
            return HttpContext.User.FindFirst(JwtRegisteredClaimNames.Jti).Value;
        }
    }
}

