using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WinFormsAspNetCore.web;

namespace WinFormsAspNetCore
{
    public partial class Form1 : Form
    {
        private IWebHost _webHost;
        private MyServer _server;
        private BlockingCollection<IFeatureCollection> _requests = new BlockingCollection<IFeatureCollection>();

        public Form1()
        {
            InitializeComponent();

            StartWeb();
        }

        private void StartWeb()
        {
            _server = new MyServer(_requests);
            _webHost = new WebHostBuilder()
                .UseStartup<Startup>()
                .UseServer(_server)
                .Build();

            _webHost.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            _webHost?.Dispose();
        }

        private class MyServer : IServer
        {
            private readonly BlockingCollection<IFeatureCollection> _requests;

            public MyServer(BlockingCollection<IFeatureCollection> requests)
            {
                _requests = requests;
            }

            public IFeatureCollection Features { get; } = new FeatureCollection();

            public void Dispose()
            {
                
            }

            public void Start<TContext>(IHttpApplication<TContext> application)
            {
                Task.Run(async () =>
                {
                    foreach (var features in _requests.GetConsumingEnumerable())
                    {
                        var context = application.CreateContext(features);
                        try
                        {
                            await application.ProcessRequestAsync(context);
                            await ((MyHttpResponseFeature)features.Get<IHttpResponseFeature>()).RequestFinished();

                            application.DisposeContext(context, null);
                        }
                        catch (Exception ex)
                        {
                            application.DisposeContext(context, ex);
                        }
                    }
                });
            }
        }

        private class MyHttpRequestFeature : IHttpRequestFeature
        {
            public Stream Body { get; set; } = new MemoryStream();

            public IHeaderDictionary Headers { get; set; }

            public string Method { get; set; }

            public string Path { get; set; }

            public string PathBase { get; set; }

            public string Protocol { get; set; }

            public string QueryString { get; set; }

            public string Scheme { get; set; }
        }

        private class MyHttpResponseFeature : IHttpResponseFeature
        {
            private Func<Task> _requestCompleted = () => Task.FromResult(0);

            public Stream Body { get; set; } = new MemoryStream();

            public bool HasStarted { get; }

            public IHeaderDictionary Headers { get; set; }

            public string ReasonPhrase { get; set; }

            public int StatusCode { get; set; }

            public void OnCompleted(Func<object, Task> callback, object state)
            {
                var prior = _requestCompleted;
                _requestCompleted = async () =>
                {
                    await prior();
                    await callback(state);
                };
            }

            public void OnStarting(Func<object, Task> callback, object state)
            {
                
            }

            public Task RequestFinished()
            {
                return _requestCompleted?.Invoke();
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var request = new MyHttpRequestFeature();
            var response = new MyHttpResponseFeature();

            var complete = new TaskCompletionSource<Stream>();
            response.OnCompleted(state =>
            {
                response.Body.Position = 0;
                complete.SetResult(response.Body);
                return Task.FromResult(0);
            }, null);

            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(request);
            features.Set<IHttpResponseFeature>(response);
            _requests.Add(features);

            webBrowser1.DocumentStream = await complete.Task;
        }
    }
}
