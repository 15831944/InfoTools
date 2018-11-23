using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using RBush;

namespace Civil3DInfoTools.RBush
{
    public class Spatial
    {
        private Envelope _envelope;
        public ref readonly Envelope Envelope => ref _envelope;

        public object Obj { get; private set; }

        public Spatial(Extents2d ext, object obj)
        {
            Obj = obj;
        }
    }
}
