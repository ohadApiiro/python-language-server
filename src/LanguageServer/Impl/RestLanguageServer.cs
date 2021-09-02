using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Threading;
using Microsoft.Python.LanguageServer.Optimization;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.SearchPaths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Python.LanguageServer.Implementation
{
    public class RestLanguageServer
    {
        private IServiceManager _services;
        private Server _server;
        private ILogger _logger;

        private InitializeParams _initParams;
        private readonly Prioritizer _prioritizer = new();
        private bool _initialized;
        private Task<IDisposable> _initializedPriorityTask;

        public RestLanguageServer(IServiceManager services) 
        {
            _services = services;
            _server = new Server(services);
            _logger = services.GetService<ILogger>();
        }
        
        public async Task<ActionResult<InitializeResult>> Initialize(InitializeParams initializeParams)
        {
            //MonitorParentProcess(_initParams);
            _initParams = initializeParams;
            RegisterServices(_initParams);

            using (await _prioritizer.InitializePriorityAsync()) {
                Debug.Assert(!_initialized);
                // Force the next handled request to be "initialized", where the work actually happens.
                _initializedPriorityTask = _prioritizer.InitializedPriorityAsync();
                var result = await _server.InitializeAsync(_initParams);
                return result;
            }
        }
        
        public async Task Initialized(InitializedParams initializedParams) 
        {
            _services.GetService<IProfileOptimizationService>()?.Profile("Initialized");
        
            using (await _initializedPriorityTask) {
                Debug.Assert(!_initialized);
                var pythonSection = await GetPythonConfigurationAsync(CancellationToken.None, 200);
                var userConfiguredPaths = GetUserConfiguredPaths(pythonSection);
        
                await _server.InitializedAsync(initializedParams, CancellationToken.None, userConfiguredPaths);
                _initialized = true;
            }
        }

        public async Task Shutdown() {
            await _server.Shutdown();
        }
        
        public async Task<Hover> Hover(TextDocumentPositionParams positionParams) 
        {
            Debug.Assert(_initialized);
            return await _server.Hover(positionParams, CancellationToken.None);
        }
        
        public async Task DidOpen(DidOpenTextDocumentParams openParams) 
        {
            Debug.Assert(_initialized);
            _server.DidOpenTextDocument(openParams);
        }
        
        public Task DidClose(DidCloseTextDocumentParams closeParams) 
        {
            Debug.Assert(_initialized);
            _server.DidCloseTextDocument(closeParams);
        }
        
        public async Task<Location> GotoDeclaration(TextDocumentPositionParams positionParams) 
        {
            Debug.Assert(_initialized);
            return await _server.GotoDeclaration(positionParams, CancellationToken.None);
        }

        public async Task<Reference[]> GotoDefinition(TextDocumentPositionParams positionParams) 
        {
            Debug.Assert(_initialized);
            return await _server.GotoDefinition(positionParams, CancellationToken.None);
        }
        
        
        //--------------------------------------------------------------------------------------------------------------
        private void RegisterServices(InitializeParams initParams) {
            // we need to register cache service first.
            // optimization service consumes the cache info.
            CacheService.Register(_services, initParams?.initializationOptions?.cacheFolderPath);
            _services.AddService(new ProfileOptimizationService(_services));
        }

        private async Task<JToken> GetPythonConfigurationAsync(
            CancellationToken cancellationToken = default,
            int? cancelAfterMilli = null) 
        {
            if (_initParams?.capabilities?.workspace?.configuration != true) {
                return null;
            }

            try {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                    if (cancelAfterMilli.HasValue) {
                        cts.CancelAfter(cancelAfterMilli.Value);
                    }
                    var args = new ConfigurationParams {
                        items = new[] {
                            new ConfigurationItem {
                                scopeUri = _initParams.rootUri,
                                section = "python"
                            }
                        }
                    };
                   // var configs = await _rpc.InvokeWithParameterObjectAsync<JToken>("workspace/configuration", args, cancellationToken);
                   return null; //configs?[0];
                }
            } catch (Exception) { }

            // The cancellation of this token could have been caught above instead of the timeout, so rethrow it.
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        private ImmutableArray<string> GetUserConfiguredPaths(JToken pythonSection) {
            var paths = ImmutableArray<string>.Empty;
            var set = false;

            if (pythonSection != null) {
                var autoComplete = pythonSection["autoComplete"];
                var analysis = pythonSection["analysis"];

                // The values of these may not be null even if the value is "unset", depending on
                // what the client uses as a default. Use null as a default anyway until the
                // extension uses a null default (and/or extraPaths is dropped entirely).
                var autoCompleteExtraPaths = GetSetting<IReadOnlyList<string>>(autoComplete, "extraPaths", null);
                var analysisSearchPaths = GetSetting<IReadOnlyList<string>>(analysis, "searchPaths", null);
                var analysisUsePYTHONPATH = GetSetting(analysis, "usePYTHONPATH", true);
                var analayisAutoSearchPaths = GetSetting(analysis, "autoSearchPaths", true);

                if (analysisSearchPaths != null) {
                    set = true;
                    paths = analysisSearchPaths.ToImmutableArray();
                } else if (autoCompleteExtraPaths != null) {
                    set = true;
                    paths = autoCompleteExtraPaths.ToImmutableArray();
                }

                if (analysisUsePYTHONPATH) {
                    var pythonpath = Environment.GetEnvironmentVariable("PYTHONPATH");
                    if (pythonpath != null) {
                        var sep = _services.GetService<IOSPlatform>().IsWindows ? ';' : ':';
                        var pythonpathPaths = pythonpath.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (pythonpathPaths.Length > 0) {
                            paths = paths.AddRange(pythonpathPaths);
                            set = true;
                        }
                    }
                }

                if (analayisAutoSearchPaths) {
                    var fs = _services.GetService<IFileSystem>();
                    var auto = AutoSearchPathFinder.Find(fs, _server.Root);
                    paths = paths.AddRange(auto);
                    set = true;
                }
            }

            if (set) {
                return paths;
            }

            var initPaths = _initParams?.initializationOptions?.searchPaths;
            if (initPaths != null) {
                return initPaths.ToImmutableArray();
            }

            return ImmutableArray<string>.Empty;
        }
        
        private T GetSetting<T>(JToken section, string settingName, T defaultValue) {
            var value = section?[settingName];
            try {
                return value != null ? value.ToObject<T>() : defaultValue;
            } catch (JsonException ex) {
                _logger?.Log(TraceEventType.Warning, $"Exception retrieving setting '{settingName}': {ex.Message}");
            }
            return defaultValue;
        }

        private class Prioritizer : IDisposable {
            private enum Priority {
                Initialize,
                Initialized,
                Configuration,
                DocumentChange,
                Default,
                Length, // Length of the enum, not a real priority.
            }

            private readonly PriorityProducerConsumer<QueueItem> _ppc;

            public Prioritizer() {
                _ppc = new PriorityProducerConsumer<QueueItem>(maxPriority: (int)Priority.Length);
                Task.Run(ConsumerLoop).DoNotWait();
            }

            private async Task ConsumerLoop() {
                while (!_ppc.IsDisposed) {
                    try {
                        var item = await _ppc.ConsumeAsync();
                        if (item.IsAwaitable) {
                            var disposable = new PrioritizerDisposable(_ppc.CancellationToken);
                            item.SetResult(disposable);
                            await disposable.Task;
                        } else {
                            item.SetResult(EmptyDisposable.Instance);
                        }
                    } catch (OperationCanceledException) when (_ppc.IsDisposed) {
                        return;
                    }
                }
            }

            public Task<IDisposable> InitializePriorityAsync(CancellationToken cancellationToken = default)
                => Enqueue(Priority.Initialize, isAwaitable: true, cancellationToken);

            public Task<IDisposable> InitializedPriorityAsync(CancellationToken cancellationToken = default)
                => Enqueue(Priority.Initialized, isAwaitable: true, cancellationToken);

            public Task<IDisposable> ConfigurationPriorityAsync(CancellationToken cancellationToken = default)
                => Enqueue(Priority.Configuration, isAwaitable: true, cancellationToken);

            public Task<IDisposable> DocumentChangePriorityAsync(CancellationToken cancellationToken = default)
                => Enqueue(Priority.DocumentChange, isAwaitable: true, cancellationToken);

            public Task DefaultPriorityAsync(CancellationToken cancellationToken = default)
                => Enqueue(Priority.Default, isAwaitable: false, cancellationToken);

            private Task<IDisposable> Enqueue(Priority priority, bool isAwaitable, CancellationToken cancellationToken) {
                var item = new QueueItem(isAwaitable, cancellationToken);
                _ppc.Produce(item, (int)priority);
                return item.Task;
            }

            private readonly struct QueueItem {
                private readonly TaskCompletionSource<IDisposable> _tcs;
                public Task<IDisposable> Task => _tcs.Task;
                public bool IsAwaitable { get; }

                public QueueItem(bool isAwaitable, CancellationToken cancellationToken) {
                    _tcs = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
                    IsAwaitable = isAwaitable;
                    _tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(_tcs.Task);
                }

                public void SetResult(IDisposable disposable) => _tcs.TrySetResult(disposable);
            }

            private class PrioritizerDisposable : IDisposable {
                private readonly TaskCompletionSource<int> _tcs;

                public PrioritizerDisposable(CancellationToken cancellationToken) {
                    _tcs = new TaskCompletionSource<int>();
                    _tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(_tcs.Task);
                }

                public Task Task => _tcs.Task;
                public void Dispose() => _tcs.TrySetResult(0);
            }

            public void Dispose() => _ppc.Dispose();
        }
        
        private class AnalysisOptionsProvider : IAnalysisOptionsProvider 
        {
            public AnalysisOptions Options { get; } = new AnalysisOptions();
        }
    }
}
