using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Server;
using Newtonsoft.Json.Linq;

namespace Microsoft.Python.LanguageServer.Controllers 
{
    [ApiController]
    [Route("api")]
    public class RestApiController : ControllerBase 
    {
        private static IServiceManager _services;
        private readonly RestLanguageServer _restLanguageServer;

        public RestApiController()
        {
            _services = RestScope.Services; 
            _restLanguageServer = new RestLanguageServer(_services);
        }

        [HttpPost("initialize")]
        public async Task<ActionResult<InitializeResult>> Initialize(InitializeParams initializeParams) 
        {
            return await _restLanguageServer.Initialize(initializeParams);
        }
      

        [HttpPost("init")]
        public string Init(InitializeParams p) 
        {
            return "post";
        }
        
        
        [HttpGet("users")]
        public string GetUsers()
        {
            return "bla";
        }
    }
}
