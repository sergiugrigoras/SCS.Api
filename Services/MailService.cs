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
        private readonly IHostEnvironment _environment;
        public MailService(IHostEnvironment env)
        {
            _environment = env;
        }
        public void SendEmail(MailAddress toEmailAddress, MailAddress fromEmailAddress, string emailSubject, string emailBody)
        {
            if (_environment.IsProduction())
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

            if (_environment.IsDevelopment())
            {
                var pathToSave = Path.Combine(_environment.ContentRootPath, "mails", "outbox");
                if (!Directory.Exists(pathToSave))
                {
                    Directory.CreateDirectory(pathToSave);
                }
                File.WriteAllText(Path.Combine(pathToSave, toEmailAddress.Address.Replace("@", "_at_")),$"{emailSubject}\n{emailBody}");
            }
        }
    }
}
