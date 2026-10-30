namespace tPoseidon.Panel;

public sealed class SpringAxis
{
    public const double MaxSubStep = 1.0 / 240;
    public const double RestDistance = 0.0005;
    public const double RestSpeed = 0.002;

    private readonly double _stiffness;
    private readonly double _damping;

    public SpringAxis(double position, double stiffness, double damping)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stiffness);
        ArgumentOutOfRangeException.ThrowIfNegative(damping);

        Position = position;
        _stiffness = stiffness;
        _damping = damping;
    }

    public double Position { get; private set; }

    public double Velocity { get; private set; }

    public double DampingRatio => _damping / (2 * Math.Sqrt(_stiffness));

    public bool IsAtRest(double target) => Math.Abs(Position - target) < RestDistance && Math.Abs(Velocity) < RestSpeed;

    public void Kick(double velocity) => Velocity += velocity;

    public void Snap(double position)
    {
        Position = position;
        Velocity = 0;
    }

    public void Step(double target, double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        var steps = (int)Math.Ceiling(seconds / MaxSubStep);
        var h = seconds / steps;
        for (var i = 0; i < steps; i++)
        {
            Velocity += (_stiffness * (target - Position) - _damping * Velocity) * h;
            Position += Velocity * h;
        }

        if (IsAtRest(target))
        {
            Snap(target);
        }
    }
}

public sealed class VelocityFilter
{
    public const double DefaultTimeConstant = 0.06;

    private readonly double _timeConstant;

    public VelocityFilter(double timeConstant = DefaultTimeConstant)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeConstant);

        _timeConstant = timeConstant;
    }

    public double X { get; private set; }

    public double Y { get; private set; }

    public void Add(double deltaX, double deltaY, double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        var blend = 1 - Math.Exp(-seconds / _timeConstant);
        X += (deltaX / seconds - X) * blend;
        Y += (deltaY / seconds - Y) * blend;
    }

    public void Reset()
    {
        X = 0;
        Y = 0;
    }
}

public readonly record struct BodyDeform(double M11, double M12, double M22);

public static class DragSquash
{
    public const double FullSpeed = 1800;
    public const double MaxStretch = 0.22;
    public const double SquashShare = 0.8;
    public const double MinimumStretch = 0.0005;
    public const double MinimumAxisSpeed = 20;

    public static double Speed(double velocityX, double velocityY) => Math.Sqrt(velocityX * velocityX + velocityY * velocityY);

    public static double Amount(double velocityX, double velocityY) => Math.Min(Speed(velocityX, velocityY) / FullSpeed, 1) * MaxStretch;

    public static BodyDeform Deform(double amount, double cos, double sin)
    {
        if (Math.Abs(amount) < MinimumStretch)
        {
            return new BodyDeform(1, 0, 1);
        }

        var limited = Math.Clamp(amount, -MaxStretch * 2, MaxStretch * 2);
        var along = 1 + limited;
        var across = 1 - limited * SquashShare;
        return new BodyDeform(
            along * cos * cos + across * sin * sin,
            (along - across) * cos * sin,
            along * sin * sin + across * cos * cos);
    }
}

public sealed class BodyDynamics
{
    public const double Stiffness = 340;
    public const double Damping = 15;
    public const double PressedScale = 0.92;
    public const double PressStiffness = 520;
    public const double PressDamping = 20;

    private readonly SpringAxis _stretch = new(0, Stiffness, Damping);
    private readonly SpringAxis _press = new(1, PressStiffness, PressDamping);
    private readonly VelocityFilter _velocity = new();
    private double _pendingX;
    private double _pendingY;
    private double _cos = 1;
    private double _sin;

    public bool Dragging { get; set; }

    public bool Pressed { get; set; }

    public double PressScale => _press.Position;

    public double Stretch => _stretch.Position;

    public BodyDeform Deform => DragSquash.Deform(_stretch.Position, _cos, _sin);

    public bool IsAtRest => !Dragging && _stretch.IsAtRest(0) && _press.IsAtRest(PressTarget);

    private double PressTarget => Pressed && !Dragging ? PressedScale : 1;

    public void Move(double deltaX, double deltaY)
    {
        _pendingX += deltaX;
        _pendingY += deltaY;
    }

    public void Step(double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        _velocity.Add(_pendingX, _pendingY, seconds);
        _pendingX = 0;
        _pendingY = 0;
        var target = 0.0;
        if (Dragging)
        {
            var speed = DragSquash.Speed(_velocity.X, _velocity.Y);
            if (speed >= DragSquash.MinimumAxisSpeed)
            {
                _cos = _velocity.X / speed;
                _sin = _velocity.Y / speed;
            }

            target = DragSquash.Amount(_velocity.X, _velocity.Y);
        }
        else
        {
            _velocity.Reset();
        }

        _stretch.Step(target, seconds);
        _press.Step(PressTarget, seconds);
    }

    public void Reset()
    {
        _stretch.Snap(0);
        _press.Snap(1);
        _velocity.Reset();
        _pendingX = 0;
        _pendingY = 0;
        Dragging = false;
        Pressed = false;
    }
}

public sealed class ButtonFeel
{
    public const double HoverScale = 1.06;
    public const double PressedScale = 0.9;
    public const double Stiffness = 560;
    public const double Damping = 17;
    public const double GlowDampingBoost = 1.6;
    public const double HoverGlow = 0.06;
    public const double PressedGlow = 0.16;

    private readonly SpringAxis _scale = new(1, Stiffness, Damping);
    private readonly SpringAxis _glow = new(0, Stiffness, Damping * GlowDampingBoost);

    public bool Hovered { get; set; }

    public bool Pressed { get; set; }

    public double Scale => _scale.Position;

    public double Glow => Math.Clamp(_glow.Position, 0, 1);

    public double ScaleTarget => Pressed ? PressedScale : Hovered ? HoverScale : 1;

    public double GlowTarget => Pressed ? PressedGlow : Hovered ? HoverGlow : 0;

    public bool IsAtRest => _scale.IsAtRest(ScaleTarget) && _glow.IsAtRest(GlowTarget);

    public void Step(double seconds)
    {
        _scale.Step(ScaleTarget, seconds);
        _glow.Step(GlowTarget, seconds);
    }

    public void Settle()
    {
        _scale.Snap(ScaleTarget);
        _glow.Snap(GlowTarget);
    }

    public void Reset()
    {
        _scale.Snap(1);
        _glow.Snap(0);
        Hovered = false;
        Pressed = false;
    }
}

public readonly record struct RingFrame(double Scale, double Opacity);

public static class ButtonEffects
{
    public const double ConfirmLength = 0.45;
    public const double ConfirmGrowth = 0.7;
    public const double ConfirmStrength = 0.8;
    public const double ShakeLength = 0.4;
    public const double ShakeCycles = 3;
    public const double ShakeDistance = 5;
    public const double SpinPeriod = 0.9;
    public const double FullTurn = 360;
    public const double BusyOpacity = 0.6;
    public const double DisabledOpacity = 0.4;

    public static RingFrame Confirm(double elapsed)
    {
        var t = Math.Clamp(elapsed / ConfirmLength, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        return new RingFrame(1 + ConfirmGrowth * eased, ConfirmStrength * (1 - eased));
    }

    public static double Shake(double elapsed)
    {
        var t = Math.Clamp(elapsed / ShakeLength, 0, 1);
        return Math.Sin(t * 2 * Math.PI * ShakeCycles) * Math.Pow(1 - t, 2) * ShakeDistance;
    }

    public static double Spin(double elapsed) => elapsed % SpinPeriod / SpinPeriod * FullTurn;
}
