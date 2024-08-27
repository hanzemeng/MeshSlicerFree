using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public struct Point2D : IComparable<Point2D>
{
    public int CompareTo(Point2D other)
    {
        return Compare(this,other);
    }
    public int Compare(Point2D a, Point2D b)
    {
        if(a.x < b.x)
        {
            return -1;
        }
        if(a.x > b.x)
        {
            return 1;
        }
        if(a.y < b.y)
        {
            return -1;
        }
        if(a.y > b.y)
        {
            return 1;
        }
        return 0;
    }

    public Point2D(Vector2 v)
    {
        x=v.x;
        y=v.y;
    }
    public Point2D(double x, double y)
    {
        this.x=x;
        this.y=y;
    }

    public override string ToString()
    {
        return $"{x.ToString("F12")}, {y.ToString("F12")}";
    }

    public double x;
    public double y;
}

public struct Point3D : IComparable<Point3D>
{
    public int CompareTo(Point3D other)
    {
        return Compare(this,other);
    }
    public int Compare(Point3D a, Point3D b)
    {
        if(a.x < b.x)
        {
            return -1;
        }
        if(a.x > b.x)
        {
            return 1;
        }
        if(a.y < b.y)
        {
            return -1;
        }
        if(a.y > b.y)
        {
            return 1;
        }
        if(a.z < b.z)
        {
            return -1;
        }
        if(a.z > b.z)
        {
            return 1;
        }
        return 0;
    }

    public Point3D(Vector3 v)
    {
        x=v.x;
        y=v.z; // swap z and y for predicates to work
        z=v.y; // swap z and y for predicates to work
    }
    public Vector3 ToVector3()
    {
        return new Vector3((float)x,(float)z,(float)y);
    }
    public Point3D(double x, double y, double z)
    {
        this.x=x;
        this.y=y;
        this.z=z;
    }
    public void Reset()
    {
        x=0d;
        y=0d;
        z=0d;
    }

    public void Normalize()
    {
        double dis = Math.Sqrt(Dot(this, this));
        x /= dis;
        y /= dis;
        z /= dis;
    }

    public Point3D GetPerpendicular()
    {
        Point3D xAxis;
        if(0d != x)
        {
            xAxis = new Point3D(-y/x, 1d, 0d);
        }
        else if(0d != y)
        {
            xAxis = new Point3D(0d, -z/y, 1d);
        }
        else
        {
            xAxis = new Point3D(1d, 0d, -x/z);
        }
        return xAxis;
        //Vector3 yAxis = Vector3.Cross(p.normal, xAxis);
    }

    public override string ToString()
    {
        return $"{x}, {y}, {z}";
    }

    public static double Dot(Point3D p1, Point3D p2)
    {
        return p1.x*p2.x+p1.y*p2.y+p1.z*p2.z;
    }
    public static Point3D Cross(Point3D p1, Point3D p2)
    {
        return new Point3D(p1.y*p2.z-p1.z*p2.y, p1.z*p2.x-p1.x*p2.z, p1.x*p2.y-p1.y*p2.x);
    }
    public static Point3D operator+(Point3D p1, Point3D p2)
    {
        return new Point3D(p1.x+p2.x,p1.y+p2.y,p1.z+p2.z);
    }
    public static Point3D operator-(Point3D p1, Point3D p2)
    {
        return new Point3D(p1.x-p2.x,p1.y-p2.y,p1.z-p2.z);
    }
    public static Point3D operator*(Point3D p, double d)
    {
        return new Point3D(p.x*d,p.y*d,p.z*d);
    }

    public double x;
    public double y;
    public double z;
}

public class Vector2Comparator : IComparer<Vector2>
{
    public int Compare(Vector2 a, Vector2 b)
    {
        if(a.x < b.x)
        {
            return -1;
        }
        if(a.x > b.x)
        {
            return 1;
        }
        if(a.y < b.y)
        {
            return -1;
        }
        if(a.y > b.y)
        {
            return 1;
        }
        return 0;
    }
}

}
