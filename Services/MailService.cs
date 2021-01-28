using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SCS.Api.Services
{
    public interface IMailService
    {
        void SendEmail(MailAddress toEmailAddress, MailAddress fromEmailAddress, string emailSubject, string emailBody);
    }
    public class MailService : IMailService
    {
        public void SendEmail(MailAddress toEmailAddress, MailAddress fromEmailAddress, string emailSubject, string emailBody)
        {
            MailMessage message = new MailMessage(fromEmailAddress, toEmailAddress);
            message.Subject = emailSubject;
            message.Body = emailBody;

            SmtpClient client = new SmtpClient("localhost", 25);
            try
            {
                client.Send(message);
            }
            catch (SmtpException ex)
            {
                throw ex;
            }
        }
    }

    public class DevMailService : IMailService
    {
        private readonly string _rootDir;
        public DevMailService(string rootDir)
        {
            _rootDir = rootDir;
        }
        public void SendEmail(MailAddress toEmailAddress, MailAddress fromEmailAddress, string emailSubject, string emailBody)
        {
            var pathToSave = Path.Combine(_rootDir, "mails", "outbox");
            if (!Directory.Exists(pathToSave))
            {
                Directory.CreateDirectory(pathToSave);
            }
            File.WriteAllText(Path.Combine(pathToSave, toEmailAddress.Address.Replace("@", "_at_")), $"{emailSubject}\n{emailBody}");
        }
    }
}
