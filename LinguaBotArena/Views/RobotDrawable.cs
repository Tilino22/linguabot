namespace LinguaBotArena.Views;

public class RobotDrawable : IDrawable
{
    public float T { get; set; } = 0;
    public string Emotion { get; set; } = "neutral";
    public bool Talking { get; set; } = false;

    // Estados de animación
    public bool IsJumping { get; set; } = false;
    public bool IsShaking { get; set; } = false;
    public float JumpT { get; set; } = 0;
    public float ShakeT { get; set; } = 0;
    public float WalkOffset { get; set; } = 0;

    private static readonly Color Dark     = Color.FromArgb("#0A0A1A");
    private static readonly Color Body     = Color.FromArgb("#1A1A3A");
    private static readonly Color BodyHL   = Color.FromArgb("#2A2A5A");
    private static readonly Color Green    = Color.FromArgb("#00FF88");
    private static readonly Color Yellow   = Color.FromArgb("#FFE66D");
    private static readonly Color Red      = Color.FromArgb("#FF4444");
    private static readonly Color Blue     = Color.FromArgb("#4488FF");
    private static readonly Color White    = Color.FromArgb("#FFFFFF");
    private static readonly Color Gray     = Color.FromArgb("#444466");
    private static readonly Color DarkGray = Color.FromArgb("#222240");

    private readonly Random _rnd = new();
    private readonly List<(float x, float y, float vx, float vy, float life, Color color)> _particles = new();

    private Color GetEyeColor() => Emotion switch
    {
        "happy" or "very_happy" => Green,
        "sad"                   => Blue,
        "listening"             => Red,
        "talking"               => Yellow,
        _                       => Green
    };

    private Color GetAccentColor() => Emotion switch
    {
        "happy" or "very_happy" => Green,
        "sad"                   => Blue,
        "listening"             => Red,
        "talking"               => Yellow,
        _                       => Green
    };

    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width, h = rect.Height;
        float cx = w / 2f;
        float ps = Math.Min(w, h) / 22f;
        float robotW = 14 * ps;
        float robotH = 20 * ps;

        // Movimiento base: float
        float bob = (float)Math.Sin(T * 2f) * ps * 0.8f;

        // Caminar de lado a lado
        float walkX = (float)Math.Sin(T * 0.8f) * ps * 2.5f;

        // Saltar
        float jumpY = 0;
        if (IsJumping)
        {
            jumpY = -(float)Math.Abs(Math.Sin(JumpT * 3f)) * ps * 6f;
            JumpT += 0.08f;
            if (JumpT > 1.1f) { IsJumping = false; JumpT = 0; }
        }

        // Temblar
        float shakeX = 0;
        if (IsShaking)
        {
            shakeX = (float)Math.Sin(ShakeT * 20f) * ps * 1.5f;
            ShakeT += 0.1f;
            if (ShakeT > 1.2f) { IsShaking = false; ShakeT = 0; }
        }

        // Bailar cuando habla
        float danceX = 0, danceRot = 0;
        if (Talking)
        {
            danceX = (float)Math.Sin(T * 6f) * ps * 1.5f;
            danceRot = (float)Math.Sin(T * 6f) * 5f;
        }

        float startX = cx - robotW / 2f + walkX + shakeX + danceX;
        float startY = (h - robotH) / 2f + bob + jumpY;

        // Background
        canvas.FillColor = Dark;
        canvas.FillRectangle(0, 0, w, h);

        // Grid
        canvas.StrokeColor = Color.FromArgb("#0F0F2A");
        canvas.StrokeSize = 1;
        for (float gx = 0; gx < w; gx += ps * 3) canvas.DrawLine(gx, 0, gx, h);
        for (float gy = 0; gy < h; gy += ps * 3) canvas.DrawLine(0, gy, w, gy);

        // Glow
        var accent = GetAccentColor();
        float glow = 0.15f + (float)Math.Abs(Math.Sin(T)) * 0.1f;
        canvas.FillColor = accent.WithAlpha(glow * 0.5f);
        canvas.FillEllipse(startX, startY + robotH * 0.3f, robotW, robotH * 0.7f);

        // Sombra (se achica cuando salta)
        float shadowScale = IsJumping ? 0.3f + (1f - JumpT) * 0.7f : 1f;
        canvas.FillColor = Colors.Black.WithAlpha(0.4f);
        canvas.FillEllipse(startX + ps + shakeX * 0.5f, startY + robotH - ps * 0.5f,
            (robotW - ps * 2) * shadowScale, ps * 0.8f);

        // Rotar cuando baila
        canvas.SaveState();
        canvas.Translate(startX + robotW / 2f, startY + robotH / 2f);
        canvas.Rotate(danceRot);
        canvas.Translate(-(startX + robotW / 2f), -(startY + robotH / 2f));

        DrawRobot(canvas, startX, startY, ps, accent);

        canvas.RestoreState();

        DrawParticles(canvas);
        SpawnParticles(startX + robotW / 2f, startY + robotH / 2f, ps, accent);

        // Scanline
        float scanY = startY + ((T * ps * 8f) % robotH);
        canvas.StrokeColor = accent.WithAlpha(0.12f);
        canvas.StrokeSize = ps * 0.5f;
        canvas.DrawLine(startX, scanY, startX + robotW, scanY);
    }

    private void DrawRobot(ICanvas canvas, float sx, float sy, float ps, Color accent)
    {
        var eye = GetEyeColor();
        bool armUp = Emotion == "very_happy";
        float armAngle = armUp ? (float)Math.Sin(T * 5) * 20f : 0f;
        if (Talking) armAngle = (float)Math.Sin(T * 6) * 15f;
        float mouthOpen = Talking ? (float)Math.Abs(Math.Sin(T * 6)) : 0f;
        bool blinking = (int)(T * 2) % 60 < 3;

        // Paso de caminar — piernas alternas
        float legOffset = (float)Math.Sin(T * 4f) * 1.5f;

        // === ANTENA ===
        float antPulse = 0.5f + (float)Math.Abs(Math.Sin(T * 4)) * 0.5f;
        Pixel(canvas, 6, -2, ps, sx, sy, Yellow.WithAlpha(antPulse));
        Pixel(canvas, 6, -1, ps, sx, sy, Gray);

        // === CABEZA ===
        for (int px = 1; px <= 12; px++) Pixel(canvas, px, 0, ps, sx, sy, Body);
        for (int row = 1; row <= 5; row++)
        {
            Pixel(canvas, 0, row, ps, sx, sy, Body);
            for (int px = 1; px <= 12; px++) Pixel(canvas, px, row, ps, sx, sy, BodyHL);
            Pixel(canvas, 13, row, ps, sx, sy, Body);
        }
        for (int px = 1; px <= 12; px++) Pixel(canvas, px, 6, ps, sx, sy, Body);

        // Ojos
        if (!blinking)
        {
            Pixel(canvas, 2, 2, ps, sx, sy, eye.WithAlpha(0.4f));
            Pixel(canvas, 3, 2, ps, sx, sy, eye);
            Pixel(canvas, 4, 2, ps, sx, sy, eye);
            Pixel(canvas, 2, 3, ps, sx, sy, eye);
            Pixel(canvas, 3, 3, ps, sx, sy, White);
            Pixel(canvas, 4, 3, ps, sx, sy, eye);
            Pixel(canvas, 2, 4, ps, sx, sy, eye);
            Pixel(canvas, 3, 4, ps, sx, sy, eye);
            Pixel(canvas, 4, 4, ps, sx, sy, eye.WithAlpha(0.4f));
            Pixel(canvas, 9, 2, ps, sx, sy, eye.WithAlpha(0.4f));
            Pixel(canvas, 10, 2, ps, sx, sy, eye);
            Pixel(canvas, 11, 2, ps, sx, sy, eye);
            Pixel(canvas, 9, 3, ps, sx, sy, eye);
            Pixel(canvas, 10, 3, ps, sx, sy, White);
            Pixel(canvas, 11, 3, ps, sx, sy, eye);
            Pixel(canvas, 9, 4, ps, sx, sy, eye.WithAlpha(0.4f));
            Pixel(canvas, 10, 4, ps, sx, sy, eye);
            Pixel(canvas, 11, 4, ps, sx, sy, eye.WithAlpha(0.4f));
        }
        else
        {
            for (int px = 2; px <= 4; px++) Pixel(canvas, px, 3, ps, sx, sy, eye);
            for (int px = 9; px <= 11; px++) Pixel(canvas, px, 3, ps, sx, sy, eye);
        }

        // Boca
        if (Talking)
        {
            int ms = 1 + (int)(mouthOpen * 2);
            for (int px = 4; px <= 9; px++) Pixel(canvas, px, 5, ps, sx, sy, accent);
            for (int px = 5; px <= 8; px++)
                for (int mr = 5; mr <= 5 + ms; mr++)
                    Pixel(canvas, px, mr, ps, sx, sy, Dark);
        }
        else if (Emotion == "happy" || Emotion == "very_happy")
        {
            Pixel(canvas, 4, 5, ps, sx, sy, accent);
            for (int px = 5; px <= 8; px++) Pixel(canvas, px, 4, ps, sx, sy, accent);
            Pixel(canvas, 9, 5, ps, sx, sy, accent);
        }
        else if (Emotion == "sad")
        {
            Pixel(canvas, 4, 4, ps, sx, sy, accent);
            for (int px = 5; px <= 8; px++) Pixel(canvas, px, 5, ps, sx, sy, accent);
            Pixel(canvas, 9, 4, ps, sx, sy, accent);
        }
        else
        {
            for (int px = 4; px <= 9; px++) Pixel(canvas, px, 5, ps, sx, sy, accent);
        }

        // === CUELLO ===
        for (int px = 5; px <= 8; px++) Pixel(canvas, px, 7, ps, sx, sy, DarkGray);

        // === CUERPO ===
        for (int row = 8; row <= 14; row++)
        {
            Pixel(canvas, 1, row, ps, sx, sy, Body);
            for (int px = 2; px <= 11; px++) Pixel(canvas, px, row, ps, sx, sy, BodyHL);
            Pixel(canvas, 12, row, ps, sx, sy, Body);
        }

        // Reactor
        float rp = 0.6f + (float)Math.Abs(Math.Sin(T * 3)) * 0.4f;
        Pixel(canvas, 5, 10, ps, sx, sy, accent.WithAlpha(rp));
        Pixel(canvas, 6, 10, ps, sx, sy, accent);
        Pixel(canvas, 7, 10, ps, sx, sy, accent);
        Pixel(canvas, 8, 10, ps, sx, sy, accent.WithAlpha(rp));
        Pixel(canvas, 6, 9, ps, sx, sy, accent.WithAlpha(rp));
        Pixel(canvas, 7, 9, ps, sx, sy, accent.WithAlpha(rp));
        Pixel(canvas, 6, 11, ps, sx, sy, accent.WithAlpha(rp));
        Pixel(canvas, 7, 11, ps, sx, sy, accent.WithAlpha(rp));
        Pixel(canvas, 6, 10, ps, sx, sy, White.WithAlpha(rp));
        for (int px = 3; px <= 10; px++) Pixel(canvas, px, 12, ps, sx, sy, Gray);

        // === BRAZOS ===
        canvas.SaveState();
        canvas.Translate(sx + 1 * ps, sy + 8 * ps);
        canvas.Rotate(-armAngle);
        canvas.Translate(-(sx + 1 * ps), -(sy + 8 * ps));
        for (int row = 8; row <= 13; row++) { Pixel(canvas, -1, row, ps, sx, sy, Body); Pixel(canvas, 0, row, ps, sx, sy, BodyHL); }
        Pixel(canvas, -1, 14, ps, sx, sy, Gray); Pixel(canvas, 0, 14, ps, sx, sy, Gray);
        canvas.RestoreState();

        canvas.SaveState();
        canvas.Translate(sx + 12 * ps, sy + 8 * ps);
        canvas.Rotate(armAngle);
        canvas.Translate(-(sx + 12 * ps), -(sy + 8 * ps));
        for (int row = 8; row <= 13; row++) { Pixel(canvas, 13, row, ps, sx, sy, BodyHL); Pixel(canvas, 14, row, ps, sx, sy, Body); }
        Pixel(canvas, 13, 14, ps, sx, sy, Gray); Pixel(canvas, 14, 14, ps, sx, sy, Gray);
        canvas.RestoreState();

        // === PIERNAS con animación de caminar ===
        float leftLegOff = (float)Math.Sin(T * 4f) * 1.5f;
        float rightLegOff = -(float)Math.Sin(T * 4f) * 1.5f;

        // Pierna izquierda
        for (int row = 15; row <= 18; row++)
        {
            Pixel(canvas, 3, row + (leftLegOff > 0 ? 0 : -1), ps, sx, sy, Body);
            Pixel(canvas, 4, row + (leftLegOff > 0 ? 0 : -1), ps, sx, sy, BodyHL);
            Pixel(canvas, 5, row + (leftLegOff > 0 ? 0 : -1), ps, sx, sy, Body);
        }
        Pixel(canvas, 2, 19, ps, sx, sy, Gray);
        Pixel(canvas, 3, 19, ps, sx, sy, Gray);
        Pixel(canvas, 4, 19, ps, sx, sy, Gray);
        Pixel(canvas, 5, 19, ps, sx, sy, Gray);

        // Pierna derecha
        for (int row = 15; row <= 18; row++)
        {
            Pixel(canvas, 8, row + (rightLegOff > 0 ? 0 : -1), ps, sx, sy, Body);
            Pixel(canvas, 9, row + (rightLegOff > 0 ? 0 : -1), ps, sx, sy, BodyHL);
            Pixel(canvas, 10, row + (rightLegOff > 0 ? 0 : -1), ps, sx, sy, Body);
        }
        Pixel(canvas, 8, 19, ps, sx, sy, Gray);
        Pixel(canvas, 9, 19, ps, sx, sy, Gray);
        Pixel(canvas, 10, 19, ps, sx, sy, Gray);
        Pixel(canvas, 11, 19, ps, sx, sy, Gray);
    }

    private static void Pixel(ICanvas canvas, float col, float row, float ps, float sx, float sy, Color color)
    {
        canvas.FillColor = color;
        canvas.FillRectangle(sx + col * ps, sy + row * ps, ps - 0.5f, ps - 0.5f);
    }

    private void SpawnParticles(float cx, float cy, float ps, Color color)
    {
        if (_particles.Count > 40) return;
        if ((Emotion == "very_happy" || IsJumping) && _rnd.NextDouble() < 0.4)
            _particles.Add((cx + (float)(_rnd.NextDouble() - 0.5) * ps * 8, cy,
                (float)(_rnd.NextDouble() - 0.5) * 3, (float)(-_rnd.NextDouble() * 4 - 1), 1f, color));
        if (Talking && _rnd.NextDouble() < 0.2)
            _particles.Add((cx + (float)(_rnd.NextDouble() - 0.5) * ps * 3, cy,
                (float)(_rnd.NextDouble() - 0.5), -1.5f, 0.8f, Yellow));
        if (IsShaking && _rnd.NextDouble() < 0.3)
            _particles.Add((cx + (float)(_rnd.NextDouble() - 0.5) * ps * 5, cy,
                (float)(_rnd.NextDouble() - 0.5) * 2, (float)(_rnd.NextDouble() - 0.5) * 2, 0.6f, Red));
    }

    private void DrawParticles(ICanvas canvas)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            float newLife = p.life - 0.04f;
            if (newLife <= 0) { _particles.RemoveAt(i); continue; }
            _particles[i] = (p.x + p.vx, p.y + p.vy, p.vx, p.vy, newLife, p.color);
            canvas.FillColor = p.color.WithAlpha(newLife);
            canvas.FillRectangle(p.x, p.y, 3, 3);
        }
    }
}