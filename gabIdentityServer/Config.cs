using IdentityServer4;
using IdentityServer4.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gabIdentityServer
{
    public class Config
    {
        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            };
        }

        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("gabMileageApi", "gabMileage API")
            };
        }

        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                new Client
                {
                    ClientId = "gabmileagemvc",
                    ClientName = "gabMileage MVC Client",
                    AllowedGrantTypes = GrantTypes.Hybrid,

                    RedirectUris = { "http://localhost:47834/signin-oidc" },
                    PostLogoutRedirectUris = { "http://localhost:47834/" },

                    ClientSecrets = new List<Secret>
                    {
                        new Secret("diVeTryCOner".Sha256())
                    },

                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        "gabMileageApi"
                    }
                }
            };
        }
    }
}
