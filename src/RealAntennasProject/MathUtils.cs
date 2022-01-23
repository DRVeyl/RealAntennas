using System;
using Unity.Mathematics;
using UnityEngine;

namespace RealAntennas
{
    public class MathUtils
    {
        //https://forum.kerbalspaceprogram.com/index.php?/topic/164418-vector3angle-more-accurate-and-numerically-stable-at-small-angles-version/
        /// <summary>
        ///  Angle between two vectors, in degrees
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double Angle2(double3 a, double3 b)
        {
            var abm = a * math.length(b);
            var bam = b * math.length(a);
            return math.degrees(2 * math.atan2(math.length(abm - bam), math.length(abm + bam)));
        }

        // https://mathworld.wolfram.com/Circle-CircleIntersection.html
        public static double CircleCircleIntersectionArea(double R, double r, double d)
        {
            double res = r * r * math.acos(((d * d) + (r * r) - (R * R)) / (2 * d * r));
            res += R * R * math.acos(((d * d) + (R * R) - (r * r)) / (2 * d * R));
            double A = -d + r + R;
            double B = d + r - R;
            double C = d - r + R;
            double D = d + r + R;
            res -= math.sqrt(A * B * C * D) / 2;
            return res;
        }
        public static float CircleCircleIntersectionArea(float R, float r, float d)
        {
            float res = r * r * math.acos(((d * d) + (r * r) - (R * R)) / (2 * d * r));
            res += R * R * math.acos(((d * d) + (R * R) - (r * r)) / (2 * d * R));
            float A = -d + r + R;
            float B = d + r - R;
            float C = d - r + R;
            float D = d + r + R;
            res -= math.sqrt(A * B * C * D) / 2;
            return res;
        }
        //  Offset from circle radius R to chord passing through intersections of circles R and r
        public static double CircleCircleIntersectionOffset(double R, double r, double d) =>
            ((d * d) - (r * r) + (R * R)) / (2 * d);
        public static float CircleCircleIntersectionOffset(float R, float r, float d) =>
            ((d * d) - (r * r) + (R * R)) / (2 * d);

        public static double AngularRadius(double radius, double distance)
        {
            double3 center = new double3(0, 0, 0);
            double3 point = center + distance * new double3(1, 0, 0);
            double x_offset = CircleCircleIntersectionOffset(radius, distance / 2, distance / 2);
            // offset is the x-coord of a point on the body.
            double y_offset_sq = Math.Max(0, ((radius * radius) - (x_offset * x_offset)));
            double y_offset = math.sqrt(y_offset_sq);
            double3 calc = new double3(x_offset, y_offset, 0);
            return Angle2(center - point, calc - point);
        }

        public static float AngularRadius(float radius, float distance)
        {
            float3 center = new float3(0, 0, 0);
            float3 point = center + distance * new float3(1, 0, 0);
            float x_offset = Convert.ToSingle(CircleCircleIntersectionOffset(radius, distance / 2, distance / 2));
            // offset is the x-coord of a point on the body.
            float y_offset_sq = Math.Max(0, (radius * radius) - (x_offset * x_offset));
            float y_offset = math.sqrt(y_offset_sq);
            float3 calc = new float3(x_offset, y_offset, 0);
            return (float)Angle2(center - point, calc - point);
        }

        public static float ElevationAngle(double3 position, double3 surfaceNormal, double3 origin)
        {
            double3 to_origin = origin - position;
            float angle = (float)Angle2(surfaceNormal, to_origin);
            float elevation = math.max(0, 90.0f - angle);
            return elevation;
        }
    }
}
