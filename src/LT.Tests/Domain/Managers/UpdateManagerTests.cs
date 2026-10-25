using LT.Contracts.Dtos.Updates;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class UpdateManagerTests
{
	private readonly FakeAppUpdater _updater = new();
	private readonly MessageLogManager _messages = new();
	private readonly UpdateManager _manager;

	public UpdateManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new UpdateManager(_updater, _messages);
	}

	[Fact]
	public void Status_StartsIdleWithTheRunningVersion()
	{
		Assert.Equal(UpdateState.Idle, _manager.Status.State);
		Assert.Equal("0.1.0", _manager.Status.CurrentVersion);
	}

	[Fact]
	public async Task Check_NewerRelease_IsAvailableAndLoggedOnce()
	{
		await _manager.CheckAsync(TestContext.Current.CancellationToken);
		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		Assert.Equal(UpdateState.Available, _manager.Status.State);
		Assert.Equal("0.2.0", _manager.Status.LatestVersion);
		Assert.Equal(_updater.Release.PageUrl, _manager.Status.ReleasePageUrl);
		Assert.Single(_messages.GetAll(), x => x.Text.Contains("0.2.0 is available"));
	}

	[Fact]
	public async Task Check_SameVersion_IsUpToDate()
	{
		_updater.Release.Tag = "v0.1.0";

		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		Assert.Equal(UpdateState.UpToDate, _manager.Status.State);
		Assert.Null(_manager.Status.LatestVersion);
	}

	[Fact]
	public async Task Check_Error_FailsWithoutThrowing()
	{
		_updater.CheckError = new HttpRequestException("offline");

		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		Assert.Equal(UpdateState.Failed, _manager.Status.State);
		Assert.Contains("offline", _manager.Status.Error);
		Assert.Contains(_messages.GetAll(), x => x.Severity == MessageSeverity.Error);
	}

	[Fact]
	public async Task Install_DownloadsThisSystemsArchiveAndRestarts()
	{
		var states = new List<UpdateState>();
		_manager.StatusChanged += x => states.Add(x.State);
		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		await _manager.InstallAsync(TestContext.Current.CancellationToken);

		Assert.Equal(_updater.Release.Assets[0], _updater.Downloaded);
		Assert.True(_updater.Restarted);
		Assert.Equal(UpdateState.Restarting, _manager.Status.State);
		Assert.Contains(UpdateState.Downloading, states);
	}

	[Fact]
	public async Task Install_WithoutAvailableUpdate_DoesNothing()
	{
		await _manager.InstallAsync(TestContext.Current.CancellationToken);

		Assert.Null(_updater.Downloaded);
		Assert.Equal(UpdateState.Idle, _manager.Status.State);
	}

	[Fact]
	public async Task Install_WhenThisCopyCantInstall_FailsWithTheReason()
	{
		_updater.CannotInstallReason = "Built from source.";
		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		await _manager.InstallAsync(TestContext.Current.CancellationToken);

		Assert.Null(_updater.Downloaded);
		Assert.Equal(UpdateState.Failed, _manager.Status.State);
		Assert.Contains("Built from source.", _manager.Status.Error);
		Assert.Equal("0.2.0", _manager.Status.LatestVersion);
	}

	[Fact]
	public async Task Install_NoArchiveForThisSystem_Fails()
	{
		_updater.AssetSuffix = "-osx-arm64.tar.gz";
		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		await _manager.InstallAsync(TestContext.Current.CancellationToken);

		Assert.Equal(UpdateState.Failed, _manager.Status.State);
		Assert.Contains("-osx-arm64.tar.gz", _manager.Status.Error);
		Assert.False(_updater.Restarted);
	}

	[Fact]
	public async Task Install_DownloadError_FailsAndCanRetry()
	{
		await _manager.CheckAsync(TestContext.Current.CancellationToken);
		_updater.DownloadError = new IOException("disk full");

		await _manager.InstallAsync(TestContext.Current.CancellationToken);
		Assert.Equal(UpdateState.Failed, _manager.Status.State);
		Assert.False(_updater.Restarted);

		_updater.DownloadError = null;
		await _manager.InstallAsync(TestContext.Current.CancellationToken);
		Assert.True(_updater.Restarted);
	}

	[Fact]
	public async Task Check_AfterRestartingStarted_KeepsRestarting()
	{
		await _manager.CheckAsync(TestContext.Current.CancellationToken);
		await _manager.InstallAsync(TestContext.Current.CancellationToken);

		await _manager.CheckAsync(TestContext.Current.CancellationToken);

		Assert.Equal(UpdateState.Restarting, _manager.Status.State);
	}
}
