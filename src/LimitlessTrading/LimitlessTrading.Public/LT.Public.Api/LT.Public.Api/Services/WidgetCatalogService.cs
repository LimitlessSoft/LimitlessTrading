using LT.Public.Api.Models;

namespace LT.Public.Api.Services;

public class WidgetCatalogService
{
    private static readonly WidgetEntry[] Catalog =
    [
        new(
            "chart",
            "Chart",
            "Candlestick / line chart with drawing tools and indicators.",
            ["price", "technicals"],
            "/gallery/previews/chart.png",
            new { type = "chart", symbol = "SPY", width = 12, height = 400 }
        ),
        new(
            "watchlist",
            "Watchlist",
            "Scrollable table of live prices for a configurable list of symbols.",
            ["price"],
            "/gallery/previews/watchlist.png",
            new { type = "watchlist", width = 6, height = 300 }
        ),
        new(
            "positions",
            "Positions",
            "Current open positions from the connected IBKR account with live P&L.",
            ["portfolio"],
            "/gallery/previews/positions.png",
            new { type = "positions", width = 12, height = 300 }
        ),
        new(
            "options-chain",
            "Options Chain",
            "Calls and puts side-by-side across strikes for a selected expiration date.",
            ["options"],
            "/gallery/previews/options-chain.png",
            new { type = "options-chain", symbol = "SPY", width = 20, height = 400 }
        ),
        new(
            "news",
            "News Feed",
            "Financial news headlines for a symbol or the broader market.",
            ["news"],
            "/gallery/previews/news.png",
            new { type = "news", symbol = "SPY", width = 8, height = 400 }
        ),
        new(
            "news-ticker",
            "News Ticker",
            "Horizontally scrolling news headline ticker.",
            ["news"],
            "/gallery/previews/news-ticker.png",
            new { type = "news-ticker", width = 20, height = 40 }
        ),
        new(
            "clock",
            "Clock",
            "Live clock showing exchange local time and current market session status.",
            ["tools"],
            "/gallery/previews/clock.png",
            new { type = "clock", exchange = "NYSE", width = 4, height = 60 }
        ),
        new(
            "notes",
            "Notes",
            "Freeform text notes widget for annotations and reminders.",
            ["tools"],
            "/gallery/previews/notes.png",
            new { type = "notes", width = 8, height = 200 }
        ),
    ];

    public IReadOnlyList<WidgetEntry> GetAll() => Catalog;
}
