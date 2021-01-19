using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SCS.Api.Models;
using SCS.Api.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NoteController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly INoteService _noteService;

        public NoteController(AppDbContext context, IUserService userService, INoteService noteService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        }

        [HttpPost("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNoteAsync(int id, [FromBody] NoteShareKey shareKey)
        {
            var note = await _noteService.GetNoteByIdAsync(id);
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (note == null)
            {
                return NotFound();
            }

            if ((user == null && note.ShareKey == shareKey.Key) || (user != null && user.Id == note.UserId))
            {
                return new JsonResult(_noteService.ToDTO(note));
            }
            else
            {
                return Forbid();
            }
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetNotesAsync()
        {
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            var notes = await _noteService.GetAllNotesByUserAsync(user);

            return new JsonResult(_noteService.ToDTO(notes));
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddNote([FromBody] Note note)
        {
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            await _noteService.CreateNoteAsync(note, user);
            return Ok(_noteService.ToDTO(note));
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateNote([FromBody] Note request)
        {
            var note = await _noteService.GetNoteByIdAsync(request.Id);
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (note == null)
            {
                return NotFound();
            }
            if (note.UserId != user.Id)
            {
                return Forbid();
            }
            await _noteService.UpdateNoteAsync(note, request.Title, request.Body);

            return Ok(_noteService.ToDTO(note));
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var note = await _noteService.GetNoteByIdAsync(id);
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (note == null)
            {
                return NotFound();
            }
            if (note.UserId != user.Id)
            {
                return Forbid();
            }
            await _noteService.DeleteNoteAsync(note);
            return Ok();
        }

        [HttpPost("share")]
        public async Task<IActionResult> ShareNoteAsync([FromBody] Note sharedNote)
        {
            var note = await _noteService.GetNoteByIdAsync(sharedNote.Id);
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (note == null)
            {
                return NotFound();
            }
            if (note.UserId != user.Id)
            {
                return Forbid();
            }
            if (note.ShareKey == null)
            {
                await _noteService.AddShareKeyAsync(note);
            }
            return Ok(new { note.ShareKey });
        }
    }

}
