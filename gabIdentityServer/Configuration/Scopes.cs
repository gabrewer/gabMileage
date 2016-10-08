using IdentityServer4.Models;
using System.Collections.Generic;

namespace gabIdentityServer.Configuration
{
    public class Scopes
    {
        public static IEnumerable<Scope> Get()
        {
            return new List<Scope>
            {
                StandardScopes.OpenId,
                StandardScopes.Profile,

                new Scope
                {
                    Name = "gabMileageApi",
                    DisplayName = "GAB Mileage Api",
                    Description = "GAB Mileage Api features and data",
                }
            };
        }
    }
}

