using System.Security.Claims;
using doLittle.Security;
using Machine.Specifications;
using Moq;

namespace doLittle.Security.Specs.for_UserSecurityActor.given
{
    public class a_user_security_actor
    {
        protected static UserSecurityActor actor;
        protected static ClaimsIdentity identity;
        protected static ClaimsPrincipal principal;

        Establish context = () =>
        {
            identity = new ClaimsIdentity();
            principal = new ClaimsPrincipal(identity);

            var principalResolver = new Mock<ICanResolvePrincipal>();
            principalResolver.Setup(p => p.Resolve()).Returns(principal);

            actor = new UserSecurityActor(principalResolver.Object);
        };
    }
}