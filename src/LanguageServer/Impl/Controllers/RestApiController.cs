using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Python.LanguageServer.Controllers 
{
    
    [ApiController]
    [Route("api")]
    public class RestApiController : ControllerBase 
    {
        [HttpGet]
        public string GetUsers()
        {
            return "bla";
        }
    }
}
