using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Fundamentals;

/// <summary>
/// One statement line: its label and the us-gaap concepts companies report it under, in order of
/// preference — the first one with a value for a period wins (companies change tags over time:
/// Apple's revenue is <c>Revenues</c> until 2018, then
/// <c>RevenueFromContractWithCustomerExcludingAssessedTax</c>). No concepts: always worked out
/// from other lines (free cash flow). A null label: read only to work other lines out.
/// <see cref="Description"/> says what the line means, <see cref="ToolTip"/> how it is read or
/// worked out here; both show on hover.
/// </summary>
public record FinancialItemDefinitionDto(
	FinancialItem Item,
	FinancialStatement Statement,
	FinancialValueKind Kind,
	string? Label,
	IReadOnlyList<string> Concepts,
	string? ToolTip = null,
	string? Description = null
);
