using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Server;

namespace Microsoft.Python.LanguageServer.Controllers 
{
    public class DumLogger 
    {
        public static void Log(string msg)
        {
            Console.WriteLine(msg);
            // if (Directory.Exists("/Lim.FeaturesExtractor.Unified"))
            //     File.AppendAllText("/Lim.FeaturesExtractor.Unified/dbg.txt", msg + Environment.NewLine);           
        }
    }
    
    [ApiController]
    [Route("api")]
    public class RestApiController : ControllerBase 
    {
        private static readonly RestLanguageServer _restLanguageServer;
        private static readonly string SERVER_READY = "ready";
        

        static RestApiController() {
            var services = RestScope.Services;
            _restLanguageServer = new RestLanguageServer(services);
        }
        
        [HttpGet("ready")]
        public ActionResult<string> Ready() {
            return SERVER_READY;
        }
        
        
        [HttpPost("initialize")]
        public async Task<ActionResult<InitializeResult>> Initialize(InitializeWrapperParams initializeParams)
        {
            return await _restLanguageServer.Initialize(initializeParams); 
        }

        [HttpPost("initialized")]
        public async Task Initialized(InitializedParams initializedParams, CancellationToken cancellationToken) 
        {
            await _restLanguageServer.Initialized(initializedParams);
        }

        [HttpPost("shutdown")]
        public async Task Shutdown() 
        {
            await _restLanguageServer.Shutdown();
        }

        [HttpPost("resolve")]
        public async Task<string> Resolve(TextDocumentPositionParams positionParams) 
        {
            var hoverResult = await _restLanguageServer.Hover(positionParams);
            if (hoverResult == null) 
            {
                return string.Empty;
            }
            
            var className = hoverResult.contents?.value ?? string.Empty;
            className = className.Split(Environment.NewLine)[0];

            Reference[] references = await _restLanguageServer.GotoDefinition(positionParams);
            if (references == null || references.Length == 0) 
            {
                return className;
            }
            var fileName = references[0]?.uri?.ToString();
            fileName ??= string.Empty;
            
            return $"{fileName}|{className}";
        }

        [HttpPost("hover")]
        public async Task<Hover> Hover(TextDocumentPositionParams positionParams) 
        {
            return  await _restLanguageServer.Hover(positionParams);
        }

        [HttpPost("didOpen")]
        public async Task DidOpenTextDocument(DidOpenTextDocumentParams openParams) 
        {
            await _restLanguageServer.DidOpen(openParams);
        }
        
        [HttpPost("didClose")]
        public async Task DidCloseTextDocument(DidCloseTextDocumentParams closeParams) 
        {
            await _restLanguageServer.DidClose(closeParams);
        }

        [HttpPost("declaration")]
        public async Task<Location> GotoDeclaration(TextDocumentPositionParams positionParams) 
        {
            return await _restLanguageServer.GotoDeclaration(positionParams);
        }
        
        [HttpPost("definition")]
        public async Task<Reference[]> GotoDefinition(TextDocumentPositionParams positionParams) 
        {
            return await _restLanguageServer.GotoDefinition(positionParams);
        }
    }
}
