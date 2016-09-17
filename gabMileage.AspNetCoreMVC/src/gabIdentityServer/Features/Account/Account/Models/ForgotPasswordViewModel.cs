using System.ComponentModel.DataAnnotations;

namespace gabIdentityServer.Features.Account
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; internal set; }
    }
}