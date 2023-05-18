using Microsoft.Extensions.Options;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Xml.Linq;
using SAP2000v1;
using Xbim.Ifc;
using Microsoft.Win32;

namespace SaptoRevitProject
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }

        #endregion

        #region Constructor
        public MainWindowViewModel()
        {
            Load = new Command(CreateSap);
            IFC = new Command(IFC_Importer);
        }
        #endregion

        #region Properties & Fields
        
        public Command IFC { get; set; }
        public Command Load { get; set; }
        
        #endregion

        #region Methods
        void CreateSap(object parameter)
        {
            string sapPath = "C:\\Program Files\\Computers and Structures\\SAP2000 22\\SAP2000.exe";
            string FilePath = "C:\\Users\\Ahmed\\Documents\\+01";
            cOAPI sapObject;


            double columnLength = 0.25;
            double columnWidth = 0.25;
        LineToJump1:
            try

            {
                //get the active SapObject

                sapObject = (cOAPI)System.Runtime.InteropServices.Marshal.GetActiveObject("CSI.SAP2000.API.SapObject");
            }

            catch (Exception ex)
            {
                cHelper helper = new Helper();
                sapObject = helper.CreateObject(sapPath);
                sapObject.ApplicationStart(eUnits.kN_m_C);
            }
            cSapModel sapModel = sapObject.SapModel;
            sapModel.InitializeNewModel(eUnits.kN_m_C);
            sapModel.File.NewBlank();

            #region Matrtials

            // fc = 30 Mpa - C30 - 25 KN/m3 - 4400*30^0.5 - 0.2

            //here we are just defining variables and then we will assign them using sap metrhods
            string concMat = "C30";
            string Rebarmat = "REBAR 360";


            double elasticModulus = 4400 * Math.Sqrt(30) * 1000;

            // to set concrete compressive strength
            int ret = sapModel.PropMaterial.SetMaterial(concMat, eMatType.Concrete);

            //to set possions ratio and elastic modulas which equals to 4400(fcu)^0.5
            ret = sapModel.PropMaterial.SetMPIsotropic(concMat, elasticModulus, 0.2, 9e-6);
            //to set unit weight of concrete
            ret = sapModel.PropMaterial.SetWeightAndMass(concMat, 1, 25);
            //
            ret = sapModel.PropMaterial.SetOConcrete_1(concMat, 30 * 1000, false, 0, 2, 2, 0.002, 0.003, -1);

            ret = sapModel.PropMaterial.SetMaterial(Rebarmat, eMatType.Rebar);
            #endregion

            #region Sections

            string ColumnSection = $"C{columnWidth * 1000}X{columnLength * 1000}";
            double[] colModifiers = new double[] { 1, 1, 1, 1, 0.7, 0.7, 1, 1 };



            ret = sapModel.PropFrame.SetRectangle(ColumnSection, concMat, columnWidth, columnLength);
            ret = sapModel.PropFrame.SetModifiers(ColumnSection, ref colModifiers);
            ret = sapModel.PropFrame.SetRebarColumn(ColumnSection, Rebarmat, Rebarmat, 1, 1, 0.025, 0, 4, 4, "25M", "10M", 0.3, 5, 5, false);
            //to be checked


            string BeamSection = "B250X600";
            double[] beamModifiers = new double[] { 1, 1, 1, 1, 0.5, 0.5, 1, 1 };
            ret = sapModel.PropFrame.SetRectangle(BeamSection, concMat, 0.6, 0.25);
            ret = sapModel.PropFrame.SetModifiers(BeamSection, ref beamModifiers);
            ret = sapModel.PropFrame.SetRebarBeam(BeamSection, Rebarmat, Rebarmat, 0.025, 0.025, .0000125, .0000125, .0000125, .0000125);
            #endregion

            #region TypesofLoads
            string dl = "DL";
            string ll = "LL";
            ret = sapModel.LoadPatterns.Add(dl, eLoadPatternType.Dead, 1);
            ret = sapModel.LoadPatterns.Add(ll, eLoadPatternType.Live, 0);

            string ult = "1.4DL+1.6LL";
            eCNameType caseType = eCNameType.LoadCase;
            ret = sapModel.RespCombo.Add(ult, 0);
            ret = sapModel.RespCombo.SetCaseList(ult, ref caseType, dl, 1.4);
            ret = sapModel.RespCombo.SetCaseList(ult, ref caseType, ll, 1.6);
            #endregion

            #region Drawing objects
            string p1, p2, p3, p4, p5, p6;
            p1 = p2 = p3 = p4 = p5 = p6 = null;
            ret = sapModel.PointObj.AddCartesian(0, 0, 0, ref p1);
            ret = sapModel.PointObj.AddCartesian(0, 0, 5, ref p2);
            ret = sapModel.PointObj.AddCartesian(0, 0, 8.6, ref p3);
            ret = sapModel.PointObj.AddCartesian(10, 0, 0, ref p4);
            ret = sapModel.PointObj.AddCartesian(10, 0, 5, ref p5);
            ret = sapModel.PointObj.AddCartesian(10, 0, 8.6, ref p6);

            string col1, col2, col3, col4, beam1, beam2;
            col1 = col2 = col3 = col4 = beam1 = beam2 = null;
            ret = sapModel.FrameObj.AddByPoint(p1, p2, ref col1, ColumnSection);
            ret = sapModel.FrameObj.AddByPoint(p2, p3, ref col2, ColumnSection);

            ret = sapModel.FrameObj.AddByPoint(p4, p5, ref col3, ColumnSection);
            ret = sapModel.FrameObj.AddByPoint(p5, p6, ref col4, ColumnSection);

            ret = sapModel.FrameObj.AddByPoint(p2, p5, ref beam1, BeamSection);
            ret = sapModel.FrameObj.AddByPoint(p3, p6, ref beam2, BeamSection);
            #endregion

            #region Assign restraints
            bool[] hingedSupport = new bool[6] { true, true, true, false, false, false };
            bool[] fixedSupport = new bool[6] { true, true, true, true, true, true };
            ret = sapModel.PointObj.SetRestraint(p1, ref hingedSupport);
            ret = sapModel.PointObj.SetRestraint(p4, ref hingedSupport);
            #endregion

            #region Assign Loads

            //SetLoadDistributed in documentation
            //10 = Gravity direction (only applies when CSys is Global)
            // FrameObj.SetLoadDistributed(name of the element , load pattern , if force -->1 if moment ---->0,
            //for gravity direction---->10 ,starting relative distance --->(from the start)--->0,
            //Ending relative distance --->(from the End)----->1 ,value of distributed load at the start --->30
            //,value of distributed load at the End --->30)if u wanted to make a triangle load for example
            ret = sapModel.FrameObj.SetLoadDistributed(beam1, ll, 1, 10, 0, 1, 30, 30);
            ret = sapModel.FrameObj.SetLoadDistributed(beam2, ll, 1, 10, 0, 1, 20, 20);

            // to apply point forces if needed
            //double[] pointForces = new double[] { 40, 0, 0, 0, 0, 0 };
            //ret = sapModel.PointObj.SetLoadForce(p2, ll, ref pointForces, true, "Local");
            #endregion

            #region Frame releases;

            ////in our project we will not need to set any releases
            //string[] newFramesNames = null;
            //bool[] startReleases = new bool[] { false, false, false, false, false, false };
            //bool[] endReleases = new bool[] { false, false, false, false, false, true };
            //double[] releaseVals = new double[] { 0, 0, 0, 0, 0, 0 };
            //sapModel.EditFrame.DivideAtDistance(beam1, 4, true, ref newFramesNames);
            //sapModel.FrameObj.SetReleases(newFramesNames[0], ref startReleases, ref endReleases, ref releaseVals, ref releaseVals);
            #endregion


            // Before doign run analyisis the model must be saved first
            ret = sapModel.File.Save(FilePath);



            #region Analyze

            //These lines are equivlant for setting analysis options on sap
            //setting degrees of freedom ux uy uz rx ry rz
            // we want ux uz  ry
            //thus we created this boolien array wit hsuch valus
            bool[] DegreesofFreedom = new bool[] { true, false, true, false, true, false };
            ret = sapModel.Analyze.SetActiveDOF(ref DegreesofFreedom);
            ret = sapModel.Analyze.RunAnalysis();
            #endregion

            #region Results
            sapModel.Results.Setup.DeselectAllCasesAndCombosForOutput();
            sapModel.Results.Setup.SetComboSelectedForOutput(ult);
            int resultNo = 0;
            string[] objs, elems, loadCases, stepType;
            double[] objStaions, elemsStations, stepNo, p, v2, v3, t, m2, m3;
            objs = elems = loadCases = stepType = null;
            objStaions = elemsStations = stepNo = p = v2 = v3 = t = m2 = m3 = null;
            sapModel.Results.FrameForce(col1, eItemTypeElm.ObjectElm, ref resultNo, ref objs, ref objStaions, ref elems, ref elemsStations,
              ref loadCases, ref stepType, ref stepNo, ref p, ref v2, ref v3, ref t, ref m2, ref m3);

            #endregion

            #region Design

            int itemsNo = 0;
            int[] designOpt = new int[] { 2 };
            string[] frameNames, pmm, vMajorCombo, vMinorCombo, error, warning, TopCombo, BotCombo, TLCombo, TTCombo;
            double[] locations, pmmArea, pmmRatio, avMajor, avMinor, TopArea, BotArea, TLArea, TTArea;
            frameNames = pmm = vMajorCombo = vMinorCombo = error = warning = TopCombo = BotCombo = TLCombo = TTCombo = null;
            locations = pmmArea = pmmRatio = avMajor = avMinor = TopArea = BotArea = TLArea = TTArea = null;

            string[] columns = new string[] { col1, col2, col3, col4 };

            for (int i = 0; i < columns.Length; i++)
            {
                sapModel.DesignConcrete.SetCode("ACI 318-14");
                sapModel.DesignConcrete.SetComboAutoGenerate(true);
                sapModel.DesignConcrete.StartDesign();
                ret = sapModel.DesignConcrete.GetSummaryResultsColumn(columns[i], ref itemsNo, ref frameNames, ref designOpt, ref locations,
            ref pmm, ref pmmArea, ref pmmRatio, ref vMajorCombo, ref avMajor,
            ref vMinorCombo, ref avMinor, ref error, ref warning);

                for (int J = 0; J < pmmRatio.Length; J++)
                {
                    if (pmmRatio[J] > 0.9)
                    {
                        ret = sapModel.SetModelIsLocked(false);
                        columnWidth += 0.05;
                        goto LineToJump1;
                    }
                }
            }
            #endregion
        }

        void IFC_Importer(object parameter) 
        {                                
            using (var model = IfcStore.Open(GetPath()))
            {
                model.SaveAs(SavePath());
            }
        }
        public string SavePath()
        {
            // create a SaveFileDialog object
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "IFC files (*.ifc)|*.ifc|All files (*.*)|*.*";

            // show the SaveFileDialog and get the user's response
            DialogResult result = saveFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {                               
                string destinationFilePath = saveFileDialog.FileName; // the selected file path
                return destinationFilePath;
            }
            else
            {
                return null;
            }

        }
        public string GetPath()
        {
            System.Windows.Forms.OpenFileDialog openDialog = new System.Windows.Forms.OpenFileDialog();
            openDialog.Title = "Select an IFC File";
            openDialog.Filter = "IFC files (*.ifc)|*.ifc|All files (*.*)|*.*";

            openDialog.RestoreDirectory = true;
            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = openDialog.FileName;
                return fileName;
            }
            else
                return null;
        }
        #endregion

    }
}
    