using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Python.Core.Text;
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
            if (Directory.Exists("/Lim.FeaturesExtractor.Unified"))
                File.AppendAllText("/Lim.FeaturesExtractor.Unified/dbg.txt", msg + Environment.NewLine);           
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
        public async Task<ActionResult<InitializeResult>> Initialize(InitializeWrapperParams initializeParams) {
            return await _restLanguageServer.Initialize(initializeParams); 
        }

        [HttpPost("initialized")]
        public async Task Initialized(InitializedParams initializedParams, CancellationToken cancellationToken) {
            await _restLanguageServer.Initialized(initializedParams);
        }

        [HttpPost("shutdown")]
        public async Task Shutdown() {
            await _restLanguageServer.Shutdown();
        }

        [HttpPost("batchResolve")]
        public async Task<ActionResult<BatchResolveResults>> BatchResolve(SourceFileMethodCalls sourceFileMethodCalls) {

            try {
                return await BatchResolveAsync(sourceFileMethodCalls);
            } catch (Exception e) {
                DumLogger.Log($"file name {sourceFileMethodCalls.FilePath}");

                // var src = Path.GetDirectoryName(sourceFileMethodCalls.FilePath);
                // var inf = Directory.CreateDirectory("/Lim.FeaturesExtractor.Unified/fck");
                // CopyFolder(src, inf.FullName);
                // var destFileName = $"/Lim.FeaturesExtractor.Unified/{Path.GetFileName(sourceFileMethodCalls.FilePath)}";
                // DumLogger.Log($"copy file to {destFileName}");
                // System.IO.File.Copy(sourceFileMethodCalls.FilePath, destFileName);

                DumLogger.Log("batchResolve exception");
                DumLogger.Log($"{e.Message} {e.StackTrace}");
            }

            return new BatchResolveResults();
        }

        private async Task<BatchResolveResults> BatchResolveAsync(SourceFileMethodCalls sourceFileMethodCalls) {
            BatchResolveResults batchResolveResults = new BatchResolveResults {
                ResolvedCalls = new string[sourceFileMethodCalls.Positions.Length]
            };

            for (int i = 0; i < batchResolveResults.ResolvedCalls.Length; i++) {
                
                TextDocumentPositionParams positionParams = new TextDocumentPositionParams {
                    position = sourceFileMethodCalls.Positions[i],
                    textDocument = new TextDocumentIdentifier { uri = new Uri(sourceFileMethodCalls.FilePath) }
                }; 
                batchResolveResults.ResolvedCalls[i] = await ResolveMethodCall(positionParams);
            }
            return batchResolveResults;
        }
        
        // [HttpPost("batchResolve")]
        // public async Task<ActionResult<BatchResolveResults>> BatchResolve(SourceFileMethodCalls sourceFileMethodCalls) {
        //     BatchResolveResults batchResolveResults = new BatchResolveResults();
        //
        //     try {
        //         DumLogger.Log($"got {sourceFileMethodCalls.Positions.Length} positions");
        //         List<string> resolvedCalls = new List<string>();
        //     
        //         foreach (var position in sourceFileMethodCalls.Positions) 
        //         {
        //             TextDocumentPositionParams positionParams = new TextDocumentPositionParams {
        //                 position = position,
        //                 textDocument = new TextDocumentIdentifier { uri = new Uri(sourceFileMethodCalls.FilePath) }
        //             };
        //         
        //             var resolved = await ResolveMethodCall(positionParams);
        //             resolvedCalls.Add(resolved);
        //         }
        //
        //         batchResolveResults.ResolvedCalls = resolvedCalls.ToArray();
        //     } catch (Exception e) {
        //         DumLogger.Log($"file name {sourceFileMethodCalls.FilePath}");
        //
        //         // var src = Path.GetDirectoryName(sourceFileMethodCalls.FilePath);
        //         // var inf = Directory.CreateDirectory("/Lim.FeaturesExtractor.Unified/fck");
        //         // CopyFolder(src, inf.FullName);
        //         // var destFileName = $"/Lim.FeaturesExtractor.Unified/{Path.GetFileName(sourceFileMethodCalls.FilePath)}";
        //         // DumLogger.Log($"copy file to {destFileName}");
        //         // System.IO.File.Copy(sourceFileMethodCalls.FilePath, destFileName);
        //
        //         DumLogger.Log("batchResolve exception");
        //         DumLogger.Log($"{e.Message} {e.StackTrace}");
        //     }
        //     return batchResolveResults;
        // }
        
        private void CopyFolder(string sourceFolder, string destFolder )
        {
            if (!Directory.Exists( destFolder ))
                Directory.CreateDirectory( destFolder );
            string[] files = Directory.GetFiles( sourceFolder );
            foreach (string file in files)
            {
                string name = Path.GetFileName( file );
                string dest = Path.Combine( destFolder, name );
                System.IO.File.Copy( file, dest );
            }
            string[] folders = Directory.GetDirectories( sourceFolder );
            foreach (string folder in folders)
            {
                string name = Path.GetFileName( folder );
                string dest = Path.Combine( destFolder, name );
                CopyFolder( folder, dest );
            }
        }
        [HttpPost("resolve")]
        public async Task<string> Resolve(TextDocumentPositionParams positionParams) {
            return await ResolveMethodCall(positionParams);
        }

        private static async Task<string> ResolveMethodCall(TextDocumentPositionParams positionParams) {
            var res = string.Empty;

            try {
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
                res = $"{fileName}|{className}";
                
            } catch (Exception e) {
                DumLogger.Log($"ResolveMethodCall exception  position {positionParams.textDocument.uri} {positionParams.position}");
                throw;
            }
            return res;
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
