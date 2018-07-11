using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitInfoTools
{
    public class App : IExternalApplication
    {
        /// <summary>
        /// Расположение загруженной сборки
        /// </summary>
        public static string AssemblyLocation { get; } = Assembly.GetExecutingAssembly().Location;

        /// <summary>
        /// Путь относительно расположения загруженной сборки в котором расположена библиотека семейств
        /// </summary>
        public static string FamilyLibRelativePath { get; } = @".\_Families\";


        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
