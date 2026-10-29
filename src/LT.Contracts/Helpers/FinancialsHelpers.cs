using LT.Contracts.Constants;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Turns a company's SEC facts into fiscal years and quarters. Periods go by the facts' own start
/// and end dates (a fact's fy / fp in companyfacts are those of the filing that carried it, not
/// the fact's), only 10-K / 10-Q values count, and a value reported again in a later filing is
/// taken from the latest one (restatements win).
/// </summary>
public static class FinancialsHelpers
{
	/// <summary>A start or end this many days apart still counts as the same period's (52 / 53-week years).</summary>
	private const int DateTolerance = 7;
	private const double DaysPerQuarter = 91.31;

	/// <summary>
	/// The statements in <paramref name="facts"/> (from
	/// <see cref="FinancialsConstants.Concepts"/>): <see cref="FinancialsStatus.NotFiled"/> when
	/// no 10-K / 10-Q period has a revenue, net income or operating cash flow.
	/// </summary>
	public static FinancialsDto Build(IEnumerable<SecFactDto> facts, string? entityName = null)
	{
		var concepts = facts
			.Where(x => SecConstants.SharesOutstandingForms.Contains(x.Form))
			.GroupBy(x => x.Concept, StringComparer.Ordinal)
			.ToDictionary(x => x.Key, x => new ConceptValues(x), StringComparer.Ordinal);

		var annualEnds = GetPeriodEnds(concepts, x => x.Annual);
		var quarterEnds = GetPeriodEnds(concepts, x => x.Quarters);
		var quarterly = quarterEnds
			.Select(end => BuildPeriod(concepts, end, annual: false, annualEnds))
			.OfType<FinancialPeriodDto>()
			.ToList();
		var annual = annualEnds
			.Select(end => BuildPeriod(concepts, end, annual: true, annualEnds))
			.OfType<FinancialPeriodDto>()
			.ToList();
		if (annual.Count == 0 && quarterly.Count == 0)
			return new FinancialsDto
			{
				Status = FinancialsStatus.NotFiled,
				EntityName = entityName,
			};

		return new FinancialsDto
		{
			Status = FinancialsStatus.Available,
			EntityName = entityName,
			Annual = annual.Take(FinancialsConstants.AnnualPeriods).ToList(),
			Quarterly = quarterly.Take(FinancialsConstants.QuarterlyPeriods).ToList(),
			TrailingTwelveMonths = GetTrailingTwelveMonths(quarterly),
		};
	}

	/// <summary>
	/// The newest four quarters added up, per flow line all four have; empty unless they follow
	/// each other and span at most <see cref="FinancialsConstants.MaxTrailingDays"/>.
	/// </summary>
	public static Dictionary<FinancialItem, decimal> GetTrailingTwelveMonths(
		IReadOnlyList<FinancialPeriodDto> quarters
	)
	{
		var sums = new Dictionary<FinancialItem, decimal>();
		if (quarters.Count < 4)
			return sums;
		var last = quarters.Take(4).ToList();
		for (var i = 0; i < 3; i++)
			if (Math.Abs(last[i].Start.DayNumber - 1 - last[i + 1].End.DayNumber) > DateTolerance)
				return sums;
		if (
			last[0].End.DayNumber - last[3].Start.DayNumber + 1
			> FinancialsConstants.MaxTrailingDays
		)
			return sums;

		foreach (var definition in FinancialsConstants.Items)
		{
			if (definition.Kind != FinancialValueKind.Flow)
				continue;
			var values = last.Select(x => x.Get(definition.Item)).ToList();
			if (values.All(x => x != null))
				sums[definition.Item] = values.Sum(x => x!.Value);
		}
		return sums;
	}

	/// <summary>
	/// One period's ratio (margins as fractions: 0.46 is 46%); null without the lines it needs,
	/// with a zero or negative denominator, and for return on equity of a quarter.
	/// </summary>
	public static double? GetRatio(FinancialRatio ratio, FinancialPeriodDto period)
	{
		var revenue = period.Get(FinancialItem.Revenue);
		var equity = period.Get(FinancialItem.Equity);
		return ratio switch
		{
			FinancialRatio.GrossMargin => Divide(period.Get(FinancialItem.GrossProfit), revenue),
			FinancialRatio.OperatingMargin => Divide(
				period.Get(FinancialItem.OperatingIncome),
				revenue
			),
			FinancialRatio.NetMargin => Divide(period.Get(FinancialItem.NetIncome), revenue),
			FinancialRatio.FreeCashFlowMargin => Divide(
				period.Get(FinancialItem.FreeCashFlow),
				revenue
			),
			FinancialRatio.ReturnOnEquity => period.FiscalQuarter == null
				? Divide(period.Get(FinancialItem.NetIncome), equity)
				: null,
			FinancialRatio.CurrentRatio => Divide(
				period.Get(FinancialItem.CurrentAssets),
				period.Get(FinancialItem.CurrentLiabilities)
			),
			FinancialRatio.DebtToEquity => GetDebt(period) is { } debt
				? Divide(debt, equity)
				: null,
			_ => null,
		};
	}

	/// <summary>Current plus long-term debt; null when neither is reported.</summary>
	public static decimal? GetDebt(FinancialPeriodDto period) =>
		period.Get(FinancialItem.CurrentDebt) is null
		&& period.Get(FinancialItem.LongTermDebt) is null
			? null
			: (period.Get(FinancialItem.CurrentDebt) ?? 0)
				+ (period.Get(FinancialItem.LongTermDebt) ?? 0);

	/// <summary>
	/// A valuation multiple: <paramref name="marketCap"/> over a positive amount (P/E over the
	/// trailing net income, P/S over revenue, P/B over equity); null otherwise.
	/// </summary>
	public static double? GetMultiple(double? marketCap, decimal? amount) =>
		marketCap is > 0 && amount is > 0 ? marketCap.Value / (double)amount.Value : null;

	/// <summary>
	/// The fiscal year a year ending on <paramref name="end"/> is named for: the year it ends in,
	/// except for a 52 / 53-week year ending in the first days of January (FY2021 ending
	/// Jan 1, 2022).
	/// </summary>
	public static int GetFiscalYear(DateOnly end) =>
		end.Month == 1 && end.Day <= DateTolerance ? end.Year - 1 : end.Year;

	/// <summary>
	/// The fiscal year and quarter of a quarter ending on <paramref name="end"/>, from the
	/// company's fiscal year ends (any order; a calendar year when there are none): the quarter is
	/// counted back from the next year end, which past the known ones is projected a year at a time.
	/// </summary>
	public static (int Year, int Quarter) GetFiscalQuarter(
		DateOnly end,
		IReadOnlyCollection<DateOnly> yearEnds
	)
	{
		var earliest = end.AddDays(-DateTolerance);
		DateOnly yearEnd;
		var known = yearEnds.Where(x => x >= earliest).ToList();
		if (known.Count > 0)
			yearEnd = known.Min();
		else
		{
			yearEnd = yearEnds.Count > 0 ? yearEnds.Max() : new DateOnly(end.Year - 1, 12, 31);
			while (yearEnd < earliest)
				yearEnd = yearEnd.AddYears(1);
		}
		var quartersLeft = (int)Math.Round((yearEnd.DayNumber - end.DayNumber) / DaysPerQuarter);
		return (GetFiscalYear(yearEnd), Math.Clamp(4 - quartersLeft, 1, 4));
	}

	// Newest first; the ends any of the period lines has a value for
	private static List<DateOnly> GetPeriodEnds(
		Dictionary<string, ConceptValues> concepts,
		Func<ConceptValues, Dictionary<DateOnly, PeriodValue>> pick
	) =>
		FinancialsConstants
			.PeriodItems.SelectMany(item => GetDefinition(item).Concepts)
			.Where(concepts.ContainsKey)
			.SelectMany(x => pick(concepts[x]).Keys)
			.Distinct()
			.OrderByDescending(x => x)
			.ToList();

	private static FinancialPeriodDto? BuildPeriod(
		Dictionary<string, ConceptValues> concepts,
		DateOnly end,
		bool annual,
		IReadOnlyCollection<DateOnly> yearEnds
	)
	{
		DateOnly? start = null;
		foreach (var item in FinancialsConstants.PeriodItems)
			if (Find(concepts, GetDefinition(item), end, annual, null) is { } value)
			{
				start = value.Start;
				break;
			}
		if (start is not { } periodStart)
			return null;

		var period = new FinancialPeriodDto { Start = periodStart, End = end };
		if (annual)
			period.FiscalYear = GetFiscalYear(end);
		else
			(period.FiscalYear, period.FiscalQuarter) = GetFiscalQuarter(end, yearEnds);

		foreach (var definition in FinancialsConstants.Items)
		{
			if (Find(concepts, definition, end, annual, periodStart) is not { } value)
				continue;
			period.Values[definition.Item] = value.Value;
			if (value.Derived)
				period.Derived.Add(definition.Item);
		}
		AddComputed(period);
		return period;
	}

	// The first concept of the line with a value for the period (instants: as of its end)
	private static PeriodValue? Find(
		Dictionary<string, ConceptValues> concepts,
		FinancialItemDefinitionDto definition,
		DateOnly end,
		bool annual,
		DateOnly? start
	)
	{
		foreach (var concept in definition.Concepts)
		{
			if (!concepts.TryGetValue(concept, out var values))
				continue;
			if (definition.Kind == FinancialValueKind.Instant)
			{
				if (values.Instants.TryGetValue(end, out var instant))
					return new PeriodValue(end, instant, false);
				continue;
			}
			var periods = annual ? values.Annual : values.Quarters;
			if (
				periods.TryGetValue(end, out var value)
				&& (definition.Kind == FinancialValueKind.Flow || !value.Derived)
				&& (
					start == null
					|| Math.Abs(value.Start.DayNumber - start.Value.DayNumber) <= DateTolerance
				)
			)
				return value;
		}
		return null;
	}

	private static void AddComputed(FinancialPeriodDto period)
	{
		if (
			period.Get(FinancialItem.GrossProfit) == null
			&& period.Get(FinancialItem.Revenue) is { } revenue
			&& period.Get(FinancialItem.CostOfRevenue) is { } cost
		)
			SetComputed(
				period,
				FinancialItem.GrossProfit,
				revenue - cost,
				FinancialItem.Revenue,
				FinancialItem.CostOfRevenue
			);
		if (
			period.Get(FinancialItem.OperatingCashFlow) is { } operating
			&& period.Get(FinancialItem.CapitalExpenditure) is { } capex
		)
			SetComputed(
				period,
				FinancialItem.FreeCashFlow,
				operating - capex,
				FinancialItem.OperatingCashFlow,
				FinancialItem.CapitalExpenditure
			);
		if (
			period.Get(FinancialItem.TotalLiabilities) == null
			&& period.Get(FinancialItem.LiabilitiesAndEquity) is { } total
			&& period.Get(FinancialItem.Equity) is { } equity
		)
			SetComputed(period, FinancialItem.TotalLiabilities, total - equity);
	}

	// Derived only when an input was worked out from years to date
	private static void SetComputed(
		FinancialPeriodDto period,
		FinancialItem item,
		decimal value,
		params FinancialItem[] inputs
	)
	{
		period.Values[item] = value;
		if (inputs.Any(period.Derived.Contains))
			period.Derived.Add(item);
	}

	private static double? Divide(decimal? numerator, decimal? denominator) =>
		numerator is { } n && denominator is > 0 ? (double)(n / denominator.Value) : null;

	private static FinancialItemDefinitionDto GetDefinition(FinancialItem item) =>
		FinancialsConstants.Items.First(x => x.Item == item);

	private static int GetDays(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

	private readonly record struct PeriodValue(DateOnly Start, decimal Value, bool Derived);

	// One concept's values by period end: fiscal years, quarters (reported, or worked out from
	// two years to date with the same start) and balances
	private sealed class ConceptValues
	{
		public ConceptValues(IEnumerable<SecFactDto> facts)
		{
			// The latest filing's value of each period
			var latest = facts
				.GroupBy(x => (x.Start, x.End))
				.Select(x => x.OrderByDescending(f => f.Filed).First())
				.ToList();
			foreach (var fact in latest.Where(x => x.Start == null))
				Instants[fact.End] = fact.Value;

			var durations = latest.Where(x => x.Start != null).ToList();
			foreach (var fact in durations)
			{
				var days = GetDays(fact.Start!.Value, fact.End);
				var value = new PeriodValue(fact.Start.Value, fact.Value, false);
				if (
					days
					is >= FinancialsConstants.MinYearDays
						and <= FinancialsConstants.MaxYearDays
				)
					Annual[fact.End] = value;
				else if (
					days
					is >= FinancialsConstants.MinQuarterDays
						and <= FinancialsConstants.MaxQuarterDays
				)
					Quarters[fact.End] = value;
			}

			// 6 months less 3, 9 less 6, the year less 9: each pair of years to date with one start
			foreach (var group in durations.GroupBy(x => x.Start))
			{
				var ordered = group
					.Where(x => GetDays(x.Start!.Value, x.End) <= FinancialsConstants.MaxYearDays)
					.OrderBy(x => x.End)
					.ToList();
				for (var i = 1; i < ordered.Count; i++)
				{
					var (previous, current) = (ordered[i - 1], ordered[i]);
					var days = current.End.DayNumber - previous.End.DayNumber;
					if (
						days
							is < FinancialsConstants.MinQuarterDays
								or > FinancialsConstants.MaxQuarterDays
						|| Quarters.ContainsKey(current.End)
					)
						continue;
					Quarters[current.End] = new PeriodValue(
						previous.End.AddDays(1),
						current.Value - previous.Value,
						true
					);
				}
			}
		}

		public Dictionary<DateOnly, PeriodValue> Annual { get; } = [];
		public Dictionary<DateOnly, PeriodValue> Quarters { get; } = [];
		public Dictionary<DateOnly, decimal> Instants { get; } = [];
	}
}
