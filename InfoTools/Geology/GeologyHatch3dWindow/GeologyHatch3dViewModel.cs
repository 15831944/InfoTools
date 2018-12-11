using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Graphics = Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Common;
using Common.Controls.NumericUpDownControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.Geology.GeologyHatch3dWindow
{
    public partial class GeologyHatch3dViewModel : INotifyPropertyChanged
    {
        private DBText elevTextTransient = null;
        private Polyline elevBasePtTransient = null;

        private Document doc;
        private PaletteSet ps;
        private ObjectId standardTxtStyle;

        private ObjectId[] soilHatchIds = null;
        public ObjectId[] SoilHatchIds
        {
            get { return soilHatchIds; }
            set
            {
                soilHatchIds = value;
                OnPropertyChanged("AcceptBtnIsEnabled");
            }
        }

        private ObjectId alignmentPolyId = ObjectId.Null;
        public ObjectId AlignmentPolyId
        {
            get { return alignmentPolyId; }
            set
            {
                alignmentPolyId = value;
                OnPropertyChanged("AcceptBtnIsEnabled");
            }
        }

        public bool AcceptBtnIsEnabled
        {
            get
            {
                return soilHatchIds != null && alignmentPolyId != ObjectId.Null && soilHatchIds.Length > 0 && elevBasePoint != null;
            }
        }



        private NumericUpDownViewModel horScalingVM = null;
        public NumericUpDownViewModel HorScalingVM
        {
            get { return horScalingVM; }
            set
            {
                horScalingVM = value;
                OnPropertyChanged("StartHorScalingVM");
            }
        }

        public double HorScaling
        {
            get { return horScalingVM.NumValue; }
            set
            {
                horScalingVM.ValueString = value.ToString();
            }
        }

        private NumericUpDownViewModel vertScalingVM = null;
        public NumericUpDownViewModel VertScalingVM
        {
            get { return vertScalingVM; }
            set
            {
                vertScalingVM = value;
                OnPropertyChanged("StartVertScalingVM");
            }
        }

        public double VertScaling
        {
            get { return vertScalingVM.NumValue; }
            set
            {
                vertScalingVM.ValueString = value.ToString();
            }
        }


        private Point2d? elevBasePoint = null;
        public Point2d? ElevBasePoint
        {
            get { return elevBasePoint; }
            set
            {
                elevBasePoint = value;
                OnPropertyChanged("ElevationInputIsEnabled");
                OnPropertyChanged("AcceptBtnIsEnabled");
            }
        }

        public bool ElevationInputIsEnabled
        {
            get { return elevBasePoint != null; }
        }

        private NumericUpDownViewModel baseElevationVM = null;
        public NumericUpDownViewModel BaseElevationVM
        {
            get { return baseElevationVM; }
            set
            {
                baseElevationVM = value;
                OnPropertyChanged("BaseElevationVM");
            }
        }

        public double ElevationInput
        {
            get { return baseElevationVM.NumValue; }
            set
            {
                baseElevationVM.ValueString = value.ToString();
            }
        }


        private readonly RelayCommand specifyAlignmentPolyCommand = null;
        public RelayCommand SpecifyAlignmentPolyCommand
        { get { return specifyAlignmentPolyCommand; } }

        private readonly RelayCommand specifySoilHatchCommand = null;
        public RelayCommand SpecifySoilHatchCommand
        { get { return specifySoilHatchCommand; } }

        private readonly RelayCommand specifyElevBasePointCommand = null;
        public RelayCommand SpecifyElevBasePointCommand
        { get { return specifyElevBasePointCommand; } }

        private readonly RelayCommand create3dProfileCommand = null;
        public RelayCommand Create3dProfileCommand
        { get { return create3dProfileCommand; } }

        public GeologyHatch3dViewModel(Document doc, PaletteSet ps)
        {
            this.doc = doc;
            this.ps = ps;

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                standardTxtStyle = Utils.GetStandardTextStyle(doc.Database, tr);
                tr.Commit();
            }

            //временная графика - http://adn-cis.org/forum/index.php?topic=4279.msg15946#msg15946, http://adn-cis.org/forum/index.php?topic=8909.0
            ps.StateChanged += PaletteSet_StateChanged;



            specifyAlignmentPolyCommand
                = new RelayCommand(new Action<object>(SpecifyAlignmentPoly));
            specifySoilHatchCommand
                = new RelayCommand(new Action<object>(SpecifySoilHatch));
            specifyElevBasePointCommand
                = new RelayCommand(new Action<object>(SpecifyElevBasePoint));
            create3dProfileCommand
                = new RelayCommand(new Action<object>(Create3dProfile));


            horScalingVM = new NumericUpDownViewModel(1, 0.1, 0);
            vertScalingVM = new NumericUpDownViewModel(1, 0.1, 0);
            baseElevationVM = new NumericUpDownViewModel(0, 0.1, formatting: "f3");
            baseElevationVM.ValueChanged += BaseElevationChanged;
        }

        private void BaseElevationChanged(object sender, EventArgs args)
        {
            //менять текст, отображающий отметку в модели
            if (elevTextTransient != null)
            {
                elevTextTransient.TextString = ElevationInput.ToString("f3");
                Graphics.TransientManager tm = Graphics.TransientManager.CurrentTransientManager;
                tm.UpdateTransient(elevTextTransient, new IntegerCollection());
            }
        }

        private void PaletteSet_StateChanged(object sender, PaletteSetStateEventArgs e)
        {
            if (e.NewState == StateEventIndex.Hide)
            {
                //удалить временную графику
                EraseTransient();
            }
            else if (e.NewState == StateEventIndex.Show)
            {
                //добавить временную графику
                AddTransient();
            }
        }

        private void AddTransient()
        {
            if (elevBasePoint != null)
            {
                Point3d pos = new Point3d(elevBasePoint.Value.X, elevBasePoint.Value.Y, 0);
                elevTextTransient = new DBText();
                elevTextTransient.Position = pos + Vector3d.YAxis * 0.5;
                elevTextTransient.ColorIndex = 1;
                elevTextTransient.TextStyleId = standardTxtStyle;
                elevTextTransient.TextString = ElevationInput.ToString("f3");
                elevTextTransient.Height = 5;

                elevBasePtTransient = new Polyline();
                Point2d pos2d = Utils.Point2DBy3D(pos);
                elevBasePtTransient.AddVertexAt(0, pos2d, 0, 0, 0);
                elevBasePtTransient.AddVertexAt(1, pos2d+new Vector2d(0.5,0.5), 0, 0, 0);
                elevBasePtTransient.AddVertexAt(2, pos2d + new Vector2d(-0.5, 0.5), 0, 0, 0);
                elevBasePtTransient.Closed = true;
                elevBasePtTransient.LineWeight = LineWeight.LineWeight030;
                elevBasePtTransient.ColorIndex = 1;

                Graphics.TransientManager tm = Graphics.TransientManager.CurrentTransientManager;
                tm.AddTransient(elevTextTransient, Graphics.TransientDrawingMode.Highlight, 0, new IntegerCollection());
                tm.AddTransient(elevBasePtTransient, Graphics.TransientDrawingMode.Highlight, 0, new IntegerCollection());
            }

        }

        private void EraseTransient()
        {
            if (elevTextTransient != null && elevBasePtTransient != null)
            {
                Graphics.TransientManager tm = Graphics.TransientManager.CurrentTransientManager;
                tm.EraseTransient(elevTextTransient, new IntegerCollection());
                tm.EraseTransient(elevBasePtTransient, new IntegerCollection());
                elevTextTransient.Dispose();
                elevBasePtTransient.Dispose();
                elevTextTransient = null;
                elevBasePtTransient = null;
            }

        }


        public void HighlightObjs(bool yes, params ObjectId[] ids)
        {
            if (doc != null && ids != null)
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        try
                        {
                            if (!id.IsNull && !id.IsErased && id.IsValid)
                            {
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                if (yes) ent.Highlight();
                                else ent.Unhighlight();
                            }
                        }
                        catch { }
                    }
                    tr.Commit();
                }

            }
        }



        private void SpecifyAlignmentPoly(object arg)
        {
            if (doc != null)
            {
                //прервать выполнение любых команд (отправляем на выполнение эскейп-символ)
                //doc.SendStringToExecute(new string(new char[] { '\x03' }), true, false, false);

                HighlightObjs(false, AlignmentPolyId);

                Editor ed = doc.Editor;

                PromptEntityOptions peo
                    = new PromptEntityOptions("\nУкажите полилинию трассы для построения 3d профиля:");
                peo.SetRejectMessage("\nМожно выбрать только полилинию");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per1 = ed.GetEntity(peo);
                if (per1.Status == PromptStatus.OK)
                {
                    AlignmentPolyId = per1.ObjectId;
                }

                HighlightObjs(true, AlignmentPolyId);
            }
        }

        private void SpecifySoilHatch(object arg)
        {
            if (doc != null)
            {
                //прервать выполнение любых команд (отправляем на выполнение эскейп-символ)
                //doc.SendStringToExecute(new string(new char[] { '\x03' }), true, false, false);

                HighlightObjs(false, SoilHatchIds);

                Editor ed = doc.Editor;
                Database db = doc.Database;

                TypedValue[] tv = new TypedValue[]
                    {
                            new TypedValue(0, "HATCH")
                    };
                SelectionFilter flt = new SelectionFilter(tv);

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nВыберите штриховки грунтов";

                PromptSelectionResult acSSPrompt = doc.Editor.GetSelection(pso, flt);
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    SelectionSet acSSet = acSSPrompt.Value;
                    if (acSSet != null)
                    {
                        SoilHatchIds = acSSet.GetObjectIds();
                    }
                }

                HighlightObjs(true, SoilHatchIds);
            }
        }

        private void SpecifyElevBasePoint(object arg)
        {
            if (doc != null)
            {
                //прервать выполнение любых команд (отправляем на выполнение эскейп-символ)
                //doc.SendStringToExecute(new string(new char[] { '\x03' }), true, false, false);

                Editor ed = doc.Editor;

                PromptPointOptions ppo = new PromptPointOptions("\nУкажите базовую точку на профиле для задания базовой отметки:");
                ppo.AllowNone = false;
                PromptPointResult ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.OK)
                {
                    Point3d pt = ppr.Value;
                    ElevBasePoint = Utils.Point2DBy3D(pt);
                    EraseTransient();
                    AddTransient();
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
