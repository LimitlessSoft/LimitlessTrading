using LT.Contracts.Constants;
using LT.Contracts.Helpers;
using LT.Repository.Repositories;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Repository;

public sealed class IndicatorScriptRepositoryTests : IDisposable
{
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		"lt-indicators-" + Guid.NewGuid().ToString("N")
	);
	private readonly FakeTimeProvider _time = new();
	private readonly IndicatorScriptRepository _repository;

	public IndicatorScriptRepositoryTests() =>
		_repository = new IndicatorScriptRepository(_time, _directory);

	public void Dispose()
	{
		_repository.Dispose();
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, true);
	}

	private string SdkPath => typeof(LT.Indicators.Sdk.Indicator).Assembly.Location;

	[Fact]
	public void First_run_sets_up_the_folder_with_the_editor_project_sdk_and_example()
	{
		_repository.EnsureWorkspace(IndicatorWorkspaceHelpers.GetFiles(SdkPath, 10));

		Assert.True(File.Exists(Path.Combine(_directory, IndicatorConstants.ExampleFileName)));
		Assert.True(File.Exists(Path.Combine(_directory, IndicatorConstants.ReadmeFileName)));
		var project = File.ReadAllText(
			Path.Combine(_directory, IndicatorConstants.ProjectFileName)
		);
		Assert.Contains("net10.0", project);
		Assert.Contains(".sdk/LT.Indicators.Sdk.dll", project);
		Assert.Contains("<Using Include=\"LT.Indicators.Sdk.Script\" Static=\"true\" />", project);
		Assert.True(
			File.Exists(
				Path.Combine(
					_directory,
					IndicatorConstants.SdkDirectoryName,
					IndicatorConstants.SdkAssemblyFileName
				)
			)
		);
		Assert.Equal(
			[IndicatorConstants.ExampleFileName],
			_repository.GetAll().Select(x => x.FileName)
		);
	}

	[Fact]
	public void Later_runs_keep_the_users_files()
	{
		var files = IndicatorWorkspaceHelpers.GetFiles(SdkPath, 10);
		_repository.EnsureWorkspace(files);
		File.Delete(Path.Combine(_directory, IndicatorConstants.ExampleFileName));
		var project = Path.Combine(_directory, IndicatorConstants.ProjectFileName);
		File.WriteAllText(project, "<Project />");

		_repository.EnsureWorkspace(files);

		Assert.False(File.Exists(Path.Combine(_directory, IndicatorConstants.ExampleFileName)));
		Assert.Equal("<Project />", File.ReadAllText(project));
	}

	[Fact]
	public void Only_script_files_are_listed()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(Path.Combine(_directory, "b.cs"), "Ta.Sma(Close, 2)");
		File.WriteAllText(Path.Combine(_directory, "a.cs"), "Ta.Sma(Close, 3)");
		File.WriteAllText(Path.Combine(_directory, ".hidden.cs"), "x");
		File.WriteAllText(Path.Combine(_directory, "notes.txt"), "x");

		var scripts = _repository.GetAll();

		Assert.Equal(["a.cs", "b.cs"], scripts.Select(x => x.FileName));
		Assert.Equal("Ta.Sma(Close, 3)", scripts[0].Source);
		Assert.Null(_repository.Get("../settings.json"));
		Assert.Null(_repository.Get("missing.cs"));
	}

	[Fact]
	public void Changes_are_reported_once_after_a_quiet_period()
	{
		var reports = new List<IReadOnlyCollection<string>>();
		_repository.Changed += reports.Add;

		_repository.Queue("a.cs");
		_time.Advance(TimeSpan.FromMilliseconds(200));
		_repository.Queue("a.cs");
		_repository.Queue("b.cs");
		_repository.Queue("readme.md");
		_time.Advance(TimeSpan.FromMilliseconds(200));
		Assert.Empty(reports);

		_time.Advance(IndicatorConstants.ReloadDebounce);

		var report = Assert.Single(reports);
		Assert.Equal(["a.cs", "b.cs"], report.Order());
	}

	[Fact]
	public void A_scan_reports_added_changed_and_deleted_scripts()
	{
		Directory.CreateDirectory(_directory);
		var path = Path.Combine(_directory, "a.cs");
		File.WriteAllText(path, "Ta.Sma(Close, 2)");
		var reports = new List<string>();
		_repository.Changed += reports.AddRange;

		_repository.Scan();
		_time.Advance(IndicatorConstants.ReloadDebounce);
		Assert.Equal(["a.cs"], reports);

		_repository.Scan();
		_time.Advance(IndicatorConstants.ReloadDebounce);
		Assert.Equal(["a.cs"], reports);

		File.WriteAllText(path, "Ta.Sma(Close, 20)");
		_repository.Scan();
		File.Delete(path);
		_repository.Scan();
		_time.Advance(IndicatorConstants.ReloadDebounce);
		Assert.Equal(["a.cs", "a.cs"], reports);
	}

	// Through the OS watcher, or the scan it falls back to when the watcher can't run
	[Fact]
	public async Task The_watcher_reports_a_saved_file()
	{
		using var repository = new IndicatorScriptRepository(TimeProvider.System, _directory);
		var reported = new TaskCompletionSource<IReadOnlyCollection<string>>();
		repository.Changed += x => reported.TrySetResult(x);
		repository.StartWatching();

		var cancellation = TestContext.Current.CancellationToken;
		await File.WriteAllTextAsync(
			Path.Combine(_directory, "new.cs"),
			"Ta.Ema(Close, 9)",
			cancellation
		);

		var names = await reported.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
		Assert.Contains("new.cs", names);
	}
}
