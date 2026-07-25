namespace Yottaverse.MachineOps.Core.GCode;

public readonly record struct Position3D(double X, double Y, double Z)
{
    public static Position3D Origin { get; } = new(0, 0, 0);

    public double DistanceTo(Position3D other)
    {
        double x = other.X - X;
        double y = other.Y - Y;
        double z = other.Z - Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }
}
