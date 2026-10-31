using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

/// <summary>
/// Dessine les 52 cartes et le dos dans Assets/Resources/Cards (PNG 320 x 448).
/// Les images ne contiennent PAS la valeur de la carte (V/D/R en français, J/Q/K en anglais) : le jeu la pose
/// en texte localisé dans le coin haut gauche (et, tournée, en bas à droite), à l'emplacement laissé libre ici.
/// Usage : dotnet run --project Tools/CardArt [-- --sheet chemin.png]
/// </summary>
internal static class Program
{
    private const int W = 320;
    private const int H = 448;

    // Mêmes encres que CardFactory.cs pour que le texte posé par le jeu s'accorde au dessin.
    private static readonly Color Cream = Color.FromArgb(243, 235, 221);
    private static readonly Color Ink = Color.FromArgb(26, 20, 20);
    private static readonly Color Red = Color.FromArgb(179, 38, 31);

    private static readonly Color Gold = Color.FromArgb(201, 162, 75);
    private static readonly Color Sulfur = Color.FromArgb(230, 201, 74);
    private static readonly Color Skin = Color.FromArgb(192, 101, 74);
    private static readonly Color Bone = Color.FromArgb(232, 222, 202);
    private static readonly Color Hair = Color.FromArgb(44, 24, 26);

    private enum Suit { Clubs, Diamonds, Hearts, Spades }

    private static bool IsRed(Suit s) => s == Suit.Hearts || s == Suit.Diamonds;
    private static Color InkOf(Suit s) => IsRed(s) ? Red : Ink;

    private static int Main(string[] args)
    {
        string outDir = Path.Combine(FindProjectRoot(), "Assets", "Resources", "Cards");
        Directory.CreateDirectory(outDir);

        string sheetPath = null;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--sheet") sheetPath = args[i + 1];

        var all = new List<(string name, Bitmap bitmap)>();
        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
                using (var g = Begin(bmp)) DrawFace(g, suit, rank);
                string name = $"{suit.ToString().ToLowerInvariant()}_{rank}";
                bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
                all.Add((name, bmp));
            }
        }

        var back = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Begin(back)) DrawBack(g);
        back.Save(Path.Combine(outDir, "back.png"), ImageFormat.Png);
        all.Add(("back", back));

        if (sheetPath != null) SaveSheet(all, sheetPath);
        foreach (var item in all) item.bitmap.Dispose();

        Console.WriteLine($"{all.Count} images écrites dans {outDir}");
        return 0;
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Projet Unity introuvable : lancez la commande depuis le dossier du projet.");
    }

    private static Graphics Begin(Bitmap bitmap)
    {
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);
        return g;
    }

    private static void SaveSheet(List<(string name, Bitmap bitmap)> cards, string path)
    {
        const int columns = 13, cellW = 160, cellH = 224;
        int rows = (cards.Count + columns - 1) / columns;
        using var sheet = new Bitmap(columns * cellW, rows * cellH);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(18, 32, 26));
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        for (int i = 0; i < cards.Count; i++)
            g.DrawImage(cards[i].bitmap, (i % columns) * cellW + 4, (i / columns) * cellH + 4, cellW - 8, cellH - 8);
        sheet.Save(path, ImageFormat.Png);
    }

    // ---------------------------------------------------------------- Formes de base

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static void FillRounded(Graphics g, RectangleF r, float radius, Color color)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void StrokeRounded(Graphics g, RectangleF r, float radius, Color color, float width)
    {
        using var path = RoundedRect(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    /// <summary>Applique une rotation d'un demi-tour autour du centre de la carte pour la moitié basse.</summary>
    private static GraphicsState FlipAroundCardCenter(Graphics g)
    {
        var state = g.Save();
        g.TranslateTransform(W / 2f, H / 2f);
        g.RotateTransform(180);
        g.TranslateTransform(-W / 2f, -H / 2f);
        return state;
    }

    // ---------------------------------------------------------------- Symboles de couleur

    private static List<GraphicsPath> SuitParts(Suit suit)
    {
        var parts = new List<GraphicsPath>();
        switch (suit)
        {
            case Suit.Hearts:
            {
                var p = new GraphicsPath();
                p.AddBezier(new PointF(0, 0.95f), new PointF(-1.45f, 0.05f), new PointF(-0.95f, -0.95f), new PointF(0, -0.30f));
                p.AddBezier(new PointF(0, -0.30f), new PointF(0.95f, -0.95f), new PointF(1.45f, 0.05f), new PointF(0, 0.95f));
                p.CloseFigure();
                parts.Add(p);
                break;
            }
            case Suit.Diamonds:
            {
                var p = new GraphicsPath();
                p.AddPolygon(new[] { new PointF(0, -1), new PointF(0.68f, 0), new PointF(0, 1), new PointF(-0.68f, 0) });
                parts.Add(p);
                break;
            }
            case Suit.Spades:
            {
                var body = new GraphicsPath();
                body.AddBezier(new PointF(0, -0.95f), new PointF(-1.45f, -0.05f), new PointF(-0.95f, 0.95f), new PointF(0, 0.30f));
                body.AddBezier(new PointF(0, 0.30f), new PointF(0.95f, 0.95f), new PointF(1.45f, -0.05f), new PointF(0, -0.95f));
                body.CloseFigure();
                parts.Add(body);

                var stem = new GraphicsPath();
                stem.AddPolygon(new[] { new PointF(0, 0.15f), new PointF(-0.30f, 1.0f), new PointF(0.30f, 1.0f) });
                parts.Add(stem);
                break;
            }
            default: // Trèfles
            {
                foreach (var (x, y, r) in new[] { (0f, -0.45f, 0.40f), (-0.46f, 0.18f, 0.40f), (0.46f, 0.18f, 0.40f), (0f, 0.0f, 0.30f) })
                {
                    var c = new GraphicsPath();
                    c.AddEllipse(x - r, y - r, 2 * r, 2 * r);
                    parts.Add(c);
                }
                var stem = new GraphicsPath();
                stem.AddPolygon(new[] { new PointF(0, 0.1f), new PointF(-0.26f, 1.0f), new PointF(0.26f, 1.0f) });
                parts.Add(stem);
                break;
            }
        }
        return parts;
    }

    private static void DrawSuit(Graphics g, Suit suit, float cx, float cy, float size, Color color, bool upsideDown = false)
    {
        var state = g.Save();
        g.TranslateTransform(cx, cy);
        if (upsideDown) g.RotateTransform(180);
        g.ScaleTransform(size / 2f, size / 2f);
        using (var brush = new SolidBrush(color))
        {
            foreach (var part in SuitParts(suit))
            {
                g.FillPath(brush, part);
                part.Dispose();
            }
        }
        g.Restore(state);
    }

    // ---------------------------------------------------------------- Face d'une carte

    private static void DrawFace(Graphics g, Suit suit, int rank)
    {
        var ink = InkOf(suit);

        FillRounded(g, new RectangleF(1, 1, W - 2, H - 2), 24, Cream);
        StrokeRounded(g, new RectangleF(1.5f, 1.5f, W - 3, H - 3), 24, Ink, 3);
        StrokeRounded(g, new RectangleF(10, 10, W - 20, H - 20), 16, Color.FromArgb(150, Gold), 2);

        // Petit symbole sous l'emplacement de la valeur (coin haut gauche) et son double en bas à droite, à
        // l'endroit lui aussi (un cœur retourné ressemble à un pique) : le jeu y pose la valeur, également à l'endroit.
        DrawSuit(g, suit, 46, 116, 34, ink);
        DrawSuit(g, suit, W - 46, H - 116, 34, ink);

        if (rank == 14) DrawAce(g, suit);
        else if (rank >= 11) DrawCourt(g, suit, rank);
        else DrawPips(g, suit, rank);
    }

    private static void DrawAce(Graphics g, Suit suit)
    {
        var ink = InkOf(suit);
        using (var ring = new Pen(Gold, 3.5f)) g.DrawEllipse(ring, 160 - 100, 224 - 100, 200, 200);
        using (var thin = new Pen(Color.FromArgb(160, Gold), 1.6f)) g.DrawEllipse(thin, 160 - 90, 224 - 90, 180, 180);
        DrawSuit(g, suit, 160, 224, 138, ink);

        // Quatre petites étincelles de soufre autour du cercle.
        foreach (var angle in new[] { 45, 135, 225, 315 })
        {
            double a = angle * Math.PI / 180;
            float x = 160 + (float)Math.Cos(a) * 118, y = 224 + (float)Math.Sin(a) * 118;
            using var brush = new SolidBrush(Sulfur);
            g.FillPolygon(brush, new[]
            {
                new PointF(x, y - 9), new PointF(x + 4, y), new PointF(x, y + 9), new PointF(x - 4, y)
            });
        }
    }

    private static void DrawPips(Graphics g, Suit suit, int rank)
    {
        var ink = InkOf(suit);
        const float left = 112, right = 208, top = 86, height = 276;
        const float size = 54;

        foreach (var (fx, fy) in PipLayout(rank))
        {
            float x = left + (right - left) * fx;
            float y = top + height * fy;
            // Tous les symboles restent à l'endroit : retournés, les cœurs ressemblent à des piques à petite taille.
            DrawSuit(g, suit, x, y, size, ink);
        }
    }

    private static IEnumerable<(float x, float y)> PipLayout(int rank)
    {
        const float L = 0f, C = 0.5f, R = 1f;
        switch (rank)
        {
            case 2: return new[] { (C, 0f), (C, 1f) };
            case 3: return new[] { (C, 0f), (C, 0.5f), (C, 1f) };
            case 4: return new[] { (L, 0f), (R, 0f), (L, 1f), (R, 1f) };
            case 5: return new[] { (L, 0f), (R, 0f), (C, 0.5f), (L, 1f), (R, 1f) };
            case 6: return new[] { (L, 0f), (R, 0f), (L, 0.5f), (R, 0.5f), (L, 1f), (R, 1f) };
            case 7: return new[] { (L, 0f), (R, 0f), (C, 0.25f), (L, 0.5f), (R, 0.5f), (L, 1f), (R, 1f) };
            case 8: return new[] { (L, 0f), (R, 0f), (C, 0.25f), (L, 0.5f), (R, 0.5f), (C, 0.75f), (L, 1f), (R, 1f) };
            case 9:
                return new[]
                {
                    (L, 0f), (R, 0f), (L, 1 / 3f), (R, 1 / 3f), (C, 0.5f),
                    (L, 2 / 3f), (R, 2 / 3f), (L, 1f), (R, 1f)
                };
            default: // 10
                return new[]
                {
                    (L, 0f), (R, 0f), (C, 1 / 6f), (L, 1 / 3f), (R, 1 / 3f),
                    (L, 2 / 3f), (R, 2 / 3f), (C, 5 / 6f), (L, 1f), (R, 1f)
                };
        }
    }

    // ---------------------------------------------------------------- Figures : démons de cour

    private static void DrawCourt(Graphics g, Suit suit, int rank)
    {
        var tint = IsRed(suit) ? Color.FromArgb(242, 212, 200) : Color.FromArgb(218, 210, 196);
        var frame = new RectangleF(92, 62, 136, 324);

        using (var brush = new SolidBrush(tint)) g.FillRectangle(brush, frame);

        // Haut, puis le même dessin retourné en bas (comme sur une vraie carte).
        DrawCourtHalf(g, suit, rank, flipped: false);
        var state = FlipAroundCardCenter(g);
        DrawCourtHalf(g, suit, rank, flipped: true);
        g.Restore(state);

        using (var line = new Pen(Ink, 2.5f)) g.DrawLine(line, 92, 224, 228, 224);
        using (var border = new Pen(Ink, 3.5f)) g.DrawRectangle(border, frame.X, frame.Y, frame.Width, frame.Height);
        using (var gold = new Pen(Gold, 2f)) g.DrawRectangle(gold, frame.X + 5, frame.Y + 5, frame.Width - 10, frame.Height - 10);
    }

    private static void DrawCourtHalf(Graphics g, Suit suit, int rank, bool flipped)
    {
        var state = g.Save();
        g.TranslateTransform(92, 70);
        g.ScaleTransform(1.36f, 1.36f);

        var robe = IsRed(suit) ? Red : Color.FromArgb(40, 32, 34);
        using var outline = new Pen(Ink, 1.7f) { LineJoin = LineJoin.Round };

        // Objet tenu, derrière le corps.
        if (rank == 11) DrawSword(g, outline);
        else if (rank == 13) DrawTrident(g, outline);
        else DrawFlame(g, 14, 72, 1f);

        // Robe et col doré.
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(new[]
            {
                new PointF(5, 112), new PointF(9, 82), new PointF(30, 66), new PointF(70, 66),
                new PointF(91, 82), new PointF(95, 112)
            });
            using var brush = new SolidBrush(robe);
            g.FillPath(brush, path);
            g.DrawPath(outline, path);
        }
        using (var trim = new Pen(Gold, 3.2f) { LineJoin = LineJoin.Round })
            g.DrawLines(trim, new[] { new PointF(31, 68), new PointF(50, 94), new PointF(69, 68) });
        DrawSuit(g, suit, 50, 102, 13, Gold);

        // Cheveux de la dame, derrière la tête.
        if (rank == 12)
        {
            using var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new PointF(27, 34), new PointF(73, 34), new PointF(80, 84), new PointF(66, 72),
                new PointF(34, 72), new PointF(20, 84)
            });
            using var brush = new SolidBrush(Hair);
            g.FillPath(brush, path);
            g.DrawPath(outline, path);
        }

        // Cou, cornes, tête.
        using (var brush = new SolidBrush(Color.FromArgb(160, 82, 60))) g.FillRectangle(brush, 43, 58, 14, 14);
        DrawHorn(g, outline, mirrored: false);
        DrawHorn(g, outline, mirrored: true);
        using (var brush = new SolidBrush(Skin))
        {
            g.FillEllipse(brush, 33, 26, 34, 40);
            g.DrawEllipse(outline, 33, 26, 34, 40);
        }

        // Barbe du roi.
        if (rank == 13)
        {
            using var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new PointF(36, 52), new PointF(44, 60), new PointF(50, 58), new PointF(56, 60),
                new PointF(64, 52), new PointF(58, 76), new PointF(50, 88), new PointF(42, 76)
            });
            using var brush = new SolidBrush(Hair);
            g.FillPath(brush, path);
            g.DrawPath(outline, path);
        }

        // Regard de braise et bouche.
        using (var eye = new SolidBrush(Sulfur))
        {
            g.FillEllipse(eye, 39, 40, 9, 5);
            g.FillEllipse(eye, 52, 40, 9, 5);
        }
        using (var pupil = new SolidBrush(Ink))
        {
            g.FillEllipse(pupil, 42.5f, 41, 2.5f, 3f);
            g.FillEllipse(pupil, 55.5f, 41, 2.5f, 3f);
        }
        using (var brow = new Pen(Ink, 2f))
        {
            g.DrawLine(brow, 38, 37, 47, 40);
            g.DrawLine(brow, 62, 37, 53, 40);
        }
        if (rank != 13)
            using (var mouth = new Pen(Ink, 1.8f)) g.DrawArc(mouth, 43, 50, 14, 8, 20, 140);

        // Coiffe : calot à plume, couronne à trois pointes, couronne à cinq pointes.
        if (rank == 11) DrawHat(g, outline);
        else DrawCrown(g, outline, points: rank == 12 ? 3 : 5);

        g.Restore(state);
    }

    private static void DrawHorn(Graphics g, Pen outline, bool mirrored)
    {
        var state = g.Save();
        if (mirrored)
        {
            g.TranslateTransform(100, 0);
            g.ScaleTransform(-1, 1);
        }
        using var path = new GraphicsPath();
        path.AddBezier(new PointF(37, 34), new PointF(27, 33), new PointF(19, 22), new PointF(19, 6));
        path.AddBezier(new PointF(19, 6), new PointF(30, 12), new PointF(38, 20), new PointF(44, 28));
        path.CloseFigure();
        using (var brush = new SolidBrush(Bone)) g.FillPath(brush, path);
        g.DrawPath(outline, path);
        g.Restore(state);
    }

    private static void DrawCrown(Graphics g, Pen outline, int points)
    {
        var pts = new List<PointF> { new PointF(31, 32) };
        float width = 38f;
        for (int i = 0; i < points; i++)
        {
            float tipX = 31 + width * (i + 0.5f) / points;
            float valleyX = 31 + width * (i + 1f) / points;
            pts.Add(new PointF(tipX, points == 5 ? 6 : 12));
            if (i < points - 1) pts.Add(new PointF(valleyX, 22));
        }
        pts.Add(new PointF(69, 32));

        using var path = new GraphicsPath();
        path.AddPolygon(pts.ToArray());
        using (var brush = new SolidBrush(Gold)) g.FillPath(brush, path);
        g.DrawPath(outline, path);

        using (var band = new Pen(Ink, 1.4f)) g.DrawLine(band, 31, 29, 69, 29);
        using (var gem = new SolidBrush(Red)) g.FillEllipse(gem, 47, 24, 6, 4);
    }

    private static void DrawHat(Graphics g, Pen outline)
    {
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(new[] { new PointF(30, 34), new PointF(70, 34), new PointF(66, 16), new PointF(34, 16) });
            using var brush = new SolidBrush(Color.FromArgb(70, 20, 20));
            g.FillPath(brush, path);
            g.DrawPath(outline, path);
        }
        using (var band = new SolidBrush(Gold)) g.FillRectangle(band, 31, 28, 38, 5);

        // Plume.
        using var feather = new GraphicsPath();
        feather.AddBezier(new PointF(64, 18), new PointF(74, 14), new PointF(80, 8), new PointF(88, 2));
        feather.AddBezier(new PointF(88, 2), new PointF(84, 14), new PointF(74, 22), new PointF(64, 22));
        feather.CloseFigure();
        using (var brush = new SolidBrush(Sulfur)) g.FillPath(brush, feather);
        g.DrawPath(outline, feather);
    }

    private static void DrawSword(Graphics g, Pen outline)
    {
        using (var blade = new SolidBrush(Color.FromArgb(205, 208, 214)))
        {
            g.FillRectangle(blade, 84, 12, 6, 66);
            g.DrawRectangle(outline, 84, 12, 6, 66);
        }
        using (var guard = new SolidBrush(Gold))
        {
            g.FillRectangle(guard, 76, 76, 22, 6);
            g.DrawRectangle(outline, 76, 76, 22, 6);
            g.FillRectangle(guard, 85, 82, 4, 14);
        }
    }

    private static void DrawTrident(Graphics g, Pen outline)
    {
        using var gold = new SolidBrush(Gold);
        g.FillRectangle(gold, 84, 14, 4, 94);
        g.DrawRectangle(outline, 84, 14, 4, 94);
        foreach (float x in new[] { 76f, 94f })
        {
            g.FillRectangle(gold, x, 8, 4, 22);
            g.DrawRectangle(outline, x, 8, 4, 22);
        }
        g.FillRectangle(gold, 76, 26, 22, 4);
        g.FillPolygon(gold, new[] { new PointF(80, 2), new PointF(88, 2), new PointF(86, 14), new PointF(82, 14) });
        g.FillPolygon(gold, new[] { new PointF(84, 0), new PointF(88, 14), new PointF(84, 14) });
    }

    private static void DrawFlame(Graphics g, float x, float y, float scale)
    {
        var state = g.Save();
        g.TranslateTransform(x, y);
        g.ScaleTransform(scale, scale);
        using var outer = new GraphicsPath();
        outer.AddBezier(new PointF(0, 22), new PointF(-16, 8), new PointF(-2, -2), new PointF(0, -18));
        outer.AddBezier(new PointF(0, -18), new PointF(4, -6), new PointF(18, 4), new PointF(0, 22));
        outer.CloseFigure();
        using (var brush = new SolidBrush(Color.FromArgb(230, 120, 40))) g.FillPath(brush, outer);
        using (var pen = new Pen(Ink, 1.6f)) g.DrawPath(pen, outer);

        using var inner = new GraphicsPath();
        inner.AddBezier(new PointF(0, 20), new PointF(-8, 12), new PointF(-1, 6), new PointF(0, -4));
        inner.AddBezier(new PointF(0, -4), new PointF(2, 4), new PointF(9, 10), new PointF(0, 20));
        inner.CloseFigure();
        using (var brush = new SolidBrush(Sulfur)) g.FillPath(brush, inner);
        g.Restore(state);
    }

    // ---------------------------------------------------------------- Dos des cartes

    private static void DrawBack(Graphics g)
    {
        var deep = Color.FromArgb(90, 22, 18);
        FillRounded(g, new RectangleF(1, 1, W - 2, H - 2), 24, deep);

        // Treillis en losanges, découpé dans le cadre intérieur.
        using (var clip = RoundedRect(new RectangleF(14, 14, W - 28, H - 28), 14))
        {
            g.SetClip(clip);
            using var lattice = new Pen(Color.FromArgb(90, 190, 70, 50), 2f);
            for (int i = -H; i < W + H; i += 30)
            {
                g.DrawLine(lattice, i, 0, i + H, H);
                g.DrawLine(lattice, i, 0, i - H, H);
            }
            g.ResetClip();
        }

        StrokeRounded(g, new RectangleF(1.5f, 1.5f, W - 3, H - 3), 24, Ink, 3);
        StrokeRounded(g, new RectangleF(14, 14, W - 28, H - 28), 14, Gold, 3);

        // Médaillon, cercles dorés et pentagramme de soufre.
        const float cx = W / 2f, cy = H / 2f;
        using (var brush = new SolidBrush(Color.FromArgb(58, 14, 12))) g.FillEllipse(brush, cx - 98, cy - 98, 196, 196);
        using (var ring = new Pen(Gold, 5f)) g.DrawEllipse(ring, cx - 98, cy - 98, 196, 196);
        using (var thin = new Pen(Color.FromArgb(180, Gold), 2f)) g.DrawEllipse(thin, cx - 86, cy - 86, 172, 172);

        var star = new PointF[5];
        for (int k = 0; k < 5; k++)
        {
            double a = (-90 + 72 * k) * Math.PI / 180;
            star[k] = new PointF(cx + (float)Math.Cos(a) * 74, cy + (float)Math.Sin(a) * 74);
        }
        using (var pen = new Pen(Sulfur, 6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            int[] order = { 0, 2, 4, 1, 3, 0 };
            for (int k = 0; k < order.Length - 1; k++) g.DrawLine(pen, star[order[k]], star[order[k + 1]]);
        }
        using (var core = new SolidBrush(Sulfur)) g.FillEllipse(core, cx - 9, cy - 9, 18, 18);

        // Losanges aux quatre coins.
        foreach (var (x, y) in new[] { (38f, 38f), (W - 38f, 38f), (38f, H - 38f), (W - 38f, H - 38f) })
        {
            using var brush = new SolidBrush(Sulfur);
            g.FillPolygon(brush, new[] { new PointF(x, y - 11), new PointF(x + 7, y), new PointF(x, y + 11), new PointF(x - 7, y) });
        }
    }
}
