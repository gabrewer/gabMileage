using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace gabIdentityServer.Features.Manage
{
    public class ManageLoginsViewModel
    {
        public IList<UserLoginInfo> CurrentLogins { get; set; }
        public object OtherLogins { get; set; }
    }
}