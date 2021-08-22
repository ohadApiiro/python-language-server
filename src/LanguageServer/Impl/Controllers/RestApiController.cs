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
        private static readonly RestLanguageServer _restLanguageServer;

        static RestApiController()
        {
            _services = RestScope.Services; 
            _restLanguageServer = new RestLanguageServer(_services);
        }

        [HttpPost("initialize")]
        public async Task<ActionResult<InitializeResult>> Initialize(InitializeParams initializeParams) 
        {
            return await _restLanguageServer.Initialize(initializeParams);
        }

        [HttpPost("initialized")]
        public async Task Initialized(InitializedParams initializedParams, CancellationToken cancellationToken) 
        {
            await _restLanguageServer.Initialized(initializedParams);
        }

        [HttpPost("hover")]
        public async Task<Hover> Hover(TextDocumentPositionParams positionParams) 
        {
            return await _restLanguageServer.Hover(positionParams);
        }

        [HttpPost("didOpen")]
        public async Task DidOpenTextDocument(DidOpenTextDocumentParams openParams) 
        {
            await _restLanguageServer.DidOpen(openParams);
        }

        [HttpPost("declaration")]
        public async Task<Location> GotoDeclaration(TextDocumentPositionParams positionParams) {
            return await _restLanguageServer.GotoDeclaration(positionParams);
        }

        public async Task<Reference[]> GotoDefinition(TextDocumentPositionParams positionParams) 
        {
            return await _restLanguageServer.GotoDefinition(positionParams);
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
