using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public ShareController(IFsoService fsoService, IUserService userService, IShareService shareService)
        {
            _fsoService = fsoService ?? throw new ArgumentNullException(nameof(fsoService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
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
            var share = await _shareService.GetShareById(id);
            var sharedObjectsList = await _shareService.GetShareContentAsync(share);
            var fsoList = await _fsoService.GetFsoListByIdAsync(sharedObjectsList.Select(x => x.FsoId).ToArray());

            return Ok(_fsoService.ToDTO(fsoList));
        }

        [HttpGet("download/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadAsync(string id)
        {
            var share = await _shareService.GetShareById(id);
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
    }
}