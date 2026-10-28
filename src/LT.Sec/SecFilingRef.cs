namespace LT.Sec;

/// <summary>One filing from a company's submissions: enough to find its documents.</summary>
public sealed record SecFilingRef(
	string AccessionNumber,
	string PrimaryDocument,
	string Form,
	DateOnly Filed
);
