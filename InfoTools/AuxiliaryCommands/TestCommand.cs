using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Win = System.Windows;

[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.TestCommand))]

namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class TestCommand
    {

        [CommandMethod("S1NF0_Test", CommandFlags.Modal)]
        public void Test()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            CivilDocument cdok = CivilDocument.GetCivilDocument(adoc.Database);


            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //посмотреть свойства structure: Rotation, BoundingShape, InnerDiameterOrWidth, InnerLength, DiameterOrWidth, Length 
            PromptEntityOptions peo = new PromptEntityOptions("\nУкажите колодец");
            peo.SetRejectMessage("\nМожно выбрать только колодец");
            peo.AddAllowedClass(typeof(Structure), true);
            PromptEntityResult per1 = ed.GetEntity(peo);
            if (per1.Status == PromptStatus.OK)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {

                    Structure str = (Structure)tr.GetObject(per1.ObjectId, OpenMode.ForRead);

                    BoundingShapeType bst = str.BoundingShape;
                    double rotation = str.Rotation;
                    double idow = str.InnerDiameterOrWidth;
                    double dow = str.DiameterOrWidth;
                    double il = double.NegativeInfinity;
                    double l = double.NegativeInfinity;
                    try
                    {
                        il = /*bst == BoundingShapeType.Box ?*/ str.InnerLength /*: double.NegativeInfinity*/;
                    }
                    catch { }
                    try
                    {
                        l = /*bst == BoundingShapeType.Box ?*/ str.Length /*: double.NegativeInfinity*/;
                    }
                    catch { }

                    string message = "Rotation = " + rotation + "\nInnerDiameterOrWidth = " + idow +
                        "\nDiameterOrWidth = " + dow + "\nInnerLength = " + il + "\nLength = " + l;

                    Win.MessageBox.Show(message);
                    tr.Commit();
                }
            }

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //Тестовое окно
            //TestWindow testWindow = new TestWindow(adoc, cdok);
            //Application.ShowModalWindow(testWindow);

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //обводка прямоугольника текста
            //PromptEntityOptions peo = new PromptEntityOptions("\nУкажите TEXT,MTEXT");
            //peo.SetRejectMessage("\nМожно выбрать только TEXT или MTEXT");
            //peo.AddAllowedClass(typeof(MText), true);
            //peo.AddAllowedClass(typeof(DBText), true);
            //PromptEntityResult per1 = ed.GetEntity(peo);
            //if (per1.Status == PromptStatus.OK)
            //{
            //    using (Transaction tr = db.TransactionManager.StartTransaction())
            //    {
            //        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
            //        BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);


            //        Entity ent = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as Entity;
            //        Extents3d? ext = ent.Bounds;
            //        if (ext != null)
            //        {
            //            using (Polyline poly = new Polyline())
            //            {
            //                poly.ColorIndex = 1;
            //                poly.AddVertexAt(0, new Point2d(ext.Value.MinPoint.X, ext.Value.MinPoint.Y), 0, 0, 0);
            //                poly.AddVertexAt(1, new Point2d(ext.Value.MinPoint.X, ext.Value.MaxPoint.Y), 0, 0, 0);
            //                poly.AddVertexAt(2, new Point2d(ext.Value.MaxPoint.X, ext.Value.MaxPoint.Y), 0, 0, 0);
            //                poly.AddVertexAt(3, new Point2d(ext.Value.MaxPoint.X, ext.Value.MinPoint.Y), 0, 0, 0);
            //                poly.Closed = true;

            //                ms.AppendEntity(poly);
            //                tr.AddNewlyCreatedDBObject(poly, true);
            //            }



            //        }

            //        tr.Commit();
            //    }
            //}
        }
    }
}
