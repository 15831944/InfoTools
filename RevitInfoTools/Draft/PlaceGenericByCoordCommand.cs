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

namespace RevitInfoTools
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    /// <summary>
    /// Расстановка объектов по координатам полилинии с ориентированием по сегментам полилинии
    /// Объекты должны быть на основе плоскости
    /// Точка вставки объекта в начале каждого сегмента
    /// </summary>
    public class PlaceGenericByCoordCommand : IExternalCommand
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
                openFileDialog1.Title = "Выберите таблицы CSV с координатами";

                if (openFileDialog1.ShowDialog() == WinForms.DialogResult.OK)
                {
                    filenames = openFileDialog1.FileNames;

                    List<List<XYZ>> lines3d = new List<List<XYZ>>();

                    if (Utils.ReadCoordinatesFromCSV(filenames, projectTransform, lines3d)
                        && lines3d.Count > 0 && lines3d.First().Count > 0)
                    {
                        Categories categories = doc.Settings.Categories;
                        ElementId IdGeneric = categories.get_Item(BuiltInCategory.OST_GenericModel).Id;



                        SelectTypeWindow selectTypeWindow = new SelectTypeWindow(doc, IdGeneric);

                        bool? result = selectTypeWindow.ShowDialog();
                        if (result != null && result.Value
                            && selectTypeWindow.SelectedFamilySymbols.Count > 0)
                        {
                            FamilySymbol familySymbol = selectTypeWindow.SelectedFamilySymbols.First();
                            //Document famDoc = doc.EditFamily(selectedSymbol.Family);//как проверить, что выбранный типоразмер - на основе плоскости???

                            using (Transaction tr = new Transaction(doc))
                            {
                                tr.Start("PlaceGenericByCoord");

                                if (!familySymbol.IsActive)
                                {
                                    familySymbol.Activate();
                                    doc.Regenerate();
                                }


                                foreach (List<XYZ> ptList in lines3d)
                                {
                                    for (int i = 0; i < ptList.Count - 1; i++)
                                    {
                                        XYZ bubbleEnd = ptList[i];
                                        XYZ thirdPnt = ptList[i + 1];
                                        XYZ freeEnd = bubbleEnd + (thirdPnt - bubbleEnd).CrossProduct(XYZ.BasisZ).Normalize();

                                        ReferencePlane refPlane = doc.Create.NewReferencePlane2(bubbleEnd, freeEnd, thirdPnt, doc.ActiveView);
                                        Reference reference = refPlane.GetReference();
                                        FamilyInstance fi = doc.Create.NewFamilyInstance(reference, bubbleEnd, thirdPnt/*freeEnd*/, familySymbol);
                                        //НЕ РАБОТАЕТ НИКАК!!!!!!!!!! ОБЪЕКТЫ ВСЕГДА ПОВЕРНУТЫ КРИВО!!!

                                        //Line axis = Line.CreateBound(bubbleEnd,
                                        //    bubbleEnd + (freeEnd - bubbleEnd).CrossProduct(thirdPnt - bubbleEnd).Normalize());
                                        //double angle = (thirdPnt - bubbleEnd).AngleOnPlaneTo(XYZ.BasisY, XYZ.BasisZ);


                                        //ElementTransformUtils.RotateElement(doc, fi.Id, axis, angle /*- 25.78 * Math.PI / 180*/);
                                    }
                                }


                                tr.Commit();
                            }



                        }
                    }



                }




            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при расстановке объектов по координатам в Revit");
            }



            return Result.Succeeded;
        }
    }
}
