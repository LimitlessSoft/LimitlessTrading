namespace LT.Tests.Nasdaq;

/// <summary>
/// Real answers of api.nasdaq.com/api/calendar/* recorded on 2026-10-27 (browser User-Agent),
/// rows trimmed to a few and long descriptions cut; field names, nesting and values as sent.
/// </summary>
public static class NasdaqFixtures
{
	/// <summary>earnings?date=2026-10-27: after hours, pre-market and "not supplied".</summary>
	public const string Earnings = """
		{"data":{"asOf":"Tue, Oct 27, 2026","headers":{"time":"Time","symbol":"Symbol","name":"Company Name","marketCap":"Market Cap","fiscalQuarterEnding":"Fiscal Quarter Ending","epsForecast":"Consensus EPS* Forecast","noOfEsts":"# of Ests","lastYearRptDt":"Last Year's Report Date","lastYearEPS":"Last year's EPS*"},"rows":[{"lastYearRptDt":"10/28/2025","lastYearEPS":"$2.98","time":"time-after-hours","symbol":"V","name":"Visa Inc.","marketCap":"$722,135,020,000","fiscalQuarterEnding":"Sep/2026","epsForecast":"$3.43","noOfEsts":"12"},{"lastYearRptDt":"10/21/2025","lastYearEPS":"$0.82","time":"time-pre-market","symbol":"KO","name":"Coca-Cola Company (The)","marketCap":"$398,846,329,000","fiscalQuarterEnding":"Sep/2026","epsForecast":"$0.87","noOfEsts":"8"},{"lastYearRptDt":"10/28/2025","lastYearEPS":"$1.80","time":"time-not-supplied","symbol":"HSBC","name":"HSBC Holdings plc","marketCap":"$310,460,088,000","fiscalQuarterEnding":"Sep/2026","epsForecast":"$2.13","noOfEsts":"1"}]},"message":null,"status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
		""";

	/// <summary>dividends?date=2026-10-27: rows under data.calendar, the rate a number.</summary>
	public const string Dividends = """
		{"data":{"calendar":{"asOf":"Tue, Oct 27, 2026","headers":{"symbol":"Symbol","companyName":"Name","dividend_Ex_Date":"Ex-Dividend Date","payment_Date":"Payment Date","record_Date":"Record Date","dividend_Rate":"Dividend","indicated_Annual_Dividend":"Historical Annual Dividend","announcement_Date":"Announcement Date"},"rows":[{"companyName":"ASML Holding N.V. New York Registry Shares","symbol":"ASML","dividend_Ex_Date":"10/27/2026","payment_Date":"11/04/2026","record_Date":"10/27/2026","dividend_Rate":2.107668,"indicated_Annual_Dividend":8.60288,"announcement_Date":"10/15/2026"},{"companyName":"WisdomTree Voya Yield Enhanced USD Universal Bond Fund","symbol":"UNIY","dividend_Ex_Date":"10/27/2026","payment_Date":"10/29/2026","record_Date":"10/27/2026","dividend_Rate":0.21,"indicated_Annual_Dividend":2.52,"announcement_Date":"10/26/2026"}]},"timeframe":{"minDate":"1988-10-03T00:00:00","maxDate":"2104-06-02T00:00:00"}},"message":null,"status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
		""";

	/// <summary>splits?date=2026-10-27: every split from that day on, each with its own date.</summary>
	public const string Splits = """
		{"data":{"asOf":"Tue, Oct 27, 2026","headers":{"symbol":"SYMBOL","name":"COMPANY","ratio":"RATIO","executionDate":"EFFECTIVE DATE"},"rows":[{"symbol":"MMT","name":"Aberdeen Multi-Market Income Fund","ratio":"1 : 4","executionDate":"11/09/2026"},{"symbol":"SOXX","name":"iShares PHLX SOX Semiconductor Sector Index Fund","ratio":"3:1","executionDate":"11/05/2026"},{"symbol":"NUWE","name":"Nuwellis, Inc.","ratio":"1:9","executionDate":"10/27/2026"}]},"message":null,"status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
		""";

	/// <summary>
	/// economicevents?date=2026-10-27 (Monday 26 October's releases): a release listed twice with
	/// different figures, "&amp;nbsp;" for none, an all-day holiday.
	/// </summary>
	public const string Economic = """
		{"data":{"asOf":"Tue, Oct 27, 2026","headers":{"gmt":"Time","country":"Country","eventName":"Event","actual":"Actual","consensus":"Consensus","previous":"Previous","description":"Description"},"rows":[{"gmt":"01:00","country":"Japan","eventName":"Leading Index","actual":"0.4%","consensus":"0.4%","previous":"1.5%","description":"The Leading Indicators Index is a composite index based on 1"},{"gmt":"01:00","country":"Japan","eventName":"Leading Index","actual":"118.0","consensus":"118.0","previous":"117.7","description":"The Leading Indicators Index is a composite index based on 1"},{"gmt":"02:00","country":"Euro Zone","eventName":"ECB's Elderson Speaks","actual":"&nbsp;","consensus":" ","previous":"&nbsp;","description":"Frank Elderson, member of the Executive Board and Vice-Chair"},{"gmt":"08:30","country":"United States","eventName":"Chicago Fed National Activity","actual":"0.07","consensus":"-0.10","previous":"-0.08","description":"A monthly report by the Chicago Federal Reserve Bank that tr"},{"gmt":"All Day","country":"New Zealand","eventName":"New Zealand - Labor Day","actual":" ","consensus":" ","previous":" ","description":""}]},"message":null,"status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
		""";

	/// <summary>earnings?date=2026-10-31 (a Saturday): rows null.</summary>
	public const string EarningsWeekend = """
		{"data":{"asOf":"Sat, Oct 31, 2026","headers":null,"rows":null},"message":null,"status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
		""";

	/// <summary>dividends?date=2026-10-31: rows null and a message.</summary>
	public const string DividendsWeekend = """
		{"data":{"calendar":{"asOf":"Sat, Oct 31, 2026","headers":null,"rows":null},"timeframe":{"minDate":"1988-10-03T00:00:00","maxDate":"2104-06-02T00:00:00"}},"message":"There are no dividends to show for this date.","status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
		""";

	/// <summary>earnings?date=2026-12-25: no data, business code 1002.</summary>
	public const string NoRecord = """
		{"data":null,"message":null,"status":{"rCode":200,"bCodeMessage":[{"code":1002,"errorMessage":"Earnings Calendar: No record found."}],"developerMessage":null}}
		""";

	/// <summary>earnings?date=garbage: status 400, business code 1011.</summary>
	public const string BadParameter = """
		{"data":null,"message":null,"status":{"rCode":400,"bCodeMessage":[{"code":1011,"errorMessage":"Bad or No parameter garbage"}],"developerMessage":null}}
		""";
}
