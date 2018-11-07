using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api;
using System.Diagnostics;

namespace NavisWorksInfoTools
{
    //[Plugin("TransformItems",
    //    DEVELOPER_ID,
    //    ToolTip = "TransformItems",
    //    DisplayName = "TransformItems")]
    public class TransformItemsOneByOne /*: AddInPlugin*/
    {

        public static int Execute(/*params string[] parameters*/)
        {
            ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

            Document doc = Application.ActiveDocument;

            ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;

            if (currSelectionColl.Count > 0)
            {
                foreach (ModelItem item in currSelectionColl.DescendantsAndSelf)
                {
                    if (item.HasGeometry)
                    {
                        ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);

                        Transform3D tr = item.Transform;//TODO: учитывать уже добавленную трансформацию!!!
                        //tr.Factor()
                        Transform3DComponents transform3DComponents = tr.Factor();
                        //transform3DComponents.ScaleOrientation
                        Point3D center = item.Geometry.BoundingBox.Center;

                        double z = center.Z;
                        double zTransformed = z * 1.1;
                        double correctionTrans = (z - zTransformed) /*/ 2*/;

                        ComApi.InwOpSelection comSelectionOut =
                            ComApiBridge.ComApiBridge.ToInwOpSelection(new ModelItemCollection() { item });

                        ComApi.InwLTransform3f3 oTrans1
                            = (ComApi.InwLTransform3f3)oState
                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwLTransform3f, null, null);
                        //растяжение по Z
                        ComApi.InwLVec3f scale
                            = (ComApi.InwLVec3f)oState
                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwLVec3f, null, null);
                        scale.SetValue(1, 1, 1.1);
                        //смещение по Z
                        ComApi.InwLVec3f trans
                            = (ComApi.InwLVec3f)oState
                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwLVec3f, null, null);
                        trans.SetValue(0, 0, correctionTrans);

                        //ComApi.InwLRotation3f scaleOrientation
                        //    = (ComApi.InwLRotation3f)oState
                        //    .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwLRotation3f, null, null);
                        //ComApi.InwLUnitVec3f axis
                        //    = (ComApi.InwLUnitVec3f)oState
                        //    .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwLUnitVec3f, null, null);
                        //axis.SetValue(0, 0, 1);
                        //scaleOrientation.SetValue(axis, 0);

                        //ComApi.InwLRotation3f Rotation
                        //    = (ComApi.InwLRotation3f)oState
                        //    .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwLRotation3f, null, null);
                        //Rotation.SetValue(axis, 0);

                        //oTrans1.factor(scale, scaleOrientation, Rotation, trans);
                        //double[] matrix = ConvertDoubleArray((Array)((object)oTrans1.Matrix));

                        //oTrans1.MakeScale(scale);
                        //double[] matrix1 = ConvertDoubleArray((Array)((object)oTrans1.Matrix));
                        //oTrans1.MakeTranslation(trans);
                        //double[] matrix2 = ConvertDoubleArray((Array)((object)oTrans1.Matrix));

                        oTrans1.SetMatrix(new double[]
                        {
                            1, 0, 0, 0, 0, 1, 0, 0, 0 ,0, 1.1, 0, 0, 0, correctionTrans, 1
                        });


                        //oTrans1.MakeScale(scaleVec);
                        oState.OverrideTransform(comSelectionOut, oTrans1);




                    }
                }
            }

            return 0;
        }


        private static double[] ConvertDoubleArray(Array arr)
        {
            if (arr.Rank != 1)
                throw new ArgumentException();

            var retval = new double[arr.GetLength(0)];
            for (int ix = arr.GetLowerBound(0); ix <= arr.GetUpperBound(0); ++ix)
                retval[ix - arr.GetLowerBound(0)] = (double)arr.GetValue(ix);
            return retval;
        }
    }
}
