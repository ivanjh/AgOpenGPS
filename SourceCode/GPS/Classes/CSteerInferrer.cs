using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgOpenGPS.Classes
{
    /// <summary>
    /// Calculates a steering angle based upon position & heading updates
    /// </summary>
    public class CSteerInferrer
    {
        private double prevNorthing = double.NaN, prevEasting = double.NaN, prevAzimuthDegrees = double.NaN, prevSteeringDegrees = 0;

        /// <summary>
        /// Calculates a steering angle based upon position & heading updates
        /// </summary>
        /// <param name="northing"></param>
        /// <param name="easting"></param>
        /// <param name="azimuthDegrees"></param>
        /// <param name="wheelBaseMeters"></param>
        /// <returns></returns>
        public double InferSteeringDegrees(double northing, double easting, double azimuthDegrees, double wheelBaseMeters)
        {
            double steeringDegrees = 0;
            if (!double.IsNaN(prevNorthing))
            {
                var deltaHeading = azimuthDegrees - prevAzimuthDegrees;
                var distDelta = Math.Sqrt(Math.Pow(prevNorthing - northing, 2) + Math.Pow(prevEasting - easting, 2));

                // If we haven't moved a significant amount - just return previous steer angle, and wait for distance to grow
                if (distDelta < 0.100)
                {
                    return prevSteeringDegrees;
                }

                // calculate turning diameter - radius=half the distDelta/sin(deltaHeading/2)
                var turnRadius = distDelta / 2 / Math.Sin(glm.toRadians(deltaHeading / 2));

                // Given a turn radius, what's the front center steering angle?
                // Front steer, center 
                steeringDegrees = Math.Atan(wheelBaseMeters / turnRadius) / glm.twoPI * 360;
            }
            prevNorthing = northing;
            prevEasting = easting;
            prevAzimuthDegrees = azimuthDegrees;
            prevSteeringDegrees = steeringDegrees;
            return steeringDegrees;
        }
    }
}
