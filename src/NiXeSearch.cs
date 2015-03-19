//==============================================================================
//  Matthias Pfrang
//  Konstruktion / Design Engineering
//  WEISS SPINDELTECHNOLOGIE GMBH 
//  A SIEMENS COMPANY
//  Matthias.Pfrang@weissgmbh.de 
//  Fon: +49 9721 7701-172 
//
//==============================================================================
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

using System.Windows.Forms;
using System.Drawing;

using NXOpen;
using NXOpen.UF;
using NXOpen.Drawings;
using NXOpen.Annotations;

namespace NiXe
{
    class NiXeSearch
    {
        public static void Main(string[] args)
        {
            new Form_NiXeSearch().ShowDialog();
        }

        public static int GetUnloadOption(string dummy) { return (int)Session.LibraryUnloadOption.Immediately; }
    }

    public class TextSearch
    {
        //------------------------------------------------------------------------------
        #region Klassenfelder
        //------------------------------------------------------------------------------
        bool _markieren = true;
        bool _case_sensetive = true;
        bool _quite_text = false;

        bool _useText = true;
        bool _useLabel = true;
        bool _useID = true;

        List<Annotation> _matches = new List<Annotation>();

        static Session theSession;
        static UFSession theUFSession;
        static UI theUI;
        static Part workPart;
        //------------------------------------------------------------------------------
        #region GetSet Klassenfelder
        //------------------------------------------------------------------------------
        public bool Markieren
        {
            get { return _markieren; }
            set { _markieren = value; }
        }

        public bool CaseSensetive
        {
            get { return _case_sensetive; }
            set { _case_sensetive = value; }
        }

        public bool QuiteText
        {
            get { return _quite_text; }
            set { _quite_text = value; }
        }

        public bool UseText
        {
            get { return _useText; }
            set { _useText = value; }
        }

        public bool UseLabel
        {
            get { return _useLabel; }
            set { _useLabel = value; }
        }

        public bool UseID
        {
            get { return _useID; }
            set { _useID = value; }
        }

        public int MatchesCount { get { return _matches.Count; } }
        public List<Annotation> Matches { get { return _matches; } }
        public Annotation CurrentAnnotion { get; set; }

        #endregion
        #endregion

        //------------------------------------------------------------------------------
        #region Konstruktoren
        //------------------------------------------------------------------------------
        public TextSearch() {
            theSession = Session.GetSession();
            theUFSession = UFSession.GetUFSession();
            theUI = UI.GetUI();
            workPart = theSession.Parts.Work;
        }

        #endregion

        //------------------------------------------------------------------------------
        #region Statische Methoden
        //------------------------------------------------------------------------------

        #endregion

        //------------------------------------------------------------------------------
        #region Klassenmethoden
        //------------------------------------------------------------------------------
        /// <summary>
        /// Sucht nach dem Angegeben String
        /// </summary>
        /// <param name="s"></param>
        public void SearchString(string s)
        {
            _matches.Clear();
            CurrentAnnotion = null;
            UnhighlightMatches();

            if (!CaseSensetive) { s = s.ToLower(); }

            if (UseText)
            {
                foreach (Note note in workPart.Notes)
                {
                    string[] notetext = note.GetText();
                    for (int i = 0; i < notetext.Length; i++)
                    {
                        if (!CaseSensetive) { notetext[i] = notetext[i].ToLower(); }
                        if (QuiteText)
                        {
                            if (notetext[i] == s) _matches.Add(note);
                        }
                        else
                        {
                            if (notetext[i].Contains(s)) _matches.Add(note);
                        }
                    }
                }
            }

            if (UseLabel)
            {
                foreach (NXOpen.Annotations.Label label in workPart.Labels)
                {
                    string[] labeltext = label.GetText();

                    for (int i = 0; i < labeltext.Length; i++)
                    {
                        if (!CaseSensetive) { labeltext[i] = labeltext[i].ToLower(); }
                        if (QuiteText)
                        {
                            if (labeltext[i] == s) _matches.Add(label);
                        }
                        else
                        {
                            if (labeltext[i].Contains(s)) _matches.Add(label);
                        }
                    }
                }
            }

            if (UseID)
            {
                foreach (IdSymbol symbole in workPart.Annotations.IdSymbols)
                {
                    IdSymbolBuilder builder = workPart.Annotations.IdSymbols.CreateIdSymbolBuilder(symbole);
                    string idtext = builder.UpperText;

                    if (!CaseSensetive) { idtext = idtext.ToLower(); }
                    if (QuiteText)
                    {
                        if (idtext == s) _matches.Add(symbole);
                    }
                    else
                    {
                        if (idtext.Contains(s)) _matches.Add(symbole);
                    }

                    builder.Destroy();
                }
            }

            if (Markieren) HighlightMatches();
        }


        public void HighlightMatches()
        {
            Session.UndoMarkId mark = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "Highlight");
            _matches.ForEach(x => x.Highlight());
            theSession.Parts.Display.Views.Refresh();
            theSession.UpdateManager.DoUpdate(mark);
        }

        public void UnhighlightMatches()
        {
            Session.UndoMarkId mark = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "Unhighlight");

            _matches.ForEach(x => x.Unhighlight());
            PartCleanup cleanup = theSession.NewPartCleanup();
            cleanup.TurnOffHighlighting = true;
            cleanup.DoCleanup();
            cleanup.Dispose();

            theSession.UpdateManager.DoUpdate(mark);
        }

        public void GoTo(Annotation ann, bool change_drawing)
        {
            if (change_drawing)
            {
                NiXeDraftingObject obj = new NiXeDraftingObject(ann);
                obj.Sheet.Open();
            }
            //Heranzoomen und dann 40% den Zoom verringern
            workPart.Views.WorkView.FitToObjects(new IFitTo[] { ann });
            for (int i = 0; i < 4; i++)
            {
                workPart.Views.WorkView.Zoom(NXOpen.View.ScaleFactor.ReduceScale);
            }

            CurrentAnnotion = ann;
            if (Markieren) HighlightMatches();
        }

        private string ExportMatches()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var item in _matches)
            {
                if (item is Note)
                {
                    NiXeDraftingObject obj = new NiXeDraftingObject(item as NXObject);
                    Note note = item as Note;
                    builder.AppendLine("Name: " + note.Name);
                    builder.AppendLine("Sheet: " + obj.SheetName);

                    builder.AppendLine("Text: " + PrintArraytoString(note.GetText(), Environment.NewLine));
                    builder.AppendLine();
                    builder.AppendLine("".PadRight(20, '-'));
                }
                else if (item is NXOpen.Annotations.Label)
                {
                    NiXeDraftingObject obj = new NiXeDraftingObject(item);
                    NXOpen.Annotations.Label label = item as NXOpen.Annotations.Label;

                    builder.AppendLine("Name: " + label.Name);
                    builder.AppendLine("Sheet: " + obj.SheetName);

                    builder.AppendLine("Text: " + PrintArraytoString(label.GetText(), Environment.NewLine));
                    builder.AppendLine();
                    builder.AppendLine("".PadRight(20, '-'));
                }
                else if (item is IdSymbol)
                {
                    NiXeDraftingObject obj = new NiXeDraftingObject(item);
                    IdSymbol id = item as IdSymbol;
                    IdSymbolBuilder id_builder = workPart.Annotations.IdSymbols.CreateIdSymbolBuilder(id);

                    builder.AppendLine("Name: " + id.Name);
                    builder.AppendLine("Sheet: " + obj.SheetName);

                    builder.AppendLine("Text: " + id_builder.UpperText + " " + id_builder.LowerText);
                    builder.AppendLine();

                    id_builder.Destroy();
                    builder.AppendLine("".PadRight(20, '-'));
                }
            }
            return builder.ToString();
        }

        public static string PrintArraytoString<T>(T[] obj, string separator)
        {
            string return_value = string.Empty;
            if (obj == null) return return_value;

            for (int i = 0; i < obj.Length; i++)
            {
                return_value = return_value + obj[i].ToString() + separator;
            }
            return_value = return_value.Substring(0, return_value.Length - separator.Length);

            return return_value;
        }

        public void ExportMatchesToListingWindow()
        {
            theSession.ListingWindow.Open();
            theSession.ListingWindow.WriteLine(ExportMatches());
        }

        #endregion
    }

    public class NiXeDraftingObject
    {
        //------------------------------------------------------------------------------
        #region Klassenfelder
        //------------------------------------------------------------------------------
        //Base Point
        Point3d _basepoint = new Point3d(0, 0, 0);
        NXObject _obj;

        string _view;
        string _sheet;
        DraftingView _view_obj;
        DrawingSheet _sheet_obj;


        //------------------------------------------------------------------------------
        #region GetSet Klassenfelder
        //------------------------------------------------------------------------------
        /// <summary>
        /// Gibt den Ursprungspunkt an, oder setzt ihn.
        /// </summary>
        public Point3d BasePoint
        {
            get { return _basepoint; }
            set { _basepoint = value; }
        }
        /// <summary>
        /// Gibt das Objekt an oder setzt es.
        /// </summary>
        public NXObject Object
        {
            get { return _obj; }
            set { _obj = value; }
        }
        /// <summary>
        /// Gibt den Namen der View zurück
        /// </summary>
        public string ViewName
        {
            get
            {
                GetViewAndDrawingSheet();
                return _view;
            }
        }
        /// <summary>
        /// Gibt die View zurück
        /// </summary>
        public DraftingView View
        {
            get
            {
                GetViewAndDrawingSheet();
                return _view_obj;
            }
        }
        /// <summary>
        /// Gibt den Namen des Sheets zurück
        /// </summary>
        public string SheetName
        {
            get
            {
                GetViewAndDrawingSheet();
                return _sheet;
            }
        }
        /// <summary>
        /// Gibt das Sheet zurück
        /// </summary>
        public DrawingSheet Sheet
        {
            get
            {
                GetViewAndDrawingSheet();
                return _sheet_obj;
            }
        }

        #endregion
        #endregion

        //------------------------------------------------------------------------------
        #region Konstruktoren
        //------------------------------------------------------------------------------
        public NiXeDraftingObject() { }

        public NiXeDraftingObject(NXObject obj)
        {
            this.Object = obj;
        }

        public NiXeDraftingObject(Point3d par_basepoint)
        {
            this.BasePoint = par_basepoint;
        }

        #endregion

        //------------------------------------------------------------------------------
        #region Statische Methoden
        //------------------------------------------------------------------------------
        /// <summary>
        /// Gibt die Zeichnung zurück auf dem sich das DraftingObject befindet
        /// </summary>
        /// <returns>Namen der Zeichnung</returns>
        public static string GetDrawingSheet(NXObject obj)
        {
            NiXeDraftingObject drafting_object = new NiXeDraftingObject(obj);
            return drafting_object.SheetName;
        }
        /// <summary>
        /// Gibt die View zurück auf dem sich das DraftingObject befindet
        /// </summary>
        /// <returns>Namen der View</returns>
        public static string GetDrawingView(NXObject obj)
        {
            NiXeDraftingObject drafting_object = new NiXeDraftingObject(obj);
            return drafting_object.ViewName;
        }
        #endregion

        //------------------------------------------------------------------------------
        #region Klassenmethoden
        //------------------------------------------------------------------------------
        /// <summary>
        /// Gibt die Zeichnung zurück auf dem sich das DraftingObject befindet
        /// </summary>
        /// <returns>Namen der Zeichnung</returns>
        private void GetViewAndDrawingSheet()
        {
            if (_obj != null)
            {
                string temp_file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
                Session session = Session.GetSession();
                //session.LogCloseWindow();
                session.ListingWindow.SelectDevice(ListingWindow.DeviceType.File, temp_file);
                NXObject[] info = new NXObject[1];
                info[0] = _obj;
                session.Information.DisplayObjectsDetails(info);
                session.ListingWindow.SelectDevice(ListingWindow.DeviceType.Window, "");
                //session.LogCloseWindow();
                try
                {
                    string drawing_view_name = "";
                    string drawing_name = "";
                    using (StreamReader reader = new StreamReader(temp_file))
                    {
                        string info_text = reader.ReadToEnd();

                        NXOpen.Drawings.DraftingView[] views = session.Parts.Work.DraftingViews.ToArray();
                        for (int i = 0; i < views.Length; i++)
                        {
                            if (info_text.ToUpper().Contains(views[i].Name.ToUpper()))
                            {
                                drawing_view_name = views[i].Name;
                                _view_obj = views[i];
                            }
                        }

                        NXOpen.Drawings.DrawingSheet[] sheets = session.Parts.Work.DrawingSheets.ToArray();
                        for (int i = 0; i < sheets.Length; i++)
                        {
                            if (info_text.ToUpper().Contains(sheets[i].Name.ToUpper()))
                            {
                                drawing_name = sheets[i].Name;
                                _sheet_obj = sheets[i];
                            }
                        }

                        //Wenn nur die View gesetzt ist, wird hier noch ausgelesen auf welchem Sheet sich die View befindet
                        if (drawing_name == "" & drawing_view_name != "")
                        {
                            for (int i = 0; i < sheets.Length; i++)
                            {
                                NXOpen.Drawings.DraftingView[] views_on_sheet = sheets[i].GetDraftingViews();
                                for (int i2 = 0; i2 < views_on_sheet.Length; i2++)
                                {
                                    if (views_on_sheet[i2].Name.ToUpper() == drawing_view_name)
                                    {
                                        drawing_name = sheets[i].Name;
                                        _sheet_obj = sheets[i];
                                    }
                                }
                            }
                        }

                    }
                    File.Delete(temp_file);
                    _view = drawing_view_name;
                    _sheet = drawing_name;
                }
                catch (Exception ex)
                {
                    session.LogFile.WriteLine(ex.Message);
                }
            }
        }


        #endregion
    }

    public class Form_NiXeSearch : Form
    {
        TextSearch _search = new TextSearch();

        bool _allready_searched = false;

        public Form_NiXeSearch()
        {
            InitializeComponent();
        }

        private void buttonSchließen_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtSuchbegriff_TextChanged(object sender, EventArgs e)
        {
            _allready_searched = false;
            buttonExport.Enabled = false;
            labelInfo.Text = "";
        }

        private void buttonNext_Click(object sender, EventArgs e)
        {
            if (!_allready_searched)
            {
                _search.CaseSensetive = checkBoxCaseSensetive.Checked;
                _search.Markieren = checkBoxMarkieren.Checked;
                _search.QuiteText = checkBoxGenauerText.Checked;

                _search.UseID = checkBoxSymbolID.Checked;
                _search.UseLabel = checkBoxLabel.Checked;
                _search.UseText = checkBoxText.Checked;

                _search.SearchString(txtSuchbegriff.Text);

                labelInfo.Text = _search.MatchesCount + " gefunden.";
                _allready_searched = true;

                if (_search.MatchesCount != 0)
                {
                    buttonExport.Enabled = true;
                    _search.GoTo(_search.Matches[0], checkBoxZeichnungsblatt.Checked);
                }
            }
            else
            {
                if (_search.CurrentAnnotion != null)
                {
                    int i = _search.Matches.IndexOf(_search.CurrentAnnotion);

                    if (i + 1 < _search.MatchesCount) i++;
                    else i = 0;

                    if (_search.MatchesCount != 0) _search.GoTo(_search.Matches[i], checkBoxZeichnungsblatt.Checked);
                }
            }
        }

        private void Form_NiXeSearch_FormClosing(object sender, FormClosingEventArgs e)
        {
            _search.UnhighlightMatches();
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            _search.ExportMatchesToListingWindow();
        }





        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.buttonSchließen = new System.Windows.Forms.Button();
            this.buttonNext = new System.Windows.Forms.Button();
            this.labelInfo = new System.Windows.Forms.Label();
            this.buttonExport = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.checkBoxGenauerText = new System.Windows.Forms.CheckBox();
            this.checkBoxCaseSensetive = new System.Windows.Forms.CheckBox();
            this.checkBoxZeichnungsblatt = new System.Windows.Forms.CheckBox();
            this.checkBoxMarkieren = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.checkBoxSymbolID = new System.Windows.Forms.CheckBox();
            this.checkBoxLabel = new System.Windows.Forms.CheckBox();
            this.checkBoxText = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtSuchbegriff = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            //this.splitContainer1.Panel1.Controls.Add(this.controlBanner1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.buttonSchließen);
            this.splitContainer1.Panel2.Controls.Add(this.buttonNext);
            this.splitContainer1.Panel2.Controls.Add(this.labelInfo);
            this.splitContainer1.Panel2.Controls.Add(this.buttonExport);
            this.splitContainer1.Panel2.Controls.Add(this.groupBox3);
            this.splitContainer1.Panel2.Controls.Add(this.groupBox2);
            this.splitContainer1.Panel2.Controls.Add(this.groupBox1);
            this.splitContainer1.Size = new System.Drawing.Size(209, 433);
            this.splitContainer1.TabIndex = 0;
            // 
            // buttonSchließen
            // 
            this.buttonSchließen.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonSchließen.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttonSchließen.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSchließen.Location = new System.Drawing.Point(0, 343);
            this.buttonSchließen.Margin = new System.Windows.Forms.Padding(10, 10, 10, 3);
            this.buttonSchließen.Name = "buttonSchließen";
            this.buttonSchließen.Size = new System.Drawing.Size(209, 30);
            this.buttonSchließen.TabIndex = 1;
            this.buttonSchließen.Text = "Schließen";
            this.buttonSchließen.UseVisualStyleBackColor = true;
            this.buttonSchließen.Click += new System.EventHandler(this.buttonSchließen_Click);
            // 
            // buttonNext
            // 
            this.buttonNext.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttonNext.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonNext.Location = new System.Drawing.Point(0, 313);
            this.buttonNext.Margin = new System.Windows.Forms.Padding(10, 10, 10, 3);
            this.buttonNext.Name = "buttonNext";
            this.buttonNext.Size = new System.Drawing.Size(209, 30);
            this.buttonNext.TabIndex = 0;
            this.buttonNext.Text = "Weitersuchen";
            this.buttonNext.UseVisualStyleBackColor = true;
            this.buttonNext.Click += new System.EventHandler(this.buttonNext_Click);
            // 
            // labelInfo
            // 
            this.labelInfo.AutoSize = true;
            this.labelInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelInfo.Location = new System.Drawing.Point(0, 280);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Padding = new System.Windows.Forms.Padding(10);
            this.labelInfo.Size = new System.Drawing.Size(20, 33);
            this.labelInfo.TabIndex = 2;
            // 
            // buttonExport
            // 
            this.buttonExport.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttonExport.Enabled = false;
            this.buttonExport.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonExport.Location = new System.Drawing.Point(0, 250);
            this.buttonExport.Margin = new System.Windows.Forms.Padding(10, 10, 10, 3);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(209, 30);
            this.buttonExport.TabIndex = 3;
            this.buttonExport.Text = "Export...";
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.checkBoxGenauerText);
            this.groupBox3.Controls.Add(this.checkBoxCaseSensetive);
            this.groupBox3.Controls.Add(this.checkBoxZeichnungsblatt);
            this.groupBox3.Controls.Add(this.checkBoxMarkieren);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox3.Location = new System.Drawing.Point(0, 140);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(209, 110);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Optionen";
            // 
            // checkBoxGenauerText
            // 
            this.checkBoxGenauerText.AutoSize = true;
            this.checkBoxGenauerText.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxGenauerText.Location = new System.Drawing.Point(3, 76);
            this.checkBoxGenauerText.Name = "checkBoxGenauerText";
            this.checkBoxGenauerText.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxGenauerText.Size = new System.Drawing.Size(203, 20);
            this.checkBoxGenauerText.TabIndex = 3;
            this.checkBoxGenauerText.Text = "Genauer Text";
            this.checkBoxGenauerText.UseVisualStyleBackColor = true;
            this.checkBoxGenauerText.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // checkBoxCaseSensetive
            // 
            this.checkBoxCaseSensetive.AutoSize = true;
            this.checkBoxCaseSensetive.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxCaseSensetive.Location = new System.Drawing.Point(3, 56);
            this.checkBoxCaseSensetive.Name = "checkBoxCaseSensetive";
            this.checkBoxCaseSensetive.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxCaseSensetive.Size = new System.Drawing.Size(203, 20);
            this.checkBoxCaseSensetive.TabIndex = 1;
            this.checkBoxCaseSensetive.Text = "Groß-/Kleinschreibung";
            this.checkBoxCaseSensetive.UseVisualStyleBackColor = true;
            this.checkBoxCaseSensetive.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // checkBoxZeichnungsblatt
            // 
            this.checkBoxZeichnungsblatt.AutoSize = true;
            this.checkBoxZeichnungsblatt.Checked = true;
            this.checkBoxZeichnungsblatt.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxZeichnungsblatt.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxZeichnungsblatt.Location = new System.Drawing.Point(3, 36);
            this.checkBoxZeichnungsblatt.Name = "checkBoxZeichnungsblatt";
            this.checkBoxZeichnungsblatt.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxZeichnungsblatt.Size = new System.Drawing.Size(203, 20);
            this.checkBoxZeichnungsblatt.TabIndex = 2;
            this.checkBoxZeichnungsblatt.Text = "Zeichnungsblatt wechseln";
            this.checkBoxZeichnungsblatt.UseVisualStyleBackColor = true;
            this.checkBoxZeichnungsblatt.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // checkBoxMarkieren
            // 
            this.checkBoxMarkieren.AutoSize = true;
            this.checkBoxMarkieren.Checked = true;
            this.checkBoxMarkieren.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxMarkieren.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxMarkieren.Location = new System.Drawing.Point(3, 16);
            this.checkBoxMarkieren.Name = "checkBoxMarkieren";
            this.checkBoxMarkieren.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxMarkieren.Size = new System.Drawing.Size(203, 20);
            this.checkBoxMarkieren.TabIndex = 0;
            this.checkBoxMarkieren.Text = "Ergebnis markieren";
            this.checkBoxMarkieren.UseVisualStyleBackColor = true;
            this.checkBoxMarkieren.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.checkBoxSymbolID);
            this.groupBox2.Controls.Add(this.checkBoxLabel);
            this.groupBox2.Controls.Add(this.checkBoxText);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox2.Location = new System.Drawing.Point(0, 55);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(209, 85);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Wo soll gesucht werden?";
            // 
            // checkBoxSymbolID
            // 
            this.checkBoxSymbolID.AutoSize = true;
            this.checkBoxSymbolID.Checked = true;
            this.checkBoxSymbolID.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxSymbolID.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxSymbolID.Location = new System.Drawing.Point(3, 56);
            this.checkBoxSymbolID.Name = "checkBoxSymbolID";
            this.checkBoxSymbolID.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxSymbolID.Size = new System.Drawing.Size(203, 20);
            this.checkBoxSymbolID.TabIndex = 2;
            this.checkBoxSymbolID.Text = "Symbol ID";
            this.checkBoxSymbolID.UseVisualStyleBackColor = true;
            this.checkBoxSymbolID.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // checkBoxLabel
            // 
            this.checkBoxLabel.AutoSize = true;
            this.checkBoxLabel.Checked = true;
            this.checkBoxLabel.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxLabel.Location = new System.Drawing.Point(3, 36);
            this.checkBoxLabel.Name = "checkBoxLabel";
            this.checkBoxLabel.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxLabel.Size = new System.Drawing.Size(203, 20);
            this.checkBoxLabel.TabIndex = 1;
            this.checkBoxLabel.Text = "Bezeichner";
            this.checkBoxLabel.UseVisualStyleBackColor = true;
            this.checkBoxLabel.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // checkBoxText
            // 
            this.checkBoxText.AutoSize = true;
            this.checkBoxText.Checked = true;
            this.checkBoxText.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxText.Dock = System.Windows.Forms.DockStyle.Top;
            this.checkBoxText.Location = new System.Drawing.Point(3, 16);
            this.checkBoxText.Name = "checkBoxText";
            this.checkBoxText.Padding = new System.Windows.Forms.Padding(5, 3, 0, 0);
            this.checkBoxText.Size = new System.Drawing.Size(203, 20);
            this.checkBoxText.TabIndex = 0;
            this.checkBoxText.Text = "Text";
            this.checkBoxText.UseVisualStyleBackColor = true;
            this.checkBoxText.CheckedChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.txtSuchbegriff);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(8);
            this.groupBox1.Size = new System.Drawing.Size(209, 55);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Suchbegriff";
            // 
            // txtSuchbegriff
            // 
            this.txtSuchbegriff.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtSuchbegriff.ForeColor = System.Drawing.Color.Black;
            this.txtSuchbegriff.Location = new System.Drawing.Point(8, 21);
            this.txtSuchbegriff.Name = "txtSuchbegriff";
            this.txtSuchbegriff.Size = new System.Drawing.Size(193, 20);
            this.txtSuchbegriff.TabIndex = 0;
            this.txtSuchbegriff.Text = "";
            this.txtSuchbegriff.TextChanged += new System.EventHandler(this.txtSuchbegriff_TextChanged);
            // 
            // Form_NiXeSearch
            // 
            this.AcceptButton = this.buttonNext;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonSchließen;
            this.ClientSize = new System.Drawing.Size(209, 433);
            this.Controls.Add(this.splitContainer1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_NiXeSearch";
            this.Text = "NiXeSearch";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_NiXeSearch_FormClosing);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button buttonNext;
        private System.Windows.Forms.Button buttonSchließen;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox checkBoxZeichnungsblatt;
        private System.Windows.Forms.CheckBox checkBoxCaseSensetive;
        private System.Windows.Forms.CheckBox checkBoxMarkieren;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox checkBoxSymbolID;
        private System.Windows.Forms.CheckBox checkBoxLabel;
        private System.Windows.Forms.CheckBox checkBoxText;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtSuchbegriff;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.CheckBox checkBoxGenauerText;
        private System.Windows.Forms.Button buttonExport;
    }
}
