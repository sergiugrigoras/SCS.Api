﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SCS.Api.Models;
using SCS.Api.Services;
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
        private readonly IFsoService _fsoService;
        private readonly IUserService _userService;
        private readonly string _storageUrl;
        private readonly string _storageSize;

        public FsoController(AppDbContext userContext, IConfiguration configuration, IFsoService fsoService, IUserService userService)
        {
            this._context = userContext ?? throw new ArgumentNullException(nameof(userContext));
            this._storageUrl = configuration.GetValue<string>("Storage:url");
            this._storageSize = configuration.GetValue<string>("Storage:size");
            _fsoService = fsoService;
            _userService = userService;
        }

        [HttpGet("getuserdrive")]
        public async Task<IActionResult> GetUserDriveIdAsync()
        {
            var user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);

            if (user == null)
            {
                return Unauthorized();
            }
            else
            {
                var fso = await _fsoService.GetFsoByIdAsync((int)user.DriveId);
                var fsoDTO = _fsoService.ToDTO(fso);
                return Ok(fsoDTO);
            }
        }

        [HttpGet("getuserdiskinfo")]
        public async Task<IActionResult> GetUserDiskInfo()
        {
            var user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);

            if (user == null)
            {
                return Unauthorized();
            }
            else
            {
                var usedBytes = await _fsoService.GetFsoSizeAsync((int)user.DriveId);
                var totalBytes = long.Parse(_storageSize);
                var diskUsed = Math.Round(usedBytes * 100.0 / totalBytes);
                return Ok(new { usedBytes = usedBytes.ToString(), totalBytes = totalBytes.ToString(), diskUsed = diskUsed.ToString() });
            }
        }
        
        [HttpGet("fullpath/{id}")]
        public async Task<IActionResult> GetFsoFullPathAsync(int id)
        {
            var fso = await _fsoService.GetFsoByIdAsync(id);
            var user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);
            if (fso == null)
            {
                return NotFound();
            }
            if (! await _fsoService.CheckOwnerAsync(fso,user))
            {
                return Forbid();
            }

            var list = await _fsoService.GetFsoFullPathAsync(fso);
            var listDTO = _fsoService.ToDTO(list);
            return new JsonResult(listDTO);
        }

        [HttpGet("folder/{id}")]
        public async Task<IActionResult> GetFolderContentAsync(int id)
        {
            FileSystemObject fso = await _fsoService.GetFsoByIdAsync(id);
            User user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);
            if (fso == null)
            {
                return NotFound();
            }

            if (! await _fsoService.CheckOwnerAsync(fso, user))
            {
                return Forbid();
            }

            if (!fso.IsFolder)
            {
                return BadRequest("Not a folder");
            }
            else
            {
                var content = await _fsoService.GetFsoContentAsync(fso);
                var listDTO = _fsoService.ToDTO(content);
                return new JsonResult(listDTO);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAsync(int id)
        {
            FileSystemObject fso = await _context.FileSystemObjects.FindAsync(id);
            User user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);
            if (fso == null)
            {
                return NotFound();
            }
            if (!await _fsoService.CheckOwnerAsync(fso,user))
            {
                return Forbid();
            }

            return Ok(_fsoService.ToDTO(fso));
        }

        [HttpPost("addfolder")]
        public async Task<IActionResult> AddAsync([FromBody] FileSystemObject fso)
        {
            User user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);
            FileSystemObject parent = await _fsoService.GetFsoByIdAsync((int)fso.ParentId);
            if (fso == null || !fso.IsFolder)
            {
                return BadRequest();
            }
            if (! await _fsoService.CheckOwnerAsync(parent, user))
            {
                return Forbid();
            }
            else
            {
                fso.Date = DateTime.Now;
                await _fsoService.AddFsoAsync(fso);
                return Ok(_fsoService.ToDTO(fso));
            }
        }

        [HttpPut("rename")]
        public async Task<IActionResult> RenameAsync([FromBody] FileSystemObject request)
        {
            FileSystemObject fso = await _fsoService.GetFsoByIdAsync(request.Id);
            User user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);

            if (fso == null)
            {
                return NotFound();
            }
            if (!await _fsoService.CheckOwnerAsync(fso, user))
            {
                return Forbid();
            }
            await _fsoService.UpdateFsoAsync(request);
            return Ok();
        }


        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAsync(string fsoIdcsv)
        {
            if (string.IsNullOrEmpty(fsoIdcsv))
            {
                return BadRequest();
            }
            var user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);
            string[] fsoIdArr = fsoIdcsv.Split(',');

            foreach (var fsoId in fsoIdArr)
            {
                var fso = await _fsoService.GetFsoByIdAsync(int.Parse(fsoId));
                if (await _fsoService.CheckOwnerAsync(fso,user))
                {
                    await _fsoService.DeleteFsoAsync(fso, user);
                }
            }
            return Ok();
        }

        [HttpPost("upload"), DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAsync()
        {
            var parentId = Request.Form["rootId"];
            var root = await  _fsoService.GetFsoByIdAsync(int.Parse(parentId));
            var user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);
            if (!await _fsoService.CheckOwnerAsync(root,user))
            {
                return Forbid();
            }
            try
            {
                var files = Request.Form.Files;
                var result = new List<FsoDTO>();
                foreach (var file in files)
                {
                    var fileName = await _fsoService.CreateFileAsync(file,user);
                    var fsoName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    var fso = await _fsoService.CreateFsoAsync(fsoName,fileName,file.Length,false,root.Id);
                    result.Add(_fsoService.ToDTO(fso));
                }
                return new JsonResult(result);

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error : {ex}");
            }
        }

        [HttpPost("download")]
        public async Task<IActionResult> DownloadAsync()
        {
            var fsoIdcsv = Request.Form["fsoIdcsv"].ToString();
            var rootId = int.Parse(Request.Form["rootId"].ToString());
            var root = await _fsoService.GetFsoByIdAsync(rootId);
            var user = await _userService.GetUserFromPrincipalAsync(HttpContext.User);

            if (string.IsNullOrEmpty(fsoIdcsv))
            {
                return BadRequest();
            }
            if (! await _fsoService.CheckOwnerAsync(root, user))
            {
                return Forbid();
            }

            int[] fsoIdArray = Array.ConvertAll(fsoIdcsv.Split(','), int.Parse);
            var fsoList = await _fsoService.GetFsoListByIdAsync(fsoIdArray);
            string contentType;
            if (fsoList.Count == 1 && !fsoList[0].IsFolder)
            {
                contentType = _fsoService.GetMimeType(Path.GetExtension(fsoList[0].Name));
            }
            else
            {
                contentType = _fsoService.GetMimeType(".zip");
            }
            var extension = Path.GetExtension(fsoList[0].Name);

            var result = await _fsoService.GetFileAsync(root, fsoList, user);

            return File(result, contentType);

        }
    }
}
