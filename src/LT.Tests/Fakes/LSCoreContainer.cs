using LSCore.DependencyInjection;
using LSCore.Validation.Domain;
using LT.Contracts.Requests.Connection;
using LT.Domain.Validators.Connection;
using Microsoft.Extensions.DependencyInjection;

namespace LT.Tests.Fakes;

/// <summary>request.Validate() resolves validators through LSCore's static container.</summary>
public static class LSCoreContainer
{
	public static void EnsureInitialized()
	{
		if (Container.ServiceProvider != null)
			return;

		Container.ServiceProvider = new ServiceCollection()
			.AddSingleton<LSCoreValidatorBase<ConnectRequest>, ConnectRequestValidator>()
			.BuildServiceProvider();
	}
}
