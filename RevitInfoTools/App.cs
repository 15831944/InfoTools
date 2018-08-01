using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

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
        public static string FamilyLibRelativePath { get; } = @".\Families\";



        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //Создание панелей и кнопок
            AddRibbonPanel(application);


            return Result.Succeeded;
        }


        /// <summary>
        /// http://archi-lab.net/create-your-own-tab-and-buttons-in-revit/
        /// Создание панелей и кнопок
        /// </summary>
        /// <param name="application"></param>
        private static void AddRibbonPanel(UIControlledApplication application)
        {
            // Create a custom ribbon tab
            String tabName = "InfoTools";
            application.CreateRibbonTab(tabName);

            // Add a new ribbon panel
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "InfoTools");

            // Get dll assembly path
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            {
                PushButtonData pbData = new PushButtonData(
                "Draw3DLine",
                "Draw3DLine",
                thisAssemblyPath,
                "RevitInfoTools.Draw3DLineCommand");
                BitmapSource bitmap = GetEmbeddedImage("RevitInfoTools.Icons.Draw3DLine.png");
                pbData.LargeImage = bitmap;
                pbData.Image = bitmap;

                PushButton pb = ribbonPanel.AddItem(pbData) as PushButton;
                pb.ToolTip = "Создание 3d линии по координатам. Вставка адаптивных семейств";
            }


            {
                PushButtonData pbData = new PushButtonData(
                "Draw3DLine2",
                "Draw3DLine2",
                thisAssemblyPath,
                "RevitInfoTools.Draw3DLine2Command");
                BitmapSource bitmap = GetEmbeddedImage("RevitInfoTools.Icons.Draw3DLine2.png");
                pbData.LargeImage = bitmap;
                pbData.Image = bitmap;

                PushButton pb = ribbonPanel.AddItem(pbData) as PushButton;
                pb.ToolTip = "Создание 3d линии по координатам. Линии модели";
            }


            {
                PushButtonData pbData = new PushButtonData(
                "SpillwaysPlacement",
                "SpillwaysPlacement",
                thisAssemblyPath,
                "RevitInfoTools.SpillwaysPlacementCommand");
                BitmapSource bitmap = GetEmbeddedImage("RevitInfoTools.Icons.SpillwaysPlacement.png");
                pbData.LargeImage = bitmap;
                pbData.Image = bitmap;

                PushButton pb = ribbonPanel.AddItem(pbData) as PushButton;
                pb.ToolTip = "Расстановка водосбросов в Revit";
            }

            {
                PushButtonData pbData = new PushButtonData(
                "PlaceCrossSections",
                "PlaceCrossSections",
                thisAssemblyPath,
                "RevitInfoTools.PlaceCrossSectionsCommand");
                BitmapSource bitmap = GetEmbeddedImage("RevitInfoTools.Icons.PlaceCrossSections.png");
                pbData.LargeImage = bitmap;
                pbData.Image = bitmap;

                PushButton pb = ribbonPanel.AddItem(pbData) as PushButton;
                pb.ToolTip = "Расстановка поперечных сечений по координатам";
            }


        }

        static BitmapSource GetEmbeddedImage(string name)
        {
            try
            {
                Assembly a = Assembly.GetExecutingAssembly();
                Stream s = a.GetManifestResourceStream(name);
                return BitmapFrame.Create(s);
            }
            catch
            {
                return null;
            }
        }
    }
}
