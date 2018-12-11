using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;
using Common;
using Common.Controls.NumericUpDownControl;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Civil3DInfoTools.Geology.GeologyTrueScalingProfileWindow
{
    public partial class GeologyTrueScalingProfileViewModel : INotifyPropertyChanged
    {
        private Document doc;
        private PaletteSet ps;

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

        private ObjectId groundSurfPolyId = ObjectId.Null;
        public ObjectId GroundSurfPolyId
        {
            get { return groundSurfPolyId; }
            set
            {
                groundSurfPolyId = value;
                OnPropertyChanged("AcceptBtnIsEnabled");
            }
        }

        public bool AcceptBtnIsEnabled
        {
            get
            {
                return soilHatchIds != null && groundSurfPolyId != ObjectId.Null && soilHatchIds.Length > 0;
            }
        }

        private NumericUpDownViewModel startHorScalingVM = null;
        public NumericUpDownViewModel StartHorScalingVM
        {
            get { return startHorScalingVM; }
            set
            {
                startHorScalingVM = value;
                OnPropertyChanged("StartHorScalingVM");
            }
        }

        public double StartHorScaling
        {
            get { return startHorScalingVM.NumValue; }
        }

        private NumericUpDownViewModel startVertScalingVM = null;
        public NumericUpDownViewModel StartVertScalingVM
        {
            get { return startVertScalingVM; }
            set
            {
                startVertScalingVM = value;
                OnPropertyChanged("StartVertScalingVM");
            }
        }

        public double StartVertScaling
        {
            get { return startVertScalingVM.NumValue; }
        }

        private NumericUpDownViewModel startVertSoilScalingVM = null;
        public NumericUpDownViewModel StartVertSoilScalingVM
        {
            get { return startVertSoilScalingVM; }
            set
            {
                startVertSoilScalingVM = value;
                OnPropertyChanged("StartVertSoilScalingVM");
            }
        }

        public double StartVertSoilScaling
        {
            get { return startVertSoilScalingVM.NumValue; }
        }

        private NumericUpDownViewModel endHorScalingVM = null;
        public NumericUpDownViewModel EndHorScalingVM
        {
            get { return endHorScalingVM; }
            set
            {
                endHorScalingVM = value;
                OnPropertyChanged("EndHorScalingVM");
            }
        }

        public double EndHorScaling
        {
            get { return endHorScalingVM.NumValue; }
        }

        private NumericUpDownViewModel endVertScalingVM = null;
        public NumericUpDownViewModel EndVertScalingVM
        {
            get { return endVertScalingVM; }
            set
            {
                endVertScalingVM = value;
                OnPropertyChanged("EndVertScalingVM");
            }
        }

        public double EndVertScaling
        {
            get { return endVertScalingVM.NumValue; }
        }




        private readonly RelayCommand specifyGroundSurfPolyCommand = null;
        public RelayCommand SpecifyGroundSurfPolyCommand
        { get { return specifyGroundSurfPolyCommand; } }

        private readonly RelayCommand specifySoilHatchCommand = null;
        public RelayCommand SpecifySoilHatchCommand
        { get { return specifySoilHatchCommand; } }

        private readonly RelayCommand createProfileCommand = null;
        public RelayCommand CreateProfileCommand
        { get { return createProfileCommand; } }

        public GeologyTrueScalingProfileViewModel(Document doc, PaletteSet ps)
        {
            this.doc = doc;
            this.ps = ps;

            specifyGroundSurfPolyCommand
                = new RelayCommand(new Action<object>(SpecifyGroundSurfPoly));
            specifySoilHatchCommand
                = new RelayCommand(new Action<object>(SpecifySoilHatch));
            createProfileCommand
                = new RelayCommand(new Action<object>(CreateProfile));

            startHorScalingVM = new NumericUpDownViewModel(2, 0.1, 0);
            startVertScalingVM = new NumericUpDownViewModel(0.2, 0.1, 0);
            startVertSoilScalingVM = new NumericUpDownViewModel(0.1, 0.1, 0);

            endHorScalingVM = new NumericUpDownViewModel(1, 0.1, 0);
            endVertScalingVM = new NumericUpDownViewModel(1, 0.1, 0);
        }

        /// <summary>
        /// Указание полилинии поверхности земли
        /// </summary>
        /// <param name="arg"></param>
        private void SpecifyGroundSurfPoly(object arg)
        {
            if (doc != null)
            {
                //прервать выполнение любых команд (отправляем на выполнение эскейп-символ)
                //doc.SendStringToExecute(new string(new char[] { '\x03' }), true, false, false);

                HighlightObjs(false, GroundSurfPolyId);

                Editor ed = doc.Editor;
                Database db = doc.Database;

                PromptEntityOptions peo
                    = new PromptEntityOptions("\nУкажите полилинию поверхности земли на профиле:");
                peo.SetRejectMessage("\nМожно выбрать только полилинию");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per1 = ed.GetEntity(peo);
                if (per1.Status == PromptStatus.OK)
                {
                    GroundSurfPolyId = per1.ObjectId;
                }

                HighlightObjs(true, GroundSurfPolyId);
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


        //////////////////////////////////////////////////////////////////////////////
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
