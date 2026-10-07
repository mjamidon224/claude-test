using System.Drawing.Drawing2D;

namespace SpiderSolitaire.Rendering;

/// <summary>The bursts of light played over the table when a game is won.</summary>
internal sealed class Fireworks
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb(255, 90, 90),
        Color.FromArgb(255, 200, 60),
        Color.FromArgb(120, 230, 120),
        Color.FromArgb(90, 190, 255),
        Color.FromArgb(220, 120, 255),
        Color.FromArgb(255, 150, 210),
        Color.FromArgb(255, 255, 255),
    };

    private readonly List<Particle> _particles = new();
    private readonly Random _random = new();
    private double _launchTimeLeft;
    private double _untilNextBurst;

    public bool IsActive => _launchTimeLeft > 0 || _particles.Count > 0;

    public void Start(double seconds)
    {
        _launchTimeLeft = seconds;
        _untilNextBurst = 0;
    }

    public void Stop()
    {
        _launchTimeLeft = 0;
        _particles.Clear();
    }

    /// <summary>Advances by <paramref name="seconds"/>, launching new bursts while the show lasts.</summary>
    public void Update(double seconds, Size area)
    {
        if (!IsActive)
        {
            return;
        }

        // With no area (a minimised window) the show still runs its course, just unseen,
        // so it ends on time rather than keeping the frame timer alive.
        bool visible = area.Width > 0 && area.Height > 0;
        float scale = Math.Max(0.5f, area.Height / 800f);

        if (_launchTimeLeft > 0)
        {
            _launchTimeLeft -= seconds;
            _untilNextBurst -= seconds;
            while (visible && _untilNextBurst <= 0)
            {
                Burst(area, scale);
                _untilNextBurst += 0.28 + _random.NextDouble() * 0.35;
            }
        }

        float gravity = 170f * scale;
        float drag = (float)Math.Pow(0.35, seconds);
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle particle = _particles[i];
            particle.Life -= (float)seconds;
            if (particle.Life <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            particle.VelocityX *= drag;
            particle.VelocityY = particle.VelocityY * drag + gravity * (float)seconds;
            particle.X += particle.VelocityX * (float)seconds;
            particle.Y += particle.VelocityY * (float)seconds;
            _particles[i] = particle;
        }
    }

    public void Paint(Graphics graphics, float cardWidth)
    {
        if (_particles.Count == 0)
        {
            return;
        }

        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float baseSize = Math.Max(2.5f, cardWidth * 0.035f);

        using SolidBrush brush = new(Color.White);
        foreach (Particle particle in _particles)
        {
            float fade = Math.Clamp(particle.Life / particle.MaxLife, 0f, 1f);

            // Sparks twinkle as they fade out.
            if (fade < 0.35f && _random.NextDouble() < 0.3)
            {
                continue;
            }

            float size = baseSize * (0.5f + fade * 0.7f);
            brush.Color = Color.FromArgb((int)(255 * Math.Min(1f, fade * 1.6f)), particle.Color);
            graphics.FillEllipse(brush, particle.X - size / 2, particle.Y - size / 2, size, size);
        }

        graphics.SmoothingMode = smoothing;
    }

    private void Burst(Size area, float scale)
    {
        float x = area.Width * (0.12f + (float)_random.NextDouble() * 0.76f);
        float y = area.Height * (0.1f + (float)_random.NextDouble() * 0.4f);
        Color color = Palette[_random.Next(Palette.Length)];
        int count = 70 + _random.Next(40);

        for (int i = 0; i < count; i++)
        {
            double angle = _random.NextDouble() * Math.PI * 2;
            float speed = (float)(90 + _random.NextDouble() * 210) * scale;
            float life = 1.1f + (float)_random.NextDouble() * 0.8f;
            Color spark = _random.NextDouble() < 0.15 ? Color.White : color;

            _particles.Add(new Particle
            {
                X = x,
                Y = y,
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed,
                Life = life,
                MaxLife = life,
                Color = spark,
            });
        }
    }

    private struct Particle
    {
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public float Life;
        public float MaxLife;
        public Color Color;
    }
}
