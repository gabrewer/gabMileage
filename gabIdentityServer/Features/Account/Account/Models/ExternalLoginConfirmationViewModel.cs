using System.ComponentModel.DataAnnotations;

namespace gabIdentityServer.Features.Account
{
    public class ExternalLoginConfirmationViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}