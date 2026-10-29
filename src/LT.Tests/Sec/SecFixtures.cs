namespace LT.Tests.Sec;

/// <summary>
/// EDGAR's latest filings feed (browse-edgar?action=getcurrent&amp;type=8-K&amp;output=atom) as
/// served on 2026-10-29: the header and three of its entries, as sent.
/// </summary>
public static class SecFixtures
{
	/// <summary>Acurx (Items 5.03, 9.01: not results), Intel and Amazon (Item 2.02, after the close).</summary>
	public const string CurrentFilings = """
		<?xml version="1.0" encoding="ISO-8859-1" ?>
		<feed xmlns="http://www.w3.org/2005/Atom">
		<title>Latest Filings - Thu, 29 Oct 2026 17:22:28 EDT</title>
		<link rel="alternate" href="/cgi-bin/browse-edgar?action=getcurrent"/>
		<link rel="self" href="/cgi-bin/browse-edgar?action=getcurrent"/>
		<id>https://www.sec.gov/cgi-bin/browse-edgar?action=getcurrent</id>
		<author><name>Webmaster</name><email>webmaster@sec.gov</email></author>
		<updated>2026-10-29T17:22:28-04:00</updated>
		<entry>
		<title>8-K - Acurx Pharmaceuticals, Inc. (0001736243) (Filer)</title>
		<link rel="alternate" type="text/html" href="https://www.sec.gov/Archives/edgar/data/1736243/000110465926120279/0001104659-26-120279-index.htm"/>
		<summary type="html">
		 &lt;b&gt;Filed:&lt;/b&gt; 2026-10-29 &lt;b&gt;AccNo:&lt;/b&gt; 0001104659-26-120279 &lt;b&gt;Size:&lt;/b&gt; 197 KB
		&lt;br&gt;Item 5.03: Amendments to Articles of Incorporation or Bylaws; Change in Fiscal Year
		&lt;br&gt;Item 9.01: Financial Statements and Exhibits
		</summary>
		<updated>2026-10-29T17:20:58-04:00</updated>
		<category scheme="https://www.sec.gov/" label="form type" term="8-K"/>
		<id>urn:tag:sec.gov,2008:accession-number=0001104659-26-120279</id>
		</entry>
		<entry>
		<title>8-K - INTEL CORP (0000050863) (Filer)</title>
		<link rel="alternate" type="text/html" href="https://www.sec.gov/Archives/edgar/data/50863/000005086326000188/0000050863-26-000188-index.htm"/>
		<summary type="html">
		 &lt;b&gt;Filed:&lt;/b&gt; 2026-10-29 &lt;b&gt;AccNo:&lt;/b&gt; 0000050863-26-000188 &lt;b&gt;Size:&lt;/b&gt; 646 KB
		&lt;br&gt;Item 2.02: Results of Operations and Financial Condition
		&lt;br&gt;Item 9.01: Financial Statements and Exhibits
		</summary>
		<updated>2026-10-29T16:08:53-04:00</updated>
		<category scheme="https://www.sec.gov/" label="form type" term="8-K"/>
		<id>urn:tag:sec.gov,2008:accession-number=0000050863-26-000188</id>
		</entry>
		<entry>
		<title>8-K - AMAZON COM INC (0001018724) (Filer)</title>
		<link rel="alternate" type="text/html" href="https://www.sec.gov/Archives/edgar/data/1018724/000101872426000038/0001018724-26-000038-index.htm"/>
		<summary type="html">
		 &lt;b&gt;Filed:&lt;/b&gt; 2026-10-29 &lt;b&gt;AccNo:&lt;/b&gt; 0001018724-26-000038 &lt;b&gt;Size:&lt;/b&gt; 1 MB
		&lt;br&gt;Item 2.02: Results of Operations and Financial Condition
		&lt;br&gt;Item 9.01: Financial Statements and Exhibits
		</summary>
		<updated>2026-10-29T16:05:27-04:00</updated>
		<category scheme="https://www.sec.gov/" label="form type" term="8-K"/>
		<id>urn:tag:sec.gov,2008:accession-number=0001018724-26-000038</id>
		</entry>
		</feed>
		""";
}
