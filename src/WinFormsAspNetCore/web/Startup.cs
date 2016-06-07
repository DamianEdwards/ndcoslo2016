using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WinFormsAspNetCore.web
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouter(new RouteBuilder(app)
                .MapGet("", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("Hello from root!<br />");
                    await context.Response.WriteAsync("<a href='sub'>Go to sub</a>");
                })

                .MapGet("sub", context => context.Response.WriteAsync("Hello from sub!"))
                .Build()
            );

            app.Run(context => context.Response.WriteAsync("No route matched!"));
        }
    }
}
