using UnityEngine;

namespace Dojo.Recording
{
    /// <summary>
    /// Helper methods for encoding recorded event data
    /// \see \link Dojo.Recording.DojoRecord DojoRecord \endlink
    /// </summary>
    public class DojoRecordEncode
    {
        /// <summary>
        /// Encode vector
        /// </summary>
        /// <param name="vec">vector</param>
        /// <returns>encoded string</returns>
        public static string Encode(Vector2 vec)
        {
            return $"({vec.x},{vec.y})";
        }

        /// <summary>
        /// Encode vector
        /// </summary>
        /// <param name="vec">vector</param>
        /// <returns>encoded string</returns>
        public static string Encode(Vector3 vec)
        {
            return $"({vec.x},{vec.y},{vec.z})";
        }

        /// <summary>
        /// Encode vector
        /// </summary>
        /// <param name="vec">vector</param>
        /// <returns>encoded string</returns>
        public static string Encode(Vector2Int vec)
        {
            return $"({vec.x},{vec.y})";
        }

        /// <summary>
        /// Encode vector
        /// </summary>
        /// <param name="vec">vector</param>
        /// <returns>encoded string</returns>
        public static string Encode(Vector3Int vec)
        {
            return $"({vec.x},{vec.y},{vec.z})";
        }

        /// <summary>
        /// Encode rotation
        /// </summary>
        /// <param name="r">rotation</param>
        /// <returns>encoded string</returns>
        public static string Encode(Quaternion r)
        {
            return $"({r.w},{r.x},{r.y},{r.z})";
        }

        /// <summary>
        /// Encode a Unity transform
        /// </summary>
        /// <param name="t">Unity transform</param>
        /// <returns>encoded string</returns>
        public static string Encode(Transform t)
        {
            return $"[{Encode(t.position)},{Encode(t.rotation)},{Encode(t.localScale)}]";
        }
    }
}
