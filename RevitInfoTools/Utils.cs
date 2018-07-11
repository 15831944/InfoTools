using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        /// Чтение координат из файла CSV
        /// </summary>
        /// <param name="csvFileName"></param>
        /// <returns></returns>
        public static List<double[]> ReadCoordinates(string csvFileName)
        {
            string[] csvLines = null;


            csvLines = File.ReadAllLines(csvFileName, Encoding.UTF8);


            List<double[]> ptList = new List<double[]>();
            foreach (string csvLine in csvLines)
            {
                string[] xyzLine = csvLine.Split(';');

                List<string> xyzLineTrim = new List<string>();
                Regex regex = new Regex("^.*\\w+.*$");//Содержит хотябы один любой символ кроме пропусков
                foreach (string str in xyzLine)
                {
                    if (regex.IsMatch(str))
                    {
                        xyzLineTrim.Add(str);
                    }
                }

                if (xyzLineTrim.Count == 0)//Строка содержит только пропуски. Переход к следующей строке
                    continue;

                if (xyzLineTrim.Count == 3)
                {
                    double[] globalPoint = new double[]
                    {
                            Convert.ToDouble(xyzLineTrim[0].Replace(" ", "").Replace(",",".")),
                            Convert.ToDouble(xyzLineTrim[1].Replace(" ", "").Replace(",",".")),
                            Convert.ToDouble(xyzLineTrim[2].Replace(" ", "").Replace(",","."))
                    };

                    ptList.Add(globalPoint);
                }
                else
                {
                    throw new FormatException("Неверный формат данных");
                }

            }
            return ptList;
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
    }
}
