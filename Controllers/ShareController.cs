using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCS.Api.Models;
using SCS.Api.Services;

namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ShareController : ControllerBase
    {
        private readonly IFsoService _fsoService;
        private readonly IUserService _userService;
        private readonly IShareService _shareService;
        private readonly IMailService _mailService;
        public ShareController(IFsoService fsoService, IUserService userService, IShareService shareService, IMailService mailService)
        {
            _fsoService = fsoService ?? throw new ArgumentNullException(nameof(fsoService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
            _mailService = mailService ?? throw new ArgumentNullException(nameof(mailService));
        }

        [HttpPost("add")]
        public async Task<IActionResult> ShareAsync()
        {
            var fsoIdcsv = Request.Form["fsoIdcsv"].ToString();
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            int[] fsoIdArray = Array.ConvertAll(fsoIdcsv.Split(','), int.Parse);

            if (string.IsNullOrEmpty(fsoIdcsv))
            {
                return BadRequest();
            }
            var fsoList = await _fsoService.GetFsoListByIdAsync(fsoIdArray);
            foreach (var fso in fsoList)
            {
                if (!await _fsoService.CheckOwnerAsync(fso, user))
                {
                    return Forbid();
                }
            }

            var shareKey = await _shareService.CreateShareAsync(fsoList, user);

            return Ok(shareKey);
        }

        [HttpGet("get/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAsync(string id)
        {
            var share = await _shareService.GetShareByIdAsync(id);
            var sharedObjectsList = await _shareService.GetShareContentAsync(share);
            var fsoList = await _fsoService.GetFsoListByIdAsync(sharedObjectsList.Select(x => x.FsoId).ToArray());

            var fsoDTOList = _fsoService.ToDTO(fsoList).OrderBy(f => f.Name).OrderByDescending(f => f.IsFolder);
            foreach (var fsoDTO in fsoDTOList)
            {
                if (fsoDTO.IsFolder)
                {
                    await _fsoService.SetContentOfDTO(fsoDTO);
                }

            }

            return new JsonResult(fsoDTOList);
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAllAsync()
        {
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            var shareList = await _shareService.GetSharesByUserAsync(user);
            var shareDTOList = new List<ShareDTO>();
            foreach (var share in shareList)
            {
                var sharedObjectsList = await _shareService.GetShareContentAsync(share);
                var fsoList = await _fsoService.GetFsoListByIdAsync(sharedObjectsList.Select(x => x.FsoId).ToArray());

                var fsoDTOList = _fsoService.ToDTO(fsoList).OrderBy(f => f.Name).OrderByDescending(f => f.IsFolder);
                foreach (var fsoDTO in fsoDTOList)
                {
                    if (fsoDTO.IsFolder)
                    {
                        await _fsoService.SetContentOfDTO(fsoDTO);
                    }
                }
                var shareDTO = new ShareDTO(share);
                shareDTO.Content = fsoDTOList.ToList();
                shareDTOList.Add(shareDTO);
            }
            return new JsonResult(shareDTOList);
        }

        [HttpGet("getinfo/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInfoAsync(string id)
        {
            var share = await _shareService.GetShareByIdAsync(id);
            if (share == null)
            {
                return NotFound();
            }
            var user = await _userService.GetUserByIdAsync(share.UserId);
            var sharedObjectsList = await _shareService.GetShareContentAsync(share);
            var fsoList = await _fsoService.GetFsoListByIdAsync(sharedObjectsList.Select(x => x.FsoId).ToArray());
            var totalSize = (long)0;
            var filesCount = 0;
            var foldersCount = 0;

            foreach (var fso in fsoList)
            {
                totalSize += await _fsoService.GetFsoSizeByIdAsync(fso.Id);
                if (fso.IsFolder)
                {
                    foldersCount++;
                }
                else
                {
                    filesCount++;
                }
            }
            return Ok(new { user.Username, share.ShareDate, foldersCount, filesCount, totalSize });

        }
        [HttpGet("download/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadAsync(string id)
        {
            var share = await _shareService.GetShareByIdAsync(id);
            var user = await _userService.GetUserByIdAsync(share.UserId);
            var sharedObjectsList = await _shareService.GetShareContentAsync(share);
            var fsoList = await _fsoService.GetFsoListByIdAsync(sharedObjectsList.Select(x => x.FsoId).ToArray());
            var root = await _fsoService.CheckParentFso(fsoList);
            if (root == null)
            {
                return BadRequest("Invalid request");
            }
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

            var stream = await _fsoService.GetFileAsync(root, fsoList, user);

            return File(stream, contentType);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteShareAsync(string id)
        {
            var share = await _shareService.GetShareByIdAsync(id);
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (share == null)
            {
                return NotFound();
            }
            if (share.UserId != user.Id)
            {
                return Forbid();
            }

            await _shareService.DeleteShareAsync(share);
            return Ok(new { shareId = id });
        }

        [HttpPost("sendemail")]
        public async Task<IActionResult> SendEmailAsync(string id, string email, string url)
        {
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            _mailService.SendEmail(
                new MailAddress(email),
                new MailAddress("files@mail.sergiug.space", "SCS Files"),
                "SCS files share",
                $"Hello, \n\n{user.Username} has shared some files.\nYou can use below link to access them.\n\n{url}"
            );
            return Ok();
        }
    }
}