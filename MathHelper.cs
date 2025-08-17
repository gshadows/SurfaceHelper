using System;
using System.Text;
using System.IO;

namespace Observatory.SurfaceHelper {
    public static class MathHelper {
        /**
        * Calculate distance between two points on planed with given radius.
        **/
        public static double calculateGreatCircleDistance(
            (double lat, double lon) p1,
            (double lat, double lon) p2,
            double radius
        ) {
            var latDeltaSin = Math.Sin(toRadians(p1.lat - p2.lat) / 2);
            var longDeltaSin = Math.Sin(toRadians(p1.lon - p2.lon) / 2);
            
            var hSin = latDeltaSin * latDeltaSin + Math.Cos(toRadians(p1.lat)) * Math.Cos(toRadians(p1.lat)) * longDeltaSin * longDeltaSin;
            return Math.Abs(2 * radius * Math.Asin(Math.Sqrt(hSin)));
        }

        public static double toRadians(double degrees) => degrees * 0.0174533;


        /**
        * Calculate middle point between two location on planet surface.
        * Given factor (-1..+1) defines middle point offset, where:
        * -1 - point 1
        *  0 - middle point
        * +1 - point 2
        * This function ignores surface curvature because it supposed to calculate short distances
        * between ship cockpit and player exit point, which is hardly more then 100 meters.
        **/
        public static (double, double) middlePoint(
            (double lat, double lon) p1,
            (double lat, double lon) p2,
            double factor
        ) {
            double weight = (factor + 1) / 2.0;
            double midLat = p1.lat + weight * (p2.lat - p1.lat);
            double midLon = p1.lon + weight * (p2.lon - p1.lon);
            return (midLat, midLon);
        }

        public static double kelvinToCelsius(double temp) => temp - 273.15f;

        public static double kelvinToFarenheit(double temp) => (temp - 273.15f) * 9/5 + 32;
    }
}
