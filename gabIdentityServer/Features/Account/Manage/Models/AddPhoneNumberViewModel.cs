using System.ComponentModel.DataAnnotations;

namespace gabIdentityServer.Features.Account
{
    public class AddPhoneNumberViewModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; internal set; }
    }
}