using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Civil3DInfoTools.Spillway
{
    public static class Constants
    {
        public static Regex SlopeLayerRegex { get; } = new Regex("^ОТК[0-9]*$");
    }
}
