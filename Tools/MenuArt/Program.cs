using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;

/// <summary>
/// Dessine le fond d'écran du menu : un ciel de braises, un grand cercle occulte, des flammes en bas et deux
/// éventails de cartes sur les côtés. Tout est tracé par code, avec un hasard fixé : le résultat est toujours le même.
/// </summary>
internal static class Program
{
    private const int W = 1920;
    private const int H = 1080;

    private static readonly Random Rng = new Random(666);

    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : Path.Combine("Assets", "Resources", "UI", "menu_background.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

        using (var bitmap = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            Setup(g);
            Sky(g);
            Glows(g);
            Smoke(g);
            OccultCircle(g);
            Flames(g);
            CardFan(g, 330, 1010, -1);
            CardFan(g, W - 330, 1010, 1);
            Embers(g);
            Vignette(g);
            Grain(bitmap);
            SaveJpeg(bitmap, output, 92);
        }

        Console.WriteLine("Écrit : " + Path.GetFullPath(output) + " (" + new FileInfo(output).Length / 1024 + " Ko)");
        return 0;
    }

    private static void Setup(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    private static Color C(int a, int r, int gr, int b) => Color.FromArgb(a, r, gr, b);

    // ---------------------------------------------------------------- Fond

    private static void Sky(Graphics g)
    {
        using (var brush = new LinearGradientBrush(new Rectangle(0, 0, W, H), C(255, 8, 4, 6), C(255, 52, 12, 8), 90f))
        {
            var blend = new ColorBlend
            {
                Colors = new[] { C(255, 7, 4, 7), C(255, 14, 7, 9), C(255, 34, 10, 8), C(255, 78, 20, 8) },
                Positions = new[] { 0f, 0.45f, 0.78f, 1f }
            };
            brush.InterpolationColors = blend;
            g.FillRectangle(brush, 0, 0, W, H);
        }
    }

    /// <summary>Une tache de lumière douce, du centre vers les bords.</summary>
    private static void Glow(Graphics g, float cx, float cy, float rx, float ry, Color center)
    {
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
            using (var brush = new PathGradientBrush(path))
            {
                brush.CenterPoint = new PointF(cx, cy);
                brush.CenterColor = center;
                brush.SurroundColors = new[] { Color.FromArgb(0, center) };
                g.FillPath(brush, path);
            }
        }
    }

    private static void Glows(Graphics g)
    {
        // Lueur de soufre derrière le cercle, braise rouge en bas.
        Glow(g, W / 2f, 470, 760, 520, C(62, 200, 120, 36));
        Glow(g, W / 2f, 470, 380, 300, C(48, 230, 170, 60));
        Glow(g, W / 2f, H + 60, 1300, 560, C(190, 235, 80, 20));
        Glow(g, 120, H, 700, 500, C(110, 210, 50, 15));
        Glow(g, W - 120, H, 700, 500, C(110, 210, 50, 15));
    }

    /// <summary>Volutes de fumée : de grandes ellipses floues, dessinées en petit puis agrandies.</summary>
    private static void Smoke(Graphics g)
    {
        const int sw = W / 6, sh = H / 6;
        using (var small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
        {
            using (var sg = Graphics.FromImage(small))
            {
                sg.SmoothingMode = SmoothingMode.AntiAlias;
                sg.Clear(Color.Transparent);
                for (int i = 0; i < 26; i++)
                {
                    float x = (float)Rng.NextDouble() * sw;
                    float y = sh * (0.15f + 0.7f * (float)Rng.NextDouble());
                    float rx = 30 + (float)Rng.NextDouble() * 70;
                    float ry = 8 + (float)Rng.NextDouble() * 22;
                    int alpha = 7 + Rng.Next(12);
                    using (var brush = new SolidBrush(C(alpha, 90, 55, 45)))
                        sg.FillEllipse(brush, x - rx, y - ry, rx * 2, ry * 2);
                }
            }
            g.DrawImage(small, new Rectangle(0, 0, W, H));
        }
    }

    // ---------------------------------------------------------------- Cercle occulte

    private static PointF Polar(float cx, float cy, float r, double deg)
    {
        double a = (deg - 90) * Math.PI / 180.0;
        return new PointF(cx + (float)(Math.Cos(a) * r), cy + (float)(Math.Sin(a) * r));
    }

    private static void OccultCircle(Graphics g)
    {
        float cx = W / 2f, cy = 520, r = 470;
        var gold = C(120, 214, 190, 90);
        var faint = C(60, 214, 190, 90);

        using (var thick = new Pen(gold, 3f))
        using (var thin = new Pen(faint, 1.5f))
        {
            // Anneaux.
            g.DrawEllipse(thick, cx - r, cy - r, r * 2, r * 2);
            g.DrawEllipse(thin, cx - r - 26, cy - r - 26, (r + 26) * 2, (r + 26) * 2);
            g.DrawEllipse(thin, cx - r + 30, cy - r + 30, (r - 30) * 2, (r - 30) * 2);

            // Graduations entre les anneaux.
            for (int i = 0; i < 120; i++)
            {
                double deg = i * 3.0;
                float inner = i % 5 == 0 ? r + 6 : r + 14;
                g.DrawLine(thin, Polar(cx, cy, inner, deg), Polar(cx, cy, r + 26, deg));
            }

            // Pentagramme.
            var points = new PointF[5];
            for (int i = 0; i < 5; i++) points[i] = Polar(cx, cy, r - 30, i * 72);
            int[] order = { 0, 2, 4, 1, 3, 0 };
            var star = order.Select(i => points[i]).ToArray();
            using (var glowPen = new Pen(C(36, 255, 210, 90), 14f))
                g.DrawLines(glowPen, star);
            g.DrawLines(thick, star);

            // Petits cercles aux pointes et cercle intérieur.
            foreach (var p in points)
                g.DrawEllipse(thin, p.X - 16, p.Y - 16, 32, 32);
            g.DrawEllipse(thin, cx - 180, cy - 180, 360, 360);
        }

        // Symboles de couleurs tout autour, entre les deux anneaux extérieurs.
        string[] suits = { "♠", "♥", "♣", "♦" };
        using (var font = new Font("Segoe UI Symbol", 26f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(C(110, 214, 190, 90)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            for (int i = 0; i < 12; i++)
            {
                var p = Polar(cx, cy, r + 58, i * 30 + 15);
                g.DrawString(suits[i % 4], font, brush, p, format);
            }
        }
    }

    // ---------------------------------------------------------------- Flammes

    private static void Flames(Graphics g)
    {
        const int scale = 5;
        int sw = W / scale, sh = H / scale;
        using (var layer = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
        {
            using (var lg = Graphics.FromImage(layer))
            {
                lg.SmoothingMode = SmoothingMode.AntiAlias;
                lg.Clear(Color.Transparent);

                // Trois rangées, de la plus lointaine (sombre, haute) à la plus proche (vive, basse).
                FlameRow(lg, sw, sh, 40, 0.50f, 190, C(150, 120, 22, 10), C(0, 60, 8, 6));
                FlameRow(lg, sw, sh, 36, 0.34f, 150, C(190, 220, 70, 14), C(0, 150, 24, 8));
                FlameRow(lg, sw, sh, 32, 0.20f, 100, C(230, 255, 160, 40), C(0, 240, 80, 14));
            }
            g.DrawImage(layer, new Rectangle(0, 0, W, H));
        }
    }

    private static void FlameRow(Graphics g, int sw, int sh, int count, float heightFactor, float maxHeight,
        Color bottom, Color top)
    {
        for (int i = 0; i < count; i++)
        {
            float x = (i + (float)Rng.NextDouble()) * sw / count;
            const float k = 3f / 5f; // les tailles ci-dessous sont pensées pour un calque réduit 3 fois
            float height = k * maxHeight * (0.45f + 0.55f * (float)Rng.NextDouble());
            // Les flammes sont plus hautes sur les côtés : le centre reste calme derrière le menu.
            float edge = Math.Abs(x - sw / 2f) / (sw / 2f);
            height *= 0.45f + 0.75f * edge;
            float width = k * (22 + (float)Rng.NextDouble() * 26);
            float sway = k * ((float)Rng.NextDouble() - 0.5f) * 40;
            float baseY = sh + 4;
            float tipY = baseY - height;

            using (var path = new GraphicsPath())
            {
                path.AddBezier(x - width, baseY, x - width * 0.9f, baseY - height * 0.45f,
                    x + sway * 0.4f, tipY + height * 0.35f, x + sway, tipY);
                path.AddBezier(x + sway, tipY, x + sway * 0.4f + width * 0.2f, tipY + height * 0.4f,
                    x + width * 0.9f, baseY - height * 0.5f, x + width, baseY);
                path.CloseFigure();

                using (var brush = new LinearGradientBrush(new PointF(x, baseY), new PointF(x, tipY), bottom, top))
                    g.FillPath(brush, path);
            }
        }
    }

    // ---------------------------------------------------------------- Cartes

    private static void CardFan(Graphics g, float cx, float cy, int side)
    {
        // side = -1 : éventail à gauche, penché vers la droite ; +1 : l'inverse.
        float[] angles = { -34f, -17f, 0f, 17f, 34f };
        string[] faces = side < 0
            ? new[] { "back", "A♠", "back", "K♥", "back" }
            : new[] { "back", "J♣", "back", "Q♦", "back" };

        // Ombre portée de l'éventail.
        Glow(g, cx, cy - 60, 330, 250, C(120, 0, 0, 0));

        for (int i = 0; i < angles.Length; i++)
        {
            float angle = angles[i] - 6f;
            DrawCard(g, cx, cy, 190, 285, angle, faces[i], i);
        }
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Une carte, pivotée autour de son bas-centre (pour l'éventail).</summary>
    private static void DrawCard(Graphics g, float pivotX, float pivotY, float w, float h, float angle, string face,
        int index)
    {
        var state = g.Save();
        g.TranslateTransform(pivotX, pivotY);
        g.RotateTransform(angle);
        g.TranslateTransform(-w / 2f, -h);

        // Ombre.
        using (var shadow = RoundedRect(6, 8, w, h, 14))
        using (var shadowBrush = new SolidBrush(C(90, 0, 0, 0)))
            g.FillPath(shadowBrush, shadow);

        using (var body = RoundedRect(0, 0, w, h, 14))
        {
            bool back = face == "back";
            if (back)
            {
                using (var brush = new LinearGradientBrush(new RectangleF(0, 0, w, h), C(255, 112, 30, 20), C(255, 60, 14, 12), 60f))
                    g.FillPath(brush, body);
            }
            else
            {
                using (var brush = new LinearGradientBrush(new RectangleF(0, 0, w, h), C(255, 244, 236, 218), C(255, 214, 198, 170), 70f))
                    g.FillPath(brush, body);
            }

            using (var edge = new Pen(back ? C(255, 232, 214, 170) : C(255, 90, 70, 50), 2.5f))
                g.DrawPath(edge, body);

            if (back) DrawBack(g, w, h);
            else DrawFace(g, w, h, face);
        }

        g.Restore(state);
    }

    private static void DrawBack(Graphics g, float w, float h)
    {
        using (var inner = RoundedRect(12, 12, w - 24, h - 24, 8))
        using (var pen = new Pen(C(210, 226, 190, 110), 2f))
            g.DrawPath(pen, inner);

        float cx = w / 2f, cy = h / 2f, r = w * 0.30f;
        using (var pen = new Pen(C(230, 230, 196, 110), 2.2f))
        {
            g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
            var pts = Enumerable.Range(0, 5).Select(i => Polar(cx, cy, r, i * 72)).ToArray();
            int[] order = { 0, 2, 4, 1, 3, 0 };
            g.DrawLines(pen, order.Select(i => pts[i]).ToArray());
        }

        // Quelques losanges dans les coins.
        using (var brush = new SolidBrush(C(190, 226, 190, 110)))
        {
            foreach (var p in new[] { new PointF(30, 30), new PointF(w - 30, 30), new PointF(30, h - 30), new PointF(w - 30, h - 30) })
                g.FillPolygon(brush, new[] { new PointF(p.X, p.Y - 8), new PointF(p.X + 6, p.Y), new PointF(p.X, p.Y + 8), new PointF(p.X - 6, p.Y) });
        }
    }

    private static void DrawFace(Graphics g, float w, float h, string face)
    {
        string rank = face.Substring(0, face.Length - 1);
        string suit = face.Substring(face.Length - 1);
        bool red = suit == "♥" || suit == "♦";
        var ink = red ? C(255, 168, 34, 28) : C(255, 28, 22, 22);

        using (var rankFont = new Font("Georgia", 40f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var smallSuit = new Font("Segoe UI Symbol", 30f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var bigSuit = new Font("Segoe UI Symbol", 120f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(ink))
        using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(rank, rankFont, brush, new PointF(w * 0.17f, h * 0.10f), center);
            g.DrawString(suit, smallSuit, brush, new PointF(w * 0.17f, h * 0.20f), center);
            g.DrawString(suit, bigSuit, brush, new PointF(w / 2f, h / 2f), center);

            var state = g.Save();
            g.TranslateTransform(w, h);
            g.RotateTransform(180);
            g.DrawString(rank, rankFont, brush, new PointF(w * 0.17f, h * 0.10f), center);
            g.DrawString(suit, smallSuit, brush, new PointF(w * 0.17f, h * 0.20f), center);
            g.Restore(state);
        }
    }

    // ---------------------------------------------------------------- Braises, vignette, grain

    private static void Embers(Graphics g)
    {
        for (int i = 0; i < 170; i++)
        {
            float x = (float)Rng.NextDouble() * W;
            // Plus denses en bas, qui montent et se raréfient.
            float t = (float)Math.Pow(Rng.NextDouble(), 1.8);
            float y = H - t * H * 0.95f;
            float size = 1.2f + (float)Rng.NextDouble() * 3.2f;
            int alpha = (int)(255 * (1f - t * 0.7f) * (0.5f + 0.5f * Rng.NextDouble()));
            var core = Rng.Next(3) == 0 ? C(alpha, 255, 235, 150) : C(alpha, 255, 140 + Rng.Next(60), 40);

            Glow(g, x, y, size * 5, size * 5, Color.FromArgb(alpha / 4, core));
            using (var brush = new SolidBrush(core))
                g.FillEllipse(brush, x - size, y - size, size * 2, size * 2);
        }
    }

    private static void Vignette(Graphics g)
    {
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(-W * 0.25f, -H * 0.35f, W * 1.5f, H * 1.7f);
            using (var brush = new PathGradientBrush(path))
            {
                brush.CenterColor = Color.FromArgb(0, 0, 0, 0);
                brush.SurroundColors = new[] { C(215, 0, 0, 0) };
                brush.FocusScales = new PointF(0.55f, 0.55f);
                var region = new Region(new Rectangle(0, 0, W, H));
                g.FillRectangle(brush, 0, 0, W, H);
            }
        }
    }

    private static void Grain(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * H];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * data.Stride + x * 4;
                    int noise = Rng.Next(-7, 8);
                    for (int c = 0; c < 3; c++) bytes[i + c] = (byte)Math.Max(0, Math.Min(255, bytes[i + c] + noise));
                    bytes[i + 3] = 255;
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void SaveJpeg(Bitmap bitmap, string path, long quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");
        using (var parameters = new EncoderParameters(1))
        {
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            bitmap.Save(path, codec, parameters);
        }
    }
}
