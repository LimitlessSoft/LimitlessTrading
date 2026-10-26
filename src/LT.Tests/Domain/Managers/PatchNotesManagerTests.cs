using LT.Contracts.Dtos.Updates;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class PatchNotesManagerTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 20, 0, 0, TimeSpan.Zero);
	private readonly FakeAppUpdater _updater = new() { CurrentVersion = "0.10.0" };
	private readonly InMemoryPatchNotesRepository _repository = new();
	private readonly FakeTimeProvider _time = new(Now);

	private PatchNotesManager CreateManager() => new(_updater, _repository, _time);

	private static AppReleaseDto Release(string tag, string body = "") =>
		new()
		{
			Tag = tag,
			PageUrl = $"https://github.com/LimitlessSoft/LimitlessTrading/releases/tag/{tag}",
			PublishedAt = Now.AddDays(-1),
			Body = body,
		};

	[Fact]
	public void Status_StartsWithTheCachedNotes()
	{
		_repository.Save(
			new PatchNotesStateEntity
			{
				Releases = [new PatchNotesReleaseEntity { Tag = "v0.10.0", Body = "* Cached" }],
				FetchedAt = Now.AddDays(-3),
			}
		);

		var status = CreateManager().Status;

		Assert.Equal(PatchNotesState.Idle, status.State);
		Assert.Equal("0.10.0", status.CurrentVersion);
		var release = Assert.Single(status.Releases);
		Assert.True(release.IsCurrent);
		Assert.Equal("Cached", Assert.Single(release.Entries).Text);
		Assert.Equal(Now.AddDays(-3), status.FetchedAt);
	}

	[Fact]
	public async Task Refresh_LoadsAndCachesTheReleases()
	{
		_updater.Releases =
		[
			Release(
				"v0.10.0",
				"* Pin watchlist tickers by @a in https://github.com/LimitlessSoft/LimitlessTrading/pull/48"
			),
			Release("v0.9.0"),
		];
		var manager = CreateManager();
		var states = new List<PatchNotesState>();
		manager.StatusChanged += x => states.Add(x.State);

		await manager.RefreshAsync(TestContext.Current.CancellationToken);

		Assert.Equal([PatchNotesState.Loading, PatchNotesState.Loaded], states);
		Assert.Equal(["0.10.0", "0.9.0"], manager.Status.Releases.Select(x => x.Version));
		Assert.Equal(48, manager.Status.Releases[0].Entries[0].PullRequestNumber);
		Assert.Equal(Now, manager.Status.FetchedAt);
		// The next start shows them before fetching
		Assert.Equal(2, CreateManager().Status.Releases.Count);
	}

	[Fact]
	public async Task Refresh_Error_FailsAndKeepsTheCachedNotes()
	{
		_repository.Save(
			new PatchNotesStateEntity
			{
				Releases = [new PatchNotesReleaseEntity { Tag = "v0.9.0", Body = "* Cached" }],
			}
		);
		_updater.ReleasesError = new HttpRequestException("offline");
		var manager = CreateManager();

		await manager.RefreshAsync(TestContext.Current.CancellationToken);

		Assert.Equal(PatchNotesState.Failed, manager.Status.State);
		Assert.Contains("offline", manager.Status.Error);
		Assert.Equal("0.9.0", Assert.Single(manager.Status.Releases).Version);
	}

	[Fact]
	public async Task Refresh_KeepsTheLastSeenVersion()
	{
		_repository.Save(new PatchNotesStateEntity { LastSeenVersion = "0.9.0" });
		_updater.Releases = [Release("v0.10.0")];

		await CreateManager().RefreshAsync(TestContext.Current.CancellationToken);

		Assert.Equal("0.9.0", _repository.Saved?.LastSeenVersion);
	}

	[Fact]
	public void TakeIsNewVersion_FirstStartEver_IsNotNewButRecorded()
	{
		Assert.False(CreateManager().TakeIsNewVersion());
		Assert.Equal("0.10.0", _repository.Saved?.LastSeenVersion);
	}

	[Fact]
	public void TakeIsNewVersion_AfterAnUpdate_IsTrueOnce()
	{
		_repository.Save(new PatchNotesStateEntity { LastSeenVersion = "0.9.0" });
		var manager = CreateManager();

		Assert.True(manager.TakeIsNewVersion());
		Assert.False(manager.TakeIsNewVersion());
		Assert.Equal("0.10.0", _repository.Saved?.LastSeenVersion);
		// Next start on the same version
		Assert.False(CreateManager().TakeIsNewVersion());
	}

	[Fact]
	public void TakeIsNewVersion_SameVersion_SavesNothing()
	{
		_repository.Save(new PatchNotesStateEntity { LastSeenVersion = "0.10.0" });

		Assert.False(CreateManager().TakeIsNewVersion());
		Assert.Equal(1, _repository.Saves);
	}

	[Fact]
	public void TakeIsNewVersion_SourceBuild_IsNeverRecorded()
	{
		_repository.Save(new PatchNotesStateEntity { LastSeenVersion = "0.9.0" });
		_updater.CurrentVersion = "0.0.0-dev";

		Assert.False(CreateManager().TakeIsNewVersion());
		Assert.Equal("0.9.0", _repository.Saved?.LastSeenVersion);
	}
}
