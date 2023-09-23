using UnityEngine;

namespace Water2D
{
    public class ColorUtils
    {

        public static bool IsNumeric(char c)
        {
            return (c >= '0' && c <= '9');
        }

        public static Color HexToRGB(string hex)
        {
            float r = (((IsNumeric(hex[0]) ? (int)(hex[0] - '0') : (int)(hex[0] - 'A' + 11)) * 16) + (IsNumeric(hex[1]) ? (int)(hex[1] - '0') : (int)(hex[1] - 'A'))) / 255f;
            float g = (((IsNumeric(hex[2]) ? (int)(hex[2] - '0') : (int)(hex[2] - 'A' + 11)) * 16) + (IsNumeric(hex[3]) ? (int)(hex[3] - '0') : (int)(hex[3] - 'A'))) / 255f;
            float b = (((IsNumeric(hex[4]) ? (int)(hex[4] - '0') : (int)(hex[4] - 'A' + 11)) * 16) + (IsNumeric(hex[5]) ? (int)(hex[5] - '0') : (int)(hex[5] - 'A'))) / 255f;
            return new Color(r, g, b);
        }
    }

}
