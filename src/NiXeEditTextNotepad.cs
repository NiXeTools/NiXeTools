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
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

using NXOpen;
using NXOpen.UF;
using NXOpen.Annotations;

namespace Journal
{
    class EditTextNotepad
    {
        /// <summary>
        /// Hier den Pfad zum Notepad oder Notepad++ eintragen
        /// </summary>
        const string NotepadExe = @"C:\Program Files (x86)\Notepad++\notepad++.exe";

        static Session theSession;
        static UFSession theUFSession;
        static UI theUI;
        static Part workPart;

        public static void Main(string[] args)
        {
            theSession = Session.GetSession();
            theUFSession = UFSession.GetUFSession();
            theUI = UI.GetUI();
            workPart = theSession.Parts.Work;

            if (workPart == null) return;

            Session.UndoMarkId current_undo_mark = theSession.SetUndoMark(Session.MarkVisibility.Visible, "EditText");
            try
            {
                //-----------------------------------------------------------------------------
                //Vorauswahl
                //-----------------------------------------------------------------------------
                int number_selected_objects = theUI.SelectionManager.GetNumSelectedObjects();
                if (number_selected_objects != 0)
                {
                    theSession.LogFile.WriteLine("NumSelectedObjects: " + number_selected_objects);
                    bool flag = false;
                    List<NoteBase> objs = new List<NoteBase>();
                    for (int i = 0; i < number_selected_objects; i++)
                    {
                        NoteBase ann = theUI.SelectionManager.GetSelectedTaggedObject(i) as NoteBase;
                        if (ann != null)
                        {
                            objs.Add(ann);
                            flag = true;
                        }
                    }

                    for (int i = 0; i < objs.Count; i++) { ProcessNote(objs[i]); }
                    if (flag) return;
                }

                //-----------------------------------------------------------------------------
                // Keine Vorauwahl getroffen
                //-----------------------------------------------------------------------------
                NXObject obj;
                Point3d cursor;
                while (theUI.SelectionManager.SelectObject("Annotation auswählen",
                    "Select",
                    Selection.SelectionScope.AnyInAssembly,
                    true,
                    false,
                    out obj,
                    out cursor) == Selection.Response.ObjectSelected)
                {
                    NoteBase current = obj as NoteBase;
                    if (current != null)
                    {
                        ProcessNote(current);
                    }
                }
            }
            catch (Exception ex)
            {
                theSession.ListingWindow.Open();
                theSession.ListingWindow.WriteLine(ex.GetType().FullName);
                theSession.ListingWindow.WriteLine(ex.Message);
                theSession.ListingWindow.WriteLine(ex.StackTrace);
            }
        }

        public static void ProcessNote(NoteBase n)
        {            
            var frm = new WaitForm();
            frm.NotepadExe = NotepadExe;
            frm.AnnationText.AddRange(n.GetText());
            
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Session.UndoMarkId current_undo_mark = theSession.SetUndoMark(Session.MarkVisibility.Invisible, "SetText");
                    n.SetText(frm.AnnationText.ToArray());
                    theSession.UpdateManager.DoUpdate(current_undo_mark);
                    theSession.DeleteUndoMark(current_undo_mark, "SetText");
                }
                catch (Exception ex)
                {
                    theSession.ListingWindow.Open();
                    theSession.ListingWindow.WriteLine("Fehler beim setzen des Textes!");
                    theSession.ListingWindow.WriteLine(ex.Message);
                    theSession.ListingWindow.WriteLine("----------------------------------------------------------------------------");
                    foreach (var item in frm.AnnationText)
                    {
                        theSession.ListingWindow.WriteLine(item);
                    }
                }
            }
        }

        public static int GetUnloadOption(string dummy) { return (int)Session.LibraryUnloadOption.Immediately; }
    }

    /// <summary>
    /// Hilfsform um auf das Exited-Event zu warten
    /// Process.WaitForExit() Methode fürt zu "Server ist ausgelastet" - Meldung im NX
    /// deswegen dieser Weg
    /// </summary>
    public class WaitForm : Form
    {
        public string NotepadExe { get; set; }
        public List<string> AnnationText { get; set; }
        public string TempFile { get; set; }

        public WaitForm()
        {
            this.ClientSize = new System.Drawing.Size(10, 10);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Text = "EditText";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new System.Drawing.Size(220, 30);
            this.ResumeLayout(false);
            this.Shown += WaitForm_Shown;

            var btn = new Button();
            btn.Dock = DockStyle.Fill;
            btn.Text = "Cancel";
            btn.Click += btn_Click;

            this.Controls.Add(btn);

            AnnationText = new List<string>();
            //StandardDialog Result, wird nur bei pro_Exited auf OK gesetzt
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        void WaitForm_Shown(object sender, EventArgs e)
        {
            try
            {                
                TempFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
                using (var writer = new StreamWriter(TempFile, true, Encoding.UTF8))
                {
                    foreach (var item in AnnationText)
                    {
                        writer.WriteLine(item);
                    }
                }

                if (File.Exists(TempFile))
                {
                    if (File.Exists(NotepadExe))
                    {
                        var info = new ProcessStartInfo();
                        info.FileName = NotepadExe;
                        info.Arguments = TempFile;
                        info.UseShellExecute = true;

                        var pro = new Process();
                        pro.StartInfo = info;
                        pro.EnableRaisingEvents = true;
                        pro.Exited += pro_Exited;
                        pro.Start();
                    }
                    else
                    {
                        MessageBox.Show("Notepad not found: " + NotepadExe);
                        this.Close();
                    }
                }
                else
                {
                    MessageBox.Show("TextFile not found: " + TempFile);
                    this.Close();
                }    
            }
            catch (Exception ex)
            {
                ShowError(ex);
                this.Close();
            }
        
        }

        /// <summary>
        /// Zurückgeben des Textes nach schließen des Editors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void pro_Exited(object sender, EventArgs e)
        {
            try
            {
                if (File.Exists(TempFile))
                {
                    using (var reader = new StreamReader(TempFile, Encoding.UTF8))
                    {
                        var new_lines = new List<string>();
                        while (!reader.EndOfStream)
                        {
                            new_lines.Add(reader.ReadLine().Replace("\t", "    "));
                        }
                        AnnationText = new_lines;
                    }
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("TextFile not found: " + TempFile);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
                this.Close();
            }
        }

        /// <summary>
        /// Button Cancel Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void btn_Click(object sender, EventArgs e)
        {            
            this.Close();
        }

        void ShowError(Exception ex)
        {
            MessageBox.Show(ex.Message, "EditText", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
