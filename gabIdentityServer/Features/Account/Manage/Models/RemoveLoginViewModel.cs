namespace gabIdentityServer.Features.Account
{
    public class RemoveLoginViewModel
    {
        public string LoginProvider { get; internal set; }
        public string ProviderKey { get; internal set; }
    }
}