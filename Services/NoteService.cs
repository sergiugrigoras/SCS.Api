using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SCS.Api.Models;

namespace SCS.Api.Services
{
    public interface INoteService
    {
        Task<List<Note>> GetAllNotesByUserAsync(User user);
        Task<Note> GetNoteByIdAsync(int id);
        NoteDTO ToDTO(Note note);
        List<NoteDTO> ToDTO(List<Note> notes);
        Task CreateNoteAsync(Note note, User user);
        Task UpdateNoteAsync(Note note, string title, string body);
        Task DeleteNoteAsync(Note note);
        Task<string> AddShareKeyAsync(Note note);

    }
    public class NoteService : INoteService
    {
        private readonly AppDbContext _context;
        public NoteService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> AddShareKeyAsync(Note note)
        {
            var randomNumber = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            var shareKey = Convert.ToBase64String(randomNumber).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            note.ShareKey = shareKey;
            _context.Notes.Update(note);
            await _context.SaveChangesAsync();

            return note.ShareKey;
        }

        public async Task CreateNoteAsync(Note note, User user)
        {
            note.CreationDate = DateTime.Now;
            note.ModificationDate = DateTime.Now;
            note.UserId = user.Id;
            await _context.Notes.AddAsync(note);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteNoteAsync(Note note)
        {
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Note>> GetAllNotesByUserAsync(User user)
        {
            var notes = await _context.Notes.Where(n => n.UserId == user.Id).OrderByDescending(c => c.ModificationDate).ToListAsync();
            return notes;
        }

        public async Task<Note> GetNoteByIdAsync(int id)
        {
            var note = await _context.Notes.FindAsync(id);
            return note;
        }

        public NoteDTO ToDTO(Note note)
        {
            return new NoteDTO(note);
        }

        public List<NoteDTO> ToDTO(List<Note> notes)
        {
            var result = new List<NoteDTO>();
            foreach (var n in notes)
            {
                result.Add(new NoteDTO(n));
            }
            return result;
        }

        public async Task UpdateNoteAsync(Note note, string title, string body)
        {
            note.Title = title;
            note.Body = body;
            note.ModificationDate = DateTime.Now;
            _context.Notes.Update(note);
            await _context.SaveChangesAsync();
        }
    }
}