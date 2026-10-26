namespace LT.App.Helpers;

/// <summary>
/// A bound text that holds times ("Cached 14:05 · DU123 · not live"). It keeps how it was built
/// and builds it again on <see cref="Rebuild"/> — called when the display time zone changes — so
/// an open widget follows the setting without reloading. Set the text only through it.
/// </summary>
public sealed class ZonedText(Action<string?> apply)
{
	private Func<string?>? _build;

	/// <summary>Builds the text now and again on each <see cref="Rebuild"/>.</summary>
	public void Set(Func<string?> build)
	{
		_build = build;
		apply(build());
	}

	/// <summary>Empties the text; a later <see cref="Rebuild"/> keeps it empty.</summary>
	public void Clear()
	{
		_build = null;
		apply(null);
	}

	/// <summary>The current text when it holds no time (it is the same in every zone).</summary>
	public void SetFixed(string? text)
	{
		if (text == null)
			Clear();
		else
			Set(() => text);
	}

	/// <summary>Adds <paramref name="suffix"/> to the text, which still follows the zone.</summary>
	public void Append(string suffix)
	{
		var previous = _build;
		Set(() => previous?.Invoke() + suffix);
	}

	public void Rebuild()
	{
		if (_build != null)
			apply(_build());
	}
}
