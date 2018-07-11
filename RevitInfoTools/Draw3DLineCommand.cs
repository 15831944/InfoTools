using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;

namespace RevitInfoTools
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Draw3DLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            //Выбрать несколько файлов CSV с координатами
            Transform projectTransform = Utils.GetProjectCoordinatesTransform(doc);

            string[] filenames = null;
            WinForms.OpenFileDialog openFileDialog1 = new WinForms.OpenFileDialog();


            string curDocPath = doc.PathName;
            if (!String.IsNullOrEmpty(curDocPath))
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(curDocPath);
            openFileDialog1.Filter = "csv files (*.csv)|*.csv";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;
            openFileDialog1.Title = "Выберите таблицы CSV с координатами 3d-полилиний";

            if (openFileDialog1.ShowDialog() == WinForms.DialogResult.OK)
            {
                filenames = openFileDialog1.FileNames;

                List<List<XYZ>> lines3d = new List<List<XYZ>>();

                foreach (string filename in filenames)
                {
                    List<double[]> inputList = Utils.ReadCoordinates(filename);

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

                //Загрузить семейство 3d линии если нет
                Family line3dFamily = Utils.GetFamily(doc, "3d line");
                ElementId symId = line3dFamily.GetFamilySymbolIds().First();
                FamilySymbol familySymbol = (FamilySymbol)doc.GetElement(symId);

                //Расставить линии по координатам
                using (Transaction tr = new Transaction(doc))
                {
                    tr.Start("Draw 3D line");

                    foreach (List<XYZ> ptList in lines3d)
                    {
                        for (int i = 0; i < ptList.Count - 1; i++)
                        {
                            FamilyInstance instance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, familySymbol);
                            IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instance);
                            ReferencePoint point1 = (ReferencePoint)doc.GetElement(placePointIds[0]);
                            point1.Position = ptList[i];
                            ReferencePoint point2 = (ReferencePoint)doc.GetElement(placePointIds[1]);
                            point2.Position = ptList[i + 1];

                        }
                    }

                    tr.Commit();
                }
                    

            }



            return Result.Succeeded;
        }
    }
}
