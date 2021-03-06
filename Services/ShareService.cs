using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SCS.Api.Models;

namespace SCS.Api.Services
{
    public interface IShareService
    {
        Task<string> CreateShareAsync(List<FileSystemObject> fsoList, User user);
        Task<List<SharedObject>> GetShareContentAsync(Share share);
        Task<Share> GetShareByIdAsync(string id);
        Task<List<Share>> GetSharesByUserAsync(User user);
        Task DeleteShareAsync(Share share);

        ICollection<ShareDTO> ToDTO(ICollection<Share> shareList);
        ShareDTO ToDTO(Share share);

    }
    public class ShareService : IShareService
    {
        private readonly AppDbContext _context;
        public ShareService(AppDbContext context)
        {
            _context = context;
        }
        public async Task<string> CreateShareAsync(List<FileSystemObject> fsoList, User user)
        {
            var randomNumber = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            var key = Convert.ToBase64String(randomNumber).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var share = new Share();
            share.PublicId = key;
            share.UserId = user.Id;
            share.ShareDate = DateTime.Now;
            _context.Shares.Add(share);
            await _context.SaveChangesAsync();

            foreach (var fso in fsoList)
            {
                var sharedObject = new SharedObject
                {
                    ShareId = share.Id,
                    FsoId = fso.Id
                };
                share.SharedObjects.Add(sharedObject);
            }

            await _context.SaveChangesAsync();
            return key;
        }

        public async Task DeleteShareAsync(Share share)
        {
            _context.Shares.Remove(share);
            await _context.SaveChangesAsync();
        }

        public async Task<Share> GetShareByIdAsync(string id)
        {
            var share = await _context.Shares.FirstOrDefaultAsync(s => s.PublicId == id);
            return share;
        }

        public async Task<List<SharedObject>> GetShareContentAsync(Share share)
        {

            var list = await _context.SharedObjects.Where(s => s.ShareId == share.Id).ToListAsync();
            return list;
        }

        public async Task<List<Share>> GetSharesByUserAsync(User user)
        {
            var shareList = await _context.Shares.Where(s => s.UserId == user.Id).ToListAsync();
            return shareList;
        }

        public ICollection<ShareDTO> ToDTO(ICollection<Share> shareList)
        {
            var result = new List<ShareDTO>();
            foreach (var share in shareList)
            {
                result.Add(new ShareDTO(share));
            }
            return result;
        }

        public ShareDTO ToDTO(Share share)
        {
            return new ShareDTO(share);
        }
    }
}