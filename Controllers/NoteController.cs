using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCS.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NoteController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NoteController(AppDbContext userContext)
        {
            this._context = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetNotesAsync()
        {
            var user = await _context.Users.FindAsync(GetJti());
            if (user == null)
            {
                return Unauthorized();
            }
            var notes = await _context.Notes.Where(n => n.UserId == user.Id).OrderByDescending(c => c.ModificationDate).ToListAsync();
            var result = new List<NoteDTO>();
            foreach (var n in notes)
            {
                result.Add(new NoteDTO(n));
            }
            return new JsonResult(result);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddNote([FromBody] Note note)
        {
            note.CreationDate = DateTime.Now;
            note.ModificationDate = DateTime.Now;
            note.UserId = GetJti();
            await _context.Notes.AddAsync(note);
            await _context.SaveChangesAsync();
            return Ok(new NoteDTO(note));
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateNote([FromBody] Note newNote)
        {
            var note =await _context.Notes.FindAsync(newNote.Id);
            if (note == null)
            {
                return NotFound();
            }
            if (note.UserId != GetJti())
            {
                return Forbid();
            }
            note.ModificationDate = DateTime.Now;
            note.Title = newNote.Title;
            note.Body = newNote.Body;
            await _context.SaveChangesAsync();
            return Ok(new NoteDTO(note));
        }
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var note = await _context.Notes.FindAsync(id);
            if (note == null)
            {
                return NotFound();
            }
            if (note.UserId != GetJti())
            {
                return Forbid();
            }
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return Ok();
        }
        private string GetJti()
        {
            return HttpContext.User.FindFirst(JwtRegisteredClaimNames.Jti).Value;
        }

        public class NoteDTO
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public DateTime CreationDate { get; set; }
            public DateTime ModificationDate { get; set; }
            public string Color { get; set; }

            public NoteDTO(Note note)
            {
                this.Id = note.Id;
                this.Title = note.Title;
                this.Body = note.Body;
                this.CreationDate = note.CreationDate;
                this.ModificationDate = note.ModificationDate;
                this.Color = note.Color;
            }
        }
    }
}
