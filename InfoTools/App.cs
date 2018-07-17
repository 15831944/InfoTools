using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Common.ExceptionHandling;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools
{
    public class App : IExtensionApplication
    {
        private static PolylineCaptionOverrule polylineCaptionOverrule = null;

        void IExtensionApplication.Initialize()
        {
            try
            {
                //Запустить PolylineCaptionOverrule
                polylineCaptionOverrule = new PolylineCaptionOverrule();
                Overrule.AddOverrule(RXClass.GetClass(typeof(Polyline3d)), polylineCaptionOverrule, false);
            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при инициализации плагина для Civil 3d");
            }
        }


        void IExtensionApplication.Terminate()
        {

        }
    }
}
