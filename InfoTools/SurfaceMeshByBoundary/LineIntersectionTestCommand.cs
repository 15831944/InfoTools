using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(Civil3DInfoTools.SurfaceMeshByBoundary.LineIntersectionTestCommand))]

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    public class LineIntersectionTestCommand
    {
        [CommandMethod("LineIntersectionTest", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void LineIntersectionTest()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            PromptEntityOptions peo1 = new PromptEntityOptions("\nУкажите линию 1");
            peo1.SetRejectMessage("\nМожно выбрать только линию");
            peo1.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per1 = ed.GetEntity(peo1);
            if (per1.Status == PromptStatus.OK)
            {
                PromptEntityOptions peo2 = new PromptEntityOptions("\nУкажите линию 2");
                peo2.SetRejectMessage("\nМожно выбрать только линию");
                peo2.AddAllowedClass(typeof(Line), true);
                PromptEntityResult per2 = ed.GetEntity(peo2);
                if (per2.Status == PromptStatus.OK)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Line line1 = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as Line;
                        Line line2 = tr.GetObject(per2.ObjectId, OpenMode.ForRead) as Line;

                        Point2d p1 = new Point2d(line1.StartPoint.X, line1.StartPoint.Y);
                        Point2d p2 = new Point2d(line1.EndPoint.X, line1.EndPoint.Y);
                        Point2d p3 = new Point2d(line2.StartPoint.X, line2.StartPoint.Y);
                        Point2d p4 = new Point2d(line2.EndPoint.X, line2.EndPoint.Y);
                        
                        bool intersecting = Utils.LineSegmentsAreIntersecting(p1, p2, p3, p4/*, out overlaying*/);

                        //ed.WriteMessage("\noverlaying = " + overlaying);
                        ed.WriteMessage("\nintersecting = " + intersecting);


                        Point2d? intersectionPt = Utils.GetLinesIntersection(p1, p2, p3, p4);
                        if (intersectionPt==null)
                        {
                            ed.WriteMessage("\nintersectionPt = null");
                        }
                        else
                        {
                            ed.WriteMessage("\nintersectionPt = "+ intersectionPt.Value.ToString());
                        }

                        ed.WriteMessage("\n\nCramer:");
                        bool overlaying = false;
                        intersectionPt = Utils.GetLinesIntersectionCramer(p1, p2, p3, p4, out overlaying);
                        if (intersectionPt == null)
                        {
                            ed.WriteMessage("\nintersectionPt = null");
                        }
                        else
                        {
                            ed.WriteMessage("\nintersectionPt = " + intersectionPt.Value.ToString());
                        }
                        ed.WriteMessage("\noverlaying = " + overlaying);


                        ed.WriteMessage("\n\nNativeIntersection:");

                        intersectionPt = Utils.GetLinesIntersectionAcad(p1, p2, p3, p4);
                        if (intersectionPt == null)
                        {
                            ed.WriteMessage("\nintersectionPt = null");
                        }
                        else
                        {
                            ed.WriteMessage("\nintersectionPt = " + intersectionPt.Value.ToString());
                        }
                        ed.WriteMessage("\noverlaying = " + overlaying);





                        tr.Commit();
                    }
                }
            }
        }
    }
}
