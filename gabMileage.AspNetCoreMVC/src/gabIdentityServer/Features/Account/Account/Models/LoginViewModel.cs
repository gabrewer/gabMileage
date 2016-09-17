using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace gabIdentityServer.Features.Account
{
    public class LoginViewModel : LoginInputModel
    {
        public LoginViewModel(HttpContext httpContext)
        {
            ExternalProviders = httpContext.Authentication.GetAuthenticationSchemes()
                .Where(x => x.DisplayName != null)
                .Select(x => new ExternalProvider
                {
                    DisplayName = x.DisplayName,
                    AuthenticationScheme = x.AuthenticationScheme
                });
        }

        public LoginViewModel(HttpContext httpContext, LoginInputModel other)
            : this(httpContext)
        {
            UserName = other.UserName;
            Password = other.Password;
            RememberMe = other.RememberMe;
            ReturnUrl = other.ReturnUrl;
        }

        public string ErrorMessage { get; set; }
        public IEnumerable<ExternalProvider> ExternalProviders { get; set; }
    }

    public class ExternalProvider
    {
        public string DisplayName { get; set; }
        public string AuthenticationScheme { get; set; }
    }
}
