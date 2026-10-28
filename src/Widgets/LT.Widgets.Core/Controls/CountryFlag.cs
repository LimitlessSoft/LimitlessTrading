using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace LT.App.Controls;

/// <summary>
/// A small country flag (3:2, 18×12 by default), drawn from shapes rather than an emoji or an
/// image: Windows draws regional-indicator emoji as two letters, and drawn flags look the same on
/// every platform. Simplified to what reads at icon size (the US flag has seven stripes and no
/// stars). <see cref="Code"/> is one of <see cref="Contracts.Helpers.CountryFlagHelpers.Codes"/>;
/// an unknown or empty code draws nothing.
/// </summary>
public class CountryFlag : Control
{
	public static readonly StyledProperty<string?> CodeProperty = AvaloniaProperty.Register<
		CountryFlag,
		string?
	>(nameof(Code));

	// A faint light edge, so dark flags still stand out on the dark panels
	private static readonly IPen Edge = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
		1
	);

	// Drawn on the UI thread only
	private static readonly Dictionary<string, IBrush> Brushes = [];

	// A maple leaf around (0, 0), from -1 to 1 each way (y down)
	private static readonly (double X, double Y)[] MapleLeaf =
	[
		(0, -1),
		(0.2, -0.6),
		(0.45, -0.7),
		(0.35, -0.15),
		(0.85, -0.45),
		(0.75, -0.2),
		(1, -0.1),
		(0.6, 0.3),
		(0.7, 0.45),
		(0.1, 0.4),
		(0.1, 0.8),
		(-0.1, 0.8),
		(-0.1, 0.4),
		(-0.7, 0.45),
		(-0.6, 0.3),
		(-1, -0.1),
		(-0.75, -0.2),
		(-0.85, -0.45),
		(-0.35, -0.15),
		(-0.45, -0.7),
		(-0.2, -0.6),
	];

	private static readonly Dictionary<string, Action<FlagPainter>> Painters = new(
		StringComparer.OrdinalIgnoreCase
	)
	{
		["US"] = p =>
		{
			p.HStripes("#B22234", "#FFFFFF", "#B22234", "#FFFFFF", "#B22234", "#FFFFFF", "#B22234");
			p.Rect(0, 0, 0.42, 4 / 7.0, "#3C3B6E");
		},
		["EU"] = p =>
		{
			p.Fill("#003399");
			for (var i = 0; i < 12; i++)
			{
				var angle = i * Math.PI / 6;
				p.Disc(0.5 + Math.Sin(angle) * 0.2, 0.5 - Math.Cos(angle) * 0.3, 0.055, "#FFCC00");
			}
		},
		["GB"] = p => p.UnionJack(0, 0, 1, 1),
		["JP"] = p =>
		{
			p.Fill("#FFFFFF");
			p.Disc(0.5, 0.5, 0.3, "#BC002D");
		},
		["CN"] = p =>
		{
			p.Fill("#EE1C25");
			p.Star(5 / 30.0, 5 / 20.0, 0.15, "#FFFF00");
			p.Star(10 / 30.0, 2 / 20.0, 0.05, "#FFFF00");
			p.Star(12 / 30.0, 4 / 20.0, 0.05, "#FFFF00");
			p.Star(12 / 30.0, 7 / 20.0, 0.05, "#FFFF00");
			p.Star(10 / 30.0, 9 / 20.0, 0.05, "#FFFF00");
		},
		["DE"] = p => p.HStripes("#000000", "#DD0000", "#FFCE00"),
		["FR"] = p => p.VStripes("#0055A4", "#FFFFFF", "#EF4135"),
		["IT"] = p => p.VStripes("#009246", "#FFFFFF", "#CE2B37"),
		["ES"] = p => p.HBands((1, "#AA151B"), (2, "#F1BF00"), (1, "#AA151B")),
		["CA"] = p =>
		{
			p.VBands((1, "#D52B1E"), (2, "#FFFFFF"), (1, "#D52B1E"));
			p.Polygon("#D52B1E", MapleLeaf, 0.5, 0.5, 0.36);
		},
		["AU"] = p =>
		{
			p.Fill("#012169");
			p.UnionJack(0, 0, 0.5, 0.5);
			p.Star(0.25, 0.75, 0.13, "#FFFFFF", points: 7);
			p.Star(0.75, 0.84, 0.07, "#FFFFFF", points: 7);
			p.Star(0.63, 0.45, 0.07, "#FFFFFF", points: 7);
			p.Star(0.75, 0.17, 0.07, "#FFFFFF", points: 7);
			p.Star(0.87, 0.38, 0.07, "#FFFFFF", points: 7);
		},
		["NZ"] = p =>
		{
			p.Fill("#012169");
			p.UnionJack(0, 0, 0.5, 0.5);
			p.Star(0.75, 0.82, 0.08, "#C8102E");
			p.Star(0.64, 0.45, 0.07, "#C8102E");
			p.Star(0.75, 0.2, 0.07, "#C8102E");
			p.Star(0.86, 0.4, 0.07, "#C8102E");
		},
		["CH"] = p =>
		{
			p.Fill("#DA291C");
			p.CenteredBar(0.5, 0.5, 0.62, 0.19, "#FFFFFF");
			p.CenteredBar(0.5, 0.5, 0.19, 0.62, "#FFFFFF");
		},
		["SE"] = p => p.NordicCross("#006AA7", "#FECC02", 5 / 16.0, 7 / 16.0, 0.4, 0.6),
		["NO"] = p =>
		{
			p.NordicCross("#BA0C2F", "#FFFFFF", 6 / 22.0, 10 / 22.0, 6 / 16.0, 10 / 16.0);
			p.Cross("#00205B", 7 / 22.0, 9 / 22.0, 7 / 16.0, 9 / 16.0);
		},
		["DK"] = p =>
			p.NordicCross("#C8102E", "#FFFFFF", 12 / 37.0, 16 / 37.0, 12 / 28.0, 16 / 28.0),
		["FI"] = p => p.NordicCross("#FFFFFF", "#002F6C", 5 / 18.0, 8 / 18.0, 4 / 11.0, 7 / 11.0),
		["IS"] = p =>
		{
			p.NordicCross("#02529C", "#FFFFFF", 7 / 25.0, 11 / 25.0, 7 / 18.0, 11 / 18.0);
			p.Cross("#DC1E35", 8 / 25.0, 10 / 25.0, 8 / 18.0, 10 / 18.0);
		},
		["IE"] = p => p.VStripes("#169B62", "#FFFFFF", "#FF883E"),
		["NL"] = p => p.HStripes("#AE1C28", "#FFFFFF", "#21468B"),
		["BE"] = p => p.VStripes("#000000", "#FDDA24", "#EF3340"),
		["AT"] = p => p.HStripes("#C8102E", "#FFFFFF", "#C8102E"),
		["PT"] = p =>
		{
			p.VBands((2, "#006600"), (3, "#FF0000"));
			p.Disc(0.4, 0.5, 0.2, "#FFCC00");
		},
		["GR"] = p =>
		{
			p.HStripes(
				"#0D5EAF",
				"#FFFFFF",
				"#0D5EAF",
				"#FFFFFF",
				"#0D5EAF",
				"#FFFFFF",
				"#0D5EAF",
				"#FFFFFF",
				"#0D5EAF"
			);
			p.Rect(0, 0, 10 / 27.0, 10 / 18.0, "#0D5EAF");
			p.Rect(0, 4 / 18.0, 10 / 27.0, 2 / 18.0, "#FFFFFF");
			p.Rect(4 / 27.0, 0, 2 / 27.0, 10 / 18.0, "#FFFFFF");
		},
		["PL"] = p => p.HStripes("#FFFFFF", "#DC143C"),
		["CZ"] = p =>
		{
			p.HStripes("#FFFFFF", "#D7141A");
			p.Polygon("#11457E", (0, 0), (0.5, 0.5), (0, 1));
		},
		["HU"] = p => p.HStripes("#CD2A3E", "#FFFFFF", "#436F4D"),
		["RU"] = p => p.HStripes("#FFFFFF", "#0039A6", "#D52B1E"),
		["UA"] = p => p.HStripes("#0057B7", "#FFD700"),
		["TR"] = p =>
		{
			p.Fill("#E30A17");
			p.Disc(0.36, 0.5, 0.25, "#FFFFFF");
			p.Disc(0.4, 0.5, 0.2, "#E30A17");
			p.Star(0.56, 0.5, 0.12, "#FFFFFF", rotation: -90);
		},
		["IN"] = p =>
		{
			p.HStripes("#FF9933", "#FFFFFF", "#138808");
			p.Ring(0.5, 0.5, 0.12, 0.035, "#000080");
			p.Disc(0.5, 0.5, 0.03, "#000080");
		},
		["KR"] = p =>
		{
			p.Fill("#FFFFFF");
			p.HalfDiscs(0.5, 0.5, 0.25, "#CD2E3A", "#0047A0");
			p.Line(0.2, 0.18, 0.3, 0.32, 0.1, "#000000");
			p.Line(0.8, 0.82, 0.7, 0.68, 0.1, "#000000");
			p.Line(0.8, 0.18, 0.7, 0.32, 0.1, "#000000");
			p.Line(0.2, 0.82, 0.3, 0.68, 0.1, "#000000");
		},
		["HK"] = p =>
		{
			p.Fill("#DE2910");
			p.Star(0.5, 0.5, 0.3, "#FFFFFF");
		},
		["SG"] = p =>
		{
			p.HStripes("#EF3340", "#FFFFFF");
			p.Disc(0.2, 0.25, 0.17, "#FFFFFF");
			p.Disc(0.245, 0.25, 0.16, "#EF3340");
			p.Star(0.33, 0.25, 0.06, "#FFFFFF");
		},
		["TW"] = p =>
		{
			p.Fill("#FE0000");
			p.Rect(0, 0, 0.5, 0.5, "#000095");
			p.Star(0.25, 0.25, 0.17, "#FFFFFF", points: 12);
		},
		["TH"] = p =>
			p.HBands(
				(1, "#A51931"),
				(1, "#F4F5F8"),
				(2, "#2D2A4A"),
				(1, "#F4F5F8"),
				(1, "#A51931")
			),
		["ID"] = p => p.HStripes("#CE1126", "#FFFFFF"),
		["ZA"] = p =>
		{
			p.HStripes("#E03C31", "#001489");
			p.Chevron(0.4, 0.4, "#FFFFFF");
			p.Chevron(0.4, 0.24, "#007749");
			p.Polygon("#FFB81C", (0, 0.12), (0.34, 0.5), (0, 0.88));
			p.Polygon("#000000", (0, 0.22), (0.26, 0.5), (0, 0.78));
		},
		["BR"] = p =>
		{
			p.Fill("#009C3B");
			p.Polygon("#FFDF00", (0.08, 0.5), (0.5, 0.1), (0.92, 0.5), (0.5, 0.9));
			p.Disc(0.5, 0.5, 0.22, "#002776");
		},
		["MX"] = p =>
		{
			p.VStripes("#006847", "#FFFFFF", "#CE1126");
			p.Disc(0.5, 0.5, 0.1, "#8C5A2B");
		},
		["AR"] = p =>
		{
			p.HStripes("#74ACDF", "#FFFFFF", "#74ACDF");
			p.Disc(0.5, 0.5, 0.1, "#F6B40E");
		},
		["CL"] = p =>
		{
			p.HStripes("#FFFFFF", "#D52B1E");
			p.Rect(0, 0, 1 / 3.0, 0.5, "#0039A6");
			p.Star(1 / 6.0, 0.25, 0.13, "#FFFFFF");
		},
		["CO"] = p => p.HBands((2, "#FCD116"), (1, "#003893"), (1, "#CE1126")),
		["IL"] = p =>
		{
			p.Fill("#FFFFFF");
			p.Rect(0, 0.1, 1, 0.15, "#0038B8");
			p.Rect(0, 0.75, 1, 0.15, "#0038B8");
			p.StarOfDavid(0.5, 0.5, 0.2, 0.05, "#0038B8");
		},
		["SA"] = p =>
		{
			p.Fill("#006C35");
			p.Rect(0.25, 0.3, 0.5, 0.14, "#FFFFFF");
			p.Rect(0.28, 0.62, 0.44, 0.05, "#FFFFFF");
		},
		["AE"] = p =>
		{
			p.HStripes("#00732F", "#FFFFFF", "#000000");
			p.Rect(0, 0, 0.25, 1, "#FF0000");
		},
	};

	static CountryFlag()
	{
		AffectsRender<CountryFlag>(CodeProperty);
		WidthProperty.OverrideDefaultValue<CountryFlag>(18);
		HeightProperty.OverrideDefaultValue<CountryFlag>(12);
	}

	/// <summary>The flag's code (<c>US</c>, <c>EU</c> …), see <see cref="Contracts.Helpers.CountryFlagHelpers"/>.</summary>
	public string? Code
	{
		get => GetValue(CodeProperty);
		set => SetValue(CodeProperty, value);
	}

	/// <summary>Whether a flag is drawn for <paramref name="code"/>.</summary>
	public static bool CanDraw(string code) => Painters.ContainsKey(code);

	public override void Render(DrawingContext context)
	{
		if (Code is not { } code || !Painters.TryGetValue(code, out var paint))
			return;
		var bounds = new Rect(Bounds.Size);
		if (bounds.Width <= 0 || bounds.Height <= 0)
			return;
		var shape = new RoundedRect(bounds, 1.5);
		using (context.PushClip(shape))
			paint(new FlagPainter(context, bounds));
		context.DrawRectangle(null, Edge, shape.Deflate(0.5, 0.5));
	}

	/// <summary>
	/// Draws shapes placed in fractions of the flag: x of its width, y of its height; sizes (a
	/// radius, a thickness) are fractions of its height.
	/// </summary>
	private sealed class FlagPainter(DrawingContext context, Rect bounds)
	{
		private Point At(double x, double y) =>
			new(bounds.X + x * bounds.Width, bounds.Y + y * bounds.Height);

		private double Size(double fraction) => fraction * bounds.Height;

		private static IBrush Brush(string color)
		{
			if (!Brushes.TryGetValue(color, out var brush))
				Brushes[color] = brush = new ImmutableSolidColorBrush(Color.Parse(color));
			return brush;
		}

		public void Fill(string color) => context.DrawRectangle(Brush(color), null, bounds);

		public void Rect(double x, double y, double width, double height, string color) =>
			context.DrawRectangle(
				Brush(color),
				null,
				new Rect(At(x, y), At(x + width, y + height))
			);

		public void HStripes(params string[] colors) => HBands([.. colors.Select(x => (1.0, x))]);

		public void VStripes(params string[] colors) => VBands([.. colors.Select(x => (1.0, x))]);

		public void HBands(params (double Weight, string Color)[] bands)
		{
			var total = bands.Sum(x => x.Weight);
			var y = 0.0;
			foreach (var (weight, color) in bands)
			{
				Rect(0, y, 1, weight / total, color);
				y += weight / total;
			}
		}

		public void VBands(params (double Weight, string Color)[] bands)
		{
			var total = bands.Sum(x => x.Weight);
			var x = 0.0;
			foreach (var (weight, color) in bands)
			{
				Rect(x, 0, weight / total, 1, color);
				x += weight / total;
			}
		}

		/// <summary>A bar <paramref name="width"/> × <paramref name="height"/> (fractions of the height) around a point.</summary>
		public void CenteredBar(double x, double y, double width, double height, string color)
		{
			var center = At(x, y);
			context.DrawRectangle(
				Brush(color),
				null,
				new Rect(
					center.X - Size(width) / 2,
					center.Y - Size(height) / 2,
					Size(width),
					Size(height)
				)
			);
		}

		/// <summary>A cross over the whole flag: the vertical arm from x0 to x1, the horizontal from y0 to y1.</summary>
		public void Cross(string color, double x0, double x1, double y0, double y1)
		{
			Rect(x0, 0, x1 - x0, 1, color);
			Rect(0, y0, 1, y1 - y0, color);
		}

		public void NordicCross(
			string background,
			string color,
			double x0,
			double x1,
			double y0,
			double y1
		)
		{
			Fill(background);
			Cross(color, x0, x1, y0, y1);
		}

		public void Disc(double x, double y, double radius, string color) =>
			context.DrawEllipse(Brush(color), null, At(x, y), Size(radius), Size(radius));

		public void Ring(double x, double y, double radius, double thickness, string color) =>
			context.DrawEllipse(
				null,
				new Pen(Brush(color), Size(thickness)),
				At(x, y),
				Size(radius),
				Size(radius)
			);

		/// <summary>A disc, its top half one colour and its bottom half another.</summary>
		public void HalfDiscs(double x, double y, double radius, string top, string bottom)
		{
			var center = At(x, y);
			var r = Size(radius);
			using (context.PushClip(new Rect(center.X - r, center.Y - r, 2 * r, r)))
				context.DrawEllipse(Brush(top), null, center, r, r);
			using (context.PushClip(new Rect(center.X - r, center.Y, 2 * r, r)))
				context.DrawEllipse(Brush(bottom), null, center, r, r);
		}

		public void Line(
			double x0,
			double y0,
			double x1,
			double y1,
			double thickness,
			string color
		) => context.DrawLine(new Pen(Brush(color), Size(thickness)), At(x0, y0), At(x1, y1));

		/// <summary>A star of <paramref name="points"/> points, the first at 12 o'clock plus <paramref name="rotation"/> degrees.</summary>
		public void Star(
			double x,
			double y,
			double radius,
			string color,
			int points = 5,
			double rotation = 0
		)
		{
			var center = At(x, y);
			var outer = Size(radius);
			var inner = outer * (points == 5 ? 0.382 : 0.5);
			var geometry = new StreamGeometry();
			using (var g = geometry.Open())
			{
				for (var i = 0; i < points * 2; i++)
				{
					var angle = (rotation - 90 + i * 180.0 / points) * Math.PI / 180;
					var r = i % 2 == 0 ? outer : inner;
					var point = new Point(
						center.X + Math.Cos(angle) * r,
						center.Y + Math.Sin(angle) * r
					);
					if (i == 0)
						g.BeginFigure(point, true);
					else
						g.LineTo(point);
				}
				g.EndFigure(true);
			}
			context.DrawGeometry(Brush(color), null, geometry);
		}

		/// <summary>Two outlined triangles, one pointing up and one down.</summary>
		public void StarOfDavid(double x, double y, double radius, double thickness, string color)
		{
			var center = At(x, y);
			var r = Size(radius);
			var pen = new Pen(Brush(color), Size(thickness));
			foreach (var start in new[] { -90.0, 90.0 })
			{
				var geometry = new StreamGeometry();
				using (var g = geometry.Open())
				{
					for (var i = 0; i < 3; i++)
					{
						var angle = (start + i * 120) * Math.PI / 180;
						var point = new Point(
							center.X + Math.Cos(angle) * r,
							center.Y + Math.Sin(angle) * r
						);
						if (i == 0)
							g.BeginFigure(point, false);
						else
							g.LineTo(point);
					}
					g.EndFigure(true);
				}
				context.DrawGeometry(null, pen, geometry);
			}
		}

		public void Polygon(string color, params (double X, double Y)[] points) =>
			DrawPolygon(color, points.Select(p => At(p.X, p.Y)));

		/// <summary><paramref name="shape"/> (from -1 to 1 each way) scaled to <paramref name="radius"/> around a point.</summary>
		public void Polygon(
			string color,
			IEnumerable<(double X, double Y)> shape,
			double x,
			double y,
			double radius
		)
		{
			var center = At(x, y);
			var r = Size(radius);
			DrawPolygon(
				color,
				shape.Select(p => new Point(center.X + p.X * r, center.Y + p.Y * r))
			);
		}

		/// <summary>South Africa's Y: arms from the hoist's corners meeting at x, on to the fly.</summary>
		public void Chevron(double x, double thickness, string color)
		{
			var pen = new Pen(Brush(color), Size(thickness), lineJoin: PenLineJoin.Miter);
			var meet = At(x, 0.5);
			var geometry = new StreamGeometry();
			using (var g = geometry.Open())
			{
				g.BeginFigure(At(-x * 0.5, -0.25), false);
				g.LineTo(meet);
				g.LineTo(At(-x * 0.5, 1.25));
				g.EndFigure(false);
				g.BeginFigure(meet, false);
				g.LineTo(At(1.1, 0.5));
				g.EndFigure(false);
			}
			context.DrawGeometry(null, pen, geometry);
		}

		/// <summary>The Union Jack in the part of the flag from (x, y), width × height as fractions.</summary>
		public void UnionJack(double x, double y, double width, double height)
		{
			var area = new Rect(At(x, y), At(x + width, y + height));
			var h = area.Height;
			using (context.PushClip(area))
			{
				context.DrawRectangle(Brush("#012169"), null, area);
				var white = new Pen(Brush("#FFFFFF"), h * 0.2);
				var red = new Pen(Brush("#C8102E"), h * 0.07);
				foreach (var pen in new[] { white, red })
				{
					context.DrawLine(pen, area.TopLeft, area.BottomRight);
					context.DrawLine(pen, area.BottomLeft, area.TopRight);
				}
				var center = area.Center;
				context.DrawRectangle(
					Brush("#FFFFFF"),
					null,
					new Rect(area.X, center.Y - h / 6, area.Width, h / 3)
				);
				context.DrawRectangle(
					Brush("#FFFFFF"),
					null,
					new Rect(center.X - h / 6, area.Y, h / 3, h)
				);
				context.DrawRectangle(
					Brush("#C8102E"),
					null,
					new Rect(area.X, center.Y - h / 10, area.Width, h / 5)
				);
				context.DrawRectangle(
					Brush("#C8102E"),
					null,
					new Rect(center.X - h / 10, area.Y, h / 5, h)
				);
			}
		}

		private void DrawPolygon(string color, IEnumerable<Point> points)
		{
			var geometry = new StreamGeometry();
			using (var g = geometry.Open())
			{
				var first = true;
				foreach (var point in points)
				{
					if (first)
						g.BeginFigure(point, true);
					else
						g.LineTo(point);
					first = false;
				}
				g.EndFigure(true);
			}
			context.DrawGeometry(Brush(color), null, geometry);
		}
	}
}
