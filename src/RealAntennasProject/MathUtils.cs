using System;
using UnityEngine;

namespace RealAntennas
{
    public class MathUtils
    {
        // https://mathworld.wolfram.com/Circle-CircleIntersection.html
        public static double CircleCircleIntersectionArea(double R, double r, double d)
        {
            double res = r * r * Math.Acos(((d * d) + (r * r) - (R * R)) / (2 * d * r));
            res += R * R * Math.Acos(((d * d) + (R * R) - (r * r)) / (2 * d * R));
            double A = -d + r + R;
            double B = d + r - R;
            double C = d - r + R;
            double D = d + r + R;
            res -= Math.Sqrt(A * B * C * D) / 2;
            return res;
        }

        //  Offset from circle radius R to chord passing through intersections of circles R and r
        public static double CircleCircleIntersectionOffset(double R, double r, double d) =>
            ((d * d) - (r * r) + (R * R)) / (2 * d);

        public static double AngularRadius(double radius, double distance)
        {
            Vector2d center = Vector2d.zero;
            Vector2d point = center + distance * Vector2d.right;
            double x_offset = CircleCircleIntersectionOffset(radius, distance / 2, distance / 2);
            // offset is the x-coord of a point on the body.
            double y_offset = Math.Sqrt((radius * radius) - (x_offset * x_offset));
            Vector2d calc;
            calc.x = x_offset;
            calc.y = y_offset;
            return Vector2d.Angle(center - point, calc - point);
        }
    }
}
