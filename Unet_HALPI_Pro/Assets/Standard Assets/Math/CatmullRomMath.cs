using UnityEngine;

namespace BC_Solution
{
    public partial class Math
    {
        /// <summary>
        /// Calcul a CatmulRom spline interpolation with 4 given points.
        /// </summary>
        /// <param name="point0"></param>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="point3"></param>
        /// <param name="t"> interpolation parameter between point1 and point2</param>
        /// <returns>An interpolation point between point 2 and 3 </returns>
        public static Vector2 CatmullRomInterpolation(Vector2 point0, Vector2 point1, Vector2 point2, Vector2 point3, float t0, float t1, float t2, float t3, float t)
        {
            Vector2 A1 = ((t1 - t) * point0 + (t - t0) * point1) / (t1 - t0);
            Vector2 A2 = ((t2 - t) * point1 + (t - t1) * point2) / (t2 - t1);
            Vector2 A3 = ((t3 - t) * point2 + (t - t2) * point3) / (t3 - t2);

            Vector2 B1 = ((t2 - t) * A1 + (t - t0) * A2) / (t2 - t0);
            Vector2 B2 = ((t3 - t) * A2 + (t - t1) * A3) / (t3 - t1);

            return ((t2 - t) * B1 + (t - t1) * B2) / (t2 - t1);
        }


        /// <summary>
        /// Calcul a CatmulRom spline interpolation with 4 given points.
        /// </summary>
        /// <param name="point0"></param>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="point3"></param>
        /// <param name="t"> interpolation parameter between point1 and point2</param>
        /// <returns>An interpolation point between point 2 and 3 </returns>
        public static Vector3 CatmullRomInterpolation(Vector3 point0, Vector3 point1, Vector3 point2, Vector3 point3, float t0, float t1, float t2, float t3, float t)
        {
            Vector3 A1 = ((t1 - t) * point0 + (t - t0) * point1) / (t1 - t0);
            Vector3 A2 = ((t2 - t) * point1 + (t - t1) * point2) / (t2 - t1);
            Vector3 A3 = ((t3 - t) * point2 + (t - t2) * point3) / (t3 - t2);

            Vector3 B1 = ((t2 - t) * A1 + (t - t0) * A2) / (t2 - t0);
            Vector3 B2 = ((t3 - t) * A2 + (t - t1) * A3) / (t3 - t1);

            return ((t2 - t) * B1 + (t - t1) * B2) / (t2 - t1);
        }


        public static void DrawCatmullRomInterpolation(Vector2 point0, Vector2 point1, Vector2 point2, Vector2 point3, float t0, float t1, float t2, float t3)
        {
            if (t2 != t1)
            {
                for (float t = t1; t <= t2; t += ((t2 - t1) / 50f))
                {
                    Vector2 A1 = ((t1 - t) * point0 + (t - t0) * point1) / (t1 - t0);
                    Vector2 A2 = ((t2 - t) * point1 + (t - t1) * point2) / (t2 - t1);
                    Vector2 A3 = ((t3 - t) * point2 + (t - t2) * point3) / (t3 - t2);

                    Vector2 B1 = ((t2 - t) * A1 + (t - t0) * A2) / (t2 - t0);
                    Vector2 B2 = ((t3 - t) * A2 + (t - t1) * A3) / (t3 - t1);

                    DebugDraw.DrawMarker(((t2 - t) * B1 + (t - t1) * B2) / (t2 - t1), 0.05f, Color.red, 2f);
                }
            }
        }
        public class CatmullRomMath : MonoBehaviour
        {

            /// <summary>
            /// Calcul a CatmulRom spline interpolation with 4 given points.
            /// </summary>
            /// <param name="point0"></param>
            /// <param name="point1"></param>
            /// <param name="point2"></param>
            /// <param name="point3"></param>
            /// <param name="t"> interpolation parameter between point1 and point2</param>
            /// <returns>An interpolation point between point 2 and 3 </returns>
            public static Vector2 CatmullRomInterpolation(Vector2 point0, Vector2 point1, Vector2 point2, Vector2 point3, float t0, float t1, float t2, float t3, float t)
            {
                Vector2 A1 = ((t1 - t) * point0 + (t - t0) * point1) / (t1 - t0);
                Vector2 A2 = ((t2 - t) * point1 + (t - t1) * point2) / (t2 - t1);
                Vector2 A3 = ((t3 - t) * point2 + (t - t2) * point3) / (t3 - t2);

                Vector2 B1 = ((t2 - t) * A1 + (t - t0) * A2) / (t2 - t0);
                Vector2 B2 = ((t3 - t) * A2 + (t - t1) * A3) / (t3 - t1);

                return ((t2 - t) * B1 + (t - t1) * B2) / (t2 - t1);
            }


            /// <summary>
            /// Calcul a CatmulRom spline interpolation with 4 given points.
            /// </summary>
            /// <param name="point0"></param>
            /// <param name="point1"></param>
            /// <param name="point2"></param>
            /// <param name="point3"></param>
            /// <param name="t"> interpolation parameter between point1 and point2</param>
            /// <returns>An interpolation point between point 2 and 3 </returns>
            public static Vector3 CatmullRomInterpolation(Vector3 point0, Vector3 point1, Vector3 point2, Vector3 point3, float t0, float t1, float t2, float t3, float t)
            {
                Vector3 A1 = ((t1 - t) * point0 + (t - t0) * point1) / (t1 - t0);
                Vector3 A2 = ((t2 - t) * point1 + (t - t1) * point2) / (t2 - t1);
                Vector3 A3 = ((t3 - t) * point2 + (t - t2) * point3) / (t3 - t2);

                Vector3 B1 = ((t2 - t) * A1 + (t - t0) * A2) / (t2 - t0);
                Vector3 B2 = ((t3 - t) * A2 + (t - t1) * A3) / (t3 - t1);

                return ((t2 - t) * B1 + (t - t1) * B2) / (t2 - t1);
            }


            public static void DrawCatmullRomInterpolation(Vector2 point0, Vector2 point1, Vector2 point2, Vector2 point3, float t0, float t1, float t2, float t3)
            {
                if (t2 != t1)
                {
                    for (float t = t1; t <= t2; t += ((t2 - t1) / 50f))
                    {
                        Vector2 A1 = ((t1 - t) * point0 + (t - t0) * point1) / (t1 - t0);
                        Vector2 A2 = ((t2 - t) * point1 + (t - t1) * point2) / (t2 - t1);
                        Vector2 A3 = ((t3 - t) * point2 + (t - t2) * point3) / (t3 - t2);

                        Vector2 B1 = ((t2 - t) * A1 + (t - t0) * A2) / (t2 - t0);
                        Vector2 B2 = ((t3 - t) * A2 + (t - t1) * A3) / (t3 - t1);

                        DebugDraw.DrawMarker(((t2 - t) * B1 + (t - t1) * B2) / (t2 - t1), 0.05f, Color.red, 2f);
                    }
                }
            }
        }
    }
}
