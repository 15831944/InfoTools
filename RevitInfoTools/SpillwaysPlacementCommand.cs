using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Creation = Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Common.XMLClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WinForms = System.Windows.Forms;
using Autodesk.Revit.DB.Structure;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace RevitInfoTools
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SpillwaysPlacementCommand : IExternalCommand
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

                //выбрать несколько xml с расположением водосбросов
                string[] filenames = null;
                WinForms.OpenFileDialog openFileDialog1 = new WinForms.OpenFileDialog();


                string curDocPath = doc.PathName;
                if (!String.IsNullOrEmpty(curDocPath))
                    openFileDialog1.InitialDirectory = Path.GetDirectoryName(curDocPath);
                openFileDialog1.Filter = "xml files (*.xml)|*.xml";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.RestoreDirectory = true;
                openFileDialog1.Multiselect = true;
                openFileDialog1.Title = "Выберите таблицы CSV с координатами 3d-полилиний";

                if (openFileDialog1.ShowDialog() == WinForms.DialogResult.OK)
                {
                    Creation.Document crDoc = doc.Create;


                    filenames = openFileDialog1.FileNames;
                    using (TransactionGroup trGr = new TransactionGroup(doc))
                    {
                        trGr.Start("CreateSpillways");


                        foreach (string filename in filenames)
                        {
                            //распаристь xml
                            PositionData positionData = null;
                            using (StreamReader sr = new StreamReader(filename))
                            {
                                string serializedData = sr.ReadToEnd();
                                var xmlSerializer = new XmlSerializer(typeof(PositionData));
                                var stringReader = new StringReader(serializedData);
                                positionData = (PositionData)xmlSerializer.Deserialize(stringReader);
                            }
                            if (positionData != null)
                            {
                                foreach (SpillwayPosition sp in positionData.SpillwayPositions)
                                {
                                    //Загрузка семейства водосброса если еще нет
                                    Family spillwayFamily = null;
                                    if (sp.Slopes.Count == 2)
                                    {
                                        spillwayFamily = Utils.GetFamily(doc, "Водосброс2Укл");
                                    }
                                    else if (sp.Slopes.Count == 3)
                                    {
                                        spillwayFamily = Utils.GetFamily(doc, "Водосброс3Укл");
                                    }

                                    if (spillwayFamily != null)
                                    {
                                        ElementId symId = spillwayFamily.GetFamilySymbolIds().First();
                                        FamilySymbol familySymbol = (FamilySymbol)doc.GetElement(symId);


                                        using (Transaction tr = new Transaction(doc))
                                        {
                                            tr.Start("CreateSpillway");

                                            //активировать типоразмер
                                            if (!familySymbol.IsActive)
                                            {
                                                familySymbol.Activate();
                                                doc.Regenerate();
                                            }
                                            //Вставка водосбросов в заданные координаты
                                            //Поворот на заданный угол
                                            //Если водосброс направлен направо, то использовать отражение (с поворотом в противоположную сторону)
                                            XYZ location = projectTransform.OfPoint(Utils.PointByMeters(sp.X, sp.Y, sp.Z));
                                            FamilyInstance fi = crDoc.NewFamilyInstance(location, familySymbol, StructuralType.NonStructural);
                                            Line rotationAxis = Line.CreateBound(location, location + XYZ.BasisZ);
                                            double rotationAngle = sp.Z_Rotation / (180 / Math.PI);
                                            if (sp.ToTheRight)
                                            {
                                                rotationAngle = rotationAngle + Math.PI;
                                            }
                                            fi.Location.Rotate(rotationAxis, rotationAngle);
                                            if (sp.ToTheRight)
                                            {
                                                XYZ spillwayDir = Transform.CreateRotation(XYZ.BasisZ, rotationAngle).OfVector(XYZ.BasisY);
                                                Plane mirrorPlane = Plane.CreateByNormalAndOrigin(spillwayDir, location);
                                                ElementTransformUtils.MirrorElements(doc, new List<ElementId>() { fi.Id }, mirrorPlane, false);
                                                //doc.Delete(fi.Id);
                                            }


                                            //Назначение параметров уклонов водосброса
                                            for (int i = 0; i < sp.Slopes.Count; i++)
                                            {
                                                string lengthParamName = "Длина" + (i + 1);
                                                string slopeParamName = "Уклон" + (i + 1);

                                                Parameter lengthParam = fi.LookupParameter(lengthParamName);
                                                Parameter slopeParam = fi.LookupParameter(slopeParamName);
                                                if (lengthParam != null && slopeParam != null)
                                                {
                                                    Slope s = sp.Slopes[i];
                                                    lengthParam.Set(UnitUtils.Convert(s.Len, DisplayUnitType.DUT_METERS, DisplayUnitType.DUT_DECIMAL_FEET));
                                                    slopeParam.Set(s.S);
                                                }
                                            }

                                            tr.Commit();
                                        }


                                    }

                                }



                            }

                        }

                        trGr.Assimilate();
                    }



                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при расстановке водосбросов в Revit");
                return Result.Succeeded;
            }




            return Result.Succeeded;
        }
    }
}
