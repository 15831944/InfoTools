using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace RevitInfoTools
{
    public static class Utils
    {
        /// <summary>
        /// Получить матрицу трансформации для перевода реальных координат в координаты проекта
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static Transform GetProjectCoordinatesTransform(Document doc, bool considerElevation = true)
        {
            ProjectLocation projectLocation = doc.ActiveProjectLocation;

            ProjectPosition position = projectLocation.GetProjectPosition(XYZ.Zero);
            double eastWest = position.EastWest;
            double northSouth = position.NorthSouth;
            double elevation = 0;
            if (considerElevation)
                elevation = position.Elevation;
            double rotation = position.Angle;
            //Матрица пересчета координат в проектные координаты
            //http://thebuildingcoder.typepad.com/blog/2010/01/project-location.html
            Transform rotationTransform = Transform.CreateRotation(XYZ.BasisZ, -rotation);
            XYZ translationVector = new XYZ(-eastWest, -northSouth, -elevation);
            Transform translationTransform = Transform.CreateTranslation(translationVector);
            Transform finalTransform = rotationTransform * translationTransform;
            return finalTransform;
        }

        
        /// <summary>
        /// Чтение координат из нескольких файлов CSV с пересчетом координат в проектную систему и переводом в футы
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="projectTransform"></param>
        /// <param name="lines3d"></param>
        /// <returns></returns>
        public static bool ReadCoordinatesFromCSV(string[] filenames, Transform projectTransform, List<List<XYZ>> lines3d)
        {
            foreach (string filename in filenames)
            {
                List<double[]> inputList = null;
                try
                {
                    inputList = Common.Utils.ReadCoordinates(filename);
                }
                catch (IOException ex)
                {
                    if (ex.Message.StartsWith("The process cannot access the file"))
                    {
                        AccessException(ex as IOException);
                        return false;
                    }
                    else
                    {
                        throw ex;
                    }
                }

                if (inputList != null && inputList.Count > 0)
                {
                    List<XYZ> ptList = new List<XYZ>();

                    foreach (double[] coordArr in inputList)
                    {
                        XYZ globalPoint = Utils.PointByMeters(coordArr[0], coordArr[1], coordArr[2]);//Перевод в футы из метров
                        XYZ projectPoint = projectTransform.OfPoint(globalPoint);//Пересчет координат в проектную систему проекта Revit
                        ptList.Add(projectPoint);
                    }

                    lines3d.Add(ptList);
                }

            }
            return true;
        }




        public static XYZ PointByMeters(double x, double y, double z)
        {
            return new XYZ(UnitUtils.Convert(x, DisplayUnitType.DUT_METERS, DisplayUnitType.DUT_DECIMAL_FEET),
                UnitUtils.Convert(y, DisplayUnitType.DUT_METERS, DisplayUnitType.DUT_DECIMAL_FEET),
                UnitUtils.Convert(z, DisplayUnitType.DUT_METERS, DisplayUnitType.DUT_DECIMAL_FEET));
        }


        /// <summary>
        /// Получить семейство. Если нет в проекте, то из библиотеки
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="famName"></param>
        public static Family GetFamily(Document doc, string famName)
        {
            //Проверить наличие в проекте семейства с заданным именем
            FilteredElementCollector symbolCollector = new FilteredElementCollector(doc);
            symbolCollector.OfClass(typeof(Family));
            Family searchedfamily = null;
            foreach (Element e in symbolCollector)
            {
                Family fam = (Family)e;
                if (fam.Name.Equals(famName))
                {
                    //Если семейство загружено, то получить список всех типов этого семейства
                    searchedfamily = fam;
                    break;
                }
            }
            if (searchedfamily == null)
            {
                //http://thebuildingcoder.typepad.com/blog/2013/06/family-api-add-in-load-family-and-place-instances.html
                //Если не найдено искомое семейство загрузить его из файла с относительным путем
                string fileName = Path.Combine(Path.GetDirectoryName(App.AssemblyLocation),
                    App.FamilyLibRelativePath + famName + ".rfa");
                fileName = Path.GetFullPath((new Uri(fileName)).LocalPath);
                if (!File.Exists(fileName))
                {
                    return searchedfamily;
                }

                // Load family from file:
                using (Transaction tx = new Transaction(doc))
                {
                    tx.Start("Load Family");
                    doc.LoadFamily(fileName, out searchedfamily);

                    tx.Commit();
                }
            }
            return searchedfamily;
        }

        /// <summary>
        /// Активировать типоразмер
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="familySymbol"></param>
        public static void ActivateFamSym(Document doc, FamilySymbol familySymbol)
        {
            if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
                doc.Regenerate();
            }
        }
    }
}
