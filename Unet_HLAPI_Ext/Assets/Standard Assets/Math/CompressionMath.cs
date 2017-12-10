/*Copyright(c) <2017> <Benoit Constantin ( France )>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/

using UnityEngine;

namespace BC_Solution
{
    public partial class Math
    {
        public static ushort CompressToShort(float value, float minValue, float maxValue, out float precision)
        {
            precision = (float)((Mathf.Ceil(maxValue - minValue)) / (System.Math.Pow(2,(8*sizeof(ushort))) - 1) / 2f);
            float scale = (float)((maxValue - minValue) / (System.Math.Pow(2, (8 * sizeof(ushort))) - 1));
            return (ushort)Mathf.Round((value - minValue) / scale);
        }

        public static float Decompress(ushort value, float minValue, float maxValue)
        {
            float scale = (float)((maxValue - minValue) / (System.Math.Pow(2, (8 * sizeof(ushort))) - 1));
            return scale * value + minValue;
        }
    }
}
