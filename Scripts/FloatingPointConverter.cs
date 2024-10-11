//#define USE_DECIMAL

namespace Hanzzz.MeshSlicerFree
{

public static class FloatingPointConverter
{
    #if USE_DECIMAL
    // expensive but keep decimals, used for debugging
    public static double FloatToDouble(float val)
    {
        return (double)new decimal(val);
    }
    public static float DoubleToFloat(double val)
    {
        return (float)new decimal(val);
    }
    public static double KeepFloatDecimals(double val)
    {
        return FloatToDouble(DoubleToFloat(val));
    }

    #else
    // cheap but decimals may be inaccurate, hopefully not a problem for predicates
    public static double FloatToDouble(float val)
    {
        return (double)(val);
    }
    public static float DoubleToFloat(double val)
    {
        return (float)(val);
    }
    public static double KeepFloatDecimals(double val)
    {
        return FloatToDouble(DoubleToFloat(val));
    }
    #endif
}

}
