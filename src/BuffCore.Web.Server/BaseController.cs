using Microsoft.AspNetCore.Mvc;

namespace BuffCore.Web.Server
{
    /// <summary>
    /// Base class for versioned API controllers. Applies the <c>api/v1/[controller]</c> route
    /// convention and ApiController behaviors to every derived controller.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    public abstract class BaseController : Controller
    {
    }
}
