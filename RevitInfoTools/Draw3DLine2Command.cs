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
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using WinForms = System.Windows.Forms;
using RCreation = Autodesk.Revit.Creation;

namespace RevitInfoTools
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Draw3DLine2Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("ОТМЕНЕНО", "Данная команда предназначена для запуска в документе проекта");
                return Result.Cancelled;
            }


            try
            {
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

                    if (Utils.ReadCoordinatesFromCSV(filenames, projectTransform, lines3d))
                    {
                        //Создать линии модели по координатам
                        RCreation.Document crDoc = doc.Create;

                        using (Transaction tr = new Transaction(doc))
                        {
                            tr.Start("Draw 3D line");

                            foreach (List<XYZ> ptList in lines3d)
                            {
                                for (int i = 0; i < ptList.Count - 1; i++)
                                {
                                    XYZ startPt = ptList[i];
                                    XYZ endPt = ptList[i + 1];

                                    if (!startPt.IsAlmostEqualTo(endPt))
                                    {
                                        Line line = Line.CreateBound(startPt, endPt);

                                        XYZ lineVector = (endPt - startPt).Normalize();
                                        XYZ horizontal = XYZ.BasisZ.CrossProduct(lineVector).Normalize();
                                        XYZ norm = lineVector.CrossProduct(horizontal).Normalize();
                                        Plane plane = Plane.CreateByNormalAndOrigin(norm, startPt);
                                        SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                                        crDoc.NewModelCurve(line, sketchPlane);
                                    }


                                }
                            }

                            tr.Commit();
                        }
                    }

                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при вычерчивании 3d линии в Revit");
            }


            return Result.Succeeded;
        }
    }
}
