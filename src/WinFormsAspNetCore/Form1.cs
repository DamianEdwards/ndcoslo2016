using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private bool _loadingDocument = false;
        private string _appAssemblyDirPath;

        public Form1()
        {
            InitializeComponent();

            StartWeb();
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var loadPath = Path.Combine(_appAssemblyDirPath, new AssemblyName(args.Name).Name + ".dll");

            if (File.Exists(loadPath))
            {
                return Assembly.LoadFile(loadPath);
            }

            loadPath = Path.ChangeExtension(loadPath, ".exe");
            if (File.Exists(loadPath))
            {
                return Assembly.LoadFile(loadPath);
            }

            return null;
        }

        private void StartWeb(string assemblyPath = null)
        {
            _webHost?.Dispose();

            _server = new MyServer();

            var webHostBuilder = new WebHostBuilder()
                .UseServer(_server);

            if (assemblyPath == null)
            {
                webHostBuilder.UseStartup<Startup>();
            }
            else
            {
                _appAssemblyDirPath = Path.GetDirectoryName(assemblyPath);
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                webHostBuilder.UseStartup(assemblyName.Name);
                var contentRoot = Path.GetFullPath( Path.Combine(_appAssemblyDirPath, "../../../../"));
                webHostBuilder.UseContentRoot(contentRoot);
            }

            _webHost = webHostBuilder.Build();
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
                    ((MyHttpResponseFeature)features.Get<IHttpResponseFeature>()).StatusCode = 500;
                    _application.DisposeContext(context, ex);
                }
            }
        }

        private class MyHttpRequestFeature : IHttpRequestFeature
        {
            public Stream Body { get; set; } = new MemoryStream();

            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

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

            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

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

        private void button1_Click(object sender, EventArgs e)
        {
            GetRequest(txtPath.Text);
        }

        private async void GetRequest(string path)
        {
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new MyHttpRequestFeature
            {
                Path = path,
                Method = "GET"
            });
            var response = new MyHttpResponseFeature();
            features.Set<IHttpResponseFeature>(response);

            await _server.ExecuteAsync(features);

            response.Body.Position = 0;
            _loadingDocument = true;
            webBrowser1.DocumentStream = response.Body;
        }

        private void webBrowser1_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (_loadingDocument == true)
            {
                return;
            }

            var path = e.Url.PathAndQuery;
            GetRequest(path);

            e.Cancel = true;

            txtPath.Text = e.Url.PathAndQuery;
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            _loadingDocument = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var result = ofdWebApp.ShowDialog();
        }

        private void ofdWebApp_FileOk(object sender, CancelEventArgs e)
        {
            StartWeb(ofdWebApp.FileName);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            webBrowser1.GoBack();
        }
    }
}
