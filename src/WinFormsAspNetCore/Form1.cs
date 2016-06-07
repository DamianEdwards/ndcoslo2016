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

        public Form1()
        {
            InitializeComponent();

            StartWeb();
        }

        private void StartWeb()
        {
            _server = new MyServer();
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
            private Executor _executor;

            public IFeatureCollection Features { get; } = new FeatureCollection();

            public void Dispose()
            {

            }

            public Task ExecuteAsync(IFeatureCollection features)
            {
                return _executor.ExecuteAsync(features);
            }

            public void Start<TContext>(IHttpApplication<TContext> application)
            {
                _executor = new Executor<TContext>(application);
            }
        }

        public abstract class Executor
        {
            public abstract Task ExecuteAsync(IFeatureCollection features);
        }

        public class Executor<TContext> : Executor
        {
            private readonly IHttpApplication<TContext> _application;
            public Executor(IHttpApplication<TContext> application)
            {
                _application = application;
            }

            public override async Task ExecuteAsync(IFeatureCollection features)
            {
                var context = _application.CreateContext(features);
                try
                {
                    await _application.ProcessRequestAsync(context);
                    await ((MyHttpResponseFeature)features.Get<IHttpResponseFeature>()).RequestFinished();

                    _application.DisposeContext(context, null);
                }
                catch (Exception ex)
                {
                    _application.DisposeContext(context, ex);
                }
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
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(request);
            features.Set<IHttpResponseFeature>(response);
            await _server.ExecuteAsync(features);

            response.Body.Position = 0;
            webBrowser1.DocumentStream = response.Body;
        }
    }
}
