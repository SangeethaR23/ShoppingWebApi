using System.Security.Claims;

namespace ShoppingWebApi.Interfaces
{

        public interface ITokenService
        {
            string CreateAccessToken(IEnumerable<Claim> claims, out DateTime expiresAtUtc);

        }
    
}
