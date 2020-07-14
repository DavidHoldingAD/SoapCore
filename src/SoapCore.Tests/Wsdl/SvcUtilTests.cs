using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Castle.Core.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.Wsdl
{
	[TestClass]
	public class SvcUtilTests
	{
		private readonly XNamespace _xmlSchema = "http://www.w3.org/2001/XMLSchema";
		private IWebHost _host;

		public static IEnumerable<object[]> GetServiceTypes()
		{
			IEnumerable<Type> serviceImplementationTypes = Assembly
				.GetAssembly(typeof(SvcUtilTests))
				.GetTypes()
				.Where(x => x.IsClass && !x.IsAbstract && x.GetInterfaces().Any(y => y.GetAttribute<ServiceContractAttribute>() != null));

			foreach (Type type in serviceImplementationTypes)
			{
				yield return new object[] { type };
			}
		}

		//[DataRow(typeof(PortTypeServiceBase.PortTypeService))]
		[TestMethod]
		[DynamicData(nameof(GetServiceTypes), DynamicDataSourceType.Method)]
		public void GenerateClients(Type serviceType)
		{
			StartService(serviceType);
			try
			{
				string outputDir = Path.Combine("GenerateClients", serviceType.Name);
				string url = GetWsdlUrl();

				Process cmd = new Process();
				cmd.StartInfo.FileName = "cmd.exe";
				cmd.StartInfo.RedirectStandardInput = true;
				cmd.StartInfo.RedirectStandardOutput = true;
				cmd.StartInfo.CreateNoWindow = true;
				cmd.StartInfo.UseShellExecute = false;
				cmd.Start();

				cmd.StandardInput.WriteLine($"dotnet-svcutil {url} --outputDir {outputDir}");
				cmd.StandardInput.Flush();
				cmd.StandardInput.Close();
				cmd.WaitForExit();
				Debug.WriteLine(cmd.StandardOutput.ReadToEnd());
			}
			finally
			{
				StopServer();
			}
		}

		[TestCleanup]
		public void StopServer()
		{
			_host?.StopAsync();
		}

		private string GetWsdlUrl()
		{
			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			return $"{address}/Service.svc?wsdl";
		}

		private void StartService(Type serviceType)
		{
			Task.Run(() =>
			{
				_host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://127.0.0.1:0")
					.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)))
					.UseStartup<Startup>()
					.Build();

				_host.Run();
			});

			//There's a race condition without this check, the host may not have an address immediately and we need to wait for it but the collection
			//may actually be totally empty, All() will be true if the collection is empty.
			while (_host == null || _host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.All(a => a.EndsWith(":0")))
			{
				Thread.Sleep(2000);
			}
		}
	}
}
