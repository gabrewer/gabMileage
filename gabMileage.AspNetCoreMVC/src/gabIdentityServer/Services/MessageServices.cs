using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gabIdentityServer.Services
{
    public class MessageServices : IEmailSender, ISmsSender
    {
        public Task SendEmailAsync(string email, string subject, string message)
        {
            // TODO: implement email service.  for now nop
            return Task.FromResult(0);
        }

        public Task SendSmsAsync(string number, string message)
        {
            // TODO: implement email service.  for now nop
            return Task.FromResult(0);
        }
    }
}
