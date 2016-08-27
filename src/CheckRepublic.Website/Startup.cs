﻿using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using idunno.Authentication;
using Knapcode.CheckRepublic.Logic.Business;
using Knapcode.CheckRepublic.Logic.Entities;
using Knapcode.CheckRepublic.Logic.Runner;
using Knapcode.CheckRepublic.Website.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Knapcode.CheckRepublic.Website
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            HostingEnvironment = env;
            Configuration = builder.Build();
        }

        public IHostingEnvironment HostingEnvironment { get; }

        public IConfigurationRoot Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddDbContext<CheckContext>(options =>
                {
                    var path = Path.Combine(HostingEnvironment.ContentRootPath, "CheckRepublic.sqlite3");
                    options.UseSqlite($"Filename={path}");
                });

            services.AddTransient<ICheckService, CheckService>();
            services.AddTransient<ICheckBatchService, CheckBatchService>();

            services.AddTransient<ICheckRunner, CheckRunner>();
            services.AddTransient<ICheckBatchRunner, CheckBatchRunner>();
            services.AddTransient<ICheckPersister, CheckPersister>();
            services.AddTransient<ICheckRunnerService, CheckRunnerService>();

            services.AddOptions();
            services.Configure<WebsiteOptions>(Configuration);

            services
                .AddAuthorization(options =>
                {
                    options.AddPolicy(
                        AuthorizationConstants.ReadPolicy,
                        policy => policy.Requirements.Add(new RolesAuthorizationRequirement(new[]
                        {
                            AuthorizationConstants.ReaderRole,
                            AuthorizationConstants.WriterRole
                        })));

                    options.AddPolicy(
                        AuthorizationConstants.WritePolicy,
                        policy => policy.Requirements.Add(new RolesAuthorizationRequirement(new[]
                        {
                            AuthorizationConstants.WriterRole
                        })));
                });

            services
                .AddMvc(options =>
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();

                    options.Filters.Add(new AuthorizeFilter(policy));
                })
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.Converters.Add(new StringEnumConverter());
                    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                });
        }
        
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStatusCodePages();

            app.UseBasicAuthentication(new BasicAuthenticationOptions
            {
                Realm = env.ApplicationName,
                Events = new BasicAuthenticationEvents
                {
                    OnValidateCredentials = context =>
                    {
                        var claims = new List<Claim>();

                        var readPassword = Configuration.GetValue<string>("ReadPassword");
                        if (string.IsNullOrWhiteSpace(readPassword) || context.Password == readPassword)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, AuthorizationConstants.ReaderRole));
                        }

                        var writePassword = Configuration.GetValue<string>("WritePassword");
                        if (string.IsNullOrWhiteSpace(writePassword) || context.Password == writePassword)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, AuthorizationConstants.WriterRole));
                        }

                        context.Ticket = new AuthenticationTicket(
                            new ClaimsPrincipal(new ClaimsIdentity(claims, context.Options.AuthenticationScheme)),
                            new AuthenticationProperties(),
                            context.Options.AuthenticationScheme);

                        return Task.FromResult(0);
                    }
                }
            });

            app.UseMvc();
        }
    }
}
