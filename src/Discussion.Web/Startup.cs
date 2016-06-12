﻿using Discussion.Web.Data;
using Discussion.Web.Data.InMemory;
using Jusfr.Persistent;
using Jusfr.Persistent.Mongo;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Configuration;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System;
using System.IO;

namespace Discussion.Web
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get;  }
        public IHostingEnvironment HostingEnvironment { get;  }

        public Startup(IHostingEnvironment env)
        {
            HostingEnvironment = env;
            Configuration = BuildConfiguration(env, Environment.GetCommandLineArgs());
        }

        public static void Main(string[] args)
        {

            var host = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }


        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            // Configure runtime to enable specified characters to be rendered as is
            // See https://github.com/aspnet/HttpAbstractions/issues/315
            // services.AddWebEncoders(option =>
            // {
            //     var enabledChars = new[]
            //     {
            //         UnicodeRanges.BasicLatin,
            //         UnicodeRanges.Latin1Supplement,
            //         // UnicodeRanges.CJKUnifiedIdeographs,
            //         UnicodeRanges.HalfwidthandFullwidthForms,
            //         UnicodeRanges.LatinExtendedAdditional,
            //         UnicodeRanges.LatinExtendedA,
            //         UnicodeRanges.LatinExtendedB,
            //         UnicodeRanges.LatinExtendedC,
            //         UnicodeRanges.LatinExtendedD,
            //         UnicodeRanges.LatinExtendedE
            //     };

            //     option.CodePointFilter = new CodePointFilter(enabledChars);
            // });
            
            services.AddMvc();
            AddDataServicesTo(services, Configuration);


            if (IsMono())
            {
                Console.WriteLine("Replaced default FileProvider with a wrapped synchronous one\n Since Mono has a bug on asynchronous filestream.\n See https://github.com/aspnet/Hosting/issues/604");
                UseSynchronousFileProvider(services, HostingEnvironment);
            }
        }


        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseDeveloperExceptionPage();
             //   app.UseExceptionHandler("/error");
            }

            // Add the platform handler to the request pipeline.
            // app.UseIISPlatformHandler();
            app.UseStaticFiles();
            app.UseMvc();
        }


        static IConfigurationRoot BuildConfiguration(IHostingEnvironment env, string[] commandlineArgs)
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(env.ContentRootPath)
              .AddJsonFile("appsettings.json", optional: true)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            builder.AddEnvironmentVariables();
            // builder.AddCommandLine(commandlineArgs);
            return builder.Build();
        }

        static void AddDataServicesTo(IServiceCollection services, IConfigurationRoot _configuration)
        {
            var mongoConnectionString = _configuration["mongoConnectionString"];
            var hasMongoCongured = !string.IsNullOrWhiteSpace(mongoConnectionString);


            if (hasMongoCongured)
            {
                services.AddScoped(typeof(IRepositoryContext), (serviceProvider) =>
                {
                // @jijiechen: detect at every time initate a new IRepositoryContext
                // may cause a performance issue
                if (!MongoDbUtils.DatabaseExists(mongoConnectionString))
                    {
                        throw new ApplicationException("Could not find a database using specified connection string");
                    }

                    return new MongoRepositoryContext(mongoConnectionString);
                });
                services.AddScoped(typeof(Repository<,>), typeof(MongoRepository<,>));
            }
            else
            {
                var dataContext = new InMemoryResponsitoryContext();
                services.AddScoped(typeof(IRepositoryContext), (serviceProvider) => dataContext);
                services.AddScoped(typeof(Repository<,>), typeof(InMemoryDataRepository<,>));
            }

            services.AddScoped(typeof(IDataRepository<>), typeof(BaseDataRepository<>));
        }

        static void UseSynchronousFileProvider(IServiceCollection services, IHostingEnvironment hostingEnvironment)
        {
            // services.Configure<RazorViewEngineOptions>(opt =>
            // {
            //     var physicalFileProvider = new PhysicalFileProvider(hostingEnvironment.ContentRootPath);
            //     opt.FileProvider = new WrappedSynchronousFileProvider(physicalFileProvider);
            // });
        }

        static bool IsMono()
        {
            var runtime = PlatformServices.Default.Runtime;
            return runtime.RuntimeType.Equals("Mono", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class WrappedSynchronousFileProvider : IFileProvider
    {
        IFileProvider _original;
        public WrappedSynchronousFileProvider(IFileProvider original)
        {
            _original = original;
        }


        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return _original.GetDirectoryContents(subpath);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            var originalFileInfo = _original.GetFileInfo(subpath);
            var isPhysical = originalFileInfo.GetType().FullName.EndsWith("PhysicalFileInfo");
            if (!isPhysical)
            {
                return originalFileInfo;
            }

            return new WrappedSynchronousFileInfo(originalFileInfo);
        }

        public IChangeToken Watch(string filter)
        {
            return _original.Watch(filter);
        }


        public class WrappedSynchronousFileInfo : IFileInfo
        {
            IFileInfo _original;
            public WrappedSynchronousFileInfo(IFileInfo original)
            {
                _original = original;
            }


            public bool Exists
            {
                get
                {
                    return _original.Exists;
                }
            }

            public bool IsDirectory
            {
                get
                {
                    return _original.IsDirectory;
                }
            }

            public DateTimeOffset LastModified
            {
                get
                {
                    return _original.LastModified;
                }
            }

            public long Length
            {
                get
                {
                    return _original.Length;
                }
            }

            public string Name
            {
                get
                {
                    return _original.Name;
                }
            }

            public string PhysicalPath
            {
                get
                {
                    return _original.PhysicalPath;
                }
            }

            public Stream CreateReadStream()
            {
                // @jijiechen: replaced implemention from https://github.com/aspnet/FileSystem/blob/32822deef3fd59b848842a500a3e989182687318/src/Microsoft.Extensions.FileProviders.Physical/PhysicalFileInfo.cs#L30
                //return new FileStream(
                //    PhysicalPath,
                //    FileMode.Open,
                //    FileAccess.Read,
                //    FileShare.ReadWrite,
                //    1024 * 64,
                //    FileOptions.Asynchronous | FileOptions.SequentialScan);


                // Note: Buffer size must be greater than zero, even if the file size is zero.
                return new FileStream(
                   PhysicalPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite,
                   1024 * 64,
                   FileOptions.SequentialScan);
            }
        }
    }

}
