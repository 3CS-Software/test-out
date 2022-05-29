using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Hosting;
using ThreeCS.TestOut.Core.Servers;
using ThreeCS.TestOut.NUnit;

namespace ThreeCS.TestOut.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSignalR(hubOptions =>
            {
                hubOptions.MaximumParallelInvocationsPerClient = 30;
                //max message size of 50mb.  We might need a higher limit as the raw test output is currently being sent for all tests, so if they are all failing noisily, then
                //this could be exceeded.  Note that this limit doesn't include the size of the files being sent around, so another option is to prepare the result file
                //and just send it to the invoker, then this could be lowered again to 5 mb or something.
                hubOptions.MaximumReceiveMessageSize = 1024 * 1024 * 50;
            });
            services.AddTestOutServer();
            services.AddNUnitTestOutServer();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            //Helper for our lazy endpoint controller.
            Guid GetId(HttpContext context)
            {
                var streamIdStr = context.Request.RouteValues["id"].ToString();
                return Guid.Parse(streamIdStr);
            }

            //We don't need controllers, just a few forwarders.  TODO: Check out MinmalAPIs, it might be a better fit
            //than the full fat asp.net core.
            //https://gist.github.com/davidfowl/ff1addd02d239d2d26f4648a06158727
            app.UseEndpoints(endpoints =>
            {
                //For sending files around.  I'm too lazy to write a whole controller.
                endpoints.MapGet("/Stream/{id}", async context =>
                {
                    var xferService = context.RequestServices.GetRequiredService<FileTransferStreamInvoker>();
                    await xferService.DownloadToFunc(GetId(context), context.Response.Body);
                });
                endpoints.MapPost("/Stream/{id}", async context =>
                {
                    context.Response.ContentType = "application/octet-stream";
                    var xferService = context.RequestServices.GetRequiredService<FileTransferStreamInvoker>();
                    await xferService.UploadFromFunc(GetId(context), context.Request.Body);
                });
            });
        }
    }
}
