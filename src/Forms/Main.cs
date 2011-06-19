﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.BluRaySup;
using Nikse.SubtitleEdit.Logic.Enums;
using Nikse.SubtitleEdit.Logic.Networking;
using Nikse.SubtitleEdit.Logic.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.VobSub;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main : Form
    {

        private class ComboBoxZoomItem
        {
            public string Text { get; set; }
            public double ZoomFactor { get; set; }
            public override string ToString()
            {
                return Text;
            }
        }

        const int TabControlListView = 0;
        const int TabControlSourceView = 1;

        Subtitle _subtitle = new Subtitle();
        Subtitle _subtitleAlternate = new Subtitle();
        string _subtitleAlternateFileName;
        string _fileName;
        string _videoFileName;
        int _videoAudioTrackNumber = -1;

        public string VideoFileName
        {
            get { return _videoFileName; }
            set { _videoFileName = value; }
        }
        DateTime _fileDateTime;
        string _title;
        FindReplaceDialogHelper _findHelper;
        int _replaceStartLineIndex = 0;
        bool _sourceViewChange;
        bool _change;
        bool _changeAlternate;
        int _subtitleListViewIndex = -1;
        Paragraph _oldSelectedParagraph;
        bool _converted;
        SubtitleFormat _oldSubtitleFormat;
        List<int> _selectedIndexes;
        LanguageStructure.Main _language;
        LanguageStructure.General _languageGeneral;
        SpellCheck _spellCheckForm;
        PositionsAndSizes _formPositionsAndSizes = new PositionsAndSizes();

        int _repeatCount = -1;
        double _endSeconds = -1;
        const double EndDelay = 0.05;
        int _autoContinueDelayCount = -1;
        long _lastTextKeyDownTicks = 0;
        System.Windows.Forms.Timer _timerAddHistoryWhenDone = new Timer();
        string _timerAddHistoryWhenDoneText;
        double? _audioWaveFormRightClickSeconds = null;

        System.Windows.Forms.Timer _timerAutoSave = new Timer();
        string _textAutoSave;

        NikseWebServiceSession _networkSession;
        NetworkChat _networkChat = null;

        ShowEarlierLater _showEarlierOrLater = null;

        bool _isVideoControlsUnDocked = false;
        VideoPlayerUnDocked _videoPlayerUnDocked = null;
        WaveFormUnDocked _waveFormUnDocked = null;
        VideoControlsUndocked _videoControlsUnDocked = null;

        GoogleOrMicrosoftTranslate _googleOrMicrosoftTranslate = null;

        bool _cancelWordSpellCheck = false;

        Keys _toggleVideoDockUndock = Keys.None;
        Keys _mainAdjustInsertViaEndAutoStartAndGoToNext = Keys.None;
        bool _videoLoadedGoToSubPosAndPause = false;
        bool _makeHistory = true;

        private bool AutoRepeatContinueOn
        {
            get
            {
                return tabControlButtons.SelectedIndex == 0;
            }
        }

        public string Title
        {
            get
            {
                if (_title == null)
                {
                    string[] versionInfo = Utilities.AssemblyVersion.Split('.');
                    _title = String.Format("{0} {1}.{2}", _languageGeneral.Title, versionInfo[0], versionInfo[1]);
                    if (versionInfo.Length >= 3 && versionInfo[2] != "0")
                        _title += "." + versionInfo[2];
                }
                return _title + " Beta 2";
            }
        }

        public void SetCurrentFormat(SubtitleFormat format)
        {
            if (format.IsVobSubIndexFile)
            {
                comboBoxSubtitleFormats.Items.Clear();
                comboBoxSubtitleFormats.Items.Add(format.FriendlyName);

                SubtitleListview1.HideNonVobSubColumns();
            }
            else if (comboBoxSubtitleFormats.Items.Count == 1)
            {
                SetFormatToSubRip();
                SubtitleListview1.ShowAllColumns();
            }

            int i = 0;
            foreach (object obj in comboBoxSubtitleFormats.Items)
            {
                if (obj.ToString() == format.FriendlyName)
                    comboBoxSubtitleFormats.SelectedIndex = i;
                i++;
            }
        }

        public void SetCurrentFormat(string subtitleFormatFriendlyName)
        {

            foreach (SubtitleFormat format in SubtitleFormat.AllSubtitleFormats)
            {
                if (format.FriendlyName == subtitleFormatFriendlyName)
                {
                    SetCurrentFormat(format);
                    break;
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();


        public Main()
        {
            try
            {
                InitializeComponent();

                textBoxListViewTextAlternate.Visible = false;
                labelAlternateText.Visible = false;
                labelAlternateCharactersPerSecond.Visible = false;
                labelTextAlternateLineLengths.Visible = false;
                labelAlternateSingleLine.Visible = false;
                labelTextAlternateLineTotal.Visible = false;

                SetLanguage(Configuration.Settings.General.Language);
                toolStripStatusNetworking.Visible = false;
                labelTextLineLengths.Text = string.Empty;
                labelCharactersPerSecond.Text = string.Empty;
                labelTextLineTotal.Text = string.Empty;
                labelStartTimeWarning.Text = string.Empty;
                labelDurationWarning.Text = string.Empty;
                labelVideoInfo.Text = string.Empty;
                labelSingleLine.Text = string.Empty;
                Text = Title;
                timeUpDownStartTime.TimeCode = new TimeCode(0, 0, 0, 0);
                checkBoxAutoRepeatOn.Checked = Configuration.Settings.General.AutoRepeatOn;
                checkBoxAutoContinue.Checked = Configuration.Settings.General.AutoContinueOn;
                checkBoxSyncListViewWithVideoWhilePlaying.Checked = Configuration.Settings.General.SyncListViewWithVideoWhilePlaying;

                SetFormatToSubRip();

                if (Configuration.Settings.General.DefaultEncoding == "ANSI")
                {
                    comboBoxEncoding.SelectedIndex = 0;
                    comboBoxEncoding.Items[0] = "ANSI - " + Encoding.Default.CodePage.ToString();
                }
                else
                {
                    comboBoxEncoding.Text = Configuration.Settings.General.DefaultEncoding;
                }

                toolStripComboBoxFrameRate.Items.Add((23.976).ToString());
                toolStripComboBoxFrameRate.Items.Add((24.0).ToString());
                toolStripComboBoxFrameRate.Items.Add((25.0).ToString());
                toolStripComboBoxFrameRate.Items.Add((29.97).ToString());
                toolStripComboBoxFrameRate.Text = Configuration.Settings.General.DefaultFrameRate.ToString();

                UpdateRecentFilesUI();
                InitializeToolbar();
                Utilities.InitializeSubtitleFont(textBoxSource);
                Utilities.InitializeSubtitleFont(textBoxListViewText);
                Utilities.InitializeSubtitleFont(SubtitleListview1);
                SubtitleListview1.AutoSizeAllColumns(this);

                tabControlSubtitle.SelectTab(TabControlSourceView); // AC
                ShowSourceLineNumber();                             // AC
                tabControlSubtitle.SelectTab(TabControlListView);   // AC
                if (Configuration.Settings.General.StartInSourceView)
                    tabControlSubtitle.SelectTab(TabControlSourceView);


                audioVisualizer.Visible = Configuration.Settings.General.ShowWaveForm;
                panelWaveFormControls.Visible = Configuration.Settings.General.ShowWaveForm;
                trackBarWaveFormPosition.Visible = Configuration.Settings.General.ShowWaveForm;
                toolStripButtonToggleWaveForm.Checked = Configuration.Settings.General.ShowWaveForm;
                toolStripButtonToggleVideo.Checked = Configuration.Settings.General.ShowVideoPlayer;

                string fileName = string.Empty;
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length >= 4 && args[1].ToLower() == "/convert")
                {
                    BatchConvert(args);
                    return;
                }
                else if (args.Length >= 2)
                    fileName = args[1];

                if (fileName.Length > 0 && File.Exists(fileName))
                {
                    OpenSubtitle(fileName, null);
                }
                else if (Configuration.Settings.General.StartLoadLastFile)
                {
                    if (Configuration.Settings.RecentFiles.Files.Count > 0)
                    {
                        fileName = Configuration.Settings.RecentFiles.Files[0].FileName;
                        if (File.Exists(fileName))
                        {
                            OpenSubtitle(fileName, null, Configuration.Settings.RecentFiles.Files[0].VideoFileName, Configuration.Settings.RecentFiles.Files[0].OriginalFileName);
                            SetRecentIndecies(fileName);
                            GotoSubPosAndPause();
                        }
                    }
                }

                labelAutoDuration.Visible = false;
                mediaPlayer.SubtitleText = string.Empty;                
                comboBoxAutoRepeat.SelectedIndex = 2;
                comboBoxAutoContinue.SelectedIndex = 2;
                timeUpDownVideoPosition.TimeCode = new TimeCode(0, 0, 0, 0);
                timeUpDownVideoPositionAdjust.TimeCode = new TimeCode(0, 0, 0, 0);
                timeUpDownVideoPosition.TimeCodeChanged += VideoPositionChanged;
                timeUpDownVideoPositionAdjust.TimeCodeChanged += VideoPositionChanged;
                timeUpDownVideoPosition.Enabled = false;
                timeUpDownVideoPositionAdjust.Enabled = false;

                switch (Configuration.Settings.VideoControls.LastActiveTab)
                {
                    case "Translate":
                        tabControlButtons.SelectedIndex = 0;
                        break;
                    case "Create":
                        tabControlButtons.SelectedIndex = 1;
                        break;
                    case "Adjust":
                        tabControlButtons.SelectedIndex = 2;
                        break;
                }
                tabControl1_SelectedIndexChanged(null, null);

                buttonCustomUrl.Text = Configuration.Settings.VideoControls.CustomSearchText;
                buttonCustomUrl.Enabled = Configuration.Settings.VideoControls.CustomSearchUrl.Length > 1;

                // Initialize events etc. for audio wave form
                audioVisualizer.OnDoubleClickNonParagraph += AudioWaveForm_OnDoubleClickNonParagraph;
                audioVisualizer.OnPositionSelected += AudioWaveForm_OnPositionSelected;
                audioVisualizer.OnTimeChanged += AudioWaveForm_OnTimeChanged;
                audioVisualizer.OnNewSelectionRightClicked += AudioWaveForm_OnNewSelectionRightClicked;
                audioVisualizer.OnParagraphRightClicked += AudioWaveForm_OnParagraphRightClicked;
                audioVisualizer.OnNonParagraphRightClicked += new AudioVisualizer.PositionChangedEventHandler(AudioWaveForm_OnNonParagraphRightClicked);
                audioVisualizer.OnSingleClick += AudioWaveForm_OnSingleClick;
                audioVisualizer.OnPause += AudioWaveForm_OnPause;
                audioVisualizer.OnTimeChangedAndOffsetRest += AudioWaveForm_OnTimeChangedAndOffsetRest;
                audioVisualizer.OnZoomedChanged += AudioWaveForm_OnZoomedChanged;
                audioVisualizer.DrawGridLines = Configuration.Settings.VideoControls.WaveFormDrawGrid;
                audioVisualizer.GridColor = Configuration.Settings.VideoControls.WaveFormGridColor;
                audioVisualizer.SelectedColor = Configuration.Settings.VideoControls.WaveFormSelectedColor;
                audioVisualizer.Color = Configuration.Settings.VideoControls.WaveFormColor;
                audioVisualizer.BackgroundColor = Configuration.Settings.VideoControls.WaveFormBackgroundColor;
                audioVisualizer.TextColor = Configuration.Settings.VideoControls.WaveFormTextColor;
                audioVisualizer.MouseWheelScrollUpIsForward = Configuration.Settings.VideoControls.WaveFormMouseWheelScrollUpIsForward;

                for (double zoomCounter = AudioVisualizer.ZoomMininum; zoomCounter <= AudioVisualizer.ZoomMaxinum + (0.001); zoomCounter += 0.1)
                {
                    int percent = (int)Math.Round((zoomCounter * 100));
                    ComboBoxZoomItem item = new ComboBoxZoomItem() { Text = percent.ToString() + "%", ZoomFactor = zoomCounter };
                    toolStripComboBoxWaveForm.Items.Add(item);
                    if (percent == 100)
                        toolStripComboBoxWaveForm.SelectedIndex = toolStripComboBoxWaveForm.Items.Count - 1;
                }
                toolStripComboBoxWaveForm.SelectedIndexChanged += toolStripComboBoxWaveForm_SelectedIndexChanged;

                FixLargeFonts();
                _timerAddHistoryWhenDone.Interval = 500;
                _timerAddHistoryWhenDone.Tick += new EventHandler(timerAddHistoryWhenDone_Tick);
            }
            catch (Exception exception)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(exception.Message + Environment.NewLine + exception.StackTrace);
            }
        }

        
        private void BatchConvert(string[] args) // E.g.: /convert *.txt SubRip
        {
            const int ATTACH_PARENT_PROCESS = -1;
            AttachConsole(ATTACH_PARENT_PROCESS);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(Title + " - Batch converter");
            Console.WriteLine();
            Console.WriteLine("- Syntax: SubtitleEdit /convert <pattern> <name-of-format-without-spaces> [/offset:hh:mm:ss:msec]");
            Console.WriteLine("    example: SubtitleEdit /convert *.srt sami");
            Console.WriteLine();
            Console.WriteLine();
            
            string pattern = args[2];
            string toFormat = args[3];
            string offset = string.Empty;
            if (args.Length > 4)
                offset = args[4].ToLower();

            string inputDirectory = Directory.GetCurrentDirectory();
            int indexOfDirectorySeparatorChar = pattern.LastIndexOf(Path.DirectorySeparatorChar.ToString());
            if (indexOfDirectorySeparatorChar > 0)
            {
                pattern = pattern.Substring(indexOfDirectorySeparatorChar);
                inputDirectory = pattern.Substring(0, indexOfDirectorySeparatorChar -1);            
            }

            int count = 0;
            int converted = 0;
            var formats = SubtitleFormat.AllSubtitleFormats;
            foreach (string fileName in Directory.GetFiles(inputDirectory, pattern))
            {
                count++;

                Encoding encoding;
                Subtitle sub = new Subtitle();
                SubtitleFormat format = sub.LoadSubtitle(fileName, out encoding, null);
                if (format == null)
                {
                    var ebu = new Ebu();
                    if (ebu.IsMine(null, fileName))
                    {
                        ebu.LoadSubtitle(sub, null, fileName);
                        format = ebu;
                    }
                }
                if (format == null)
                {
                    var pac = new Pac();
                    if (pac.IsMine(null, fileName))
                    {
                        pac.LoadSubtitle(sub, null, fileName);
                        format = pac;
                    }
                }
                if (format == null)
                {
                    var cavena890 = new Cavena890();
                    if (cavena890.IsMine(null, fileName))
                    {
                        cavena890.LoadSubtitle(sub, null, fileName);
                        format = cavena890;
                    }
                }
                if (format == null)
                {
                    var spt = new Spt();
                    if (spt.IsMine(null, fileName))
                    {
                        spt.LoadSubtitle(sub, null, fileName);
                        format = spt;
                    }
                }

                if (format == null)
                {
                    Console.WriteLine(string.Format("{0}: {1} - input file format unknown!", count, fileName, toFormat));
                }
                else
                {
                    // adjust offset
                    if (!string.IsNullOrEmpty(offset) && (offset.StartsWith("/offset:") || offset.StartsWith("offset:")))
                    {
                        string[] parts = offset.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 5)
                        {
                            try
                            {
                                TimeSpan ts = new TimeSpan(0, int.Parse(parts[1].TrimStart('-')), int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]));
                                if (parts[1].StartsWith("-"))
                                    sub.AddTimeToAllParagraphs(ts.Negate());
                                else
                                    sub.AddTimeToAllParagraphs(ts);
                            }
                            catch 
                            {
                                Console.Write(" (unable to read offset " + offset + ")");
                            }
                        }
                    }               

                    bool targetFormatFound = false;
                    foreach (SubtitleFormat sf in formats)
                    {
                        if (sf.Name.ToLower().Replace(" ", string.Empty) == toFormat.ToLower())
                        {
                            targetFormatFound = true;
                            string outputFileName = Path.GetFileNameWithoutExtension(fileName) + sf.Extension;
                            if (File.Exists(outputFileName))
                                outputFileName = Path.GetFileNameWithoutExtension(fileName) + "_" + Guid.NewGuid().ToString() + sf.Extension;
                            Console.Write(string.Format("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName));
                            if (sf.IsFrameBased && !sub.WasLoadedWithFrameNumbers)
                                sub.CalculateFrameNumbersFromTimeCodesNoCheck(Configuration.Settings.General.DefaultFrameRate);
                            else if (sf.IsTimeBased && sub.WasLoadedWithFrameNumbers)
                                sub.CalculateTimeCodesFromFrameNumbers(Configuration.Settings.General.DefaultFrameRate);
                            System.IO.File.WriteAllText(outputFileName, sub.ToText(sf));
                            Console.WriteLine(" done.");
                        }
                    }
                    if (!targetFormatFound)
                    {
                        var ebu = new Ebu();
                        if (ebu.Name.ToLower().Replace(" ", string.Empty) == toFormat.ToLower())
                        {
                            targetFormatFound = true;
                            string outputFileName = Path.GetFileNameWithoutExtension(fileName) + ebu.Extension;
                            if (File.Exists(outputFileName))
                                outputFileName = Path.GetFileNameWithoutExtension(fileName) + "_" + Guid.NewGuid().ToString() + ebu.Extension;
                            Console.Write(string.Format("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName));
                            ebu.Save(outputFileName, sub);
                            Console.WriteLine(" done.");
                        }
                    }
                    if (!targetFormatFound)
                    {
                        var pac = new Pac();
                        if (pac.Name.ToLower().Replace(" ", string.Empty) == toFormat.ToLower())
                        {
                            targetFormatFound = true;
                            string outputFileName = Path.GetFileNameWithoutExtension(fileName) + pac.Extension;
                            if (File.Exists(outputFileName))
                                outputFileName = Path.GetFileNameWithoutExtension(fileName) + "_" + Guid.NewGuid().ToString() + pac.Extension;
                            Console.Write(string.Format("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName));
                            pac.Save(outputFileName, sub);
                            Console.WriteLine(" done.");
                        }
                    }
                    if (!targetFormatFound)
                    {
                        var cavena890 = new Cavena890();
                        if (cavena890.Name.ToLower().Replace(" ", string.Empty) == toFormat.ToLower())
                        {
                            targetFormatFound = true;
                            string outputFileName = Path.GetFileNameWithoutExtension(fileName) + cavena890.Extension;
                            if (File.Exists(outputFileName))
                                outputFileName = Path.GetFileNameWithoutExtension(fileName) + "_" + Guid.NewGuid().ToString() + cavena890.Extension;
                            Console.Write(string.Format("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName));
                            cavena890.Save(outputFileName, sub);
                            Console.WriteLine(" done.");
                        }
                    }
                    if (!targetFormatFound)
                    {
                        Console.WriteLine(string.Format("{0}: {1} - target format '{2}' not found!", count, fileName, toFormat));
                    }
                    else
                    {
                        converted++;
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine(string.Format("{0} file(s) converted", converted));
            Console.WriteLine();
            Console.Write(inputDirectory + ">");
            if (count == converted)
                Environment.Exit(0);
            else
                Environment.Exit(1);
            FreeConsole();
        }

        void AudioWaveForm_OnNonParagraphRightClicked(double seconds, Paragraph paragraph)
        {
            addParagraphHereToolStripMenuItem.Visible = false;
            deleteParagraphToolStripMenuItem.Visible = false;
            splitToolStripMenuItem1.Visible = false;
            mergeWithPreviousToolStripMenuItem.Visible = false;
            mergeWithNextToolStripMenuItem.Visible = false;
            toolStripSeparator11.Visible = false;
            toolStripMenuItemWaveFormPlaySelection.Visible = false;
            toolStripSeparator24.Visible = false;
            contextMenuStripWaveForm.Show(MousePosition.X, MousePosition.Y);
        }

        void AudioWaveForm_OnDoubleClickNonParagraph(double seconds, Paragraph paragraph)
        {
            if (mediaPlayer.VideoPlayer != null)
            {
                _endSeconds = -1;
                if (paragraph == null)
                {
                    if (Configuration.Settings.VideoControls.WaveFormDoubleClickOnNonParagraphAction == "PlayPause")
                        mediaPlayer.TogglePlayPause();
                }
                else
                {
                    SubtitleListview1.SelectIndexAndEnsureVisible(_subtitle.GetIndex(paragraph));
                }
            }
        }

        void timerAddHistoryWhenDone_Tick(object sender, EventArgs e)
        {
            _timerAddHistoryWhenDone.Stop();
            _subtitle.MakeHistoryForUndo(_timerAddHistoryWhenDoneText, GetCurrentSubtitleFormat(), _fileDateTime);
        }

        void AudioWaveForm_OnZoomedChanged(object sender, EventArgs e)
        {
            SelectZoomTextInComboBox();
        }

        void AudioWaveForm_OnTimeChangedAndOffsetRest(double seconds, Paragraph paragraph)
        {
            int index = _subtitle.GetIndex(paragraph);
            if (mediaPlayer.VideoPlayer != null && index >= 0)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
                mediaPlayer.CurrentPosition = seconds;
                ButtonSetStartAndOffsetRestClick(null, null);
                audioVisualizer.Invalidate();
            }   
        }

        void AudioWaveForm_OnPause(object sender, EventArgs e)
        {
            _endSeconds = -1;
            if (mediaPlayer.VideoPlayer != null)
                mediaPlayer.Pause();
        }

        void AudioWaveForm_OnSingleClick(double seconds, Paragraph paragraph)
        {
            _endSeconds = -1;
            if (mediaPlayer.VideoPlayer != null)
                mediaPlayer.Pause();
            mediaPlayer.CurrentPosition = seconds;
        }

        void AudioWaveForm_OnParagraphRightClicked(double seconds, Paragraph paragraph)
        {
            SubtitleListview1.SelectIndexAndEnsureVisible(_subtitle.GetIndex(paragraph));

            addParagraphHereToolStripMenuItem.Visible = false;
            deleteParagraphToolStripMenuItem.Visible = true;
            splitToolStripMenuItem1.Visible = true;
            mergeWithPreviousToolStripMenuItem.Visible = true;
            mergeWithNextToolStripMenuItem.Visible = true;
            toolStripSeparator11.Visible = true;
            toolStripMenuItemWaveFormPlaySelection.Visible = true;
            toolStripSeparator24.Visible = true;

            _audioWaveFormRightClickSeconds = seconds;
            contextMenuStripWaveForm.Show(MousePosition.X, MousePosition.Y);
        }

        void AudioWaveForm_OnNewSelectionRightClicked(Paragraph paragraph)
        {
            SubtitleListview1.SelectIndexAndEnsureVisible(_subtitle.GetIndex(paragraph));

            addParagraphHereToolStripMenuItem.Visible = true;
            deleteParagraphToolStripMenuItem.Visible = false;
            splitToolStripMenuItem1.Visible = false;
            mergeWithPreviousToolStripMenuItem.Visible = false;
            mergeWithNextToolStripMenuItem.Visible = false;

            contextMenuStripWaveForm.Show(MousePosition.X, MousePosition.Y);
        }

        void AudioWaveForm_OnTimeChanged(double seconds, Paragraph paragraph)
        {
            _change = true;
            MakeHistoryForUndoWhenNoMoreChanges(string.Format(_language.VideoControls.BeforeChangingTimeInWaveFormX, "#" + paragraph.Number + " " + paragraph.Text));
            _makeHistory = false;
            int index = _subtitle.GetIndex(paragraph);
            if (index == _subtitleListViewIndex)
            {
                timeUpDownStartTime.TimeCode = paragraph.StartTime;
                decimal durationInSeconds = (decimal) (paragraph.Duration.TotalSeconds);
                if (durationInSeconds >= numericUpDownDuration.Minimum && durationInSeconds <= numericUpDownDuration.Maximum)
                    numericUpDownDuration.Value = durationInSeconds;
            }
            else
            {
                SubtitleListview1.SetStartTime(index, paragraph);
                SubtitleListview1.SetDuration(index, paragraph);
            }
            _makeHistory = true;
        }

        void AudioWaveForm_OnPositionSelected(double seconds, Paragraph paragraph)
        {
            mediaPlayer.CurrentPosition = seconds;
            if (paragraph != null)
                SubtitleListview1.SelectIndexAndEnsureVisible(_subtitle.GetIndex(paragraph));
        }

        private void VideoPositionChanged(object sender, EventArgs e)
        {
            TimeUpDown tud = (TimeUpDown)sender;
            if (tud.Enabled)
            {
                mediaPlayer.CurrentPosition = tud.TimeCode.TotalSeconds;
            }
        }

        private void Main_Load(object sender, EventArgs e)
        {
            splitContainer1.Panel1MinSize = 525;
            splitContainer1.Panel2MinSize = 250;
            splitContainerMain.Panel1MinSize = 250;
            splitContainerMain.Panel2MinSize = 200;

            if (Configuration.Settings.General.StartListViewWidth < 250)
                Configuration.Settings.General.StartListViewWidth = (Width / 3) * 2;

            if (Configuration.Settings.General.StartRememberPositionAndSize &&
                !string.IsNullOrEmpty(Configuration.Settings.General.StartPosition))
            {
                string[] parts = Configuration.Settings.General.StartPosition.Split(';');
                if (parts.Length == 2)
                {
                    int x;
                    int y;
                    if (int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                    {
                        if (x > -100 || y > -100)
                        {
                            Left = x;
                            Top = y;
                        }
                    }
                }

                if (Configuration.Settings.General.StartSize == "Maximized")
                {
                    CenterFormOnCurrentScreen();
                    WindowState = FormWindowState.Maximized;
                    if (!splitContainer1.Panel2Collapsed && Configuration.Settings.General.StartRememberPositionAndSize)
                        splitContainer1.SplitterDistance = Configuration.Settings.General.StartListViewWidth;
                    return;
                }

                parts = Configuration.Settings.General.StartSize.Split(';');
                if (parts.Length == 2)
                {
                    int x;
                    int y;
                    if (int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                    {
                        Width = x;
                        Height = y;
                    }
                }

                Screen screen = Screen.FromControl(this);

                if (screen.Bounds.Width < Width)
                    Width = screen.Bounds.Width;
                if (screen.Bounds.Height < Height)
                    Height = screen.Bounds.Height;

                if (screen.Bounds.X + screen.Bounds.Width - 200 < Left)
                    Left = screen.Bounds.X + screen.Bounds.Width - Width;
                if (screen.Bounds.Y + screen.Bounds.Height - 100 < Top)
                    Top = screen.Bounds.Y + screen.Bounds.Height - Height;
            }
            else
            {
                CenterFormOnCurrentScreen();
            }
            if (!splitContainer1.Panel2Collapsed && Configuration.Settings.General.StartRememberPositionAndSize)
            {
                splitContainer1.SplitterDistance = Configuration.Settings.General.StartListViewWidth;
            }

            if (Environment.OSVersion.Version.Major < 6) // 6 == Vista/Win2008Server/Win7
            {
                string unicodeFontName = Utilities.WinXp2kUnicodeFontName;
                Configuration.Settings.General.SubtitleFontName = unicodeFontName;
                float fontSize = toolStripMenuItemSingleNote.Font.Size;
                toolStripMenuItemSingleNote.Font = new System.Drawing.Font(unicodeFontName, fontSize);
                toolStripMenuItemDoubleNote.Font = new System.Drawing.Font(unicodeFontName, fontSize);
                toolStripMenuItemSmiley.Font = new System.Drawing.Font(unicodeFontName, fontSize);
                toolStripMenuItemLove.Font = new System.Drawing.Font(unicodeFontName, fontSize);
                textBoxSource.Font = new System.Drawing.Font(unicodeFontName, fontSize);
                textBoxListViewText.Font = new System.Drawing.Font(unicodeFontName, fontSize);
                SubtitleListview1.Font = new System.Drawing.Font(unicodeFontName, fontSize);

                toolStripWaveControls.RenderMode = ToolStripRenderMode.System;
            }
        }

        private void InitializeLanguage()
        {
            fileToolStripMenuItem.Text = _language.Menu.File.Title;
            newToolStripMenuItem.Text = _language.Menu.File.New;
            openToolStripMenuItem.Text = _language.Menu.File.Open;
            reopenToolStripMenuItem.Text = _language.Menu.File.Reopen;
            saveToolStripMenuItem.Text = _language.Menu.File.Save;
            saveAsToolStripMenuItem.Text = _language.Menu.File.SaveAs;
            openOriginalToolStripMenuItem.Text = _language.Menu.File.OpenOriginal;
            saveOriginalToolStripMenuItem.Text = _language.Menu.File.SaveOriginal;
            saveOriginalAstoolStripMenuItem.Text = _language.SaveOriginalSubtitleAs;
            removeOriginalToolStripMenuItem.Text = _language.Menu.File.CloseOriginal;

            toolStripMenuItemOpenContainingFolder.Text = _language.Menu.File.OpenContainingFolder;
            toolStripMenuItemCompare.Text = _language.Menu.File.Compare;
            toolStripMenuItemImportDvdSubtitles.Text = _language.Menu.File.ImportOcrFromDvd;
            toolStripMenuItemSubIdx.Text = _language.Menu.File.ImportOcrVobSubSubtitle;
            toolStripButtonGetFrameRate.ToolTipText = _language.GetFrameRateFromVideoFile;

            toolStripMenuItemImportBluRaySup.Text = _language.Menu.File.ImportBluRaySupFile;

            matroskaImportStripMenuItem.Text = _language.Menu.File.ImportSubtitleFromMatroskaFile;
            toolStripMenuItemManualAnsi.Text = _language.Menu.File.ImportSubtitleWithManualChosenEncoding;
            toolStripMenuItemImportText.Text = _language.Menu.File.ImportText;
            toolStripMenuItemImportTimeCodes.Text = _language.Menu.File.ImportTimecodes;
            exitToolStripMenuItem.Text = _language.Menu.File.Exit;

            editToolStripMenuItem.Text = _language.Menu.Edit.Title;
            showHistoryforUndoToolStripMenuItem.Text = _language.Menu.Edit.ShowUndoHistory;

            toolStripMenuItemInsertUnicodeCharacter.Text = _language.Menu.Edit.InsertUnicodeSymbol;

            findToolStripMenuItem.Text = _language.Menu.Edit.Find;
            findNextToolStripMenuItem.Text = _language.Menu.Edit.FindNext;
            replaceToolStripMenuItem.Text = _language.Menu.Edit.Replace;
            multipleReplaceToolStripMenuItem.Text = _language.Menu.Edit.MultipleReplace;
            gotoLineNumberToolStripMenuItem.Text = _language.Menu.Edit.GoToSubtitleNumber;
            editSelectAllToolStripMenuItem.Text = _language.Menu.ContextMenu.SelectAll;

            toolsToolStripMenuItem.Text = _language.Menu.Tools.Title;
            adjustDisplayTimeToolStripMenuItem.Text = _language.Menu.Tools.AdjustDisplayDuration;
            fixToolStripMenuItem.Text = _language.Menu.Tools.FixCommonErrors;
            startNumberingFromToolStripMenuItem.Text = _language.Menu.Tools.StartNumberingFrom;
            removeTextForHearImparedToolStripMenuItem.Text = _language.Menu.Tools.RemoveTextForHearingImpaired;
            ChangeCasingToolStripMenuItem.Text = _language.Menu.Tools.ChangeCasing;
            toolStripMenuItemChangeFrameRate2.Text = _language.Menu.Tools.ChangeFrameRate;
            toolStripMenuItemAutoMergeShortLines.Text = _language.Menu.Tools.MergeShortLines;
            toolStripMenuItemAutoSplitLongLines.Text = _language.Menu.Tools.SplitLongLines;
            setMinimumDisplayTimeBetweenParagraphsToolStripMenuItem.Text = _language.Menu.Tools.MinimumDisplayTimeBetweenParagraphs;
            toolStripMenuItem1.Text = _language.Menu.Tools.SortBy;
            sortNumberToolStripMenuItem.Text = _languageGeneral.Number;
            sortStartTimeToolStripMenuItem.Text = _languageGeneral.StartTime;
            sortEndTimeToolStripMenuItem.Text = _languageGeneral.EndTime;
            sortDisplayTimeToolStripMenuItem.Text = _languageGeneral.Duration;
            sortTextAlphabeticallytoolStripMenuItem.Text = _language.Menu.Tools.TextAlphabetically;
            sortTextMaxLineLengthToolStripMenuItem.Text = _language.Menu.Tools.TextSingleLineMaximumLength;
            sortTextTotalLengthToolStripMenuItem.Text = _language.Menu.Tools.TextTotalLength;
            sortTextNumberOfLinesToolStripMenuItem.Text = _language.Menu.Tools.TextNumberOfLines;
            toolStripMenuItemShowOriginalInPreview.Text = _language.Menu.Tools.ShowOriginalTextInAudioAndVideoPreview;
            toolStripMenuItemMakeEmptyFromCurrent.Text = _language.Menu.Tools.MakeNewEmptyTranslationFromCurrentSubtitle;
            splitToolStripMenuItem.Text = _language.Menu.Tools.SplitSubtitle;
            appendTextVisuallyToolStripMenuItem.Text = _language.Menu.Tools.AppendSubtitle;

            toolStripMenuItemVideo.Text = _language.Menu.Video.Title;
            openVideoToolStripMenuItem.Text = _language.Menu.Video.OpenVideo;
            toolStripMenuItemSetAudioTrack.Text = _language.Menu.Video.ChooseAudioTrack;
            closeVideoToolStripMenuItem.Text = _language.Menu.Video.CloseVideo;

            if (Configuration.Settings.VideoControls.GenerateSpectrogram)
                showhideWaveFormToolStripMenuItem.Text = _language.Menu.Video.ShowHideWaveformAndSpectrogram;
            else
                showhideWaveFormToolStripMenuItem.Text = _language.Menu.Video.ShowHideWaveForm;

            showhideVideoToolStripMenuItem.Text = _language.Menu.Video.ShowHideVideo;
            undockVideoControlsToolStripMenuItem.Text = _language.Menu.Video.UnDockVideoControls;
            redockVideoControlsToolStripMenuItem.Text = _language.Menu.Video.ReDockVideoControls;

            toolStripMenuItemSpellCheckMain.Text = _language.Menu.SpellCheck.Title;
            spellCheckToolStripMenuItem.Text = _language.Menu.SpellCheck.SpellCheck;
            findDoubleWordsToolStripMenuItem.Text = _language.Menu.SpellCheck.FindDoubleWords;
            GetDictionariesToolStripMenuItem.Text = _language.Menu.SpellCheck.GetDictionaries;
            addWordToNamesetcListToolStripMenuItem.Text = _language.Menu.SpellCheck.AddToNamesEtcList;

            toolStripMenuItemSyncronization.Text = _language.Menu.Synchronization.Title;
            toolStripMenuItemAdjustAllTimes.Text = _language.Menu.Synchronization.AdjustAllTimes;
            visualSyncToolStripMenuItem.Text = _language.Menu.Synchronization.VisualSync;
            toolStripMenuItemPointSync.Text = _language.Menu.Synchronization.PointSync;
            pointSyncViaOtherSubtitleToolStripMenuItem.Text = _language.Menu.Synchronization.PointSyncViaOtherSubtitle;

            toolStripMenuItemAutoTranslate.Text = _language.Menu.AutoTranslate.Title;
            translateByGoogleToolStripMenuItem.Text = _language.Menu.AutoTranslate.TranslatePoweredByGoogle;
            translatepoweredByMicrosoftToolStripMenuItem.Text = _language.Menu.AutoTranslate.TranslatePoweredByMicrosoft;
            translateFromSwedishToDanishToolStripMenuItem.Text = _language.Menu.AutoTranslate.TranslateFromSwedishToDanish;

            optionsToolStripMenuItem.Text = _language.Menu.Options.Title;
            settingsToolStripMenuItem.Text = _language.Menu.Options.Settings;
            changeLanguageToolStripMenuItem.Text = _language.Menu.Options.ChooseLanguage;
            try
            {
                var ci = new System.Globalization.CultureInfo(_languageGeneral.CultureName);
                changeLanguageToolStripMenuItem.Text += " [" + ci.NativeName + "]";
            }
            catch
            { 
            }

            toolStripMenuItemNetworking.Text = _language.Menu.Networking.Title;
            startServerToolStripMenuItem.Text = _language.Menu.Networking.StartNewSession;
            joinSessionToolStripMenuItem.Text = _language.Menu.Networking.JoinSession;
            showSessionKeyLogToolStripMenuItem.Text = _language.Menu.Networking.ShowSessionInfoAndLog;
            chatToolStripMenuItem.Text = _language.Menu.Networking.Chat;
            leaveSessionToolStripMenuItem.Text = _language.Menu.Networking.LeaveSession;

            helpToolStripMenuItem.Text = _language.Menu.Help.Title;
            helpToolStripMenuItem1.Text = _language.Menu.Help.Help;
            aboutToolStripMenuItem.Text = _language.Menu.Help.About;

            toolStripButtonFileNew.ToolTipText = _language.Menu.ToolBar.New;
            toolStripButtonFileOpen.ToolTipText = _language.Menu.ToolBar.Open;
            toolStripButtonSave.ToolTipText = _language.Menu.ToolBar.Save;
            toolStripButtonSaveAs.ToolTipText = _language.Menu.ToolBar.SaveAs;
            toolStripButtonFind.ToolTipText = _language.Menu.ToolBar.Find;
            toolStripButtonReplace.ToolTipText = _language.Menu.ToolBar.Replace;
            toolStripButtonVisualSync.ToolTipText = _language.Menu.ToolBar.VisualSync;
            toolStripButtonSpellCheck.ToolTipText = _language.Menu.ToolBar.SpellCheck;
            toolStripButtonSettings.ToolTipText = _language.Menu.ToolBar.Settings;
            toolStripButtonHelp.ToolTipText = _language.Menu.ToolBar.Help;
            toolStripButtonToggleWaveForm.ToolTipText = _language.Menu.ToolBar.ShowHideWaveForm;
            toolStripButtonToggleVideo.ToolTipText = _language.Menu.ToolBar.ShowHideVideo;

            setStylesForSelectedLinesToolStripMenuItem.Text = _language.Menu.ContextMenu.SubStationAlphaSetStyle;
            toolStripMenuItemDelete.Text = _language.Menu.ContextMenu.Delete;
            insertLineToolStripMenuItem.Text = _language.Menu.ContextMenu.InsertFirstLine;
            toolStripMenuItemInsertBefore.Text = _language.Menu.ContextMenu.InsertBefore;
            toolStripMenuItemInsertAfter.Text = _language.Menu.ContextMenu.InsertAfter;
            toolStripMenuItemInsertSubtitle.Text = _language.Menu.ContextMenu.InsertSubtitleAfter;

            toolStripMenuItemCopySourceText.Visible = !string.IsNullOrEmpty(_language.Menu.ContextMenu.CopyToClipboard); //TODO: remove in 3.2 final
            toolStripMenuItemCopySourceText.Text = _language.Menu.ContextMenu.CopyToClipboard;

            splitLineToolStripMenuItem.Text = _language.Menu.ContextMenu.Split;
            toolStripMenuItemMergeLines.Text = _language.Menu.ContextMenu.MergeSelectedLines;
            mergeBeforeToolStripMenuItem.Text = _language.Menu.ContextMenu.MergeWithLineBefore;
            mergeAfterToolStripMenuItem.Text = _language.Menu.ContextMenu.MergeWithLineAfter;
            normalToolStripMenuItem.Text = _language.Menu.ContextMenu.Normal;
            boldToolStripMenuItem.Text = _languageGeneral.Bold;
            underlineToolStripMenuItem.Text = _language.Menu.ContextMenu.Underline;
            italicToolStripMenuItem.Text = _languageGeneral.Italic;
            colorToolStripMenuItem.Text = _language.Menu.ContextMenu.Color;
            toolStripMenuItemFont.Text = _language.Menu.ContextMenu.FontName;
            toolStripMenuItemAutoBreakLines.Text = _language.Menu.ContextMenu.AutoBalanceSelectedLines;
            toolStripMenuItemUnbreakLines.Text = _language.Menu.ContextMenu.RemoveLineBreaksFromSelectedLines;
            typeEffectToolStripMenuItem.Text = _language.Menu.ContextMenu.TypewriterEffect;
            karokeeEffectToolStripMenuItem.Text = _language.Menu.ContextMenu.KaraokeEffect;
            showSelectedLinesEarlierlaterToolStripMenuItem.Text = _language.Menu.ContextMenu.ShowSelectedLinesEarlierLater;
            visualSyncSelectedLinesToolStripMenuItem.Text = _language.Menu.ContextMenu.VisualSyncSelectedLines;
            toolStripMenuItemGoogleMicrosoftTranslateSelLine.Text = _language.Menu.ContextMenu.GoogleAndMicrosoftTranslateSelectedLine;
            googleTranslateSelectedLinesToolStripMenuItem.Text = _language.Menu.ContextMenu.GoogleTranslateSelectedLines;
            adjustDisplayTimeForSelectedLinesToolStripMenuItem.Text = _language.Menu.ContextMenu.AdjustDisplayDurationForSelectedLines;
            fixCommonErrorsInSelectedLinesToolStripMenuItem.Text = _language.Menu.ContextMenu.FixCommonErrorsInSelectedLines;
            changeCasingForSelectedLinesToolStripMenuItem.Text = _language.Menu.ContextMenu.ChangeCasingForSelectedLines;

            // textbox context menu
            cutToolStripMenuItem.Text = _language.Menu.ContextMenu.Cut;
            copyToolStripMenuItem.Text = _language.Menu.ContextMenu.Copy;
            pasteToolStripMenuItem.Text = _language.Menu.ContextMenu.Paste;
            deleteToolStripMenuItem.Text = _language.Menu.ContextMenu.Delete;
            selectAllToolStripMenuItem.Text = _language.Menu.ContextMenu.SelectAll;
            normalToolStripMenuItem1.Text = _language.Menu.ContextMenu.Normal;
            boldToolStripMenuItem1.Text = _languageGeneral.Bold;
            italicToolStripMenuItem1.Text = _languageGeneral.Italic;
            underlineToolStripMenuItem1.Text = _language.Menu.ContextMenu.Underline;
            colorToolStripMenuItem1.Text = _language.Menu.ContextMenu.Color;
            fontNameToolStripMenuItem.Text = _language.Menu.ContextMenu.FontName;
            toolStripMenuItemInsertUnicodeSymbol.Text = _language.Menu.Edit.InsertUnicodeSymbol;

            // main controls
            SubtitleListview1.InitializeLanguage(_languageGeneral, Configuration.Settings);
            toolStripLabelSubtitleFormat.Text = _language.Controls.SubtitleFormat;
            toolStripLabelEncoding.Text = _language.Controls.FileEncoding;            
            tabControlSubtitle.TabPages[0].Text = _language.Controls.ListView;
            tabControlSubtitle.TabPages[1].Text = _language.Controls.SourceView;
            labelDuration.Text = _languageGeneral.Duration;
            labelStartTime.Text = _languageGeneral.StartTime;
            labelText.Text = _languageGeneral.Text;
            toolStripLabelFrameRate.Text = _languageGeneral.FrameRate;
            buttonUndoListViewChanges.Text = _language.Controls.UndoChangesInEditPanel;
            buttonPrevious.Text = _language.Controls.Previous;
            buttonNext.Text = _language.Controls.Next;
            buttonAutoBreak.Text = _language.Controls.AutoBreak;
            buttonUnBreak.Text = _language.Controls.Unbreak;
            buttonSplitLine.Text = _languageGeneral.SplitLine;
            ShowSourceLineNumber();

            // Video controls
            tabPageTranslate.Text = _language.VideoControls.Translate;
            tabPageCreate.Text = _language.VideoControls.Create;
            tabPageAdjust.Text = _language.VideoControls.Adjust;
            checkBoxSyncListViewWithVideoWhilePlaying.Text = _language.VideoControls.SelectCurrentElementWhilePlaying;
            if (_videoFileName == null)
                labelVideoInfo.Text = Configuration.Settings.Language.General.NoVideoLoaded;
            toolStripButtonLockCenter.Text = _language.VideoControls.Center;
            toolStripSplitButtonPlayRate.Text = _language.VideoControls.PlayRate;
            toolStripMenuItemPlayRateSlow.Text = _language.VideoControls.Slow;
            toolStripMenuItemPlayRateNormal.Text = _language.VideoControls.Normal;
            toolStripMenuItemPlayRateFast.Text = _language.VideoControls.Fast;
            toolStripMenuItemPlayRateVeryFast.Text = _language.VideoControls.VeryFast;

            groupBoxAutoRepeat.Text = _language.VideoControls.AutoRepeat;
            checkBoxAutoRepeatOn.Text = _language.VideoControls.AutoRepeatOn;
            labelAutoRepeatCount.Text = _language.VideoControls.AutoRepeatCount;
            groupBoxAutoContinue.Text = _language.VideoControls.AutoContinue;
            checkBoxAutoContinue.Text = _language.VideoControls.AutoContinueOn;
            labelAutoContinueDelay.Text = _language.VideoControls.DelayInSeconds;
            buttonPlayPrevious.Text = _language.VideoControls.Previous;
            buttonPlayCurrent.Text = _language.VideoControls.PlayCurrent;
            buttonPlayNext.Text = _language.VideoControls.Next;
            buttonStop.Text = _language.VideoControls.Pause;
            groupBoxTranslateSearch.Text = _language.VideoControls.SearchTextOnline;
            buttonGoogleIt.Text = _language.VideoControls.GoogleIt;
            buttonGoogleTranslateIt.Text = _language.VideoControls.GoogleTranslate;
            labelTranslateTip.Text = _language.VideoControls.TranslateTip;

            buttonInsertNewText.Text = _language.VideoControls.InsertNewSubtitleAtVideoPosition;
            buttonBeforeText.Text = _language.VideoControls.PlayFromJustBeforeText;
            buttonGotoSub.Text = _language.VideoControls.GoToSubtitlePositionAndPause;
            buttonSetStartTime.Text = _language.VideoControls.SetStartTime;
            buttonSetEnd.Text = _language.VideoControls.SetEndTime;
            buttonSecBack1.Text = _language.VideoControls.SecondsBackShort;
            buttonSecBack2.Text = _language.VideoControls.SecondsBackShort;
            buttonForward1.Text = _language.VideoControls.SecondsForwardShort;
            buttonForward2.Text = _language.VideoControls.SecondsForwardShort;
            labelVideoPosition.Text = _language.VideoControls.VideoPosition;
            labelVideoPosition2.Text = _language.VideoControls.VideoPosition;
            labelCreateTip.Text = _language.VideoControls.CreateTip;

            buttonSetStartAndOffsetRest.Text = _language.VideoControls.SetstartTimeAndOffsetOfRest;
            buttonSetEndAndGoToNext.Text = _language.VideoControls.SetEndTimeAndGoToNext;
            buttonAdjustSetStartTime.Text = _language.VideoControls.SetStartTime;
            buttonAdjustSetEndTime.Text = _language.VideoControls.SetEndTime;
            buttonAdjustPlayBefore.Text = _language.VideoControls.PlayFromJustBeforeText;
            buttonAdjustGoToPosAndPause.Text = _language.VideoControls.GoToSubtitlePositionAndPause;
            buttonAdjustSecBack1.Text = _language.VideoControls.SecondsBackShort;
            buttonAdjustSecBack2.Text = _language.VideoControls.SecondsBackShort;
            buttonAdjustSecForward1.Text = _language.VideoControls.SecondsForwardShort;
            buttonAdjustSecForward2.Text = _language.VideoControls.SecondsForwardShort;
            labelAdjustTip.Text = _language.VideoControls.CreateTip;

            //waveform
            addParagraphHereToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.AddParagraphHere;
            deleteParagraphToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.DeleteParagraph;
            splitToolStripMenuItem1.Text = Configuration.Settings.Language.WaveForm.Split;
            mergeWithPreviousToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.MergeWithPrevious;
            mergeWithNextToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.MergeWithNext;
            toolStripMenuItemWaveFormPlaySelection.Text = Configuration.Settings.Language.WaveForm.PlaySelection;
            showWaveformAndSpectrogramToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.ShowWaveformAndSpectrogram;
            showOnlyWaveformToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.ShowWaveformOnly;
            showOnlySpectrogramToolStripMenuItem.Text = Configuration.Settings.Language.WaveForm.ShowSpectrogramOnly;

            toolStripButtonWaveFormZoomOut.ToolTipText = Configuration.Settings.Language.WaveForm.ZoomOut;
            toolStripButtonWaveFormZoomIn.ToolTipText = Configuration.Settings.Language.WaveForm.ZoomIn;

            if (Configuration.Settings.VideoControls.GenerateSpectrogram)
                audioVisualizer.WaveFormNotLoadedText = Configuration.Settings.Language.WaveForm.ClickToAddWaveformAndSpectrogram;
            else 
                audioVisualizer.WaveFormNotLoadedText = Configuration.Settings.Language.WaveForm.ClickToAddWaveForm;
        }

        private void SetFormatToSubRip()
        {
            comboBoxSubtitleFormats.SelectedIndexChanged -= ComboBoxSubtitleFormatsSelectedIndexChanged;
            foreach (SubtitleFormat format in SubtitleFormat.AllSubtitleFormats)
            {
                if (!format.IsVobSubIndexFile)
                    comboBoxSubtitleFormats.Items.Add(format.FriendlyName);
            }
            comboBoxSubtitleFormats.SelectedIndex = 0;
            comboBoxSubtitleFormats.SelectedIndexChanged += ComboBoxSubtitleFormatsSelectedIndexChanged;
        }

        private int FirstSelectedIndex
        {
            get
            {
                if (SubtitleListview1.SelectedItems.Count == 0)
                    return -1;
                return SubtitleListview1.SelectedItems[0].Index;
            }
        }

        private int FirstVisibleIndex
        {
            get
            {
                if (SubtitleListview1.Items.Count == 0)
                    return -1;
                return SubtitleListview1.TopItem.Index;
            }
        }

        private bool ContinueNewOrExit()
        {
            if (_change)
            {
                string promptText = _language.SaveChangesToUntitled;
                if (!string.IsNullOrEmpty(_fileName))
                    promptText = string.Format(_language.SaveChangesToX, _fileName);

                DialogResult dr = MessageBox.Show(promptText, Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);

                if (dr == DialogResult.Cancel)
                    return false;

                if (dr == DialogResult.Yes)
                {
                    if (string.IsNullOrEmpty(_fileName))
                    {
                        if (!string.IsNullOrEmpty(openFileDialog1.InitialDirectory))
                            saveFileDialog1.InitialDirectory = openFileDialog1.InitialDirectory;
                        saveFileDialog1.Title = _language.SaveSubtitleAs;
                        if (saveFileDialog1.ShowDialog(this) == DialogResult.OK)
                        {
                            openFileDialog1.InitialDirectory = saveFileDialog1.InitialDirectory;
                            _fileName = saveFileDialog1.FileName;
                            SetTitle();
                            Configuration.Settings.RecentFiles.Add(_fileName, FirstVisibleIndex, FirstSelectedIndex, _videoFileName, _subtitleAlternateFileName);
                            Configuration.Settings.Save();

                        }
                        else
                        {
                            return false;
                        }
                    }
                    if (SaveSubtitle(GetCurrentSubtitleFormat()) != DialogResult.OK)
                        return false;
                }
            }

            return ContinueNewOrExitAlternate();
        }

        private bool ContinueNewOrExitAlternate()
        {
            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0 && _changeAlternate)
            {
                string promptText = _language.SaveChangesToUntitledOriginal;
                if (!string.IsNullOrEmpty(_subtitleAlternateFileName))
                    promptText = string.Format(_language.SaveChangesToOriginalX, _subtitleAlternateFileName);

                DialogResult dr = MessageBox.Show(promptText, Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);

                if (dr == DialogResult.Cancel)
                    return false;

                if (dr == DialogResult.Yes)
                {
                    if (string.IsNullOrEmpty(_subtitleAlternateFileName))
                    {
                        if (!string.IsNullOrEmpty(openFileDialog1.InitialDirectory))
                            saveFileDialog1.InitialDirectory = openFileDialog1.InitialDirectory;
                        saveFileDialog1.Title = _language.SaveOriginalSubtitleAs;
                        if (saveFileDialog1.ShowDialog(this) == DialogResult.OK)
                        {
                            openFileDialog1.InitialDirectory = saveFileDialog1.InitialDirectory;
                            _subtitleAlternateFileName = saveFileDialog1.FileName;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    if (SaveOriginalSubtitle(GetCurrentSubtitleFormat()) != DialogResult.OK)
                        return false;
                }
            }
            return true;
        }

        private void ExitToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Application.Exit();
        }

        private void AboutToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            new About().ShowDialog(this);
        }

        private void VisualSyncToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            ShowVisualSync(false);
        }

        public void MakeHistoryForUndo(string description)
        {
            _subtitle.MakeHistoryForUndo(description, GetCurrentSubtitleFormat(), _fileDateTime);
        }

        /// <summary>
        /// Add undo history - but only if nothing happens for half a second
        /// </summary>
        /// <param name="description">Undo description</param>
        public void MakeHistoryForUndoWhenNoMoreChanges(string description)
        {
            _timerAddHistoryWhenDone.Stop();
            _timerAddHistoryWhenDoneText = description;
            _timerAddHistoryWhenDone.Start();
        }

        private void ShowVisualSync(bool onlySelectedLines)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                var visualSync = new VisualSync();
                _formPositionsAndSizes.SetPositionAndSize(visualSync);
                visualSync.VideoFileName = _videoFileName;
                visualSync.AudioTrackNumber = _videoAudioTrackNumber;

                SaveSubtitleListviewIndexes();
                if (onlySelectedLines)
                {
                    var selectedLines = new Subtitle { WasLoadedWithFrameNumbers = _subtitle.WasLoadedWithFrameNumbers };
                    foreach (int index in SubtitleListview1.SelectedIndices)
                        selectedLines.Paragraphs.Add(_subtitle.Paragraphs[index]);
                    visualSync.Initialize(toolStripButtonVisualSync.Image as Bitmap, selectedLines, _fileName, _language.VisualSyncSelectedLines, CurrentFrameRate);
                }
                else
                {
                    visualSync.Initialize(toolStripButtonVisualSync.Image as Bitmap, _subtitle, _fileName, _language.VisualSyncTitle, CurrentFrameRate);
                }

                _endSeconds = -1;
                mediaPlayer.Pause();
                if (visualSync.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeVisualSync);

                    if (onlySelectedLines)
                    { // we only update selected lines
                        int i = 0;
                        foreach (int index in SubtitleListview1.SelectedIndices)
                        {
                            _subtitle.Paragraphs[index] = visualSync.Paragraphs[i];
                            i++;
                        }
                        ShowStatus(_language.VisualSyncPerformedOnSelectedLines);
                    }
                    else
                    {
                        _subtitle.Paragraphs.Clear();
                        foreach (Paragraph p in visualSync.Paragraphs)
                            _subtitle.Paragraphs.Add(new Paragraph(p));
                        ShowStatus(_language.VisualSyncPerformed);
                    }
                    if (visualSync.FrameRateChanged)
                        toolStripComboBoxFrameRate.Text = visualSync.FrameRate.ToString();
                    if (IsFramesRelevant && visualSync.FrameRate > 0)
                        _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(CurrentFrameRate);
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                    if (onlySelectedLines && SubtitleListview1.SelectedItems.Count > 0)
                    {
                        SubtitleListview1.EnsureVisible(SubtitleListview1.SelectedItems[SubtitleListview1.SelectedItems.Count - 1].Index);
                    }
                }
                _videoFileName = visualSync.VideoFileName;
                _formPositionsAndSizes.SavePositionAndSize(visualSync);
                visualSync.Dispose();
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        private void OpenToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            OpenNewFile();
        }

        private void OpenNewFile()
        {
            openFileDialog1.Title = _languageGeneral.OpenSubtitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK) 
            {
                OpenSubtitle(openFileDialog1.FileName, null);
            }
        }

        public double CurrentFrameRate
        {
            get
            {
                double f;
                if (double.TryParse(toolStripComboBoxFrameRate.Text, out f))
                    return f;
                return Configuration.Settings.General.DefaultFrameRate;
            }
        }

        private void OpenSubtitle(string fileName, Encoding encoding)
        {
            OpenSubtitle(fileName, encoding, null, null);
        }

        private void OpenSubtitle(string fileName, Encoding encoding, string videoFileName, string originalFileName)
        {
            if (File.Exists(fileName))
            {
                bool videoFileLoaded = false;

                // save last first visible index + first selected index from listview
                if (!string.IsNullOrEmpty(_fileName))
                    Configuration.Settings.RecentFiles.Add(_fileName, FirstVisibleIndex, FirstSelectedIndex, _videoFileName, originalFileName);

                openFileDialog1.InitialDirectory = Path.GetDirectoryName(fileName);

                if (Path.GetExtension(fileName).ToLower() == ".sub" && IsVobSubFile(fileName, false))
                {
                    if (MessageBox.Show(_language.ImportThisVobSubSubtitle, _title, MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        ImportAndOcrVobSubSubtitleNew(fileName);
                    }
                    return;
                }

                if (Path.GetExtension(fileName).ToLower() == ".sup" && IsBluRaySupFile(fileName))
                {
                    ImportAndOcrBluRaySup(fileName);
                    return;
                }

                if (Path.GetExtension(fileName).ToLower() == ".mkv")
                {
                    ImportSubtitleFromMatroskaFile();                    
                    return;
                }

                var fi = new FileInfo(fileName);
                if (fi.Length > 1024 * 1024 * 10) // max 10 mb
                {
                    if (MessageBox.Show(string.Format(_language.FileXIsLargerThan10Mb + Environment.NewLine +
                                                      Environment.NewLine +
                                                      _language.ContinueAnyway,
                                                      fileName), Title, MessageBoxButtons.YesNoCancel) != DialogResult.Yes)
                        return;
                }
                 
                if (_subtitle.HistoryItems.Count > 0 || _subtitle.Paragraphs.Count > 0)
                    MakeHistoryForUndo(string.Format(_language.BeforeLoadOf, Path.GetFileName(fileName)));

                SubtitleFormat format = _subtitle.LoadSubtitle(fileName, out encoding, encoding);

                bool justConverted = false;
                if (format == null)
                {
                    var ebu = new Ebu();
                    if (ebu.IsMine(null, fileName))
                    {
                        ebu.LoadSubtitle(_subtitle, null, fileName);
                        _oldSubtitleFormat = ebu;
                        SetFormatToSubRip();
                        justConverted = true;
                        format = GetCurrentSubtitleFormat();
                    }
                }

                if (format == null)
                {
                    var pac = new Pac();
                    if (pac.IsMine(null, fileName))
                    {
                        pac.LoadSubtitle(_subtitle, null, fileName);
                        _oldSubtitleFormat = pac;
                        SetFormatToSubRip();
                        justConverted = true;
                        format = GetCurrentSubtitleFormat();
                    }
                }

                if (format == null)
                {
                    var cavena890 = new Cavena890();
                    if (cavena890.IsMine(null, fileName))
                    {
                        cavena890.LoadSubtitle(_subtitle, null, fileName);
                        _oldSubtitleFormat = cavena890;
                        SetFormatToSubRip();
                        justConverted = true;
                        format = GetCurrentSubtitleFormat();
                    }
                }

                if (format == null)
                {
                    var spt = new Spt();
                    if (spt.IsMine(null, fileName))
                    {
                        spt.LoadSubtitle(_subtitle, null, fileName);
                        _oldSubtitleFormat = spt;
                        SetFormatToSubRip();
                        justConverted = true;
                        format = GetCurrentSubtitleFormat();
                    }
                }

                if (format == null)
                {
                    var bdnXml = new BdnXml();
                    string[] arr = File.ReadAllLines(fileName);
                    List<string> list = new List<string>();
                    foreach (string l in arr)
                        list.Add(l);
                    if (bdnXml.IsMine(list, fileName))
                    {
                        if (ContinueNewOrExit())
                        {
                            ImportAndOcrBdnXml(fileName, bdnXml, list);
                        }
                        return;
                    }
                }


                _fileDateTime = File.GetLastWriteTime(fileName);

                if (GetCurrentSubtitleFormat().IsFrameBased)
                    _subtitle.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                else
                    _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);

                if (format != null)
                {
                    if (Configuration.Settings.General.RemoveBlankLinesWhenOpening)
                    {
                        _subtitle.RemoveEmptyLines();
                    }

                    _subtitleListViewIndex = -1;

                    if (format.FriendlyName == new Sami().FriendlyName)
                        encoding = Encoding.Default;

                    SetCurrentFormat(format);

                    _subtitleAlternateFileName = null;
                    if (LoadAlternateSubtitleFile(originalFileName))
                        _subtitleAlternateFileName = originalFileName;

                    textBoxSource.Text = _subtitle.ToText(format);
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    if (SubtitleListview1.Items.Count > 0)
                        SubtitleListview1.Items[0].Selected = true;
                    _findHelper = null;
                    _spellCheckForm = null;
                    _videoFileName = null;
                    _videoAudioTrackNumber = -1;
                    labelVideoInfo.Text = Configuration.Settings.Language.General.NoVideoLoaded;
                    audioVisualizer.WavePeaks = null;
                    audioVisualizer.ResetSpectrogram();
                    audioVisualizer.Invalidate();

                    if (Configuration.Settings.General.ShowVideoPlayer || Configuration.Settings.General.ShowWaveForm)
                    {
                        if (!string.IsNullOrEmpty(videoFileName) && File.Exists(videoFileName))
                        {
                            OpenVideo(videoFileName);
                        }
                        else if (!string.IsNullOrEmpty(fileName) && (toolStripButtonToggleVideo.Checked || toolStripButtonToggleWaveForm.Checked))
                        {
                            TryToFindAndOpenVideoFile(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName)));
                        }
                    }
                    videoFileLoaded = _videoFileName != null;
                    

                    if (Configuration.Settings.RecentFiles.Files.Count > 0 &&
                        Configuration.Settings.RecentFiles.Files[0].FileName == fileName)
                    {
                    }
                    else
                    {
                        Configuration.Settings.RecentFiles.Add(fileName, _videoFileName, _subtitleAlternateFileName);
                        Configuration.Settings.Save();
                        UpdateRecentFilesUI();
                    }
                    _fileName = fileName;
                    SetTitle();
                    ShowStatus(string.Format(_language.LoadedSubtitleX, _fileName));
                    _sourceViewChange = false;
                    _change = false;
                    _converted = false;

                    SetUndockedWindowsTitle();

                    if (justConverted)
                    {
                        _converted = true;
                        ShowStatus(string.Format(_language.LoadedSubtitleX, _fileName) + " - " + string.Format(_language.ConvertedToX, format.FriendlyName));                        
                    }

                    if (encoding == Encoding.UTF7)
                        comboBoxEncoding.Text = "UTF-7";
                    else if (encoding == Encoding.UTF8)
                        comboBoxEncoding.Text = "UTF-8";
                    else if (encoding == System.Text.Encoding.Unicode)
                        comboBoxEncoding.Text = "Unicode";
                    else if (encoding == System.Text.Encoding.BigEndianUnicode)
                        comboBoxEncoding.Text = "Unicode (big endian)";
                    else
                    {
                        comboBoxEncoding.Items[0] = "ANSI - " + encoding.CodePage.ToString();
                        comboBoxEncoding.SelectedIndex = 0;
                    }
                }
                else
                {
                    var info = new FileInfo(fileName);
                    if (info.Length < 50)
                    {
                        _findHelper = null;
                        _spellCheckForm = null;
                        _videoFileName = null;
                        _videoAudioTrackNumber = -1;
                        labelVideoInfo.Text = Configuration.Settings.Language.General.NoVideoLoaded;
                        audioVisualizer.WavePeaks = null;
                        audioVisualizer.ResetSpectrogram();
                        audioVisualizer.Invalidate();

                        Configuration.Settings.RecentFiles.Add(fileName, FirstVisibleIndex, FirstSelectedIndex, _videoFileName, _subtitleAlternateFileName);
                        Configuration.Settings.Save();
                        UpdateRecentFilesUI();
                        _fileName = fileName;
                        SetTitle();
                        ShowStatus(string.Format(_language.LoadedEmptyOrShort, _fileName));
                        _sourceViewChange = false;
                        _change = false;
                        _converted = false;

                        MessageBox.Show(_language.FileIsEmptyOrShort);
                    }
                    else
                        ShowUnknownSubtitle();
                }

                if (!videoFileLoaded && mediaPlayer.VideoPlayer != null)
                {
                        mediaPlayer.VideoPlayer.DisposeVideoPlayer();
                        mediaPlayer.VideoPlayer = null;
                        timer1.Stop();
                }
            }
            else
            {
                MessageBox.Show(string.Format(_language.FileNotFound, fileName));
            }
        }

        private void SetUndockedWindowsTitle()
        {
            string title = Configuration.Settings.Language.General.NoVideoLoaded;
            if (!string.IsNullOrEmpty(_videoFileName))
                title = Path.GetFileNameWithoutExtension(_videoFileName);

            try //TODO: Remove in 3.2 final
            {
                if (_videoControlsUnDocked != null && !_videoControlsUnDocked.IsDisposed)
                    _videoControlsUnDocked.Text = string.Format(Configuration.Settings.Language.General.ControlsWindowTitle, title);

                if (_videoPlayerUnDocked != null && !_videoPlayerUnDocked.IsDisposed)
                    _videoPlayerUnDocked.Text = string.Format(Configuration.Settings.Language.General.VideoWindowTitle, title);

                if (_waveFormUnDocked != null && !_waveFormUnDocked.IsDisposed)
                    _waveFormUnDocked.Text = string.Format(Configuration.Settings.Language.General.AudioWindowTitle, title);
            }
            catch
            { 
            }
        }

        private void ImportAndOcrBdnXml(string fileName, BdnXml bdnXml, List<string> list)
        {
            Subtitle bdnSubtitle = new Subtitle();
            bdnXml.LoadSubtitle(bdnSubtitle, list, fileName);
            bdnSubtitle.FileName = fileName;
            var formSubOcr = new VobSubOcr();
            formSubOcr.Initialize(bdnSubtitle, Configuration.Settings.VobSubOcr);
            if (formSubOcr.ShowDialog(this) == DialogResult.OK)
            {
                MakeHistoryForUndo(_language.BeforeImportingBdnXml);
                FileNew();
                _subtitle.Paragraphs.Clear();
                SetCurrentFormat(new SubRip().FriendlyName);
                _subtitle.WasLoadedWithFrameNumbers = false;
                _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                foreach (Paragraph p in formSubOcr.SubtitleFromOcr.Paragraphs)
                {
                    _subtitle.Paragraphs.Add(p);
                }

                ShowSource();
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                _change = true;
                _subtitleListViewIndex = -1;
                SubtitleListview1.FirstVisibleIndex = -1;
                SubtitleListview1.SelectIndexAndEnsureVisible(0);

                _fileName = Path.ChangeExtension(formSubOcr.FileName, ".srt");
                SetTitle();
                _converted = true;
            }
        }

        private void ShowUnknownSubtitle()
        {
            var unknownSubtitle = new UnknownSubtitle();
            unknownSubtitle.Initialize(Title);
            unknownSubtitle.ShowDialog(this);
        }

        private void UpdateRecentFilesUI()
        {
            reopenToolStripMenuItem.DropDownItems.Clear();
            if (Configuration.Settings.General.ShowRecentFiles &&
                Configuration.Settings.RecentFiles.Files.Count > 0)
            {
                reopenToolStripMenuItem.Visible = true;
                foreach (var file in Configuration.Settings.RecentFiles.Files)
                {
                    if (File.Exists(file.FileName))
                        reopenToolStripMenuItem.DropDownItems.Add(file.FileName, null, ReopenSubtitleToolStripMenuItemClick);
                }
            }
            else
            {
                Configuration.Settings.RecentFiles.Files.Clear();
                reopenToolStripMenuItem.Visible = false;
            }
        }

        private void ReopenSubtitleToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            var item = sender as ToolStripItem;

            if (ContinueNewOrExit())
            {
                RecentFileEntry rfe = null;
                foreach (var file in Configuration.Settings.RecentFiles.Files)
                {
                    if (file.FileName == item.Text)
                        rfe = file;
                }

                if (rfe == null)
                    OpenSubtitle(item.Text, null);
                else
                    OpenSubtitle(rfe.FileName, null, rfe.VideoFileName, rfe.OriginalFileName);
                SetRecentIndecies(item.Text);
                GotoSubPosAndPause();
            }
        }

        private void GotoSubPosAndPause()
        {
            if (!string.IsNullOrEmpty(_videoFileName))
            {
                _videoLoadedGoToSubPosAndPause = true;
            }
            else
            {
                mediaPlayer.SubtitleText = string.Empty;
            }
        }

        private void SetRecentIndecies(string fileName)
        {
            if (!Configuration.Settings.General.RememberSelectedLine)
                return;

            foreach (var x in Configuration.Settings.RecentFiles.Files)
            {
                if (string.Compare(fileName, x.FileName, true) == 0)
                {
                    int sIndex = x.FirstSelectedIndex;
                    if (sIndex >= 0 && sIndex < SubtitleListview1.Items.Count)
                    {
                        SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
                        for (int i = 0; i < SubtitleListview1.Items.Count; i++)
                            SubtitleListview1.Items[i].Selected = i == sIndex;
                        _subtitleListViewIndex = -1;
                        SubtitleListview1.EnsureVisible(sIndex);
                        SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                        SubtitleListview1.Items[sIndex].Focused = true;
                    }

                    int topIndex = x.FirstVisibleIndex;
                    if (topIndex >= 0 && topIndex < SubtitleListview1.Items.Count)
                    {
                        // to fix bug in .net framework we have to set topitem 3 times... wtf!?
                        SubtitleListview1.TopItem = SubtitleListview1.Items[topIndex];
                        SubtitleListview1.TopItem = SubtitleListview1.Items[topIndex];
                        SubtitleListview1.TopItem = SubtitleListview1.Items[topIndex];
                    }

                    RefreshSelectedParagraph();
                    break;
                }
            }
        }

        private void SaveToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            SaveSubtitle(GetCurrentSubtitleFormat());
        }

        private void SaveAsToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            FileSaveAs();
        }

        private DialogResult FileSaveAs()
        {
            SubtitleFormat currentFormat = GetCurrentSubtitleFormat();
            Utilities.SetSaveDialogFilter(saveFileDialog1, currentFormat);

            var ebu = new Ebu();
            saveFileDialog1.Filter += "| " + ebu.FriendlyName + "|*" + ebu.Extension;

            var pac = new Pac();
            saveFileDialog1.Filter += "| " + pac.FriendlyName + "|*" + pac.Extension;

            var cavena890 = new Cavena890();
            saveFileDialog1.Filter += "| " + cavena890.FriendlyName + "|*" + cavena890.Extension;

            saveFileDialog1.Title = _language.SaveSubtitleAs;
            saveFileDialog1.DefaultExt = "*" + currentFormat.Extension;
            saveFileDialog1.AddExtension = true;               
 
            if (!string.IsNullOrEmpty(_videoFileName))
                saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(_videoFileName);
            else
                saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(_fileName);

            if (!string.IsNullOrEmpty(openFileDialog1.InitialDirectory))
                saveFileDialog1.InitialDirectory = openFileDialog1.InitialDirectory;

            DialogResult result = saveFileDialog1.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                openFileDialog1.InitialDirectory = saveFileDialog1.InitialDirectory;
                if (saveFileDialog1.FilterIndex == SubtitleFormat.AllSubtitleFormats.Count + 1)
                {
                    string fileName = saveFileDialog1.FileName;
                    string ext = Path.GetExtension(fileName).ToLower();
                    bool extOk = ext == ebu.Extension.ToLower();
                    if (!extOk)
                    {
                        if (fileName.EndsWith("."))
                            fileName = fileName.Substring(0, fileName.Length - 1);
                        fileName += ebu.Extension;
                    }
                    ebu.Save(fileName, _subtitle);
                    return DialogResult.OK;
                }
                else if (saveFileDialog1.FilterIndex == SubtitleFormat.AllSubtitleFormats.Count + 2)
                {
                    string fileName = saveFileDialog1.FileName;
                    string ext = Path.GetExtension(fileName).ToLower();
                    bool extOk = ext == pac.Extension.ToLower();
                    if (!extOk)
                    {
                        if (fileName.EndsWith("."))
                            fileName = fileName.Substring(0, fileName.Length - 1);
                        fileName += pac.Extension;
                    }
                    pac.Save(fileName, _subtitle);
                    return DialogResult.OK;
                }
                else if (saveFileDialog1.FilterIndex == SubtitleFormat.AllSubtitleFormats.Count + 3)
                {
                    string fileName = saveFileDialog1.FileName;
                    string ext = Path.GetExtension(fileName).ToLower();
                    bool extOk = ext == cavena890.Extension.ToLower();
                    if (!extOk)
                    {
                        if (fileName.EndsWith("."))
                            fileName = fileName.Substring(0, fileName.Length - 1);
                        fileName += cavena890.Extension;
                    }
                    cavena890.Save(fileName, _subtitle);
                    return DialogResult.OK;
                }

                _converted = false;
                _fileName = saveFileDialog1.FileName;

                _fileDateTime = File.GetLastWriteTime(_fileName);
                SetTitle();
                Configuration.Settings.RecentFiles.Add(_fileName, FirstVisibleIndex, FirstSelectedIndex, _videoFileName, _subtitleAlternateFileName);
                Configuration.Settings.Save();

                int index = 0;
                foreach (SubtitleFormat format in SubtitleFormat.AllSubtitleFormats)
                {
                    if (saveFileDialog1.FilterIndex == index + 1)
                    {
                        // only allow current extension or ".txt"
                        string ext = Path.GetExtension(_fileName).ToLower();
                        bool extOk = ext == format.Extension.ToLower() || format.AlternateExtensions.Contains(ext) || ext == ".txt";
                        if (!extOk)
                        {
                            if (_fileName.EndsWith("."))
                                _fileName = _fileName.Substring(0, _fileName.Length - 1);
                            _fileName += format.Extension;
                        }
                        
                        if (SaveSubtitle(format) == DialogResult.OK)
                            SetCurrentFormat(format);
                    }
                    index++;
                }
            }
            return result;
        }

        private Encoding GetCurrentEncoding()
        {
            if (comboBoxEncoding.Text == "UTF-7")
                return System.Text.Encoding.UTF7;
            if (comboBoxEncoding.Text == "UTF-8")
                return System.Text.Encoding.UTF8;
            else if (comboBoxEncoding.Text == "Unicode")
                return System.Text.Encoding.Unicode;
            else if (comboBoxEncoding.Text == "Unicode (big endian)")
                return System.Text.Encoding.BigEndianUnicode;
            else 
            {
                if (comboBoxEncoding.Text.StartsWith("ANSI - "))
                {
                    string codePage = comboBoxEncoding.Text.Substring(6).Trim();
                    int codePageNumber = 0;
                    if (int.TryParse(codePage, out codePageNumber))
                    {
                        return Encoding.GetEncoding(codePageNumber);
                    }
                }
                return System.Text.Encoding.Default;
            }
        }

        private DialogResult SaveSubtitle(SubtitleFormat format)
        {           
            if (string.IsNullOrEmpty(_fileName) || _converted)
                return FileSaveAs();

            try
            {
                string allText = _subtitle.ToText(format);
                var currentEncoding = GetCurrentEncoding();
                if (currentEncoding == Encoding.Default && (allText.Contains("♪") || allText.Contains("♫") | allText.Contains("♥"))) // ANSI & music/unicode symbols
                {
                    if (MessageBox.Show(string.Format(_language.UnicodeMusicSymbolsAnsiWarning), Title, MessageBoxButtons.YesNo) == DialogResult.No)
                        return DialogResult.No;
                }


                bool containsNegativeTime = false;
                foreach (var p in _subtitle.Paragraphs)
                {
                    if (p.StartTime.TotalMilliseconds < 0 || p.EndTime.TotalMilliseconds < 0)
                    {
                        containsNegativeTime = true;
                        break;
                    }
                }
                if (containsNegativeTime && !string.IsNullOrEmpty(_language.NegativeTimeWarning))
                {
                    if (MessageBox.Show(_language.NegativeTimeWarning, Title, MessageBoxButtons.YesNo) == DialogResult.No)
                        return DialogResult.No;
                }

                if (File.Exists(_fileName))
                {
                    DateTime fileOnDisk = File.GetLastWriteTime(_fileName);
                    if (_fileDateTime != fileOnDisk && _fileDateTime != new DateTime())
                    {
                        if (MessageBox.Show(string.Format(_language.OverwriteModifiedFile,
                                                          _fileName, fileOnDisk.ToShortDateString(), fileOnDisk.ToString("HH:mm:ss"),
                                                          Environment.NewLine, _fileDateTime.ToShortDateString(), _fileDateTime.ToString("HH:mm:ss")),
                                             Title + " - " + _language.FileOnDiskModified, MessageBoxButtons.YesNo) == DialogResult.No)
                            return DialogResult.No;
                    }
                    File.Delete(_fileName);
                }

                File.WriteAllText(_fileName, allText, currentEncoding);
                _fileDateTime = File.GetLastWriteTime(_fileName);
                ShowStatus(string.Format(_language.SavedSubtitleX, _fileName));
                _change = false;
                return DialogResult.OK;
            }
            catch (Exception exception)
            {
                MessageBox.Show(string.Format(_language.UnableToSaveSubtitleX, _fileName));
                System.Diagnostics.Debug.Write(exception.Message);
                return DialogResult.Cancel;
            }
        }

        private DialogResult SaveOriginalSubtitle(SubtitleFormat format)
        {
            try
            {
                string allText = _subtitleAlternate.ToText(format).Trim();
                var currentEncoding = GetCurrentEncoding();
                if (currentEncoding == Encoding.Default && (allText.Contains("♪") || allText.Contains("♫") | allText.Contains("♥"))) // ANSI & music/unicode symbols
                {
                    if (MessageBox.Show(string.Format(_language.UnicodeMusicSymbolsAnsiWarning), Title, MessageBoxButtons.YesNo) == DialogResult.No)
                        return DialogResult.No;
                }

                bool containsNegativeTime = false;
                foreach (var p in _subtitleAlternate.Paragraphs)
                {
                    if (p.StartTime.TotalMilliseconds < 0 || p.EndTime.TotalMilliseconds < 0)
                    {
                        containsNegativeTime = true;
                        break;
                    }
                }
                if (containsNegativeTime && !string.IsNullOrEmpty(_language.NegativeTimeWarning))
                {
                    if (MessageBox.Show(_language.NegativeTimeWarning, Title, MessageBoxButtons.YesNo) == DialogResult.No)
                        return DialogResult.No;
                }

                File.WriteAllText(_subtitleAlternateFileName, allText, currentEncoding);
                ShowStatus(string.Format(_language.SavedOriginalSubtitleX, _subtitleAlternateFileName));
                _changeAlternate = false;
                return DialogResult.OK;
            }
            catch
            {
                MessageBox.Show(string.Format(_language.UnableToSaveSubtitleX, _fileName));
                return DialogResult.Cancel;
            }
        }

        private void NewToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            FileNew();
        }

        private void ResetSubtitle()
        {
            SetCurrentFormat(new SubRip());
            _subtitle = new Subtitle(_subtitle.HistoryItems);
            _subtitleAlternate = new Subtitle();
            _subtitleAlternateFileName = null;
            textBoxSource.Text = string.Empty;
            SubtitleListview1.Items.Clear();
            _fileName = string.Empty;
            _fileDateTime = new DateTime();
            Text = Title;
            _oldSubtitleFormat = null;
            labelSingleLine.Text = string.Empty;
            RemoveAlternate(true);
            toolStripComboBoxFrameRate.Text = Configuration.Settings.General.DefaultFrameRate.ToString();

            comboBoxEncoding.Items[0] = "ANSI - " + Encoding.Default.CodePage.ToString();
            if (Configuration.Settings.General.DefaultEncoding == "ANSI")
                comboBoxEncoding.SelectedIndex = 0;
            else
                comboBoxEncoding.Text = Configuration.Settings.General.DefaultEncoding;

            toolStripComboBoxFrameRate.Text = Configuration.Settings.General.DefaultFrameRate.ToString();
            _findHelper = null;
            _spellCheckForm = null;
            _videoFileName = null;
            _videoAudioTrackNumber = -1;
            labelVideoInfo.Text = Configuration.Settings.Language.General.NoVideoLoaded;
            audioVisualizer.WavePeaks = null;
            audioVisualizer.ResetSpectrogram();
            audioVisualizer.Invalidate();

            ShowStatus(_language.New);
            _sourceViewChange = false;

            _subtitleListViewIndex = -1;
            textBoxListViewText.Text = string.Empty;
            textBoxListViewTextAlternate.Text = string.Empty;
            textBoxListViewText.Enabled = false;
            labelTextLineLengths.Text = string.Empty;
            labelCharactersPerSecond.Text = string.Empty;
            labelTextLineTotal.Text = string.Empty;
            
            if (mediaPlayer.VideoPlayer != null)
            {
                mediaPlayer.VideoPlayer.DisposeVideoPlayer();
                mediaPlayer.VideoPlayer = null;
            }

            _change = false;
            _converted = false;

            SetUndockedWindowsTitle();
        }

        private void FileNew()
        {
            if (ContinueNewOrExit())
            {
                MakeHistoryForUndo(_language.BeforeNew);
                ResetSubtitle();
            }
        }

        private void ComboBoxSubtitleFormatsSelectedIndexChanged(object sender, EventArgs e)
        {            
            _converted = true;
            if (_oldSubtitleFormat == null)
            {
                MakeHistoryForUndo(string.Format(_language.BeforeConvertingToX, GetCurrentSubtitleFormat().FriendlyName));
            }
            else
            {
                _subtitle.MakeHistoryForUndo(string.Format(_language.BeforeConvertingToX, GetCurrentSubtitleFormat().FriendlyName), _oldSubtitleFormat, _fileDateTime);
                _oldSubtitleFormat.RemoveNativeFormatting(_subtitle);
                SaveSubtitleListviewIndexes();
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();

                if ((_oldSubtitleFormat.GetType() == typeof(AdvancedSubStationAlpha) || _oldSubtitleFormat.GetType() == typeof(SubStationAlpha)) && _networkSession == null)
                {
                    SubtitleListview1.HideExtraColumn();
                }
            }
            ShowSource();

            SubtitleListview1.DisplayExtraFromExtra = false;
            SubtitleFormat format = GetCurrentSubtitleFormat();
            if (format != null)
            {
                ShowStatus(string.Format(_language.ConvertedToX, format.FriendlyName));
                _oldSubtitleFormat = format;

                if ((format.GetType() == typeof(AdvancedSubStationAlpha) || format.GetType() == typeof(SubStationAlpha)) && _networkSession == null)
                {
                    SubtitleListview1.ShowExtraColumn("Style");
                    SubtitleListview1.DisplayExtraFromExtra = true;
                }
            }

        }

        private void ComboBoxSubtitleFormatsEnter(object sender, EventArgs e)
        {
            SubtitleFormat format = GetCurrentSubtitleFormat();
            if (format != null)
                _oldSubtitleFormat = format;
        }

        private SubtitleFormat GetCurrentSubtitleFormat()
        {
            return Utilities.GetSubtitleFormatByFriendlyName(comboBoxSubtitleFormats.SelectedItem.ToString());
        }

        private void ShowSource()
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 0)
            {
                SubtitleFormat format = GetCurrentSubtitleFormat();
                if (format != null)
                {
                    if (GetCurrentSubtitleFormat().IsFrameBased)
                        _subtitle.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                    else
                        _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);

                    textBoxSource.TextChanged -= TextBoxSourceTextChanged;
                    textBoxSource.Text = _subtitle.ToText(format);
                    textBoxSource.TextChanged += TextBoxSourceTextChanged;
                    return;
                }
            }
            textBoxSource.Text = string.Empty;
        }

        private void SettingsToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowSettings();
        }

        private void ShowSettings()
        {
            string oldListViewLineSeparatorString = Configuration.Settings.General.ListViewLineSeparatorString;
            string oldSubtitleFontSettings = Configuration.Settings.General.SubtitleFontName +
                                          Configuration.Settings.General.SubtitleFontBold +
                                          Configuration.Settings.General.SubtitleFontSize +
                                          Configuration.Settings.General.SubtitleFontColor.ToArgb().ToString() +
                                          Configuration.Settings.General.SubtitleBackgroundColor.ToArgb().ToString();

            var oldAllowEditOfOriginalSubtitle = Configuration.Settings.General.AllowEditOfOriginalSubtitle;
            var settings = new Settings();
            settings.Initialize(this.Icon, toolStripButtonFileNew.Image, toolStripButtonFileOpen.Image, toolStripButtonSave.Image, toolStripButtonSaveAs.Image, 
                                toolStripButtonFind.Image, toolStripButtonReplace.Image, toolStripButtonVisualSync.Image, toolStripButtonSpellCheck.Image, toolStripButtonSettings.Image, toolStripButtonHelp.Image);
            _formPositionsAndSizes.SetPositionAndSize(settings);
            settings.ShowDialog(this);
            _formPositionsAndSizes.SavePositionAndSize(settings);
            settings.Dispose();

            InitializeToolbar();
            UpdateRecentFilesUI();
            Utilities.InitializeSubtitleFont(textBoxSource);
            Utilities.InitializeSubtitleFont(textBoxListViewText);
            Utilities.InitializeSubtitleFont(SubtitleListview1);
            buttonCustomUrl.Text = Configuration.Settings.VideoControls.CustomSearchText;
            buttonCustomUrl.Enabled = Configuration.Settings.VideoControls.CustomSearchUrl.Length > 1;

            audioVisualizer.DrawGridLines = Configuration.Settings.VideoControls.WaveFormDrawGrid;
            audioVisualizer.GridColor = Configuration.Settings.VideoControls.WaveFormGridColor;
            audioVisualizer.SelectedColor = Configuration.Settings.VideoControls.WaveFormSelectedColor;
            audioVisualizer.Color = Configuration.Settings.VideoControls.WaveFormColor;
            audioVisualizer.BackgroundColor = Configuration.Settings.VideoControls.WaveFormBackgroundColor;
            audioVisualizer.TextColor =  Configuration.Settings.VideoControls.WaveFormTextColor; 

            if (oldSubtitleFontSettings != Configuration.Settings.General.SubtitleFontName + 
                                          Configuration.Settings.General.SubtitleFontBold +
                                          Configuration.Settings.General.SubtitleFontSize +
                                          Configuration.Settings.General.SubtitleFontColor.ToArgb().ToString() +
                                          Configuration.Settings.General.SubtitleBackgroundColor.ToArgb().ToString())
            {
                Utilities.InitializeSubtitleFont(textBoxListViewText);
                Utilities.InitializeSubtitleFont(textBoxSource);
                SubtitleListview1.SubtitleFontName = Configuration.Settings.General.SubtitleFontName;
                SubtitleListview1.SubtitleFontBold = Configuration.Settings.General.SubtitleFontBold;
                SubtitleListview1.SubtitleFontSize = Configuration.Settings.General.SubtitleFontSize;
                SubtitleListview1.ForeColor = Configuration.Settings.General.SubtitleFontColor;
                SubtitleListview1.BackColor = Configuration.Settings.General.SubtitleBackgroundColor;
                SaveSubtitleListviewIndexes();
                Utilities.InitializeSubtitleFont(SubtitleListview1);
                SubtitleListview1.AutoSizeAllColumns(this);
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();                
                mediaPlayer.SetSubtitleFont();
                ShowSubtitle();
            }
            mediaPlayer.SetSubtitleFont();

            if (oldListViewLineSeparatorString != Configuration.Settings.General.ListViewLineSeparatorString)
            {
                SubtitleListview1.InitializeLanguage(_languageGeneral, Configuration.Settings);
                SaveSubtitleListviewIndexes();
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();
            }

            if (oldAllowEditOfOriginalSubtitle != Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                if (Configuration.Settings.General.AllowEditOfOriginalSubtitle)
                {
                    buttonUnBreak.Visible = false;
                    buttonUndoListViewChanges.Visible = false;
                    buttonSplitLine.Visible = false;
                    textBoxListViewTextAlternate.Visible = true;
                    labelAlternateText.Visible = true;
                    labelAlternateCharactersPerSecond.Visible = true;
                    labelTextAlternateLineLengths.Visible = true;
                    labelAlternateSingleLine.Visible = true;
                    labelTextAlternateLineTotal.Visible = true;
                }
                else
                { 
                    RemoveAlternate(false);
                }
                Main_Resize(null, null);
            }
            textBoxListViewTextAlternate.Enabled = Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleListViewIndex >= 0;

            SetShortcuts();

            _timerAutoSave.Stop();
            if (Configuration.Settings.General.AutoBackupSeconds > 0)
            {
                _timerAutoSave.Interval = 1000 * Configuration.Settings.General.AutoBackupSeconds; // take backup every x second if changes were made
                _timerAutoSave.Start();
            }
            SetTitle();
            if (Configuration.Settings.VideoControls.GenerateSpectrogram)
            {
                audioVisualizer.WaveFormNotLoadedText = Configuration.Settings.Language.WaveForm.ClickToAddWaveformAndSpectrogram;
                showhideWaveFormToolStripMenuItem.Text = _language.Menu.Video.ShowHideWaveformAndSpectrogram;
            }
            else
            {
                audioVisualizer.WaveFormNotLoadedText = Configuration.Settings.Language.WaveForm.ClickToAddWaveForm;
                showhideWaveFormToolStripMenuItem.Text = _language.Menu.Video.ShowHideWaveForm;
            }
            audioVisualizer.Invalidate();
        }

        private int ShowSubtitle()
        {
            if (SubtitleListview1.IsAlternateTextColumnVisible && Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable)
                return Utilities.ShowSubtitle(_subtitleAlternate.Paragraphs, mediaPlayer);
            return Utilities.ShowSubtitle(_subtitle.Paragraphs, mediaPlayer);
        }

        private void TryLoadIcon(ToolStripButton button, string iconName)
        { 
            string fullPath = Configuration.IconsFolder + iconName + ".png";
            if (File.Exists(fullPath))
                button.Image = new Bitmap(fullPath);
        }

        private void InitializeToolbar()
        {
            GeneralSettings gs = Configuration.Settings.General;          

            TryLoadIcon(toolStripButtonFileNew, "New");
            TryLoadIcon(toolStripButtonFileOpen, "Open");
            TryLoadIcon(toolStripButtonSave, "Save");
            TryLoadIcon(toolStripButtonSaveAs, "SaveAs");
            TryLoadIcon(toolStripButtonFind, "Find");
            TryLoadIcon(toolStripButtonReplace, "Replace");
            TryLoadIcon(toolStripButtonVisualSync, "VisualSync");
            TryLoadIcon(toolStripButtonSettings, "Settings");
            TryLoadIcon(toolStripButtonSpellCheck, "SpellCheck");
            TryLoadIcon(toolStripButtonHelp, "Help");

            TryLoadIcon(toolStripButtonToggleVideo, "VideoToggle");
            TryLoadIcon(toolStripButtonToggleWaveForm, "WaveFormToggle");

            toolStripButtonFileNew.Visible = gs.ShowToolbarNew;
            toolStripButtonFileOpen.Visible = gs.ShowToolbarOpen;
            toolStripButtonSave.Visible = gs.ShowToolbarSave;
            toolStripButtonSaveAs.Visible = gs.ShowToolbarSaveAs;
            toolStripButtonFind.Visible = gs.ShowToolbarFind;
            toolStripButtonReplace.Visible = gs.ShowToolbarReplace;
            toolStripButtonVisualSync.Visible = gs.ShowToolbarVisualSync;
            toolStripButtonSettings.Visible = gs.ShowToolbarSettings;
            toolStripButtonSpellCheck.Visible = gs.ShowToolbarSpellCheck;
            toolStripButtonHelp.Visible = gs.ShowToolbarHelp;

            toolStripSeparatorFrameRate.Visible = gs.ShowFrameRate;
            toolStripLabelFrameRate.Visible = gs.ShowFrameRate;
            toolStripComboBoxFrameRate.Visible = gs.ShowFrameRate;
            toolStripButtonGetFrameRate.Visible = gs.ShowFrameRate;

            toolStripSeparatorFindReplace.Visible = gs.ShowToolbarFind || gs.ShowToolbarReplace;
            toolStripSeparatorHelp.Visible = gs.ShowToolbarHelp;
        }

        private void ToolStripButtonFileNewClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            FileNew();
        }

        private void ToolStripButtonFileOpenClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            OpenNewFile();
        }

        private void ToolStripButtonSaveClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            bool oldChange = _change;
            SaveSubtitle(GetCurrentSubtitleFormat());

            if (_changeAlternate && Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                saveOriginalToolStripMenuItem_Click(null, null);
                if (oldChange && !_change && !_changeAlternate)
                    ShowStatus(string.Format(_language.SavedSubtitleX, Path.GetFileName(_fileName)) + " + " +
                        string.Format(_language.SavedOriginalSubtitleX, Path.GetFileName(_subtitleAlternateFileName)));
            }
        }

        private void ToolStripButtonSaveAsClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            FileSaveAs();
        }

        private void ToolStripButtonFindClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Find();
        }

        private void ToolStripButtonVisualSyncClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            ShowVisualSync(false);
        }

        private void ToolStripButtonSettingsClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            ShowSettings();
        }
        
        private void TextBoxSourceClick(object sender, EventArgs e)
        {
            ShowSourceLineNumber();
        }

        private void TextBoxSourceKeyDown(object sender, KeyEventArgs e)
        {            
            ShowSourceLineNumber();
            e.Handled = false;

            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                textBoxSource.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.D)
            {
                textBoxSource.SelectionLength = 0;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void TextBoxSourceTextChanged(object sender, EventArgs e)
        {
            ShowSourceLineNumber();
            _sourceViewChange = true;
            _change = true;
        }


        private void ShowSourceLineNumber()
        {
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
            {
                string number = textBoxSource.GetLineFromCharIndex(textBoxSource.SelectionStart).ToString();
                if (number.Length > 0)
                    toolStripSelected.Text = string.Format(_language.LineNumberX, int.Parse(number) + 1);
                else
                    toolStripSelected.Text = string.Empty;
            }
        }

        private void ButtonGetFrameRateClick(object sender, EventArgs e)
        {
            openFileDialog1.Title = _language.OpenVideoFile;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetVideoFileFilter(); 
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                _videoFileName = openFileDialog1.FileName;
                VideoInfo info = Utilities.GetVideoInfo(openFileDialog1.FileName, delegate { Application.DoEvents(); });
                if (info != null && info.Success)
                {
                    string oldFrameRate = toolStripComboBoxFrameRate.Text;
                    toolStripComboBoxFrameRate.Text = info.FramesPerSecond.ToString();

                    if (oldFrameRate != toolStripComboBoxFrameRate.Text)
                    {
                        ShowSource();
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);

                        SubtitleFormat format = Utilities.GetSubtitleFormatByFriendlyName(comboBoxSubtitleFormats.SelectedItem.ToString());
                        if (_subtitle.WasLoadedWithFrameNumbers && format.IsTimeBased)
                        {
                            MessageBox.Show(string.Format(_language.NewFrameRateUsedToCalculateTimeCodes, info.FramesPerSecond));
                        }
                        else if (!_subtitle.WasLoadedWithFrameNumbers && format.IsFrameBased)
                        {
                            MessageBox.Show(string.Format(_language.NewFrameRateUsedToCalculateFrameNumbers, info.FramesPerSecond));
                        }
                    }
                }
            }
        }

        private void FindToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Find();
        }

        private void Find()
        {
            string selectedText;
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)                
                selectedText = textBoxSource.SelectedText;
            else
                selectedText = textBoxListViewText.SelectedText;

            if (selectedText.Length == 0 && _findHelper != null)
                selectedText = _findHelper.FindText;

            var findDialog = new FindDialog();
            findDialog.SetIcon(toolStripButtonFind.Image as Bitmap);
            findDialog.Initialize(selectedText, _findHelper);
            if (findDialog.ShowDialog(this) == DialogResult.OK)
            {
                _findHelper = findDialog.GetFindDialogHelper(_subtitleListViewIndex);
                if (tabControlSubtitle.SelectedIndex == TabControlListView)
                {
                    int selectedIndex = -1;
                    //set the starting selectedIndex if a row is highlighted
                    if (SubtitleListview1.SelectedItems.Count > 0)
                        selectedIndex = SubtitleListview1.SelectedItems[0].Index;

                    //if we fail to find the text, we might want to start searching from the top of the file.
                    bool foundIt = false;
                    if (_findHelper.Find(_subtitle, selectedIndex))
                    {
                        foundIt = true;
                    }
                    else if (_findHelper.StartLineIndex >= 1)
                    {
                        if (MessageBox.Show(_language.FindContinue, _language.FindContinueTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            selectedIndex = -1;
                            if (_findHelper.Find(_subtitle, selectedIndex))
                                foundIt = true;
                        }
                    }

                    if (foundIt)
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                        textBoxListViewText.Focus();
                        textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                        textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                        ShowStatus(string.Format(_language.XFoundAtLineNumberY, _findHelper.FindText, _findHelper.SelectedIndex + 1));
                        _findHelper.SelectedPosition++;
                    }
                    else
                    {
                        ShowStatus(string.Format(_language.XNotFound, _findHelper.FindText));
                    }
                }
                else if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
                {
                    if (_findHelper.Find(textBoxSource, textBoxSource.SelectionStart))
                    {
                        textBoxSource.SelectionStart = _findHelper.SelectedIndex;
                        textBoxSource.SelectionLength = _findHelper.FindTextLength;
                        textBoxSource.ScrollToCaret();
                        ShowStatus(string.Format(_language.XFoundAtLineNumberY, _findHelper.FindText, textBoxSource.GetLineFromCharIndex(textBoxSource.SelectionStart)));
                    }
                    else
                    {
                        ShowStatus(string.Format(_language.XNotFound, _findHelper.FindText));
                    }
                }
            }
            findDialog.Dispose();
        }

        private void FindNextToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            FindNext();
        }

        private void FindNext()
        {
            if (_findHelper != null)
            {
                if (tabControlSubtitle.SelectedIndex == TabControlListView)
                {
                    int selectedIndex = -1;
                    if (SubtitleListview1.SelectedItems.Count > 0)
                        selectedIndex = SubtitleListview1.SelectedItems[0].Index;
                    if (_findHelper.FindNext(_subtitle, selectedIndex, _findHelper.SelectedPosition))
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                        ShowStatus(string.Format(_language.XFoundAtLineNumberY, _findHelper.FindText, _findHelper.SelectedIndex+1));
                        textBoxListViewText.Focus();
                        textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                        textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                        _findHelper.SelectedPosition++;
                    }
                    else
                    {
                        if (_findHelper.StartLineIndex >= 1)
                        {
                            if (MessageBox.Show(_language.FindContinue, _language.FindContinueTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                _findHelper.StartLineIndex = 0;
                                if (_findHelper.Find(_subtitle, 0))
                                {
                                    SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                                    textBoxListViewText.Focus();
                                    textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                                    textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                                    ShowStatus(string.Format(_language.XFoundAtLineNumberY, _findHelper.FindText, _findHelper.SelectedIndex + 1));
                                    _findHelper.SelectedPosition++;
                                    return;
                                }
                            }
                        }
                        ShowStatus(string.Format(_language.XNotFound, _findHelper.FindText));                            
                    }
                }
                else if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
                {
                    if (_findHelper.FindNext(textBoxSource, textBoxSource.SelectionStart))
                    {
                        textBoxSource.SelectionStart = _findHelper.SelectedIndex;
                        textBoxSource.SelectionLength = _findHelper.FindTextLength;
                        textBoxSource.ScrollToCaret();
                        ShowStatus(string.Format(_language.XFoundAtLineNumberY, _findHelper.FindText, textBoxSource.GetLineFromCharIndex(textBoxSource.SelectionStart)));
                    }
                    else
                    {
                        ShowStatus(string.Format(_language.XNotFound, _findHelper.FindText));
                    }
                }
            }

        }

        private void ToolStripButtonReplaceClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Replace(null);
        }

        private void ReplaceToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Replace(null);
        }

        private void ReplaceSourceView(ReplaceDialog replaceDialog)
        {
            bool isFirst = true;
            string selectedText = textBoxSource.SelectedText;
            if (selectedText.Length == 0 && _findHelper != null)
                selectedText = _findHelper.FindText;

            if (replaceDialog == null)
            {
                replaceDialog = new ReplaceDialog();
                replaceDialog.SetIcon(toolStripButtonReplace.Image as Bitmap);
                if (_findHelper == null)
                {
                    _findHelper = replaceDialog.GetFindDialogHelper(_subtitleListViewIndex);
                    _findHelper.WindowPositionLeft = Left + (Width / 2) - (replaceDialog.Width / 2);
                    _findHelper.WindowPositionTop = Top + (Height / 2) - (replaceDialog.Height / 2);
                }
            }
            else
                isFirst = false;

            replaceDialog.Initialize(selectedText, _findHelper);
            if (replaceDialog.ShowDialog(this) == DialogResult.OK)
            {
                _findHelper = replaceDialog.GetFindDialogHelper(_subtitleListViewIndex);
                int replaceCount = 0;
                bool searchStringFound = true;
                while (searchStringFound)
                {
                    searchStringFound = false;
                    int start = textBoxSource.SelectionStart;
                    if (isFirst)
                    {
                        MakeHistoryForUndo(string.Format(_language.BeforeReplace, _findHelper.FindText));
                        isFirst = false;
                        if (start >= 0)
                            start--;
                    }
                    if (_findHelper.FindNext(textBoxSource, start))
                    {
                        textBoxSource.SelectionStart = _findHelper.SelectedIndex;
                        textBoxSource.SelectionLength = _findHelper.FindTextLength;
                        if (!replaceDialog.FindOnly)
                            textBoxSource.SelectedText = _findHelper.ReplaceText;
                        textBoxSource.ScrollToCaret();

                        replaceCount++;
                        searchStringFound = true;
                    }
                    if (replaceDialog.FindOnly)
                    {
                        if (searchStringFound)
                            ShowStatus(string.Format(_language.MatchFoundX, _findHelper.FindText));
                        else
                            ShowStatus(string.Format(_language.NoMatchFoundX, _findHelper.FindText));

                        Replace(replaceDialog);
                        return;
                    }
                    if (!replaceDialog.ReplaceAll)
                    {
                        break; // out of while loop
                    }
                }
                ReloadFromSourceView();
                if (replaceCount == 0)
                    ShowStatus(_language.FoundNothingToReplace);
                else
                    ShowStatus(string.Format(_language.ReplaceCountX, replaceCount));
            }
            replaceDialog.Dispose();
        }

        private void ReplaceListView(ReplaceDialog replaceDialog)
        {
            bool isFirst = true;
            string selectedText = textBoxListViewText.SelectedText;
            if (selectedText.Length == 0 && _findHelper != null)
                selectedText = _findHelper.FindText;

            if (replaceDialog == null)
            {
                replaceDialog = new ReplaceDialog();
                replaceDialog.SetIcon(toolStripButtonReplace.Image as Bitmap);
                if (_findHelper == null)
                {
                    _findHelper = replaceDialog.GetFindDialogHelper(_subtitleListViewIndex);
                    _findHelper.WindowPositionLeft = Left + (Width / 2) - (replaceDialog.Width / 2);
                    _findHelper.WindowPositionTop = Top + (Height / 2) - (replaceDialog.Height / 2);
                }
                int index = 0;

                if (SubtitleListview1.SelectedItems.Count > 0)
                    index = SubtitleListview1.SelectedItems[0].Index;

                _findHelper.SelectedIndex = index;
                _findHelper.SelectedPosition = index;
                _replaceStartLineIndex = index;
            }
            else
            {
                isFirst = false;
                if (_findHelper != null)
                    selectedText = _findHelper.FindText;
            }
            replaceDialog.Initialize(selectedText, _findHelper);
            if (replaceDialog.ShowDialog(this) == DialogResult.OK)
            {
                if (_findHelper == null)
                {
                    _findHelper = replaceDialog.GetFindDialogHelper(_subtitleListViewIndex);
                }
                else
                {
                    int line = _findHelper.SelectedIndex;
                    int pos = _findHelper.SelectedPosition;
                    bool success = _findHelper.Success;
                    _findHelper = replaceDialog.GetFindDialogHelper(_subtitleListViewIndex);
                    _findHelper.SelectedIndex = line;
                    _findHelper.SelectedPosition = pos;
                    _findHelper.Success = success;
                }
                int replaceCount = 0;
                bool searchStringFound = true;
                while (searchStringFound)
                {
                    searchStringFound = false;
                    if (isFirst)
                    {
                        MakeHistoryForUndo(string.Format(_language.BeforeReplace, _findHelper.FindText));
                        isFirst = false;
                    }

                    if (replaceDialog.ReplaceAll)
                    {
                        if (_findHelper.FindNext(_subtitle, _findHelper.SelectedIndex, _findHelper.SelectedPosition))
                        {
                            SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                            textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                            textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                            textBoxListViewText.SelectedText = _findHelper.ReplaceText;
                            _findHelper.SelectedPosition += _findHelper.ReplaceText.Length;
                            searchStringFound = true;
                            replaceCount++;
                        }
                        else
                        {
                            ShowStatus(string.Format(_language.NoMatchFoundX, _findHelper.FindText));

                            if (_replaceStartLineIndex >= 1) // Prompt for start over
                            {
                                _replaceStartLineIndex = 0;
                                if (MessageBox.Show(_language.FindContinue, _language.FindContinueTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                                {
                                    SubtitleListview1.SelectIndexAndEnsureVisible(0);
                                    _findHelper.StartLineIndex = 0;
                                    _findHelper.SelectedIndex = 0;
                                    _findHelper.SelectedPosition = 0;

                                    if (_findHelper.FindNext(_subtitle, _findHelper.SelectedIndex, _findHelper.SelectedPosition))
                                    {
                                        SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                                        textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                                        textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                                        textBoxListViewText.SelectedText = _findHelper.ReplaceText;
                                        _findHelper.SelectedPosition += _findHelper.ReplaceText.Length;
                                        searchStringFound = true;
                                        replaceCount++;
                                    }
                                }
                            }
                        }
                    }
                    else if (replaceDialog.FindOnly)
                    {
                        if (_findHelper.FindNext(_subtitle, _findHelper.SelectedIndex, _findHelper.SelectedPosition))
                        {
                            SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                            textBoxListViewText.Focus();
                            textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                            textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                            _findHelper.SelectedPosition += _findHelper.FindTextLength;
                            ShowStatus(string.Format(_language.NoXFoundAtLineY, _findHelper.SelectedIndex + 1, _findHelper.FindText));
                            Replace(replaceDialog);
                            return;
                        }
                        else if (_replaceStartLineIndex >= 1) // Prompt for start over
                        {
                            _replaceStartLineIndex = 0;
                            if (MessageBox.Show(_language.FindContinue, _language.FindContinueTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                SubtitleListview1.SelectIndexAndEnsureVisible(0);
                                _findHelper.StartLineIndex = 0;
                                _findHelper.SelectedIndex = 0;
                                _findHelper.SelectedPosition = 0;
                                if (_findHelper.FindNext(_subtitle, _findHelper.SelectedIndex, _findHelper.SelectedPosition))
                                {
                                    SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                                    textBoxListViewText.Focus();
                                    textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                                    textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                                    _findHelper.SelectedPosition += _findHelper.FindTextLength;
                                    ShowStatus(string.Format(_language.NoXFoundAtLineY, _findHelper.SelectedIndex + 1, _findHelper.FindText));
                                    Replace(replaceDialog);
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                        ShowStatus(string.Format(_language.NoMatchFoundX, _findHelper.FindText));
                    }
                    else if (!replaceDialog.FindOnly) // replace once only
                    {
                        string msg = string.Empty;
                        if (_findHelper.FindType == FindType.RegEx && _findHelper.Success)
                        {
                            textBoxListViewText.SelectedText = _findHelper.ReplaceText;
                            msg = _language.OneReplacementMade + " "; 
                        }
                        else if (textBoxListViewText.SelectionLength == _findHelper.FindTextLength)
                        {
                            textBoxListViewText.SelectedText = _findHelper.ReplaceText;
                            msg = _language.OneReplacementMade + " ";
                        }

                        if (_findHelper.FindNext(_subtitle, _findHelper.SelectedIndex, _findHelper.SelectedPosition))
                        {
                            SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                            textBoxListViewText.Focus();
                            textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                            textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                            _findHelper.SelectedPosition += _findHelper.ReplaceText.Length;
                            ShowStatus(string.Format(msg + _language.XFoundAtLineNumberY + _language.XFoundAtLineNumberY, _findHelper.SelectedIndex + 1, _findHelper.FindText));
                        }
                        else
                        {
                            ShowStatus(msg + string.Format(_language.XNotFound, _findHelper.FindText));

                            // Prompt for start over
                            if (_replaceStartLineIndex >= 1)
                            {
                                _replaceStartLineIndex = 0;
                                if (MessageBox.Show(_language.FindContinue, _language.FindContinueTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                                {
                                    SubtitleListview1.SelectIndexAndEnsureVisible(0);
                                    _findHelper.StartLineIndex = 0;
                                    _findHelper.SelectedIndex = 0;
                                    _findHelper.SelectedPosition = 0;


                                    if (_findHelper.FindNext(_subtitle, _findHelper.SelectedIndex, _findHelper.SelectedPosition))
                                    {
                                        SubtitleListview1.SelectIndexAndEnsureVisible(_findHelper.SelectedIndex);
                                        textBoxListViewText.Focus();
                                        textBoxListViewText.SelectionStart = _findHelper.SelectedPosition;
                                        textBoxListViewText.SelectionLength = _findHelper.FindTextLength;
                                        _findHelper.SelectedPosition += _findHelper.ReplaceText.Length;
                                        ShowStatus(string.Format(msg + _language.XFoundAtLineNumberY + _language.XFoundAtLineNumberY, _findHelper.SelectedIndex + 1, _findHelper.FindText));
                                    }

                                }
                                else
                                {
                                    return;
                                }
                            }

                        }
                        Replace(replaceDialog);
                        return;
                    }
                }

                ShowSource();
                if (replaceCount == 0)
                    ShowStatus(_language.FoundNothingToReplace);
                else
                    ShowStatus(string.Format(_language.ReplaceCountX, replaceCount));
            }
            replaceDialog.Dispose();
        }

        private void Replace(ReplaceDialog replaceDialog)
        {
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
            {
                ReplaceSourceView(replaceDialog);
            }
            else
            {
                ReplaceListView(replaceDialog);
            }
        }

        public void ShowStatus(string message)
        {
            labelStatus.Text = message;
            statusStrip1.Refresh();
        }

        private void ReloadFromSourceView()
        {
            if (_sourceViewChange)
            {
                SaveSubtitleListviewIndexes();
                if (textBoxSource.Text.Trim().Length > 0)
                {
                    Subtitle temp = new Subtitle(_subtitle);
                    SubtitleFormat format = temp.ReloadLoadSubtitle(new List<string>(textBoxSource.Lines), null);
                    if (format == null)
                    {
                        MessageBox.Show(_language.UnableToParseSourceView);
                        return;
                    }
                    else
                    {
                        _sourceViewChange = false;
                        MakeHistoryForUndo(_language.BeforeChangesMadeInSourceView);
                        _subtitle.ReloadLoadSubtitle(new List<string>(textBoxSource.Lines), null);
                        if (format.IsFrameBased)
                            _subtitle.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                        int index = 0;
                        foreach (object obj in comboBoxSubtitleFormats.Items)
                        {
                            if (obj.ToString() == format.FriendlyName)
                                comboBoxSubtitleFormats.SelectedIndex = index;
                            index++;
                        }
                    }
                }
                else
                {
                    _sourceViewChange = false;
                    MakeHistoryForUndo(_language.BeforeChangesMadeInSourceView);
                    _sourceViewChange = false;
                    _subtitle.Paragraphs.Clear();
                }
                _subtitleListViewIndex = -1;
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();
            }
        }

        private void HelpToolStripMenuItem1Click(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Utilities.ShowHelp(string.Empty);
        }

        private void ToolStripButtonHelpClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            Utilities.ShowHelp(string.Empty);
        }

        private void GotoLineNumberToolStripMenuItemClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            if (_subtitle.Paragraphs.Count < 1 || textBoxSource.Lines.Length < 1)
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var goToLine = new GoToLine();
            if (tabControlSubtitle.SelectedIndex == TabControlListView)
            {
                goToLine.Initialize(1, SubtitleListview1.Items.Count);
            }
            else if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
            {
                goToLine.Initialize(1, textBoxSource.Lines.Length);
            }
            if (goToLine.ShowDialog(this) == DialogResult.OK)
            {
                if (tabControlSubtitle.SelectedIndex == TabControlListView)
                {
                    SubtitleListview1.SelectNone();

                    SubtitleListview1.Items[goToLine.LineNumber - 1].Selected = true;
                    SubtitleListview1.Items[goToLine.LineNumber - 1].EnsureVisible();
                    SubtitleListview1.Items[goToLine.LineNumber - 1].Focused = true;
                    ShowStatus(string.Format(_language.GoToLineNumberX, goToLine.LineNumber));
                }
                else if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
                {
                    // binary search
                    int start = 0;
                    int end = textBoxSource.Text.Length;
                    while (end - start > 10)
                    {
                        int middle = (end - start) / 2;
                        if (goToLine.LineNumber - 1 >= textBoxSource.GetLineFromCharIndex(start + middle))
                            start += middle;
                        else
                            end = start + middle;
                    }

                    // go before line, so we can find first char on line
                    start -= 100;
                    if (start < 0)
                        start = 0;

                    for (int i = start; i <= end; i++)
                    {
                        if (textBoxSource.GetLineFromCharIndex(i) == goToLine.LineNumber - 1)
                        {
                            // select line, scroll to line, and focus...
                            textBoxSource.SelectionStart = i;
                            textBoxSource.SelectionLength = textBoxSource.Lines[goToLine.LineNumber - 1].Length;
                            textBoxSource.ScrollToCaret();
                            ShowStatus(string.Format(_language.GoToLineNumberX, goToLine.LineNumber));
                            if (textBoxSource.CanFocus)
                                textBoxSource.Focus();
                            break;
                        }
                    }

                    ShowSourceLineNumber();
                }
            }
            goToLine.Dispose();
        }

        private void TextBoxSourceLeave(object sender, EventArgs e)
        {
            ReloadFromSourceView();
        }

        private void AdjustDisplayTimeToolStripMenuItemClick(object sender, EventArgs e)
        {
            AdjustDisplayTime(false);
        }

        private void AdjustDisplayTime(bool onlySelectedLines)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var adjustDisplayTime = new AdjustDisplayDuration();
                _formPositionsAndSizes.SetPositionAndSize(adjustDisplayTime);

                ListView.SelectedIndexCollection selectedIndexes = null;
                if (onlySelectedLines)
                {
                    adjustDisplayTime.Text += " - " + _language.SelectedLines;
                    selectedIndexes = SubtitleListview1.SelectedIndices;
                }

                if (adjustDisplayTime.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeDisplayTimeAdjustment);
                    if (adjustDisplayTime.AdjustUsingPercent)
                    {
                        double percent = double.Parse(adjustDisplayTime.AdjustValue);
                        _subtitle.AdjustDisplayTimeUsingPercent(percent, selectedIndexes);
                    }
                    else
                    {
                        double seconds = double.Parse(adjustDisplayTime.AdjustValue);
                        _subtitle.AdjustDisplayTimeUsingSeconds(seconds, selectedIndexes);
                    }
                    ShowStatus(string.Format(_language.DisplayTimesAdjustedX, adjustDisplayTime.AdjustValue));
                    SaveSubtitleListviewIndexes();
                    if (IsFramesRelevant)
                        _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(CurrentFrameRate);
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                    _change = true;
                }
                _formPositionsAndSizes.SavePositionAndSize(adjustDisplayTime);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool IsFramesRelevant
        {
            get
            {
                return _subtitle.WasLoadedWithFrameNumbers || GetCurrentSubtitleFormat().IsFrameBased;
            }
        }

        private void FixToolStripMenuItemClick(object sender, EventArgs e)
        {
            FixCommonErrors(false);
        }

        private void FixCommonErrors(bool onlySelectedLines)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                SaveSubtitleListviewIndexes();
                var fixErrors = new FixCommonErrors();
                _formPositionsAndSizes.SetPositionAndSize(fixErrors);

                if (onlySelectedLines)
                {
                    var selectedLines = new Subtitle { WasLoadedWithFrameNumbers = _subtitle.WasLoadedWithFrameNumbers };
                    foreach (int index in SubtitleListview1.SelectedIndices)
                        selectedLines.Paragraphs.Add(_subtitle.Paragraphs[index]);
                    fixErrors.Initialize(selectedLines);
                }
                else
                {
                    fixErrors.Initialize(_subtitle);
                }

                if (fixErrors.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeCommonErrorFixes);

                    if (onlySelectedLines)
                    { // we only update selected lines
                        int i = 0;
                        foreach (int index in SubtitleListview1.SelectedIndices)
                        {
                            _subtitle.Paragraphs[index] = fixErrors.FixedSubtitle.Paragraphs[i];
                            i++;
                        }
                        ShowStatus(_language.CommonErrorsFixedInSelectedLines);
                    }
                    else
                    {
                        _subtitle.Paragraphs.Clear();
                        foreach (Paragraph p in fixErrors.FixedSubtitle.Paragraphs)
                            _subtitle.Paragraphs.Add(p);
                        ShowStatus(_language.CommonErrorsFixed);
                    }
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                    _change = true;
                    _formPositionsAndSizes.SavePositionAndSize(fixErrors);
                }
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StartNumberingFromToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var startNumberingFrom = new StartNumberingFrom();
                _formPositionsAndSizes.SetPositionAndSize(startNumberingFrom);
                if (startNumberingFrom.ShowDialog(this) == DialogResult.OK)
                {
                    SaveSubtitleListviewIndexes();
                    MakeHistoryForUndo(_language.BeforeRenumbering);
                    ShowStatus(string.Format(_language.RenumberedStartingFromX, startNumberingFrom.StartFromNumber));
                    _subtitle.Renumber(startNumberingFrom.StartFromNumber);
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                    _change = true;
                }
                _formPositionsAndSizes.SavePositionAndSize(startNumberingFrom);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Renumber()
        { 
            if (_subtitle != null && _subtitle.Paragraphs != null && _subtitle.Paragraphs.Count > 0)
                _subtitle.Renumber(_subtitle.Paragraphs[0].Number);
        }

        private void RemoveTextForHearImparedToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var removeTextFromHearImpaired = new FormRemoveTextForHearImpaired();
                _formPositionsAndSizes.SetPositionAndSize(removeTextFromHearImpaired);
                removeTextFromHearImpaired.Initialize(_subtitle);
                if (removeTextFromHearImpaired.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeRemovalOfTextingForHearingImpaired);
                    int count = removeTextFromHearImpaired.RemoveTextFromHearImpaired();
                    if (count > 0)
                    {
                        if (count == 1)
                            ShowStatus(_language.TextingForHearingImpairedRemovedOneLine);
                        else
                            ShowStatus(string.Format(_language.TextingForHearingImpairedRemovedXLines, count));
                        _subtitleListViewIndex = -1;
                        Renumber();
                        ShowSource();
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                        _change = true;
                        if (_subtitle.Paragraphs.Count > 0)
                            SubtitleListview1.SelectIndexAndEnsureVisible(0);
                    }
                }
                _formPositionsAndSizes.SavePositionAndSize(removeTextFromHearImpaired);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SplitToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var splitSubtitle = new SplitSubtitle();
                double lengthInSeconds = 0;
                if (mediaPlayer.VideoPlayer != null)
                    lengthInSeconds = mediaPlayer.Duration;
                splitSubtitle.Initialize(_subtitle, _fileName , GetCurrentSubtitleFormat(), GetCurrentEncoding(), lengthInSeconds);
                if (splitSubtitle.ShowDialog(this) == DialogResult.OK)
                {
                    ShowStatus(_language.SubtitleSplitted);
                }
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AppendTextVisuallyToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();

                if (MessageBox.Show(_language.SubtitleAppendPrompt, _language.SubtitleAppendPromptTitle, MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
                {
                    openFileDialog1.Title = _language.OpenSubtitleToAppend;
                    openFileDialog1.FileName = string.Empty;
                    openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
                    if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
                    {
                        bool success = false;
                        string fileName = openFileDialog1.FileName;
                        if (File.Exists(fileName))
                        {
                            var subtitleToAppend = new Subtitle();
                            Encoding encoding;
                            SubtitleFormat format = subtitleToAppend.LoadSubtitle(fileName, out encoding, null);
                            if (GetCurrentSubtitleFormat().IsFrameBased)
                                subtitleToAppend.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                            else
                                subtitleToAppend.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);

                            if (format != null)
                            {
                                if (subtitleToAppend != null && subtitleToAppend.Paragraphs.Count > 1)
                                {
                                    VisualSync visualSync = new VisualSync();

                                    visualSync.Initialize(toolStripButtonVisualSync.Image as Bitmap, subtitleToAppend, _fileName, _language.AppendViaVisualSyncTitle, CurrentFrameRate);

                                    visualSync.ShowDialog(this);
                                    if (visualSync.OKPressed)
                                    {
                                        if (MessageBox.Show(_language.AppendSynchronizedSubtitlePrompt, _language.SubtitleAppendPromptTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                                        {
                                            int start = _subtitle.Paragraphs.Count +1;

                                            MakeHistoryForUndo(_language.BeforeAppend);
                                            foreach (Paragraph p in visualSync.Paragraphs)
                                                _subtitle.Paragraphs.Add(new Paragraph(p));
                                            _subtitle.Renumber(1);
                                            ShowSource();
                                            SubtitleListview1.Fill(_subtitle, _subtitleAlternate);

                                            // select appended lines
                                            for (int i = start; i < _subtitle.Paragraphs.Count; i++)
                                                SubtitleListview1.Items[i].Selected = true;
                                            SubtitleListview1.EnsureVisible(start);

                                            ShowStatus(string.Format(_language.SubtitleAppendedX, fileName));
                                            success = true;
                                        }
                                    }
                                    visualSync.Dispose();
                                }
                            }
                        }
                        if (!success)
                            ShowStatus(_language.SubtitleNotAppended);
                    }
                }
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TranslateByGoogleToolStripMenuItemClick(object sender, EventArgs e)
        {
            TranslateViaGoogle(false, true);
        }

        private void TranslateViaGoogle(bool onlySelectedLines, bool useGoogle)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count >= 1)
            {
                ReloadFromSourceView();
                var googleTranslate = new GoogleTranslate();
                _formPositionsAndSizes.SetPositionAndSize(googleTranslate);
                SaveSubtitleListviewIndexes();
                string title = _language.GoogleTranslate;
                if (!useGoogle)
                    title = _language.MicrosoftTranslate;
                if (onlySelectedLines)
                {
                    var selectedLines = new Subtitle { WasLoadedWithFrameNumbers = _subtitle.WasLoadedWithFrameNumbers };
                    foreach (int index in SubtitleListview1.SelectedIndices)
                        selectedLines.Paragraphs.Add(_subtitle.Paragraphs[index]);
                    title += " - " + _language.SelectedLines;
                    googleTranslate.Initialize(selectedLines, title, useGoogle);
                }
                else
                {
                    googleTranslate.Initialize(_subtitle, title, useGoogle);
                }
                if (googleTranslate.ShowDialog(this) == DialogResult.OK)
                {
                    _subtitleListViewIndex = -1;

                    _subtitleAlternate = new Subtitle(_subtitle);
                    _subtitleAlternateFileName = null;
                    MakeHistoryForUndo(_language.BeforeGoogleTranslation);

                    if (onlySelectedLines)
                    { // we only update selected lines
                        int i = 0;
                        foreach (int index in SubtitleListview1.SelectedIndices)
                        {
                            _subtitle.Paragraphs[index] = googleTranslate.TranslatedSubtitle.Paragraphs[i];
                            i++;
                        }
                        ShowStatus(_language.SelectedLinesTranslated);
                    }
                    else
                    {

                        _subtitle.Paragraphs.Clear();
                        foreach (Paragraph p in googleTranslate.TranslatedSubtitle.Paragraphs)
                            _subtitle.Paragraphs.Add(new Paragraph(p));
                        ShowStatus(_language.SubtitleTranslated);
                    }
                    ShowSource();

                    SubtitleListview1.ShowAlternateTextColumn(Configuration.Settings.Language.General.OriginalText);
                    SubtitleListview1.AutoSizeAllColumns(this);
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);

                    RestoreSubtitleListviewIndexes();
                    _change = true;
                    _converted = true;
                }
                _formPositionsAndSizes.SavePositionAndSize(googleTranslate);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }    

        private static string GetTranslateStringFromNikseDk(string input)
        {
            WebRequest.DefaultWebProxy = Utilities.GetProxy();
//            WebRequest request = WebRequest.Create("http://localhost:54942/MultiTranslator/TranslateForSubtitleEdit");
            WebRequest request = WebRequest.Create("http://www.nikse.dk/MultiTranslator/TranslateForSubtitleEdit");
            request.Method = "POST";
            string postData = String.Format("languagePair={1}&text={0}", Utilities.UrlEncode(input), "svda");
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);            
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse response = request.GetResponse();          
            dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();
            string result = responseFromServer;
            reader.Close();
            dataStream.Close();
            response.Close();
            return result;
        }

        private void TranslateFromSwedishToDanishToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                bool isSwedish = Utilities.AutoDetectGoogleLanguage(_subtitle) == "sv";
                string promptText = _language.TranslateSwedishToDanish;
                if (!isSwedish)
                    promptText = _language.TranslateSwedishToDanishWarning;

                if (MessageBox.Show(promptText, Title, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    try
                    {
                        _subtitleAlternate = new Subtitle(_subtitle);
                        _subtitleAlternateFileName = null;
                        int firstSelectedIndex = 0;
                        if (SubtitleListview1.SelectedItems.Count > 0)
                            firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;
                        _subtitleListViewIndex = -1;

                        Cursor.Current = Cursors.WaitCursor;
                        ShowStatus(_language.TranslatingViaNikseDkMt);
                        var sb = new StringBuilder();
                        var output = new StringBuilder();
                        foreach (Paragraph p in _subtitle.Paragraphs)
                        {
                            string s = p.Text;
                            s = s.Replace(Environment.NewLine, "<br/>");
                            s = "<p>" + s + "</p>";
                            sb.Append(s);

                            if (sb.Length > 9000)
                            {
                                output.Append(GetTranslateStringFromNikseDk(sb.ToString()));
                                sb = new StringBuilder();
                            }
                        }
                        if (sb.Length > 0)
                            output.Append(GetTranslateStringFromNikseDk(sb.ToString()));

                        MakeHistoryForUndo(_language.BeforeSwedishToDanishTranslation);
                        string result = output.ToString();                        
                        if (result.Length > 0)
                        {
                            int index = 0;
                            foreach (string s in result.Split(new string[] { "<p>", "</p>" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (index < _subtitle.Paragraphs.Count)
                                    _subtitle.Paragraphs[index].Text = s;
                                index++;
                            }
                            ShowSource();
                            SubtitleListview1.ShowAlternateTextColumn(Configuration.Settings.Language.General.OriginalText);
                            SubtitleListview1.AutoSizeAllColumns(this);
                            SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                            ShowStatus(_language.TranslationFromSwedishToDanishComplete);
                            SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedIndex);
                            _change = true;
                            _converted = true;
                        }                        
                    }
                    catch
                    {
                        ShowStatus(_language.TranslationFromSwedishToDanishFailed);
                    }
                    Cursor.Current = Cursors.Default;
                }
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowHistoryforUndoToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.CanUndo)
            {
                ReloadFromSourceView();
                var showHistory = new ShowHistory();
                showHistory.Initialize(_subtitle);
                if (showHistory.ShowDialog(this) == DialogResult.OK)
                {
                    int selectedIndex = FirstSelectedIndex;
                    _subtitleListViewIndex = -1;
                    textBoxListViewText.Text = string.Empty;
                    textBoxListViewTextAlternate.Text = string.Empty;
                    MakeHistoryForUndo(_language.BeforeUndo);
                    string subtitleFormatFriendlyName;

                    string oldFileName = _fileName;
                    DateTime oldFileDateTime = _fileDateTime;

                    _fileName = _subtitle.UndoHistory(showHistory.SelectedIndex, out subtitleFormatFriendlyName, out _fileDateTime);

                    if (string.Compare(oldFileName, _fileName, true) == 0)
                        _fileDateTime = oldFileDateTime; // undo will not give overwrite-newer-file warning

                    SetTitle();
                    ShowStatus(_language.UndoPerformed);

                    comboBoxSubtitleFormats.SelectedIndexChanged -= ComboBoxSubtitleFormatsSelectedIndexChanged;
                    SetCurrentFormat(subtitleFormatFriendlyName);
                    comboBoxSubtitleFormats.SelectedIndexChanged += ComboBoxSubtitleFormatsSelectedIndexChanged;

                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    _change = true;

                    if (selectedIndex >= 0 && selectedIndex < _subtitle.Paragraphs.Count)
                        SubtitleListview1.SelectIndexAndEnsureVisible(selectedIndex);
                    else
                        SubtitleListview1.SelectIndexAndEnsureVisible(0);

                    audioVisualizer.Invalidate();
                }                
            }
            else
            {
                MessageBox.Show(_language.NothingToUndo, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ToolStripButtonSpellCheckClick(object sender, EventArgs e)
        {
            SpellCheck(true);
        }

        private void SpellCheckToolStripMenuItemClick(object sender, EventArgs e)
        {
            SpellCheck(true);
        }

        private void SpellCheckViaWord()
        {            
            if (_subtitle == null | _subtitle.Paragraphs.Count == 0)
                return;            

            WordSpellChecker wordSpellChecker = null;
            int totalLinesChanged = 0;
            try
            {
                wordSpellChecker = new WordSpellChecker(this);
                wordSpellChecker.NewDocument();
                Application.DoEvents();
            }
            catch
            {
                MessageBox.Show(_language.UnableToStartWord);
                return;
            }
            string version = wordSpellChecker.Version;  
          
            int index = FirstSelectedIndex;
            if (index < 0)
                index = 0;

            _cancelWordSpellCheck = false;
            for (;index < _subtitle.Paragraphs.Count; index++)
            {
                Paragraph p = _subtitle.Paragraphs[index];
                int errorsBefore;
                int errorsAfter;
                ShowStatus(string.Format(_language.SpellChekingViaWordXLineYOfX, version, index+1, _subtitle.Paragraphs.Count.ToString()));
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
                string newText = wordSpellChecker.CheckSpelling(p.Text, out errorsBefore, out errorsAfter);
                if (errorsAfter > 0)
                {
                    wordSpellChecker.CloseDocument();
                    wordSpellChecker.Quit();
                    ShowStatus(string.Format(_language.SpellCheckAbortedXCorrections, totalLinesChanged));
                    Cursor = Cursors.Default;
                    return;
                }
                else if (errorsBefore != errorsAfter)
                {
                    if (textBoxListViewText.Text != newText)
                    {
                        textBoxListViewText.Text = newText;
                        totalLinesChanged++;
                    }
                }

                Application.DoEvents();
                if (_cancelWordSpellCheck)
                    break;
            }
            wordSpellChecker.CloseDocument();
            wordSpellChecker.Quit();
            ShowStatus(string.Format(_language.SpellCheckCompletedXCorrections, totalLinesChanged));
            Cursor = Cursors.Default;
        }

        private void SpellCheck(bool autoDetect)
        {
            if (Configuration.Settings.General.SpellChecker.ToLower().Contains("word"))
            {
                SpellCheckViaWord();
                return;
            }

            try
            {
                string dictionaryFolder = Utilities.DictionaryFolder;
                if (!Directory.Exists(dictionaryFolder) || Directory.GetFiles(dictionaryFolder, "*.dic").Length == 0)
                {
                    ShowGetDictionaries();
                    return;
                }

                if (_subtitle != null && _subtitle.Paragraphs.Count > 0)
                {
                    if (_spellCheckForm != null)
                    {
                        DialogResult result = MessageBox.Show(_language.ContinueWithCurrentSpellCheck, Title, MessageBoxButtons.YesNoCancel);
                        if (result == System.Windows.Forms.DialogResult.Cancel)
                            return;

                        if (result == System.Windows.Forms.DialogResult.No)
                        {
                            _spellCheckForm = new SpellCheck();
                            _spellCheckForm.DoSpellCheck(autoDetect, _subtitle, dictionaryFolder, this);
                        }
                        else
                        {
                            _spellCheckForm.ContinueSpellcheck(_subtitle);
                        }
                    }
                    else
                    {
                        _spellCheckForm = new SpellCheck();
                        _spellCheckForm.DoSpellCheck(autoDetect, _subtitle, dictionaryFolder, this);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0}{1}{2}{3}{4}", ex.Source, Environment.NewLine, ex.Message, Environment.NewLine, ex.StackTrace), _title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ChangeWholeTextMainPart(ref int noOfChangedWords, ref bool firstChange, int i, Paragraph p)
        {
            SubtitleListview1.SetText(i, p.Text);
            _change = true;
            noOfChangedWords++;
            if (firstChange)
            {
                MakeHistoryForUndo(_language.BeforeSpellCheck);
                firstChange = false;
            }
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
                ShowSource();
            else
                RefreshSelectedParagraph();
        }     

        public void FocusParagraph(int index)
        {
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
            {
                tabControlSubtitle.SelectedIndex = TabControlListView;
            }

            if (tabControlSubtitle.SelectedIndex == TabControlListView)
            {
               SubtitleListview1.SelectIndexAndEnsureVisible(index);
            }
        }

        private void RefreshSelectedParagraph()
        {
            _subtitleListViewIndex = -1;
            SubtitleListview1_SelectedIndexChanged(null, null);
        }

        public void CorrectWord(string changeWord, Paragraph p, string oldWord, ref bool firstChange)
        {
            if (oldWord != changeWord)
            {
                if (firstChange)
                {
                    MakeHistoryForUndo(_language.BeforeSpellCheck);
                    firstChange = false;
                }
                var regEx = new Regex("\\b" + oldWord + "\\b");
                if (regEx.IsMatch(p.Text))
                {
                    p.Text = regEx.Replace(p.Text, changeWord);
                }
                else
                {                    
                    int startIndex = p.Text.IndexOf(oldWord);
                    while (startIndex >= 0 && startIndex < p.Text.Length && p.Text.Substring(startIndex).Contains(oldWord))
                    {
                        bool startOk = (startIndex == 0) || (p.Text[startIndex - 1] == ' ') ||
                                       (Environment.NewLine.EndsWith(p.Text[startIndex - 1].ToString()));

                        if (startOk)
                        {
                            int end = startIndex + oldWord.Length;
                            if (end <= p.Text.Length)
                            {
                                if ((end == p.Text.Length) || ((" ,.!?:;')" + Environment.NewLine).Contains(p.Text[end].ToString())))
                                    p.Text = p.Text.Remove(startIndex, oldWord.Length).Insert(startIndex, changeWord);
                            }
                        }
                        startIndex = p.Text.IndexOf(oldWord, startIndex + 2);
                    }

                }
                ShowStatus(string.Format(_language.SpellCheckChangedXToY, oldWord, changeWord));
                SubtitleListview1.SetText(_subtitle.GetIndex(p), p.Text);
                _change = true;
                if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
                {
                    ShowSource();
                }
                else
                {
                    RefreshSelectedParagraph();
                }
            }
        }

        private void GetDictionariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowGetDictionaries();
        }

        private void ShowGetDictionaries()
        {
            new GetDictionaries().ShowDialog(this);
        }

        private void ContextMenuStripListviewOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if ((GetCurrentSubtitleFormat().GetType() == typeof(AdvancedSubStationAlpha) || GetCurrentSubtitleFormat().GetType() == typeof(SubStationAlpha))
                && SubtitleListview1.SelectedItems.Count > 0)
            {
                var styles = AdvancedSubStationAlpha.GetStylesFromHeader(_subtitle.Header);
                setStylesForSelectedLinesToolStripMenuItem.DropDownItems.Clear();
                foreach (string style in styles)
                {
                    setStylesForSelectedLinesToolStripMenuItem.DropDownItems.Add(style, null, tsi_Click);
                }
                setStylesForSelectedLinesToolStripMenuItem.Visible = styles.Count > 1;
            }
            else
            {
                setStylesForSelectedLinesToolStripMenuItem.Visible = false; 
            }
            

            toolStripMenuItemGoogleMicrosoftTranslateSelLine.Visible = false;
            if (SubtitleListview1.SelectedItems.Count == 0)
            {
                contextMenuStripEmpty.Show(MousePosition.X, MousePosition.Y);               
                e.Cancel = true;
            }
            else
            {
                toolStripMenuItemInsertBefore.Visible = true;
                toolStripMenuItemInsertAfter.Visible = true;
                toolStripMenuItemInsertSubtitle.Visible = _networkSession == null;
                toolStripMenuItemMergeLines.Visible = true;
                mergeAfterToolStripMenuItem.Visible = true;
                mergeBeforeToolStripMenuItem.Visible = true;
                splitLineToolStripMenuItem.Visible = true;
                toolStripSeparator7.Visible = true;
                typeEffectToolStripMenuItem.Visible = _networkSession == null;
                karokeeEffectToolStripMenuItem.Visible = _networkSession == null;
                toolStripSeparatorAdvancedFunctions.Visible = _networkSession == null;
                showSelectedLinesEarlierlaterToolStripMenuItem.Visible = true;
                visualSyncSelectedLinesToolStripMenuItem.Visible = true;
                googleTranslateSelectedLinesToolStripMenuItem.Visible = true;
                adjustDisplayTimeForSelectedLinesToolStripMenuItem.Visible = true;
                toolStripMenuItemUnbreakLines.Visible = true;
                toolStripMenuItemAutoBreakLines.Visible = true;
                toolStripSeparatorBreakLines.Visible = true;

                if (SubtitleListview1.SelectedItems.Count == 1)
                {
                    toolStripMenuItemMergeLines.Visible = false;
                    visualSyncSelectedLinesToolStripMenuItem.Visible = false;
                    toolStripMenuItemUnbreakLines.Visible = false;
                    toolStripMenuItemAutoBreakLines.Visible = false;
                    toolStripSeparatorBreakLines.Visible = false;

                    toolStripMenuItemGoogleMicrosoftTranslateSelLine.Visible = _subtitleAlternate != null;
                }
                else if (SubtitleListview1.SelectedItems.Count == 2)
                {
                    toolStripMenuItemInsertBefore.Visible = false;
                    toolStripMenuItemInsertAfter.Visible = false;
                    toolStripMenuItemInsertSubtitle.Visible = false;
                    mergeAfterToolStripMenuItem.Visible = false;
                    mergeBeforeToolStripMenuItem.Visible = false;
                    splitLineToolStripMenuItem.Visible = false;
                    typeEffectToolStripMenuItem.Visible = false;
                }
                else if (SubtitleListview1.SelectedItems.Count >= 2)
                {
                    toolStripMenuItemInsertBefore.Visible = false;
                    toolStripMenuItemInsertAfter.Visible = false;
                    toolStripMenuItemInsertSubtitle.Visible = false;
                    splitLineToolStripMenuItem.Visible = false;
                    mergeAfterToolStripMenuItem.Visible = false;
                    mergeBeforeToolStripMenuItem.Visible = false;
                    typeEffectToolStripMenuItem.Visible = false;
                    toolStripSeparator7.Visible = false;

                    if (SubtitleListview1.SelectedItems.Count > 5)
                        toolStripMenuItemMergeLines.Visible = false;
                }

                if (GetCurrentSubtitleFormat().GetType() != typeof(SubRip))
                {
                    karokeeEffectToolStripMenuItem.Visible = false;
                    toolStripSeparatorAdvancedFunctions.Visible = SubtitleListview1.SelectedItems.Count == 1;
                }
            }
        }

        void tsi_Click(object sender, EventArgs e)
        {
            string style = (sender as ToolStripItem).Text;
            foreach (int index in SubtitleListview1.SelectedIndices)
            {
                _subtitle.Paragraphs[index].Extra = style;
                SubtitleListview1.SetExtraText(index, style, SubtitleListview1.ForeColor);
            }
        }

        private void BoldToolStripMenuItemClick(object sender, EventArgs e)
        {
            ListViewToggleTag("b");
        }

        private void ItalicToolStripMenuItemClick(object sender, EventArgs e)
        {
            ListViewToggleTag("i");
        }

        private void UnderlineToolStripMenuItemClick(object sender, EventArgs e)
        {
            ListViewToggleTag("u");
        }

        private void ListViewToggleTag(string tag)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
                MakeHistoryForUndo(string.Format(_language.BeforeAddingTagX, tag));

                var indexes = new List<int>();
                foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    indexes.Add(item.Index);

                SubtitleListview1.BeginUpdate();
                foreach (int i in indexes)
                {
                    if (_subtitle.Paragraphs[i].Text.Contains("<" + tag + ">"))
                    {
                        _subtitle.Paragraphs[i].Text = _subtitle.Paragraphs[i].Text.Replace("<" + tag + ">", string.Empty);
                        _subtitle.Paragraphs[i].Text = _subtitle.Paragraphs[i].Text.Replace("</" + tag + ">", string.Empty);
                    }
                    else
                    {
                        _subtitle.Paragraphs[i].Text = string.Format("<{0}>{1}</{0}>", tag, _subtitle.Paragraphs[i].Text);
                    }
                    SubtitleListview1.SetText(i, _subtitle.Paragraphs[i].Text);
                }
                SubtitleListview1.EndUpdate();

                ShowStatus(string.Format(_language.TagXAdded, tag));
                ShowSource();
                _change = true;
                RefreshSelectedParagraph();
                SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
            }
        }

        private void ToolStripMenuItemDeleteClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                string statusText;
                string historyText;
                string askText;

                if (SubtitleListview1.SelectedItems.Count > 1)
                {
                    statusText = string.Format(_language.XLinesDeleted, SubtitleListview1.SelectedItems.Count);
                    historyText = string.Format(_language.BeforeDeletingXLines, SubtitleListview1.SelectedItems.Count);
                    askText = string.Format(_language.DeleteXLinesPrompt, SubtitleListview1.SelectedItems.Count);
                }
                else
                {
                    statusText = _language.OneLineDeleted;
                    historyText = _language.BeforeDeletingOneLine;
                    askText = _language.DeleteOneLinePrompt;
                }

                if (Configuration.Settings.General.PromptDeleteLines && MessageBox.Show(askText, Title, MessageBoxButtons.YesNo) == DialogResult.No)
                    return;

                MakeHistoryForUndo(historyText);
                _subtitleListViewIndex = -1;

                if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                {
                    var alternateIndexes = new List<int>();
                    foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    {
                        Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                        if (p != null)
                        {
                            Paragraph original = Utilities.GetOriginalParagraph(item.Index, p, _subtitleAlternate.Paragraphs);
                            if (original != null)
                                alternateIndexes.Add(_subtitleAlternate.GetIndex(original));
                        }
                        alternateIndexes.Add(item.Index);
                    }
                    
                    alternateIndexes.Reverse();
                    foreach (int i in alternateIndexes)
                    {
                        if (i <_subtitleAlternate.Paragraphs.Count)
                            _subtitleAlternate.Paragraphs.RemoveAt(i);
                    }
                }

                var indexes = new List<int>();
                foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    indexes.Add(item.Index);
                int firstIndex = SubtitleListview1.SelectedItems[0].Index;              

                if (_networkSession != null)
                {
                    _networkSession.TimerStop();
                    NetworkGetSendUpdates(indexes, 0, null);                    
                }
                else
                {
                    int startNumber = _subtitle.Paragraphs[0].Number;
                    indexes.Reverse();
                    foreach (int i in indexes)
                    {
                        _subtitle.Paragraphs.RemoveAt(i);
                        if (_networkSession != null && _networkSession.LastSubtitle != null && i < _networkSession.LastSubtitle.Paragraphs.Count)
                            _networkSession.LastSubtitle.Paragraphs.RemoveAt(i);
                    }
                    _subtitle.Renumber(startNumber);
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    if (SubtitleListview1.FirstVisibleIndex == 0)
                        SubtitleListview1.FirstVisibleIndex = -1;
                    if (SubtitleListview1.Items.Count > firstIndex)
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(firstIndex);
                    }
                    else if (SubtitleListview1.Items.Count > 0)
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(SubtitleListview1.Items.Count - 1);
                    }
                }

                ShowStatus(statusText);
                ShowSource();
                _change = true;
            }
        }

        private void ToolStripMenuItemInsertBeforeClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0)
                InsertBefore();
        }

        private void InsertBefore()
        {            
            MakeHistoryForUndo(_language.BeforeInsertLine);

            int startNumber = 1;
            if (_subtitle.Paragraphs.Count > 0)
                startNumber = _subtitle.Paragraphs[0].Number;
            int firstSelectedIndex = 0;
            if (SubtitleListview1.SelectedItems.Count > 0)
                firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

            int addMilliseconds = Configuration.Settings.General.MininumMillisecondsBetweenLines +1;
            if (addMilliseconds < 1)
                addMilliseconds = 1;

            var newParagraph = new Paragraph();
            Paragraph prev = _subtitle.GetParagraphOrDefault(firstSelectedIndex - 1);
            Paragraph next = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
            if (prev != null && next != null)
            {
                newParagraph.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - addMilliseconds;
                newParagraph.StartTime.TotalMilliseconds = newParagraph.EndTime.TotalMilliseconds - 2000;
                if (newParagraph.StartTime.TotalMilliseconds <= prev.EndTime.TotalMilliseconds)
                    newParagraph.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
                if (newParagraph.Duration.TotalMilliseconds < 100)
                    newParagraph.EndTime.TotalMilliseconds += 100;
            }
            else if (prev != null)
            {
                newParagraph.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + addMilliseconds;
                newParagraph.EndTime.TotalMilliseconds = newParagraph.StartTime.TotalMilliseconds + 1200;
                if (next != null && newParagraph.EndTime.TotalMilliseconds > next.StartTime.TotalMilliseconds)
                    newParagraph.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1;
                if (newParagraph.StartTime.TotalMilliseconds > newParagraph.EndTime.TotalMilliseconds)
                    newParagraph.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
            }
            else if (next != null)
            {
                newParagraph.StartTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1200;
                newParagraph.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1;
            }
            else
            {
                newParagraph.StartTime.TotalMilliseconds = 1000;
                newParagraph.EndTime.TotalMilliseconds = 3000;
            }
            if (GetCurrentSubtitleFormat().IsFrameBased)
            {
                newParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                newParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
            }

            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                _subtitleAlternate.InsertParagraphInCorrectTimeOrder(new Paragraph(newParagraph));
            }

            if (_networkSession != null)
            {
                _networkSession.TimerStop();
                NetworkGetSendUpdates(new List<int>(), firstSelectedIndex, newParagraph);
            }
            else
            {
                _subtitle.Paragraphs.Insert(firstSelectedIndex, newParagraph);
                _subtitleListViewIndex = -1;
                _subtitle.Renumber(startNumber);
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
            }
            SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedIndex);
            ShowSource();
            ShowStatus(_language.LineInserted);
            _change = true;
        }

        private void ToolStripMenuItemInsertAfterClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0)
            {
                InsertAfter();
            }
        }

        private void InsertAfter()
        {
            MakeHistoryForUndo(_language.BeforeInsertLine);

            int startNumber = 1;
            if (_subtitle.Paragraphs.Count > 0)
                startNumber = _subtitle.Paragraphs[0].Number;
            
            int firstSelectedIndex = 0;
            if (SubtitleListview1.SelectedItems.Count > 0)
                firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index + 1;

            var newParagraph = new Paragraph();
            Paragraph prev = _subtitle.GetParagraphOrDefault(firstSelectedIndex - 1);
            Paragraph next = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
            if (prev != null)
            {
                int addMilliseconds = Configuration.Settings.General.MininumMillisecondsBetweenLines;
                if (addMilliseconds < 1)
                    addMilliseconds = 1;

                newParagraph.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + addMilliseconds;
                newParagraph.EndTime.TotalMilliseconds = newParagraph.StartTime.TotalMilliseconds + 1200;
                if (next != null && newParagraph.EndTime.TotalMilliseconds > next.StartTime.TotalMilliseconds)
                    newParagraph.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1;
                if (newParagraph.StartTime.TotalMilliseconds > newParagraph.EndTime.TotalMilliseconds)
                    newParagraph.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
            }
            else if (next != null)
            {
                newParagraph.StartTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1200;
                newParagraph.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1;
            }
            else
            {
                newParagraph.StartTime.TotalMilliseconds = 1000;
                newParagraph.EndTime.TotalMilliseconds = 3000;
            }
            if (GetCurrentSubtitleFormat().IsFrameBased)
            {
                newParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                newParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
            }

            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                _subtitleAlternate.InsertParagraphInCorrectTimeOrder(new Paragraph(newParagraph));
            }

            if (_networkSession != null)
            {
                _networkSession.TimerStop();
                NetworkGetSendUpdates(new List<int>(), firstSelectedIndex, newParagraph);
            }
            else
            {
                _subtitle.Paragraphs.Insert(firstSelectedIndex, newParagraph);
                _subtitle.Renumber(startNumber);
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
            }
            SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedIndex);
            ShowSource();
            ShowStatus(_language.LineInserted);
            _change = true;
        }

        private void SubtitleListView1SelectedIndexChange()
        {
            if (buttonUndoListViewChanges.Visible)
                buttonUndoListViewChanges.Enabled = false;
            StopAutoDuration();
            ShowLineInformationListView();            
            if (_subtitle.Paragraphs.Count > 0)
            {
                int firstSelectedIndex = 0;
                if (SubtitleListview1.SelectedItems.Count > 0)
                    firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                if (_subtitleListViewIndex >= 0)
                {
                    if (_subtitleListViewIndex == firstSelectedIndex)
                        return;

                    bool showSource = false;

                    Paragraph last = _subtitle.GetParagraphOrDefault(_subtitleListViewIndex);
                    if (textBoxListViewText.Text != last.Text)
                    {
                        last.Text = textBoxListViewText.Text.TrimEnd();
                        SubtitleListview1.SetText(_subtitleListViewIndex, last.Text);
                        showSource = true;
                    }

                    TimeCode startTime = timeUpDownStartTime.TimeCode;
                    if (startTime != null)
                    {
                        if (last.StartTime.TotalMilliseconds != startTime.TotalMilliseconds)
                        {
                            double dur = last.Duration.TotalMilliseconds;
                            last.StartTime.TotalMilliseconds = startTime.TotalMilliseconds;
                            last.EndTime.TotalMilliseconds = startTime.TotalMilliseconds + dur;
                            SubtitleListview1.SetStartTime(_subtitleListViewIndex, last);
                            showSource = true;
                        }
                    }

                    double duration = (double)numericUpDownDuration.Value * 1000.0;
                    if (duration > 0 && duration < 100000 && duration != last.Duration.TotalMilliseconds)
                    {
                        last.EndTime.TotalMilliseconds = last.StartTime.TotalMilliseconds + duration;
                        SubtitleListview1.SetDuration(_subtitleListViewIndex, last);
                        showSource = true;
                    }

                    if (showSource)
                    {
                        MakeHistoryForUndo(_language.BeforeLineUpdatedInListView);
                        ShowSource();
                    }
                }

                Paragraph p = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (p != null)
                {
                    InitializeListViewEditBox(p);
                    _subtitleListViewIndex = firstSelectedIndex;
                    _oldSelectedParagraph = new Paragraph(p);
                    UpdateListViewTextInfo(labelTextLineLengths, labelSingleLine, labelTextLineTotal, labelCharactersPerSecond, p);

                    if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                    {
                        InitializeListViewEditBoxAlternate(p, firstSelectedIndex);
                        labelAlternateCharactersPerSecond.Left = textBoxListViewTextAlternate.Left + (textBoxListViewTextAlternate.Width - labelAlternateCharactersPerSecond.Width);
                        labelTextAlternateLineTotal.Left = textBoxListViewTextAlternate.Left + (textBoxListViewTextAlternate.Width - labelTextAlternateLineTotal.Width);
                    }
                }
            }
        }

        private void SubtitleListview1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SubtitleListView1SelectedIndexChange();
        }

        private void ShowLineInformationListView()
        {
            if (SubtitleListview1.SelectedItems.Count == 1)
                toolStripSelected.Text = string.Format("{0}/{1}", SubtitleListview1.SelectedItems[0].Index + 1, SubtitleListview1.Items.Count);
            else
                toolStripSelected.Text = string.Format(_language.XLinesSelected, SubtitleListview1.SelectedItems.Count);
        }

        private void UpdateListViewTextCharactersPerSeconds(Label charsPerSecond, Paragraph paragraph)
        {
            const string zeroWhiteSpace = "\u200B";
            const string zeroWidthNoBreakSpace = "\uFEFF";

            string s = Utilities.RemoveHtmlTags(paragraph.Text).Replace(" ", string.Empty).Replace(Environment.NewLine, string.Empty).Replace(zeroWhiteSpace, string.Empty).Replace(zeroWidthNoBreakSpace, string.Empty);
            if (paragraph.Duration.TotalSeconds > 0)
            {
                double charactersPerSecond = s.Length / paragraph.Duration.TotalSeconds;                
                if (charactersPerSecond > Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds + 7)
                    charsPerSecond.ForeColor = System.Drawing.Color.Red;
                else if (charactersPerSecond > Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds)
                    charsPerSecond.ForeColor = System.Drawing.Color.Orange;
                else
                    charsPerSecond.ForeColor = System.Drawing.Color.Black;
                charsPerSecond.Text = string.Format(_language.CharactersPerSecond, charactersPerSecond);
            }
            else
            {
                charsPerSecond.ForeColor = System.Drawing.Color.Red;
                charsPerSecond.Text = string.Format(_language.CharactersPerSecond, _languageGeneral.NotAvailable);
            }
        }

        private void UpdateListViewTextInfo(Label lineLengths, Label singleLine, Label lineTotal, Label charactersPerSecond, Paragraph paragraph)
        {
            if (paragraph == null)
                return;

            string text = paragraph.Text;
            lineLengths.Text = _languageGeneral.SingleLineLengths;
            singleLine.Left = lineLengths.Left + lineLengths.Width - 6;
            Utilities.GetLineLengths(singleLine, text);

            buttonSplitLine.Visible = false;
            string s = Utilities.RemoveHtmlTags(text).Replace(Environment.NewLine, " ");
            if (s.Length < Configuration.Settings.General.SubtitleLineMaximumLength * 1.9)
            {
                lineTotal.ForeColor = System.Drawing.Color.Black;
                lineTotal.Text = string.Format(_languageGeneral.TotalLengthX, s.Length);
            }
            else if (s.Length < Configuration.Settings.General.SubtitleLineMaximumLength * 2.1)
            {
                lineTotal.ForeColor = System.Drawing.Color.Orange;
                lineTotal.Text = string.Format(_languageGeneral.TotalLengthX, s.Length);
            }
            else
            {
                lineTotal.ForeColor = System.Drawing.Color.Red;
                lineTotal.Text = string.Format(_languageGeneral.TotalLengthXSplitLine, s.Length);
                if (buttonUnBreak.Visible)
                {
                    lineTotal.Text = string.Format(_languageGeneral.TotalLengthX, s.Length);
                    buttonSplitLine.Visible = true;
                }
            }
            UpdateListViewTextCharactersPerSeconds(charactersPerSecond, paragraph);
            labelCharactersPerSecond.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelCharactersPerSecond.Width);
            labelTextLineTotal.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelTextLineTotal.Width);
        }        

        private void ButtonNextClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0)
            {
                int firstSelectedIndex = 0;
                if (SubtitleListview1.SelectedItems.Count > 0)
                    firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                firstSelectedIndex++;
                Paragraph p = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (p != null)
                { 
                    SubtitleListview1.SelectNone();
                    SubtitleListview1.Items[firstSelectedIndex].Selected = true;
                    SubtitleListview1.EnsureVisible(firstSelectedIndex);
                }
            }
        }

        private void ButtonPreviousClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0)
            {
                int firstSelectedIndex = 1;
                if (SubtitleListview1.SelectedItems.Count > 0)
                    firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                firstSelectedIndex--;
                Paragraph p = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (p != null)
                {
                    SubtitleListview1.SelectNone();
                    SubtitleListview1.Items[firstSelectedIndex].Selected = true;
                    SubtitleListview1.EnsureVisible(firstSelectedIndex);
                }
            }
        }

        private void NormalToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                MakeHistoryForUndo(_language.BeforeSettingFontToNormal);
                foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                    if (p != null)
                    {
                        p.Text = Utilities.RemoveHtmlTags(p.Text);
                        SubtitleListview1.SetText(item.Index, p.Text);
                    }
                }
                ShowSource();
                _change = true;
                RefreshSelectedParagraph();
            }
        }

        private void ButtonAutoBreakClick(object sender, EventArgs e)
        {
            if (textBoxListViewText.Text.Length > 0)
                textBoxListViewText.Text = Utilities.AutoBreakLine(textBoxListViewText.Text);
        }

        private void TextBoxListViewTextTextChanged(object sender, EventArgs e)
        {
            if (_subtitleListViewIndex >= 0)
            {
                string text = textBoxListViewText.Text.TrimEnd();
                
                // update _subtitle + listview
                _subtitle.Paragraphs[_subtitleListViewIndex].Text = text;
                UpdateListViewTextInfo(labelTextLineLengths, labelSingleLine, labelTextLineTotal, labelCharactersPerSecond, _subtitle.Paragraphs[_subtitleListViewIndex]);
                SubtitleListview1.SetText(_subtitleListViewIndex, text);
                _change = true;

                if (buttonUndoListViewChanges.Visible)
                    buttonUndoListViewChanges.Enabled = _oldSelectedParagraph != null && text != _oldSelectedParagraph.Text;
            }
        }

        private void TextBoxListViewTextAlternateTextChanged(object sender, EventArgs e)
        {
            if (_subtitleListViewIndex >= 0)
            {
                Paragraph p = _subtitle.GetParagraphOrDefault(_subtitleListViewIndex);
                if (p == null)
                    return;

                Paragraph original = Utilities.GetOriginalParagraph(_subtitleListViewIndex, p, _subtitleAlternate.Paragraphs);
                if (original != null)
                {
                    string text = textBoxListViewTextAlternate.Text.TrimEnd();

                    // update _subtitle + listview
                    original.Text = text;
                    UpdateListViewTextInfo(labelTextAlternateLineLengths, labelAlternateSingleLine, labelTextAlternateLineTotal, labelAlternateCharactersPerSecond, original);
                    SubtitleListview1.SetAlternateText(_subtitleListViewIndex, text);
                    _changeAlternate = true;
                }
                else
                { 
                }
            }
        }

        private void TextBoxListViewTextKeyDown(object sender, KeyEventArgs e)
        {
            int numberOfNewLines = textBoxListViewText.Text.Length - textBoxListViewText.Text.Replace(Environment.NewLine, " ").Length;

            Utilities.CheckAutoWrap(textBoxListViewText, e, numberOfNewLines);

            if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None && numberOfNewLines > 1)
            {
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.R)
            {
                ButtonAutoBreakClick(null, null);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.U)
            {
                ButtonUnBreakClick(null, null);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.I)
            {
                if (textBoxListViewText.SelectionLength == 0)
                {
                    string tag = "i";
                    if (textBoxListViewText.Text.Contains("<" + tag + ">"))
                    {
                        textBoxListViewText.Text = textBoxListViewText.Text.Replace("<" + tag + ">", string.Empty);
                        textBoxListViewText.Text = textBoxListViewText.Text.Replace("</" + tag + ">", string.Empty);
                    }
                    else
                    {
                        textBoxListViewText.Text = string.Format("<{0}>{1}</{0}>", tag, textBoxListViewText.Text);
                    }
                    //SubtitleListview1.SetText(i, textBoxListViewText.Text);
                }
                else
                {
                    TextBoxListViewToggleTag("i");
                }
            }
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.D)
            {
                textBoxListViewText.SelectionLength = 0;
                e.SuppressKeyPress = true;
            }

            // last key down in text
            _lastTextKeyDownTicks = DateTime.Now.Ticks;
        }

        private void SplitLineToolStripMenuItemClick(object sender, EventArgs e)
        {
            SplitSelectedParagraph(null, null);
        }

        private void SplitSelectedParagraph(double? splitSeconds, int? textIndex)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
                MakeHistoryForUndo(_language.BeforeSplitLine);

                int startNumber = _subtitle.Paragraphs[0].Number;
                int firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                Paragraph currentParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                var newParagraph = new Paragraph();

                string oldText = currentParagraph.Text;
                string[] lines = currentParagraph.Text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (textIndex != null && textIndex.Value > 2 && textIndex.Value < oldText.Length -2)
                {
                    currentParagraph.Text = Utilities.AutoBreakLine(oldText.Substring(0, textIndex.Value).Trim());
                    newParagraph.Text = Utilities.AutoBreakLine(oldText.Substring(textIndex.Value).Trim());
                }
                else
                {
                    if (lines.Length == 2 && (lines[0].EndsWith(".") || lines[0].EndsWith("!") || lines[0].EndsWith("?")))
                    {
                        currentParagraph.Text = Utilities.AutoBreakLine(lines[0]);
                        newParagraph.Text = Utilities.AutoBreakLine(lines[1]);
                    }
                    else
                    {
                        string s = Utilities.AutoBreakLine(currentParagraph.Text, 5, Configuration.Settings.General.SubtitleLineMaximumLength * 2, Configuration.Settings.Tools.MergeLinesShorterThan);
                        lines = s.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length == 2)
                        {
                            currentParagraph.Text = Utilities.AutoBreakLine(lines[0]);
                            newParagraph.Text = Utilities.AutoBreakLine(lines[1]);
                        }
                    }
                }

                double startFactor = (double)Utilities.RemoveHtmlTags(currentParagraph.Text).Length / Utilities.RemoveHtmlTags(oldText).Length;
                if (startFactor < 0.20)
                    startFactor = 0.20;
                if (startFactor > 0.80)
                    startFactor = 0.80;

                double middle = currentParagraph.StartTime.TotalMilliseconds + (currentParagraph.Duration.TotalMilliseconds * startFactor);
                if (splitSeconds.HasValue && splitSeconds.Value > (currentParagraph.StartTime.TotalSeconds + 0.2) && splitSeconds.Value < (currentParagraph.EndTime.TotalSeconds - 0.2))
                    middle = splitSeconds.Value * 1000.0;
                newParagraph.EndTime.TotalMilliseconds = currentParagraph.EndTime.TotalMilliseconds;
                currentParagraph.EndTime.TotalMilliseconds = middle;
                newParagraph.StartTime.TotalMilliseconds = currentParagraph.EndTime.TotalMilliseconds + 1;

                if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                {
                    Paragraph originalCurrent = Utilities.GetOriginalParagraph(firstSelectedIndex, currentParagraph, _subtitleAlternate.Paragraphs);
                    if (originalCurrent != null)
                    {
                        originalCurrent.EndTime.TotalMilliseconds = currentParagraph.EndTime.TotalMilliseconds;
                        Paragraph originalNew = new Paragraph(newParagraph);

                        lines = originalCurrent.Text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length == 2 && (lines[0].EndsWith(".") || lines[0].EndsWith("!") || lines[0].EndsWith("?")))
                        {
                            currentParagraph.Text = Utilities.AutoBreakLine(lines[0]);
                            newParagraph.Text = Utilities.AutoBreakLine(lines[1]);
                        }
                        else
                        {
                            string s = Utilities.AutoBreakLine(originalCurrent.Text, 5, Configuration.Settings.General.SubtitleLineMaximumLength * 2, Configuration.Settings.Tools.MergeLinesShorterThan);
                            lines = s.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        }
                        if (lines.Length == 2)
                        {
                            originalCurrent.Text = Utilities.AutoBreakLine(lines[0]);
                            originalNew.Text = Utilities.AutoBreakLine(lines[1]);
                        }
                        _subtitleAlternate.InsertParagraphInCorrectTimeOrder(originalNew);
                    }
                }

                if (_networkSession != null)
                {
                    _networkSession.TimerStop();
                    NetworkGetSendUpdates(new List<int>(), firstSelectedIndex, newParagraph);
                }
                else
                {
                    if (GetCurrentSubtitleFormat().IsFrameBased)
                    {
                        if (currentParagraph != null)
                        {
                            currentParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                            currentParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                        }
                        newParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                        newParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                    }
                    _subtitle.Paragraphs.Insert(firstSelectedIndex + 1, newParagraph);
                    _subtitle.Renumber(startNumber);
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                }
                SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedIndex);
                ShowSource();
                ShowStatus(_language.LineSplitted);
                SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                RefreshSelectedParagraph();
                _change = true;
            }
        }

        private void MergeBeforeToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                int startNumber = _subtitle.Paragraphs[0].Number;
                int firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                Paragraph prevParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex-1);
                Paragraph currentParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex);

                if (prevParagraph != null && currentParagraph != null)
                {
                    SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
                    MakeHistoryForUndo(_language.BeforeMergeLines);

                    prevParagraph.Text = prevParagraph.Text.Replace(Environment.NewLine, " ");
                    prevParagraph.Text += Environment.NewLine + currentParagraph.Text.Replace(Environment.NewLine, " ");
                    prevParagraph.Text = Utilities.AutoBreakLine(prevParagraph.Text);

//                    prevParagraph.EndTime.TotalMilliseconds = prevParagraph.EndTime.TotalMilliseconds + currentParagraph.Duration.TotalMilliseconds; 
                    prevParagraph.EndTime.TotalMilliseconds = currentParagraph.EndTime.TotalMilliseconds;

                    if (_networkSession != null)
                    {
                        _networkSession.TimerStop();
                        List<int> deleteIndices = new List<int>();
                        deleteIndices.Add(_subtitle.GetIndex(currentParagraph));
                        NetworkGetSendUpdates(deleteIndices, 0, null);
                    }
                    else
                    {
                        _subtitle.Paragraphs.Remove(currentParagraph);
                        _subtitle.Renumber(startNumber);
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                        SubtitleListview1.Items[firstSelectedIndex-1].Selected = true;
                    }
                    SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedIndex - 1);
                    ShowSource();
                    ShowStatus(_language.LinesMerged);
                    SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                    RefreshSelectedParagraph();
                    _change = true;
                }
            }
        }

        private void MergeSelectedLines()
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 1)
            {
                StringBuilder sb = new StringBuilder();
                List<int> deleteIndices = new List<int>();
                bool first = true;
                int firstIndex = 0;
                foreach (int index in SubtitleListview1.SelectedIndices)
                {
                    if (first)
                        firstIndex = index;
                    else
                        deleteIndices.Add(index);
                    sb.AppendLine(_subtitle.Paragraphs[index].Text);
                    first = false;
                }

                if (sb.Length > 200)
                    return;

                SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
                MakeHistoryForUndo(_language.BeforeMergeLines);

                Paragraph currentParagraph = _subtitle.Paragraphs[firstIndex];
                string text = sb.ToString();
                text = Utilities.FixInvalidItalicTags(text);
                text = ChangeAllLinesItalictoSingleItalic(text);
                text = Utilities.AutoBreakLine(text);
                currentParagraph.Text = text;

                //display time
                currentParagraph.EndTime.TotalMilliseconds = currentParagraph.StartTime.TotalMilliseconds + Utilities.GetDisplayMillisecondsFromText(text);
                Paragraph nextParagraph = _subtitle.GetParagraphOrDefault(_subtitle.GetIndex(currentParagraph) + 1);
                if (nextParagraph != null && currentParagraph.EndTime.TotalMilliseconds > nextParagraph.StartTime.TotalMilliseconds && currentParagraph.StartTime.TotalMilliseconds < nextParagraph.StartTime.TotalMilliseconds)
                {
                    currentParagraph.EndTime.TotalMilliseconds = nextParagraph.StartTime.TotalMilliseconds - 1;
                }

                if (_networkSession != null)
                {
                    _networkSession.TimerStop();
                    _networkSession.UpdateLine(firstIndex, currentParagraph);
                    NetworkGetSendUpdates(deleteIndices, 0, null);
                }
                else
                {
                    for (int i = deleteIndices.Count - 1; i >= 0; i--)
                        _subtitle.Paragraphs.RemoveAt(deleteIndices[i]);
                    _subtitle.Renumber(1);
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                }
                ShowSource();
                ShowStatus(_language.LinesMerged);
                SubtitleListview1.SelectIndexAndEnsureVisible(firstIndex);
                SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                RefreshSelectedParagraph();
                _change = true;
            }
        }

        private static string ChangeAllLinesItalictoSingleItalic(string text)
        {
            bool allLinesStartAndEndsWithItalic = true;
            foreach (string line in text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.Trim().StartsWith("<i>") || !line.Trim().EndsWith("</i>"))
                    allLinesStartAndEndsWithItalic = false;
            }
            if (allLinesStartAndEndsWithItalic)
            {
                text = text.Replace("<i>", string.Empty).Replace("</i>", string.Empty).Trim();
                text = "<i>" + text + "</i>";
            }
            return text;
        }

        private void MergeAfterToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                if (SubtitleListview1.SelectedItems.Count > 2)
                {
                    MergeSelectedLines();
                    return;
                }

                int startNumber = _subtitle.Paragraphs[0].Number;
                int firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                Paragraph currentParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                Paragraph nextParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex + 1);

                if (nextParagraph != null && currentParagraph != null)
                {
                    SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
                    MakeHistoryForUndo(_language.BeforeMergeLines);

                    currentParagraph.Text = currentParagraph.Text.Replace(Environment.NewLine, " ");
                    currentParagraph.Text += Environment.NewLine + nextParagraph.Text.Replace(Environment.NewLine, " ");
                    currentParagraph.Text = ChangeAllLinesItalictoSingleItalic(currentParagraph.Text);
                    currentParagraph.Text = Utilities.AutoBreakLine(currentParagraph.Text);

                    //currentParagraph.EndTime.TotalMilliseconds = currentParagraph.EndTime.TotalMilliseconds + nextParagraph.Duration.TotalMilliseconds; //nextParagraph.EndTime;
                    currentParagraph.EndTime.TotalMilliseconds = nextParagraph.EndTime.TotalMilliseconds;

                    if (_networkSession != null)
                    {
                        _networkSession.TimerStop();
                        _networkSession.UpdateLine(_subtitle.GetIndex(currentParagraph), currentParagraph);
                        List<int> deleteIndices = new List<int>();
                        deleteIndices.Add(_subtitle.GetIndex(nextParagraph));
                        NetworkGetSendUpdates(deleteIndices, 0, null);
                    }
                    else
                    {
                        _subtitle.Paragraphs.Remove(nextParagraph);
                        _subtitle.Renumber(startNumber);
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    }
                    ShowSource();
                    ShowStatus(_language.LinesMerged);
                    SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedIndex);
                    SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                    RefreshSelectedParagraph();                    
                    _change = true;
                }
            }
        }

        private void UpdateStartTimeInfo(TimeCode startTime)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0 && startTime != null)
            {
                UpdateOverlapErrors(startTime);

                // update _subtitle + listview
                Paragraph p = _subtitle.Paragraphs[_subtitleListViewIndex];
                p.EndTime.TotalMilliseconds += (startTime.TotalMilliseconds - p.StartTime.TotalMilliseconds);
                p.StartTime = startTime;
                SubtitleListview1.SetStartTime(_subtitleListViewIndex, p);
                if (GetCurrentSubtitleFormat().IsFrameBased)
                {
                    p.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                    p.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                }
            }
        }

        private void UpdateOverlapErrors(TimeCode startTime)
        {
            labelStartTimeWarning.Text = string.Empty;
            labelDurationWarning.Text = string.Empty;

            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0 && startTime != null)
            {

                int firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                Paragraph prevParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex - 1);
                if (prevParagraph != null && prevParagraph.EndTime.TotalMilliseconds > startTime.TotalMilliseconds)
                    labelStartTimeWarning.Text = string.Format(_languageGeneral.OverlapPreviousLineX, prevParagraph.EndTime.TotalSeconds - startTime.TotalSeconds);

                Paragraph nextParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex + 1);
                if (nextParagraph != null)
                {
                    double durationMilliSeconds = (double)numericUpDownDuration.Value * 1000.0;
                    if (startTime.TotalMilliseconds + durationMilliSeconds > nextParagraph.StartTime.TotalMilliseconds)
                    {
                        labelDurationWarning.Text = string.Format(_languageGeneral.OverlapX, ((startTime.TotalMilliseconds + durationMilliSeconds) - nextParagraph.StartTime.TotalMilliseconds) / 1000.0);
                    }

                    if (labelStartTimeWarning.Text.Length == 0 &&
                        startTime.TotalMilliseconds > nextParagraph.StartTime.TotalMilliseconds)
                    {
                        double di = (startTime.TotalMilliseconds - nextParagraph.StartTime.TotalMilliseconds) / 1000.0;
                        labelStartTimeWarning.Text = string.Format(_languageGeneral.OverlapNextX, di);
                    }
                    else if (numericUpDownDuration.Value < 0)
                    {
                        labelDurationWarning.Text = _languageGeneral.Negative;
                    }
                }                
            }
        }
       
        private void NumericUpDownDurationValueChanged(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                int firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                Paragraph currentParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (currentParagraph != null)
                {
                    UpdateOverlapErrors(timeUpDownStartTime.TimeCode);
                    UpdateListViewTextCharactersPerSeconds(labelCharactersPerSecond, currentParagraph);                   

                    // update _subtitle + listview
                    string oldDuration = currentParagraph.Duration.ToString();
                    currentParagraph.EndTime.TotalMilliseconds = currentParagraph.StartTime.TotalMilliseconds + ((double)numericUpDownDuration.Value * 1000.0);
                    SubtitleListview1.SetDuration(firstSelectedIndex, currentParagraph);
                    _change = true;

                    if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                    {
                        Paragraph original = Utilities.GetOriginalParagraph(firstSelectedIndex, currentParagraph, _subtitleAlternate.Paragraphs);
                        if (original != null)
                        {
                            original.EndTime.TotalMilliseconds = currentParagraph.EndTime.TotalMilliseconds;
                            _changeAlternate = true;
                        }
                    }

                    if (GetCurrentSubtitleFormat().IsFrameBased)
                    {
                        currentParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                        currentParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                    }

                    if (_makeHistory)
                        MakeHistoryForUndoWhenNoMoreChanges(string.Format(_language.DisplayTimeAdjustedX, "#" + currentParagraph.Number + ": " +  oldDuration + " -> " + currentParagraph.Duration.ToString()));
                }
            }
        }

        private void ButtonUndoListViewChangesClick(object sender, EventArgs e)
        {
            if (_subtitleListViewIndex >= 0 && _oldSelectedParagraph != null)
            {
                var p = new Paragraph(_oldSelectedParagraph);
                _subtitle.Paragraphs[_subtitleListViewIndex] = p;

                SubtitleListview1.SetText(_subtitleListViewIndex, p.Text);
                SubtitleListview1.SetStartTime(_subtitleListViewIndex, p);
                SubtitleListview1.SetDuration(_subtitleListViewIndex, p);

                InitializeListViewEditBox(p);
                buttonUndoListViewChanges.Enabled = false;
            }
        }


        private void InitializeListViewEditBoxAlternate(Paragraph p, int firstSelectedIndex)
        {
            if (_subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                Paragraph original = Utilities.GetOriginalParagraph(firstSelectedIndex, p, _subtitleAlternate.Paragraphs);
                if (original == null)
                {
                    textBoxListViewTextAlternate.Enabled = false;
                    textBoxListViewTextAlternate.Text = string.Empty;
                    labelAlternateCharactersPerSecond.Text = string.Empty;
                }
                else
                {
                    textBoxListViewTextAlternate.Enabled = true;
                    textBoxListViewTextAlternate.TextChanged -= TextBoxListViewTextAlternateTextChanged;
                    bool changeAlternate = _changeAlternate;
                    textBoxListViewTextAlternate.Text = original.Text;
                    _changeAlternate = changeAlternate;
                    textBoxListViewTextAlternate.TextChanged += TextBoxListViewTextAlternateTextChanged;
                    //UpdateOverlapErrors(timeUpDownStartTime.TimeCode);
                    UpdateListViewTextCharactersPerSeconds(labelAlternateCharactersPerSecond, p);
                }                
            }
        }

        private void InitializeListViewEditBox(Paragraph p)
        {
            textBoxListViewText.TextChanged -= TextBoxListViewTextTextChanged;
            textBoxListViewText.Text = p.Text;
            textBoxListViewText.TextChanged += TextBoxListViewTextTextChanged;
            
            timeUpDownStartTime.MaskedTextBox.TextChanged -= MaskedTextBox_TextChanged;
            timeUpDownStartTime.TimeCode = p.StartTime;
            timeUpDownStartTime.MaskedTextBox.TextChanged += MaskedTextBox_TextChanged;

            numericUpDownDuration.ValueChanged -= NumericUpDownDurationValueChanged;
            numericUpDownDuration.Value = (decimal)(p.Duration.TotalSeconds);
            numericUpDownDuration.ValueChanged += NumericUpDownDurationValueChanged;

            UpdateOverlapErrors(timeUpDownStartTime.TimeCode);
            UpdateListViewTextCharactersPerSeconds(labelCharactersPerSecond, p);
            if (_subtitle != null && _subtitle.Paragraphs.Count > 0)
                textBoxListViewText.Enabled = true;
        }

        void MaskedTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_subtitleListViewIndex >= 0)
            {
                int firstSelectedIndex = FirstSelectedIndex;
                Paragraph oldParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (oldParagraph != null)
                    oldParagraph = new Paragraph(oldParagraph);

                UpdateStartTimeInfo(timeUpDownStartTime.TimeCode);
                _change = true;

                UpdateOriginalTimeCodes(oldParagraph);

                if (_makeHistory)
                    MakeHistoryForUndoWhenNoMoreChanges(string.Format(_language.StarTimeAdjustedX, "#" + (_subtitleListViewIndex+1).ToString() + ": " + timeUpDownStartTime.TimeCode.ToString()));
            }
        }

        private void UpdateOriginalTimeCodes(Paragraph currentPargraphBeforeChange)
        {
            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                int firstSelectedIndex = FirstSelectedIndex;
                Paragraph p = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (currentPargraphBeforeChange != null && p != null)
                {
                    Paragraph original = Utilities.GetOriginalParagraph(FirstSelectedIndex, currentPargraphBeforeChange, _subtitleAlternate.Paragraphs);
                    if (original != null)
                    {
                        original.StartTime.TotalMilliseconds = p.StartTime.TotalMilliseconds;
                        original.EndTime.TotalMilliseconds = p.EndTime.TotalMilliseconds;
                        _changeAlternate = true;
                    }
                }
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            ReloadFromSourceView();
            if (!ContinueNewOrExit())
            {
                e.Cancel = true;
            }
            else
            {
                if (_networkSession != null)
                {
                    try
                    {
                        _networkSession.TimerStop();
                        _networkSession.Leave();
                    }
                    catch
                    { 
                    }
                }

                if (Configuration.Settings.General.StartRememberPositionAndSize && WindowState != FormWindowState.Minimized)
                {
                    Configuration.Settings.General.StartPosition = Left + ";" + Top;
                    if (WindowState == FormWindowState.Maximized)
                        Configuration.Settings.General.StartSize = "Maximized";
                    else
                        Configuration.Settings.General.StartSize = Width + ";" + Height;
                    Configuration.Settings.General.StartListViewWidth = splitContainer1.SplitterDistance;
                }
                else if (Configuration.Settings.General.StartRememberPositionAndSize)
                {
                    Configuration.Settings.General.StartListViewWidth = splitContainer1.SplitterDistance;
                }
                Configuration.Settings.General.AutoRepeatOn = checkBoxAutoRepeatOn.Checked;
                Configuration.Settings.General.AutoContinueOn = checkBoxAutoContinue.Checked;
                Configuration.Settings.General.SyncListViewWithVideoWhilePlaying = checkBoxSyncListViewWithVideoWhilePlaying.Checked;
                if (!string.IsNullOrEmpty(_fileName))
                    Configuration.Settings.RecentFiles.Add(_fileName, FirstVisibleIndex, FirstSelectedIndex, _videoFileName, _subtitleAlternateFileName);

                SaveUndockedPositions();

                Configuration.Settings.Save();

                if (mediaPlayer.VideoPlayer != null)
                {
                    mediaPlayer.VideoPlayer.DisposeVideoPlayer();
                }

            }
        }

        private void SaveUndockedPositions()
        {
            if (_videoPlayerUnDocked != null && !_videoPlayerUnDocked.IsDisposed)
                Configuration.Settings.General.UndockedVideoPosition = _videoPlayerUnDocked.Left.ToString() + ";" + _videoPlayerUnDocked.Top.ToString() + ";" + _videoPlayerUnDocked.Width + ";" + _videoPlayerUnDocked.Height;
            if (_waveFormUnDocked != null && !_waveFormUnDocked.IsDisposed)
                Configuration.Settings.General.UndockedWaveformPosition = _waveFormUnDocked.Left.ToString() + ";" + _waveFormUnDocked.Top.ToString() + ";" + _waveFormUnDocked.Width + ";" + _waveFormUnDocked.Height;
            if (_videoControlsUnDocked != null && !_videoControlsUnDocked.IsDisposed)
                Configuration.Settings.General.UndockedVideoControlsPosition = _videoControlsUnDocked.Left.ToString() + ";" + _videoControlsUnDocked.Top.ToString() + ";" + _videoControlsUnDocked.Width + ";" + _videoControlsUnDocked.Height;
        }

        private void ButtonUnBreakClick(object sender, EventArgs e)
        {
            textBoxListViewText.Text = Utilities.UnbreakLine(textBoxListViewText.Text);
        }

        private void TabControlSubtitleSelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
            {
                ShowSource();
                ShowSourceLineNumber();
                if (textBoxSource.CanFocus)
                    textBoxSource.Focus();
            }
            else
            {
                ReloadFromSourceView();
                ShowLineInformationListView();
                if (SubtitleListview1.CanFocus)
                    SubtitleListview1.Focus();
            }            
        }

        private void ColorToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                if (colorDialog1.ShowDialog(this) == DialogResult.OK)
                { 
                    string color = Utilities.ColorToHex(colorDialog1.Color);

                    MakeHistoryForUndo(_language.BeforeSettingColor);

                    foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    {
                        Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                        if (p != null)
                        {
                            bool done = false;

                            string s = p.Text;
                            if (s.StartsWith("<font "))
                            {
                                int start = s.IndexOf("<font ");
                                if (start >= 0)
                                {
                                    int end = s.IndexOf(">", start);
                                    if (end > 0)
                                    {
                                        string f = s.Substring(start, end - start);
                                        if (f.Contains(" face=") && !f.Contains(" color="))
                                        {
                                            start = s.IndexOf(" face=", start);
                                            s = s.Insert(start, string.Format(" color=\"{0}\"", color));
                                            p.Text = s;
                                            done = true;
                                        }
                                        else if (f.Contains(" color="))
                                        {
                                            int colorStart = f.IndexOf(" color=");
                                            if (s.IndexOf("\"", colorStart + " color=".Length + 1) > 0)
                                                end = s.IndexOf("\"", colorStart + " color=".Length + 1);
                                            s = s.Substring(0, colorStart) + string.Format(" color=\"{0}", color) + s.Substring(end);
                                            p.Text = s;
                                            done = true;
                                        }
                                    }
                                }
                            }


                            if (!done)
                                p.Text = string.Format("<font color=\"{0}\">{1}</font>", color, p.Text);
                            SubtitleListview1.SetText(item.Index, p.Text);
                        }
                    }
                    _change = true;
                    RefreshSelectedParagraph();
                }
            }
        }

        private void toolStripMenuItemFont_Click(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count > 0)
            {
                if (fontDialog1.ShowDialog(this) == DialogResult.OK)
                {                   
                    MakeHistoryForUndo(_language.BeforeSettingFontName); 

                    foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    {
                        Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                        if (p != null)
                        {
                            bool done = false;

                            string s = p.Text;
                            if (s.StartsWith("<font "))
                            {
                                int start = s.IndexOf("<font ");
                                if (start >= 0)
                                {
                                    int end = s.IndexOf(">", start);
                                    if (end > 0)
                                    {
                                        string f = s.Substring(start, end - start);
                                        if (f.Contains(" color=") && !f.Contains(" face="))
                                        {
                                            start = s.IndexOf(" color=", start);
                                            s = s.Insert(start, string.Format(" face=\"{0}\"", fontDialog1.Font.Name));
                                            p.Text = s;
                                            done = true;
                                        }
                                        else if (f.Contains(" face="))
                                        { 
                                            int faceStart = f.IndexOf(" face=");
                                            if (s.IndexOf("\"", faceStart + " face=".Length + 1) > 0)
                                                end = s.IndexOf("\"", faceStart + " face=".Length + 1);
                                            s = s.Substring(0, faceStart) + string.Format(" face=\"{0}", fontDialog1.Font.Name) + s.Substring(end);
                                            p.Text = s;
                                            done = true;
                                        }
                                    }
                                }
                            }


                            if (!done)
                                p.Text = string.Format("<font face=\"{0}\">{1}</font>", fontDialog1.Font.Name, s);
                            SubtitleListview1.SetText(item.Index, p.Text);
                        }
                    }
                    _change = true;
                    RefreshSelectedParagraph();
                }
            }
        }

        private void TypeEffectToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count > 0)
            {
                var typewriter = new EffectTypewriter();

                typewriter.Initialize(SubtitleListview1.GetSelectedParagraph(_subtitle));

                if (typewriter.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeTypeWriterEffect);
                    int firstNumber = _subtitle.Paragraphs[0].Number;
                    int lastSelectedIndex = SubtitleListview1.SelectedItems[0].Index;
                    int index = lastSelectedIndex;
                    _subtitle.Paragraphs.RemoveAt(index);
                    bool isframeBased = GetCurrentSubtitleFormat().IsFrameBased;
                    foreach (Paragraph p in typewriter.TypewriterParagraphs)
                    {
                        if (isframeBased)
                        {
                            p.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                            p.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                        }
                        _subtitle.Paragraphs.Insert(index, p);
                        index++;
                    }
                    _subtitle.Renumber(firstNumber);
                    _subtitleListViewIndex = -1;
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    SubtitleListview1.SelectIndexAndEnsureVisible(lastSelectedIndex);
                }
                typewriter.Dispose();
            }
        }

        private void KarokeeEffectToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count > 0)
            {
                var karaoke = new EffectKaraoke();

                karaoke.Initialize(SubtitleListview1.GetSelectedParagraph(_subtitle));

                if (karaoke.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeKaraokeEffect);
                    int firstNumber = _subtitle.Paragraphs[0].Number;
                    int lastSelectedIndex = SubtitleListview1.SelectedItems[0].Index;
                    bool isframeBased = GetCurrentSubtitleFormat().IsFrameBased;

                    int i = SubtitleListview1.SelectedItems.Count - 1;
                    while (i >= 0)
                    {
                        ListViewItem item = SubtitleListview1.SelectedItems[i];
                        Paragraph p = _subtitle.GetParagraphOrDefault(item.Index);
                        if (p != null)
                        {
                            int index = item.Index;
                            _subtitle.Paragraphs.RemoveAt(index);
                            foreach (Paragraph kp in karaoke.MakeAnimation(p))
                            {
                                if (isframeBased)
                                {
                                    p.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                                    p.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                                }
                                _subtitle.Paragraphs.Insert(index, kp);
                                index++;
                            }
                        }
                        i--;
                    }

                    _subtitle.Renumber(firstNumber);
                    _subtitleListViewIndex = -1;
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    SubtitleListview1.SelectIndexAndEnsureVisible(lastSelectedIndex);
                }
                karaoke.Dispose();
            }
        }

        private void MatroskaImportStripMenuItemClick(object sender, EventArgs e)
        {
            openFileDialog1.Title = _language.OpenMatroskaFile;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = _language.MatroskaFiles + "|*.mkv;*.mks|" + _languageGeneral.AllFiles + "|*.*";
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(openFileDialog1.FileName);
                ImportSubtitleFromMatroskaFile();
            }
        }

        private void ImportSubtitleFromMatroskaFile()
        {
            bool isValid;
            var matroska = new Matroska();
            var subtitleList = matroska.GetMatroskaSubtitleTracks(openFileDialog1.FileName, out isValid);
            if (isValid)
            {
                if (subtitleList.Count == 0)
                {
                    MessageBox.Show(_language.NoSubtitlesFound);
                }
                else
                {
                    if (ContinueNewOrExit())
                    {
                        if (subtitleList.Count > 1)
                        {
                            MatroskaSubtitleChooser subtitleChooser = new MatroskaSubtitleChooser();
                            subtitleChooser.Initialize(subtitleList);
                            if (subtitleChooser.ShowDialog(this) == DialogResult.OK)
                            {
                                LoadMatroskaSubtitle(subtitleList[subtitleChooser.SelectedIndex], openFileDialog1.FileName);
                            }
                        }
                        else
                        {
                            LoadMatroskaSubtitle(subtitleList[0], openFileDialog1.FileName);
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show(string.Format(_language.NotAValidMatroskaFileX, openFileDialog1.FileName));
            }
        }

        private void LoadMatroskaSubtitle(MatroskaSubtitleInfo matroskaSubtitleInfo, string fileName)
        {
            bool isValid;
            bool isSsa = false;
            var matroska = new Matroska();

            SubtitleFormat format;

            if (matroskaSubtitleInfo.CodecId.ToUpper() == "S_VOBSUB")
            {
                LoadVobSubFromMatroska(matroskaSubtitleInfo, fileName);
                return;
            }
            if (matroskaSubtitleInfo.CodecId.ToUpper() == "S_HDMV/PGS")
            {
                LoadBluRaySubFromMatroska(matroskaSubtitleInfo, fileName);
                return;
            }
            else if (matroskaSubtitleInfo.CodecPrivate.ToLower().Contains("[script info]"))
            {
                format = new SubStationAlpha();
                isSsa = true;
            }
            else
            {
                format = new SubRip();
            }

            comboBoxSubtitleFormats.SelectedIndexChanged -= ComboBoxSubtitleFormatsSelectedIndexChanged;
            SetCurrentFormat(format);
            comboBoxSubtitleFormats.SelectedIndexChanged += ComboBoxSubtitleFormatsSelectedIndexChanged;
            
            ShowStatus(_language.ParsingMatroskaFile);
            Refresh();
            Cursor.Current = Cursors.WaitCursor;
            List<SubtitleSequence> sub = matroska.GetMatroskaSubtitle(fileName, (int)matroskaSubtitleInfo.TrackNumber, out isValid);
            Cursor.Current = Cursors.Default;
            if (isValid)
            {
                MakeHistoryForUndo(_language.BeforeImportFromMatroskaFile);
                _subtitleListViewIndex = -1;
                FileNew();
                _subtitle.Paragraphs.Clear();

                if (isSsa)
                {
                    int commaCount = 100;

                    foreach (SubtitleSequence p in sub)
                    {
                        string s1 = p.Text;
                        if (s1.Contains(@"{\"))
                            s1 = s1.Substring(0, s1.IndexOf(@"{\"));
                        int temp = s1.Split(',').Length;
                        if (temp < commaCount)
                            commaCount = temp;
                    }

                    foreach (SubtitleSequence p in sub)
                    {
                        string s = string.Empty;
                        string[] arr = p.Text.Split(',');
                        if (arr.Length >= commaCount)
                        {
                            for (int i = commaCount; i <= arr.Length; i++)
                            {
                                if (s.Length > 0)
                                    s += ",";
                                s += arr[i-1];
                            }
                        }
                        _subtitle.Paragraphs.Add(new Paragraph(s, p.StartMilliseconds, p.EndMilliseconds));
                    }
                }
                else
                {
                    foreach (SubtitleSequence p in sub)
                    {
                        _subtitle.Paragraphs.Add(new Paragraph(p.Text, p.StartMilliseconds, p.EndMilliseconds));
                    }
                }

                comboBoxEncoding.Text = "UTF-8";
                ShowStatus(_language.SubtitleImportedFromMatroskaFile);
                _subtitle.Renumber(1);
                _subtitle.WasLoadedWithFrameNumbers = false;
                if (fileName.ToLower().EndsWith(".mkv"))
                {
                    _fileName = fileName.Substring(0, fileName.Length - 4);
                    Text = Title + " - " + _fileName;
                }
                else
                {
                    Text = Title;
                }
                _fileDateTime = new DateTime();
                
                _converted = true;

                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                if (_subtitle.Paragraphs.Count > 0)
                    SubtitleListview1.SelectIndexAndEnsureVisible(0);

                if (format.FriendlyName == new SubStationAlpha().FriendlyName)
                    _subtitle.Header = matroskaSubtitleInfo.CodecPrivate;
                ShowSource();
            }
        }

        public static void CopyStream(System.IO.Stream input, System.IO.Stream output)
        {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }
            output.Flush();
        }

        private void LoadVobSubFromMatroska(MatroskaSubtitleInfo matroskaSubtitleInfo, string fileName)
        {
            if (matroskaSubtitleInfo.ContentEncodingType == 1)
            {
                MessageBox.Show("Encrypted vobsub content not supported");
            }

            bool isValid;
            var matroska = new Matroska();

            ShowStatus(_language.ParsingMatroskaFile);
            Refresh();
            Cursor.Current = Cursors.WaitCursor;
            List<SubtitleSequence> sub = matroska.GetMatroskaSubtitle(fileName, (int)matroskaSubtitleInfo.TrackNumber, out isValid);
            Cursor.Current = Cursors.Default;

            if (isValid)
            {
                MakeHistoryForUndo(_language.BeforeImportFromMatroskaFile);
                _subtitleListViewIndex = -1;
                _subtitle.Paragraphs.Clear();

                List<VobSubMergedPack> mergedVobSubPacks = new List<VobSubMergedPack>();
                Nikse.SubtitleEdit.Logic.VobSub.Idx idx = new Logic.VobSub.Idx(matroskaSubtitleInfo.CodecPrivate.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
                foreach (SubtitleSequence p in sub)
                {
                    if (matroskaSubtitleInfo.ContentEncodingType == 0) // compressed with zlib
                    {
                        MemoryStream outStream = new MemoryStream();
                        ComponentAce.Compression.Libs.zlib.ZOutputStream outZStream = new ComponentAce.Compression.Libs.zlib.ZOutputStream(outStream);
                        MemoryStream inStream = new MemoryStream(p.BinaryData);
                        byte[] buffer;
                        try
                        {
                            CopyStream(inStream, outZStream);
                            buffer = new byte[outZStream.TotalOut];
                            outStream.Position = 0;
                            outStream.Read(buffer, 0, buffer.Length);
                        }
                        finally
                        {
                            outStream.Close();
                            outZStream.Close();
                            inStream.Close();
                        }
                        mergedVobSubPacks.Add(new VobSubMergedPack(buffer, TimeSpan.FromMilliseconds(p.StartMilliseconds), 32, null));
                    }
                    else
                    {
                        mergedVobSubPacks.Add(new VobSubMergedPack(p.BinaryData, TimeSpan.FromMilliseconds(p.StartMilliseconds), 32, null));
                    }
                    mergedVobSubPacks[mergedVobSubPacks.Count - 1].EndTime = TimeSpan.FromMilliseconds(p.EndMilliseconds);

                    // fix overlapping (some versions of Handbrake makes overlapping time codes - thx Hawke)
                    if (mergedVobSubPacks.Count > 1 && mergedVobSubPacks[mergedVobSubPacks.Count - 2].EndTime > mergedVobSubPacks[mergedVobSubPacks.Count - 1].StartTime)
                        mergedVobSubPacks[mergedVobSubPacks.Count - 2].EndTime = TimeSpan.FromMilliseconds(mergedVobSubPacks[mergedVobSubPacks.Count - 1].StartTime.TotalMilliseconds - 1);
                }                

                var formSubOcr = new VobSubOcr();
                formSubOcr.Initialize(mergedVobSubPacks, idx.Palette, Configuration.Settings.VobSubOcr, null); //TODO - language???
                if (formSubOcr.ShowDialog(this) == DialogResult.OK)
                {
                    ResetSubtitle();
                    _subtitle.Paragraphs.Clear();
                    _subtitle.WasLoadedWithFrameNumbers = false;
                    foreach (Paragraph p in formSubOcr.SubtitleFromOcr.Paragraphs)
                        _subtitle.Paragraphs.Add(p);

                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    _change = true;
                    _subtitleListViewIndex = -1;
                    SubtitleListview1.FirstVisibleIndex = -1;
                    SubtitleListview1.SelectIndexAndEnsureVisible(0);

                    _fileName =  Path.GetFileNameWithoutExtension(fileName);
                    _converted = true;
                    Text = Title;

                    Configuration.Settings.Save();
                }
            }
        }

        private void LoadBluRaySubFromMatroska(MatroskaSubtitleInfo matroskaSubtitleInfo, string fileName)
        {
            if (matroskaSubtitleInfo.ContentEncodingType == 1)
            {
                MessageBox.Show("Encrypted vobsub content not supported");
            }

            bool isValid;
            var matroska = new Matroska();

            ShowStatus(_language.ParsingMatroskaFile);
            Refresh();
            Cursor.Current = Cursors.WaitCursor;
            List<SubtitleSequence> sub = matroska.GetMatroskaSubtitle(fileName, (int)matroskaSubtitleInfo.TrackNumber, out isValid);
            Cursor.Current = Cursors.Default;

            if (isValid)
            {
                MakeHistoryForUndo(_language.BeforeImportFromMatroskaFile);
                _subtitleListViewIndex = -1;
                _subtitle.Paragraphs.Clear();
                List<BluRaySupPicture> subtitles = new List<BluRaySupPicture>();
                StringBuilder log = new StringBuilder();
                foreach (SubtitleSequence p in sub)
                {
                    byte[] buffer;
                    if (matroskaSubtitleInfo.ContentEncodingType == 0) // compressed with zlib
                    {
                        MemoryStream outStream = new MemoryStream();
                        ComponentAce.Compression.Libs.zlib.ZOutputStream outZStream = new ComponentAce.Compression.Libs.zlib.ZOutputStream(outStream);
                        MemoryStream inStream = new MemoryStream(p.BinaryData);
                        try
                        {
                            CopyStream(inStream, outZStream);
                            buffer = new byte[outZStream.TotalOut];
                            outStream.Position = 0;
                            outStream.Read(buffer, 0, buffer.Length);
                        }
                        finally
                        {
                            outStream.Close();
                            outZStream.Close();
                            inStream.Close();
                        }
                    }
                    else
                    {
                        buffer = p.BinaryData;
                    }
                    if (buffer.Length > 100)
                    {
                        MemoryStream ms = new MemoryStream(buffer);
                        var list = BluRaySupParser.ParseBluRaySup(ms, log, true); 
                        foreach (var sup in list)
                        {
                            sup.StartTime = p.StartMilliseconds;
                            sup.EndTime = p.EndMilliseconds;
                            subtitles.Add(sup);

                            // fix overlapping 
                            if (subtitles.Count > 1 && sub[subtitles.Count - 2].EndMilliseconds > sub[subtitles.Count - 1].StartMilliseconds)
                                subtitles[subtitles.Count - 2].EndTime = subtitles[subtitles.Count - 1].StartTime - 1;                             
                        }
                        ms.Close();
                    }
                }

                var formSubOcr = new VobSubOcr();
                formSubOcr.Initialize(subtitles, Configuration.Settings.VobSubOcr);
                if (formSubOcr.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeImportingDvdSubtitle);

                    _subtitle.Paragraphs.Clear();
                    SetCurrentFormat(new SubRip().FriendlyName);
                    _subtitle.WasLoadedWithFrameNumbers = false;
                    _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                    foreach (Paragraph p in formSubOcr.SubtitleFromOcr.Paragraphs)
                    {
                        _subtitle.Paragraphs.Add(p);
                    }

                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    _change = true;
                    _subtitleListViewIndex = -1;
                    SubtitleListview1.FirstVisibleIndex = -1;
                    SubtitleListview1.SelectIndexAndEnsureVisible(0);

                    _fileName = string.Empty;
                    Text = Title;

                    Configuration.Settings.Save();
                }
            }
        }

        private void SubtitleListview1_DragEnter(object sender, DragEventArgs e)
        {
            // make sure they're actually dropping files (not text or anything else)
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void SubtitleListview1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                if (ContinueNewOrExit())
                {
                    string fileName = files[0];

                    var fi = new FileInfo(fileName);
                    string ext = Path.GetExtension(fileName).ToLower();

                    if (ext == ".mkv")
                    { 
                        bool isValid;
                        var matroska = new Matroska();
                        var subtitleList = matroska.GetMatroskaSubtitleTracks(fileName, out isValid);
                        if (isValid)
                        {
                            if (subtitleList.Count == 0)
                            {
                                MessageBox.Show(_language.NoSubtitlesFound);
                            }
                            else if (subtitleList.Count > 1)
                            {
                                MatroskaSubtitleChooser subtitleChooser = new MatroskaSubtitleChooser();
                                subtitleChooser.Initialize(subtitleList);
                                if (subtitleChooser.ShowDialog(this) == DialogResult.OK)
                                {
                                    LoadMatroskaSubtitle(subtitleList[subtitleChooser.SelectedIndex], fileName);
                                }
                            }
                            else
                            {
                                LoadMatroskaSubtitle(subtitleList[0], fileName);
                            }
                        }
                        return;
                    }

                    if (fi.Length < 1024 * 1024 * 2) // max 2 mb
                    {
                        OpenSubtitle(fileName, null);
                    }
                    else if (fi.Length < 50000000 && ext == ".sub" && IsVobSubFile(fileName, true)) // max 50 mb
                    {
                        OpenSubtitle(fileName, null);
                    }
                    else
                    {
                        MessageBox.Show(string.Format(_language.DropFileXNotAccepted, fileName));
                    }
                }
            }
            else
            {
                MessageBox.Show(_language.DropOnlyOneFile);
            }
        }

        private void TextBoxSourceDragEnter(object sender, DragEventArgs e)
        {
            // make sure they're actually dropping files (not text or anything else)
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void TextBoxSourceDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                if (ContinueNewOrExit())
                {
                    OpenSubtitle(files[0], null);
                }
            }
            else
            {
                MessageBox.Show(_language.DropOnlyOneFile);
            }
        }

        private void ToolStripMenuItemManualAnsiClick(object sender, EventArgs e)
        {
            ReloadFromSourceView();
            openFileDialog1.Title = _language.OpenAnsiSubtitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                var chooseEncoding = new ChooseEncoding();
                chooseEncoding.Initialize(openFileDialog1.FileName);
                if (chooseEncoding.ShowDialog(this) == DialogResult.OK)
                {
                    Encoding encoding = chooseEncoding.GetEncoding();
                    comboBoxEncoding.Text = "UTF-8";
                    OpenSubtitle(openFileDialog1.FileName, encoding);
                    _converted = true;
                }
            }

        }

        private void ChangeCasingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeCasing(false);
        }

        private void ChangeCasing(bool onlySelectedLines)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                SaveSubtitleListviewIndexes();
                var changeCasing = new ChangeCasing();
                _formPositionsAndSizes.SetPositionAndSize(changeCasing);
                if (onlySelectedLines)
                    changeCasing.Text += " - " + _language.SelectedLines;
                ReloadFromSourceView();
                if (changeCasing.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeChangeCasing);

                    Cursor.Current = Cursors.WaitCursor;
                    var selectedLines = new Subtitle();
                    selectedLines.WasLoadedWithFrameNumbers = _subtitle.WasLoadedWithFrameNumbers;
                    if (onlySelectedLines)
                    {
                        foreach (int index in SubtitleListview1.SelectedIndices)
                            selectedLines.Paragraphs.Add(new Paragraph(_subtitle.Paragraphs[index]));
                    }
                    else
                    {
                        foreach (Paragraph p in _subtitle.Paragraphs)
                            selectedLines.Paragraphs.Add(new Paragraph(p));
                    }

                    bool saveChangeCaseChanges = true;
                    changeCasing.FixCasing(selectedLines, Utilities.AutoDetectLanguageName(Configuration.Settings.General.SpellCheckLanguage, _subtitle));
                    var changeCasingNames = new ChangeCasingNames();
                    if (changeCasing.ChangeNamesToo)
                    {
                        changeCasingNames.Initialize(selectedLines);
                        if (changeCasingNames.ShowDialog(this) == DialogResult.OK)
                        {
                            changeCasingNames.FixCasing();

                            if (changeCasing.LinesChanged == 0)
                                ShowStatus(string.Format(_language.CasingCompleteMessageOnlyNames, changeCasingNames.LinesChanged, _subtitle.Paragraphs.Count));
                            else
                                ShowStatus(string.Format(_language.CasingCompleteMessage, changeCasing.LinesChanged, _subtitle.Paragraphs.Count, changeCasingNames.LinesChanged));
                        }
                        else
                        {
                            saveChangeCaseChanges = false;
                        }
                    }
                    else
                    {
                        ShowStatus(string.Format(_language.CasingCompleteMessageNoNames, changeCasing.LinesChanged, _subtitle.Paragraphs.Count));
                    }

                    if (saveChangeCaseChanges)
                    {
                        if (onlySelectedLines)
                        {
                            int i = 0;
                            foreach (int index in SubtitleListview1.SelectedIndices)
                            {
                                _subtitle.Paragraphs[index].Text = selectedLines.Paragraphs[i].Text;
                                i++;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
                            {
                                _subtitle.Paragraphs[i].Text = selectedLines.Paragraphs[i].Text;
                            }
                        }
                        ShowSource();
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                        if (changeCasing.LinesChanged > 0 || changeCasingNames.LinesChanged > 0)
                        {
                            _change = true;
                            _subtitleListViewIndex = -1;
                            RestoreSubtitleListviewIndexes();
                            UpdateSourceView();
                        }
                    }
                    Cursor.Current = Cursors.Default;
                }
                _formPositionsAndSizes.SavePositionAndSize(changeCasing);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ToolStripMenuItemChangeFramerateClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                int lastSelectedIndex = 0;
                if (SubtitleListview1.SelectedItems.Count > 0)
                    lastSelectedIndex = SubtitleListview1.SelectedItems[0].Index;

                ReloadFromSourceView();
                var changeFramerate = new ChangeFrameRate();
                _formPositionsAndSizes.SetPositionAndSize(changeFramerate);
                changeFramerate.Initialize(CurrentFrameRate.ToString());
                if (changeFramerate.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeChangeFrameRate);

                    double oldFramerate = changeFramerate.OldFrameRate;
                    double newFramerate = changeFramerate.NewFrameRate;
                    _subtitle.ChangeFramerate(oldFramerate, newFramerate);

                    ShowStatus(string.Format(_language.FrameRateChangedFromXToY, oldFramerate, newFramerate));
                    toolStripComboBoxFrameRate.Text = newFramerate.ToString();

                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    _change = true;
                    _subtitleListViewIndex = -1;
                    SubtitleListview1.SelectIndexAndEnsureVisible(lastSelectedIndex);
                }
                _formPositionsAndSizes.SavePositionAndSize(changeFramerate);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool IsVobSubFile(string subFileName, bool verbose)
        {
            try
            {
                var buffer = new byte[4];
                var fs = new FileStream(subFileName, FileMode.Open, FileAccess.Read, FileShare.Read) {Position = 0};
                fs.Read(buffer, 0, 4);
                bool isHeaderOk = VobSubParser.IsMpeg2PackHeader(buffer) || VobSubParser.IsPrivateStream1(buffer, 0);
                fs.Close();
                if (isHeaderOk) 
                {
                    if (!verbose)
                        return true;

                    string idxFileName = Path.Combine(Path.GetDirectoryName(subFileName), Path.GetFileNameWithoutExtension(subFileName) + ".idx");
                    if (File.Exists(idxFileName))
                        return true;
                    return (MessageBox.Show(string.Format(_language.IdxFileNotFoundWarning, idxFileName ), _title, MessageBoxButtons.YesNo) ==  DialogResult.Yes);
                }
                if (verbose)
                    MessageBox.Show(string.Format(_language.InvalidVobSubHeader,  subFileName));
            }
            catch (Exception ex)
            {
                if (verbose)
                    MessageBox.Show(ex.Message);
            }
            return false;
        }

        private bool IsBluRaySupFile(string subFileName)
        { 
            var buffer = new byte[4];
            var fs = new FileStream(subFileName, FileMode.Open, FileAccess.Read, FileShare.Read) {Position = 0};
            fs.Read(buffer, 0, 4);
            fs.Close();
            return (buffer[0] == 0x50 && buffer[1] == 0x47); // 80 + 71 - P G                    
        }

        private void ImportAndOcrVobSubSubtitleNew(string fileName)
        {
            if (IsVobSubFile(fileName, true))
            {
                var vobSubOcr = new VobSubOcr();
                if (vobSubOcr.Initialize(fileName, Configuration.Settings.VobSubOcr, true))
                {
                    if (vobSubOcr.ShowDialog(this) == DialogResult.OK)
                    {
                        MakeHistoryForUndo(_language.BeforeImportingVobSubFile);
                        FileNew();
                        _subtitle.Paragraphs.Clear();
                        SetCurrentFormat(new SubRip().FriendlyName);
                        _subtitle.WasLoadedWithFrameNumbers = false;
                        _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                        foreach (Paragraph p in vobSubOcr.SubtitleFromOcr.Paragraphs)
                        {
                            _subtitle.Paragraphs.Add(p);
                        }

                        ShowSource();
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                        _change = true;
                        _subtitleListViewIndex = -1;
                        SubtitleListview1.FirstVisibleIndex = -1;
                        SubtitleListview1.SelectIndexAndEnsureVisible(0);

                        _fileName = Path.ChangeExtension(vobSubOcr.FileName, ".srt");
                        SetTitle();
                        _converted = true;

                        Configuration.Settings.Save();
                    }
                }
            }
        }

        private void ToolStripMenuItemMergeLinesClick(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count >= 1)
                MergeAfterToolStripMenuItemClick(null, null);
        }

        private void VisualSyncSelectedLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowVisualSync(true);           
        }

        private void GoogleTranslateSelectedLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            TranslateViaGoogle(true, true);
        }

        private void SaveSubtitleListviewIndexes()
        {
            _selectedIndexes = new List<int>();
            foreach (int index in SubtitleListview1.SelectedIndices)
                _selectedIndexes.Add(index);
        }

        private void RestoreSubtitleListviewIndexes()
        {
            _subtitleListViewIndex = -1;
            if (_selectedIndexes != null)
            {
                SubtitleListview1.SelectNone();
                int i = 0;
                foreach (int index in _selectedIndexes)
                {
                    if (index >= 0 && index < SubtitleListview1.Items.Count)
                    { 
                        SubtitleListview1.Items[index].Selected = true;
                        if (i == 0)
                            SubtitleListview1.Items[index].EnsureVisible();
                    }
                    i++;
                }
            }
        }

        private void ShowSelectedLinesEarlierlaterToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                if (_showEarlierOrLater != null && !_showEarlierOrLater.IsDisposed)
                {
                    _showEarlierOrLater.WindowState = FormWindowState.Normal;
                    _showEarlierOrLater.Focus();
                    return;
                }             

                bool waveFormEnabled = timerWaveForm.Enabled;
                timerWaveForm.Stop();
                bool videoTimerEnabled = videoTimer.Enabled;
                videoTimer.Stop();               
                timer1.Stop();

                //if (SubtitleListview1.SelectedIndices.Count > 1)
                //    SubtitleListview1.EnsureVisible(SubtitleListview1.SelectedIndices[0]);

                _showEarlierOrLater = new ShowEarlierLater();
                if (!_formPositionsAndSizes.SetPositionAndSize(_showEarlierOrLater))
                {
                    _showEarlierOrLater.Top = this.Top + 100;
                    _showEarlierOrLater.Left = this.Left + (this.Width / 2) - (_showEarlierOrLater.Width / 3);
                }
                _showEarlierOrLater.Initialize(ShowEarlierOrLater, _formPositionsAndSizes, true);
                MakeHistoryForUndo(_language.BeforeShowSelectedLinesEarlierLater);
                _showEarlierOrLater.Show(this);

                timerWaveForm.Enabled = waveFormEnabled;
                videoTimer.Enabled = videoTimerEnabled;
                timer1.Start();

                RefreshSelectedParagraph();
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        internal void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Alt && e.KeyCode == (Keys.RButton | Keys.ShiftKey) && textBoxListViewText.Focused)
            { // annoying that focus leaves textbox while typing, when pressing Alt alone 
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.Insert)
            {
                InsertAfter();
                e.SuppressKeyPress = true;
            }
            else if (e.Shift && e.KeyCode == Keys.Insert)
            {
                InsertBefore();
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Z)
            {
                ShowHistoryforUndoToolStripMenuItemClick(null, null);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Right && e.Modifiers == Keys.Control)
            {
                if (!textBoxListViewText.Focused && !textBoxListViewTextAlternate.Focused)
                {
                    mediaPlayer.CurrentPosition += 0.10;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Left && e.Modifiers == Keys.Control)
            {
                if (!textBoxListViewText.Focused && !textBoxListViewTextAlternate.Focused)
                {
                    mediaPlayer.CurrentPosition -= 0.10;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Right && e.Modifiers == Keys.Alt)
            {
                if (mediaPlayer.VideoPlayer != null)
                {
                    mediaPlayer.CurrentPosition += 0.5;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Left && e.Modifiers == Keys.Alt)
            {
                if (mediaPlayer.VideoPlayer != null)
                {
                    mediaPlayer.CurrentPosition -= 0.5;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Down && e.Modifiers == Keys.Alt)
            {
                if (AutoRepeatContinueOn)
                    Next();
                else
                    ButtonNextClick(null, null);
            }
            else if (e.KeyCode == Keys.Up && e.Modifiers == Keys.Alt)
            {
                if (AutoRepeatContinueOn)
                    PlayPrevious();
                else
                    ButtonPreviousClick(null, null);
            }
            else if (e.KeyCode == Keys.Home && e.Modifiers == Keys.Alt)
            {
                SubtitleListview1.FirstVisibleIndex = -1;
                SubtitleListview1.SelectIndexAndEnsureVisible(0);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.End && e.Modifiers == Keys.Alt)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(SubtitleListview1.Items.Count - 1);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.L) //Locate first selected line in subtitle listview
            {
                if (SubtitleListview1.SelectedItems.Count > 0)
                    SubtitleListview1.SelectedItems[0].EnsureVisible();
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == (Keys.Control | Keys.Alt | Keys.Shift) && e.KeyCode == Keys.R) // reload "Language.xml"
            {
                if (File.Exists(Configuration.BaseDirectory + "Language.xml"))
                    SetLanguage("Language.xml");
            }
            else if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.M)
            {
                if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count >= 1)
                {
                    e.SuppressKeyPress = true;
                    if (SubtitleListview1.SelectedItems.Count == 2)
                        MergeAfterToolStripMenuItemClick(null, null);
                    else
                        MergeSelectedLines();
                }
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.K)
            {
                if (_subtitle.Paragraphs.Count > 0 && SubtitleListview1.SelectedItems.Count >= 1)
                {
                    e.SuppressKeyPress = true;
                    if (SubtitleListview1.SelectedItems.Count == 2)
                        MergeAfterToolStripMenuItemClick(null, null);
                    else
                        MergeSelectedLines();
                }
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.U)
            { // toggle translator mode
                EditToolStripMenuItemDropDownOpening(null, null);
                toolStripMenuItemTranslationMode_Click(null, null);
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.P)
            {
                if (mediaPlayer.VideoPlayer != null)
                {
                    _endSeconds = -1;
                    mediaPlayer.TogglePlayPause();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.Right)
            {
                if (!textBoxListViewText.Focused && !textBoxListViewTextAlternate.Focused)
                {
                    mediaPlayer.CurrentPosition += 1.0;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.Left)
            {
                if (!textBoxListViewText.Focused && !textBoxListViewTextAlternate.Focused)
                {
                    mediaPlayer.CurrentPosition -= 1.0;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.Space)
            {
                if (!textBoxListViewText.Focused && !textBoxListViewTextAlternate.Focused && !textBoxSource.Focused && mediaPlayer.VideoPlayer != null)
                {
                    _endSeconds = -1;
                    mediaPlayer.TogglePlayPause();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.D1)
            {
                if (SubtitleListview1.SelectedItems.Count > 0 && _subtitle != null && mediaPlayer.VideoPlayer != null)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(SubtitleListview1.SelectedItems[0].Index);
                    if (p != null)
                    {
                        mediaPlayer.CurrentPosition = p.StartTime.TotalSeconds;
                        e.SuppressKeyPress = true;
                    }
                }
            }
            else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.D2)
            {
                if (SubtitleListview1.SelectedItems.Count > 0 && _subtitle != null && mediaPlayer.VideoPlayer != null)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(SubtitleListview1.SelectedItems[0].Index);
                    if (p != null)
                    {
                        mediaPlayer.CurrentPosition = p.EndTime.TotalSeconds;
                        e.SuppressKeyPress = true;
                    }
                }
            }
            else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.D3)
            {
                if (SubtitleListview1.SelectedItems.Count > 0 && _subtitle != null && mediaPlayer.VideoPlayer != null)
                {
                    int index = SubtitleListview1.SelectedItems[0].Index -1;
                    Paragraph p = _subtitle.GetParagraphOrDefault(index);
                    if (p != null)
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(index);
                        mediaPlayer.CurrentPosition = p.StartTime.TotalSeconds;
                        e.SuppressKeyPress = true;
                    }
                }
            }
            else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.D4)
            {
                if (SubtitleListview1.SelectedItems.Count > 0 && _subtitle != null && mediaPlayer.VideoPlayer != null)
                {
                    int index = SubtitleListview1.SelectedItems[0].Index + 1;
                    Paragraph p = _subtitle.GetParagraphOrDefault(index);
                    if (p != null)
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(index);
                        mediaPlayer.CurrentPosition = p.StartTime.TotalSeconds;
                        e.SuppressKeyPress = true;
                    }
                }
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F4)
            {
                if (SubtitleListview1.SelectedItems.Count > 0 && _subtitle != null && mediaPlayer.VideoPlayer != null)
                {
                    mediaPlayer.Pause();
                    Paragraph p = _subtitle.GetParagraphOrDefault(SubtitleListview1.SelectedItems[0].Index);
                    if (p != null)
                    {
                        if (Math.Abs(mediaPlayer.CurrentPosition - p.StartTime.TotalSeconds) < 0.1)
                            mediaPlayer.CurrentPosition = p.EndTime.TotalSeconds;
                        else
                            mediaPlayer.CurrentPosition = p.StartTime.TotalSeconds;
                        e.SuppressKeyPress = true;
                    }
                    
                }
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F5)
            {
                if (SubtitleListview1.SelectedItems.Count > 0 && _subtitle != null && mediaPlayer.VideoPlayer != null)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(SubtitleListview1.SelectedItems[0].Index);
                    if (p != null)
                    {
                        mediaPlayer.CurrentPosition = p.StartTime.TotalSeconds;
                        ShowSubtitle();
                        mediaPlayer.Play();
                        _endSeconds = p.EndTime.TotalSeconds;
                        e.SuppressKeyPress = true;
                    }
                }
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F6)
            {
                if (mediaPlayer.VideoPlayer != null)
                {
                    GotoSubPositionAndPause();
                }
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F7)
            {
                if (mediaPlayer.VideoPlayer != null)
                {
                    GoBackSeconds(3);
                }
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F8)
            {
                if (mediaPlayer.VideoPlayer != null)
                {
                    _endSeconds = -1;
                    mediaPlayer.TogglePlayPause();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Modifiers == (Keys.Control | Keys.Alt | Keys.Shift) && e.KeyCode == Keys.W) // watermak
            {
                if (comboBoxEncoding.Text.StartsWith("ANSI - "))
                {
                    MessageBox.Show("Watermark only works with unicode file encoding");
                }
                else
                {
                    Watermark watermarkForm = new Watermark();
                    watermarkForm.Initialize(_subtitle, FirstSelectedIndex);
                    if (watermarkForm.ShowDialog(this) == DialogResult.OK)
                    {
                        watermarkForm.AddOrRemove(_subtitle);
                        RefreshSelectedParagraph();
                    }
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _cancelWordSpellCheck = true;
            }
            else if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.U) // Ctrl+Shift+U = switch original/current
            {
                if (_subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0 && _networkSession == null)
                {
                    Subtitle temp = _subtitle;
                    _subtitle = _subtitleAlternate;
                    _subtitleAlternate = temp;

                    string tempName = _fileName;
                    _fileName = _subtitleAlternateFileName;
                    _subtitleAlternateFileName = tempName;

                    bool tempChange = _change;
                    _change = _changeAlternate;
                    _changeAlternate = tempChange;

                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RefreshSelectedParagraph();

                    SetTitle();

                    _fileDateTime = new DateTime();
                }
            }
            else if (e.KeyData == _toggleVideoDockUndock)
            {
                if (_isVideoControlsUnDocked)
                    redockVideoControlsToolStripMenuItem_Click(null, null);
                else
                    undockVideoControlsToolStripMenuItem_Click(null, null);
            }

            // TABS - MUST BE LAST
            else if (tabControlButtons.SelectedTab == tabPageAdjust && mediaPlayer.VideoPlayer != null)
            {
                if ((e.Modifiers == Keys.Control && e.KeyCode == Keys.Space))
                {
                    ButtonSetStartAndOffsetRestClick(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.Shift && e.KeyCode == Keys.Space)
                {
                    buttonSetEndAndGoToNext_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F9)
                {
                    ButtonSetStartAndOffsetRestClick(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F10)
                {
                    buttonSetEndAndGoToNext_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F11)
                {
                    buttonSetStartTime_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F12)
                {
                    StopAutoDuration();
                    buttonSetEnd_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (_mainAdjustInsertViaEndAutoStartAndGoToNext == e.KeyData)
                {
                    SetCurrentViaEndPositionAndGotoNext(FirstSelectedIndex);
                    e.SuppressKeyPress = true;
                }
            }
            else if (tabControlButtons.SelectedTab == tabPageCreate && mediaPlayer.VideoPlayer != null)
            {
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.F9)
                {
                    InsertNewTextAtVideoPosition();
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.Shift && e.KeyCode == Keys.F9)
                {
                    var p = InsertNewTextAtVideoPosition();
                    p.Text = p.StartTime.ToShortString();
                    SubtitleListview1.SetText(_subtitle.GetIndex(p), p.Text);
                    textBoxListViewText.Text = p.Text;
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.F9)
                {
                    StopAutoDuration();
                    buttonSetEnd_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F9)
                {
                    buttonInsertNewText_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F10)
                {
                    buttonBeforeText_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F11)
                {
                    buttonSetStartTime_Click(null, null);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F12)
                {
                    StopAutoDuration();
                    buttonSetEnd_Click(null, null);
                    e.SuppressKeyPress = true;
                }
            }
            // put new entries above tabs
        }

        private void SetTitle()
        {
            Text = Title;

            string seperator = " - ";
            if (!string.IsNullOrEmpty(_fileName))
            {
                Text = Text + seperator + _fileName;
                seperator = " + ";
            }

            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                Text = Text + seperator;
                if (string.IsNullOrEmpty(_fileName))
                    Text = Text + Configuration.Settings.Language.Main.New + " + ";
                if (!string.IsNullOrEmpty(_subtitleAlternateFileName))
                    Text = Text + _subtitleAlternateFileName;
                else
                    Text = Text + Configuration.Settings.Language.Main.New;
            }
        }

        private void SubtitleListview1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control) //Copy to clipboard
            {
                Subtitle tmp = new Subtitle();
                foreach (int i in SubtitleListview1.SelectedIndices)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(i);
                    if (p != null)
                        tmp.Paragraphs.Add(new Paragraph(p));
                }
                if (tmp.Paragraphs.Count > 0)
                {
                    Clipboard.SetText(tmp.ToText(new SubRip()));
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.V && e.Control) //Paste from clipboard
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    Subtitle tmp = new Subtitle();
                    SubRip format = new SubRip();
                    List<string> list = new List<string>();
                    foreach (string line in text.Replace(Environment.NewLine, "|").Split("|".ToCharArray(), StringSplitOptions.None))
                        list.Add(line);
                    format.LoadSubtitle(tmp, list, null);
                    if (SubtitleListview1.SelectedItems.Count == 1 && tmp.Paragraphs.Count > 0)
                    {
                        Paragraph lastParagraph = null;
                        Paragraph lastTempParagraph = null;
                        foreach (Paragraph p in tmp.Paragraphs)
                        {
                            InsertAfter();
                            textBoxListViewText.Text = p.Text;
                            if (lastParagraph != null && lastTempParagraph != null)
                            {
                                double millisecondsBetween = p.StartTime.TotalMilliseconds - lastTempParagraph.EndTime.TotalMilliseconds;
                                timeUpDownStartTime.TimeCode = new TimeCode(TimeSpan.FromMilliseconds(lastParagraph.EndTime.TotalMilliseconds + millisecondsBetween));
                            }
                            numericUpDownDuration.Value = (decimal)p.Duration.TotalSeconds;
                            lastParagraph = _subtitle.GetParagraphOrDefault(_subtitleListViewIndex);
                            lastTempParagraph = p;
                        }
                    }
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.A && e.Control) //SelectAll
            {
                foreach (ListViewItem item in SubtitleListview1.Items)
                    item.Selected = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.D && e.Control) //SelectFirstSelectedItemOnly
            {
                if (SubtitleListview1.SelectedItems.Count > 0)
                {
                    bool skipFirst = true;
                    foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    {
                        if (skipFirst)
                            skipFirst = false;
                        else
                            item.Selected = false;
                    }
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Delete && SubtitleListview1.SelectedItems.Count > 0) //Delete
            {
                ToolStripMenuItemDeleteClick(null, null);
            }
            else if (e.Shift && e.KeyCode == Keys.Insert)
            {
                InsertBefore();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Insert)
            {
                InsertAfter();
                e.SuppressKeyPress = true;
            }
            else if (e.Shift && e.Control && e.KeyCode == Keys.I) //InverseSelection
            {
                foreach (ListViewItem item in SubtitleListview1.Items)
                    item.Selected = !item.Selected;
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Home)
            {
                SubtitleListview1.FirstVisibleIndex = -1;
                SubtitleListview1.SelectIndexAndEnsureVisible(0);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.End)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(SubtitleListview1.Items.Count-1);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.None && e.KeyCode == Keys.Enter)
            {
                SubtitleListview1_MouseDoubleClick(null, null);
            }
        }

        private void AdjustDisplayTimeForSelectedLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            AdjustDisplayTime(true);
        }

        private void FixCommonErrorsInSelectedLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            FixCommonErrors(true);
        }

        private void FindDoubleWordsToolStripMenuItemClick(object sender, EventArgs e)
        {
            var regex = new Regex(@"\b([\w]+)[ \r\n]+\1[ ,.!?]");
            _findHelper = new FindReplaceDialogHelper(FindType.RegEx, string.Format(_language.DoubleWordsViaRegEx, regex), regex, string.Empty, 0, 0, _subtitleListViewIndex);

            ReloadFromSourceView();
            FindNext();
        }

        private void ChangeCasingForSelectedLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            ChangeCasing(true);
        }

        private void CenterFormOnCurrentScreen()
        {
            Screen screen = Screen.FromControl(this);
            Left = screen.Bounds.X + ((screen.Bounds.Width - Width) / 2);
            Top = screen.Bounds.Y + ((screen.Bounds.Height - Height) / 2);
        }

        private void SortSubtitle(SubtitleSortCriteria subtitleSortCriteria, string description)
        {
            Paragraph firstSelectedParagraph = null;
            if (SubtitleListview1.SelectedItems.Count > 0)
                firstSelectedParagraph = _subtitle.Paragraphs[SubtitleListview1.SelectedItems[0].Index];

            _subtitleListViewIndex = -1;
            MakeHistoryForUndo(string.Format(_language.BeforeSortX, description));
            _subtitle.Sort(subtitleSortCriteria);
            ShowSource();
            SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
            SubtitleListview1.SelectIndexAndEnsureVisible(firstSelectedParagraph);
            ShowStatus(string.Format(_language.SortedByX, description));
        }

        private void SortNumberToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.Number, (sender as ToolStripItem).Text);
        }

        private void SortStartTimeToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.StartTime, (sender as ToolStripItem).Text);
        }

        private void SortEndTimeToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.EndTime, (sender as ToolStripItem).Text);
        }

        private void SortDisplayTimeToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.Duration, (sender as ToolStripItem).Text);
        }

        private void SortTextMaxLineLengthToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.TextMaxLineLength, (sender as ToolStripItem).Text);
        }

        private void SortTextTotalLengthToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.TextTotalLength, (sender as ToolStripItem).Text);
        }

        private void SortTextNumberOfLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.TextNumberOfLines, (sender as ToolStripItem).Text);
        }

        private void SortTextAlphabeticallytoolStripMenuItemClick(object sender, EventArgs e)
        {
            SortSubtitle(SubtitleSortCriteria.Text, (sender as ToolStripItem).Text);
        }

        private void ChangeLanguageToolStripMenuItemClick(object sender, EventArgs e)
        {
            var cl = new ChooseLanguage();
            _formPositionsAndSizes.SetPositionAndSize(cl);
            if (cl.ShowDialog(this) == DialogResult.OK)
            {
                SetLanguage(cl.CultureName);
                Configuration.Settings.Save();
            }
            _formPositionsAndSizes.SavePositionAndSize(cl);
         }

        private void SetLanguage(string cultureName)
        {
            try
            {
                if (string.IsNullOrEmpty(cultureName) || cultureName == "en-US")
                {
                    Configuration.Settings.Language = new Language(); // default is en-US
                }
                else
                {
                    var reader = new System.IO.StreamReader(Path.Combine(Configuration.DataDirectory, "Languages") + Path.DirectorySeparatorChar + cultureName + ".xml");
                    Configuration.Settings.Language = Language.Load(reader);
                    reader.Close();
                }
                Configuration.Settings.General.Language = cultureName;
                _languageGeneral = Configuration.Settings.Language.General;
                _language = Configuration.Settings.Language.Main;
                InitializeLanguage();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + Environment.NewLine +
                                Environment.NewLine +
                                exception.StackTrace, "Error loading language file");
                Configuration.Settings.Language = new Language(); // default is en-US
                _languageGeneral = Configuration.Settings.Language.General;
                _language = Configuration.Settings.Language.Main;
                InitializeLanguage();
                Configuration.Settings.General.Language = null;
            }
        }

        private void ToolStripMenuItemCompareClick(object sender, EventArgs e)
        {
            var compareForm = new Compare();
            compareForm.Initialize(_subtitle, _fileName, Configuration.Settings.Language.General.CurrentSubtitle);
            compareForm.Show();
        }

        private void ToolStripMenuItemAutoBreakLinesClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var autoBreakUnbreakLines = new AutoBreakUnbreakLines();
                var selectedLines = new Subtitle();
                foreach (int index in SubtitleListview1.SelectedIndices)
                    selectedLines.Paragraphs.Add(_subtitle.Paragraphs[index]);
                autoBreakUnbreakLines.Initialize(selectedLines, true);

                if (autoBreakUnbreakLines.ShowDialog() == DialogResult.OK && autoBreakUnbreakLines.FixedParagraphs.Count > 0)
                {
                    MakeHistoryForUndo(_language.BeforeAutoBalanceSelectedLines);

                    SubtitleListview1.BeginUpdate();
                    foreach (int index in SubtitleListview1.SelectedIndices)
                    {
                        Paragraph p = _subtitle.GetParagraphOrDefault(index);

                        int indexFixed = autoBreakUnbreakLines.FixedParagraphs.IndexOf(p);
                        if (indexFixed >= 0)
                        {
                            p.Text = Utilities.AutoBreakLine(p.Text);
                            SubtitleListview1.SetText(index, p.Text);
                        }
                    }
                    SubtitleListview1.EndUpdate();
                    _change = true;
                    RefreshSelectedParagraph();
                    ShowStatus(string.Format(_language.NumberOfLinesAutoBalancedX, autoBreakUnbreakLines.FixedParagraphs.Count));
                }
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ToolStripMenuItemUnbreakLinesClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var autoBreakUnbreakLines = new AutoBreakUnbreakLines();
                var selectedLines = new Subtitle();
                foreach (int index in SubtitleListview1.SelectedIndices)
                    selectedLines.Paragraphs.Add(_subtitle.Paragraphs[index]);
                autoBreakUnbreakLines.Initialize(selectedLines, false);

                if (autoBreakUnbreakLines.ShowDialog() == DialogResult.OK && autoBreakUnbreakLines.FixedParagraphs.Count > 0)
                {
                    MakeHistoryForUndo(_language.BeforeRemoveLineBreaksInSelectedLines);

                    SubtitleListview1.BeginUpdate();
                    foreach (int index in SubtitleListview1.SelectedIndices)
                    {
                        Paragraph p = _subtitle.GetParagraphOrDefault(index);

                        int indexFixed = autoBreakUnbreakLines.FixedParagraphs.IndexOf(p);
                        if (indexFixed >= 0)
                        {
                            p.Text = Utilities.UnbreakLine(p.Text);
                            SubtitleListview1.SetText(index, p.Text);
                        }
                    }
                    SubtitleListview1.EndUpdate();
                    _change = true;
                    RefreshSelectedParagraph();
                    ShowStatus(string.Format(_language.NumberOfWithRemovedLineBreakX, autoBreakUnbreakLines.FixedParagraphs.Count));
                }
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void MultipleReplaceToolStripMenuItemClick(object sender, EventArgs e)
        {
            var multipleReplace = new MultipleReplace();
            multipleReplace.Initialize(_subtitle);
            if (multipleReplace.ShowDialog(this) == DialogResult.OK)
            {
                MakeHistoryForUndo(_language.BeforeMultipleReplace);
                SaveSubtitleListviewIndexes();

                for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
                {
                    _subtitle.Paragraphs[i].Text = multipleReplace.FixedSubtitle.Paragraphs[i].Text;
                }

                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();
                RefreshSelectedParagraph();
                ShowSource();
                if (multipleReplace.FixCount > 0)
                    _change = true;
                ShowStatus(string.Format(_language.NumberOfLinesReplacedX , multipleReplace.FixCount));
            }
        }

        private void ToolStripMenuItemImportDvdSubtitlesClick(object sender, EventArgs e)
        {
            if (ContinueNewOrExit())
            {
                var formSubRip = new DvdSubRip();
                if (formSubRip.ShowDialog(this) == DialogResult.OK)
                {
                    var showSubtitles = new DvdSubRipChooseLanguage();
                    showSubtitles.Initialize(formSubRip.MergedVobSubPacks, formSubRip.Palette, formSubRip.Languages, formSubRip.SelectedLanguage);
                    if (formSubRip.Languages.Count == 1 || showSubtitles.ShowDialog(this) == DialogResult.OK)
                    {
                        var formSubOcr = new VobSubOcr();
                        var subs = formSubRip.MergedVobSubPacks;
                        if (showSubtitles.SelectedVobSubMergedPacks != null)
                            subs = showSubtitles.SelectedVobSubMergedPacks;
                        formSubOcr.Initialize(subs, formSubRip.Palette, Configuration.Settings.VobSubOcr, formSubRip.SelectedLanguage);
                        if (formSubOcr.ShowDialog(this) == DialogResult.OK)
                        {
                            MakeHistoryForUndo(_language.BeforeImportingDvdSubtitle);
                            FileNew();
                            _subtitle.Paragraphs.Clear();
                            SetCurrentFormat(new SubRip().FriendlyName);
                            _subtitle.WasLoadedWithFrameNumbers = false;
                            _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                            foreach (Paragraph p in formSubOcr.SubtitleFromOcr.Paragraphs)
                            {
                                _subtitle.Paragraphs.Add(p);
                            }

                            ShowSource();
                            SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                            _change = true;
                            _subtitleListViewIndex = -1;
                            SubtitleListview1.FirstVisibleIndex = -1;
                            SubtitleListview1.SelectIndexAndEnsureVisible(0);

                            _fileName = string.Empty;
                            Text = Title;

                            Configuration.Settings.Save();
                        }
                    }
                }
            }
        }

        private void ToolStripMenuItemSubIdxClick1(object sender, EventArgs e)
        {
            if (ContinueNewOrExit())
            {
                openFileDialog1.Title = _language.OpenVobSubFile;
                openFileDialog1.FileName = string.Empty;
                openFileDialog1.Filter = _language.VobSubFiles + "|*.sub";
                if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
                {
                    ImportAndOcrVobSubSubtitleNew(openFileDialog1.FileName);
                    openFileDialog1.InitialDirectory = Path.GetDirectoryName(openFileDialog1.FileName);
                }
            }
        }

        private void SubtitleListview1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (Configuration.Settings.General.ListViewDoubleClickAction == 1)
            {
                GotoSubPositionAndPause();
            }
            else if (Configuration.Settings.General.ListViewDoubleClickAction == 2)
            {
                if (AutoRepeatContinueOn)
                    PlayCurrent();
                else
                    buttonBeforeText_Click(null, null);
            }
            else if (Configuration.Settings.General.ListViewDoubleClickAction == 3)
            {
                textBoxListViewText.Focus();
            }
        }

        private void AddWordToNamesetcListToolStripMenuItemClick(object sender, EventArgs e)
        {
            var addToNamesList = new AddToNamesList();
            _formPositionsAndSizes.SetPositionAndSize(addToNamesList);
            addToNamesList.Initialize(_subtitle, textBoxListViewText.SelectedText);
            if (addToNamesList.ShowDialog(this) == DialogResult.OK)
                ShowStatus(string.Format(_language.NameXAddedToNamesEtcList, addToNamesList.NewName));
            else if (!string.IsNullOrEmpty(addToNamesList.NewName))
                ShowStatus(string.Format(_language.NameXNotAddedToNamesEtcList, addToNamesList.NewName));
            _formPositionsAndSizes.SavePositionAndSize(addToNamesList);
        }

        private void EditToolStripMenuItemDropDownOpening(object sender, EventArgs e)
        {
            if (GetCurrentEncoding() == Encoding.Default || _subtitleListViewIndex == -1)
            {
                toolStripMenuItemInsertUnicodeCharacter.Visible = false;
                toolStripSeparatorInsertUnicodeCharacter.Visible = false;
            }
            else
            {
                toolStripMenuItemInsertUnicodeCharacter.Visible = true;
                toolStripSeparatorInsertUnicodeCharacter.Visible = true;                
            }
        }

        private void InsertUnicodeSymbol(object sender, EventArgs e)
        {
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
            {

                textBoxSource.Text = textBoxSource.Text.Insert(textBoxSource.SelectionStart, (sender as ToolStripMenuItem).Text);
            }
            else
            {
                if (textBoxListViewTextAlternate.Visible && textBoxListViewTextAlternate.Enabled && textBoxListViewTextAlternate.Focused)
                    textBoxListViewTextAlternate.Text = textBoxListViewTextAlternate.Text.Insert(textBoxListViewTextAlternate.SelectionStart, (sender as ToolStripMenuItem).Text);
                else
                    textBoxListViewText.Text = textBoxListViewText.Text.Insert(textBoxListViewText.SelectionStart, (sender as ToolStripMenuItem).Text);
            }
        }

        private void ToolStripMenuItemAutoMergeShortLinesClick(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var formMergeShortLines = new MergeShortLines();
                _formPositionsAndSizes.SetPositionAndSize(formMergeShortLines);
                formMergeShortLines.Initialize(_subtitle);
                if (formMergeShortLines.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeMergeShortLines);
                    _subtitle = formMergeShortLines.MergedSubtitle;
                    ShowStatus(string.Format(_language.MergedShortLinesX, formMergeShortLines.NumberOfMerges));
                    SaveSubtitleListviewIndexes();
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                    _change = true;
                }
                _formPositionsAndSizes.SavePositionAndSize(formMergeShortLines);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void toolStripMenuItemAutoSplitLongLines_Click(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
            {
                ReloadFromSourceView();
                var splitLongLines = new SplitLongLines();
                _formPositionsAndSizes.SetPositionAndSize(splitLongLines);
                splitLongLines.Initialize(_subtitle);
                if (splitLongLines.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeMergeShortLines);
                    _subtitle = splitLongLines.SplittedSubtitle;
                    ShowStatus(string.Format(_language.MergedShortLinesX, splitLongLines.NumberOfSplits));
                    SaveSubtitleListviewIndexes();
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                    _change = true;
                }
                _formPositionsAndSizes.SavePositionAndSize(splitLongLines);
            }
            else
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void setMinimalDisplayTimeDifferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetMinimumDisplayTimeBetweenParagraphs setMinDisplayDiff = new SetMinimumDisplayTimeBetweenParagraphs();
            _formPositionsAndSizes.SetPositionAndSize(setMinDisplayDiff);
            setMinDisplayDiff.Initialize(_subtitle);
            if (setMinDisplayDiff.ShowDialog() == System.Windows.Forms.DialogResult.OK && setMinDisplayDiff.FixCount > 0)
            {
                MakeHistoryForUndo(_language.BeforeSetMinimumDisplayTimeBetweenParagraphs);                
                _subtitle = setMinDisplayDiff.FixedSubtitle;
                _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(CurrentFrameRate);
                ShowStatus(string.Format(_language.XMinimumDisplayTimeBetweenParagraphsChanged, setMinDisplayDiff.FixCount));
                SaveSubtitleListviewIndexes();
                ShowSource();
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();
                _change = true;
            }
            _formPositionsAndSizes.SavePositionAndSize(setMinDisplayDiff);
        }

        private void toolStripMenuItemImportText_Click(object sender, EventArgs e)
        {
            if (ContinueNewOrExit())
            {                
                ImportText importText = new ImportText();
                if (importText.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    FileNew();

                    if (!string.IsNullOrEmpty(importText.VideoFileName))
                        OpenVideo(importText.VideoFileName);

                    SyncPointsSync syncPointSync = new SyncPointsSync();
                    syncPointSync.Initialize(importText.FixedSubtitle, _fileName, importText.VideoFileName, _videoAudioTrackNumber);
                    if (syncPointSync.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                    {
                        ResetSubtitle();

                        _subtitleListViewIndex = -1;
                        MakeHistoryForUndo(_language.BeforeImportText);
                        _subtitle = importText.FixedSubtitle;
                        _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(CurrentFrameRate);
                        ShowStatus(_language.TextImported);
                        ShowSource();
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                        _change = true;
                    }
                    _videoFileName = syncPointSync.VideoFileName;
                }
            }
        }

        private void toolStripMenuItemPointSync_Click(object sender, EventArgs e)
        {
            SyncPointsSync pointSync = new SyncPointsSync();            
            pointSync.Initialize(_subtitle, _fileName,  _videoFileName, _videoAudioTrackNumber);
            _formPositionsAndSizes.SetPositionAndSize(pointSync);
            mediaPlayer.Pause();
            if (pointSync.ShowDialog(this) == DialogResult.OK)
            {
                _subtitleListViewIndex = -1;
                MakeHistoryForUndo(_language.BeforePointSynchronization);
                _subtitle = pointSync.FixedSubtitle;
                _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(CurrentFrameRate);
                ShowStatus(_language.PointSynchronizationDone);
                ShowSource();
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                _change = true;              
            }
            _videoFileName = pointSync.VideoFileName;
            _formPositionsAndSizes.SavePositionAndSize(pointSync);
        }

        private void pointSyncViaOtherSubtitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SyncPointsSync pointSync = new SyncPointsSync();
            openFileDialog1.Title = _language.OpenOtherSubtitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Subtitle sub = new Subtitle();
                Encoding enc;
                SubtitleFormat f = sub.LoadSubtitle(openFileDialog1.FileName, out enc, null);
                if (f == null)
                {
                    ShowUnknownSubtitle();
                    return;
                }

                pointSync.Initialize(_subtitle, _fileName, _videoFileName, _videoAudioTrackNumber, openFileDialog1.FileName, sub);
                mediaPlayer.Pause();
                if (pointSync.ShowDialog(this) == DialogResult.OK)
                {
                    _subtitleListViewIndex = -1;
                    MakeHistoryForUndo(_language.BeforePointSynchronization);
                    _subtitle = pointSync.FixedSubtitle;
                    _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(CurrentFrameRate);
                    ShowStatus(_language.PointSynchronizationDone);
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    _change = true;
                }
                _videoFileName = pointSync.VideoFileName;
            }
        }

        private void toolStripMenuItemImportTimeCodes_Click(object sender, EventArgs e)
        {
            if (_subtitle.Paragraphs.Count < 1)
            {
                MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            openFileDialog1.Title = _languageGeneral.OpenSubtitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                Encoding encoding = null;
                Subtitle timeCodeSubtitle = new Subtitle();
                SubtitleFormat format = timeCodeSubtitle.LoadSubtitle(openFileDialog1.FileName, out encoding, encoding);
                if (format == null)
                {
                    ShowUnknownSubtitle();
                    return;
                }

                MakeHistoryForUndo(_language.BeforeTimeCodeImport);

                if (GetCurrentSubtitleFormat().IsFrameBased)
                    timeCodeSubtitle.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                else
                    timeCodeSubtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);

                int count = 0;
                for (int i = 0; i < timeCodeSubtitle.Paragraphs.Count; i++)
                {
                    Paragraph existing = _subtitle.GetParagraphOrDefault(i);
                    Paragraph newTimeCode = timeCodeSubtitle.GetParagraphOrDefault(i);
                    if (existing == null || newTimeCode == null)
                        break;
                    existing.StartTime.TotalMilliseconds = newTimeCode.StartTime.TotalMilliseconds;
                    existing.EndTime.TotalMilliseconds = newTimeCode.EndTime.TotalMilliseconds;
                    existing.StartFrame = newTimeCode.StartFrame;
                    existing.EndFrame = newTimeCode.EndFrame;
                    count++;

                }
                ShowStatus(string.Format(_language.TimeCodeImportedFromXY, Path.GetFileName(openFileDialog1.FileName), count));
                SaveSubtitleListviewIndexes();
                ShowSource();
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();
                _change = true;
            }

        }
       
        private void toolStripMenuItemTranslationMode_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.IsAlternateTextColumnVisible)
            {
                SubtitleListview1.HideAlternateTextColumn();
                SubtitleListview1.AutoSizeAllColumns(this);
                _subtitleAlternate = new Subtitle();
                _subtitleAlternateFileName = null;

                buttonUnBreak.Visible = true;
                buttonUndoListViewChanges.Visible = true;
                textBoxListViewTextAlternate.Visible = false;
                labelAlternateText.Visible = false;
                labelAlternateCharactersPerSecond.Visible = false;
                labelTextAlternateLineLengths.Visible = false;
                labelAlternateSingleLine.Visible = false;
                labelTextAlternateLineTotal.Visible = false;
                textBoxListViewText.Width = (groupBoxEdit.Width - (textBoxListViewText.Left + 8 + buttonUnBreak.Width));
                textBoxListViewText.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

                labelCharactersPerSecond.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelCharactersPerSecond.Width);
                labelTextLineTotal.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelTextLineTotal.Width);
            }
            else
            {
                OpenAlternateSubtitle();
            }
            SetTitle();
        }

        private void OpenAlternateSubtitle()
        {
            if (ContinueNewOrExitAlternate())
            {
                SaveSubtitleListviewIndexes();
                openFileDialog1.Title = Configuration.Settings.Language.General.OpenOriginalSubtitleFile;
                openFileDialog1.FileName = string.Empty;
                openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
                if (!(openFileDialog1.ShowDialog(this) == DialogResult.OK))
                    return;

                if (!LoadAlternateSubtitleFile(openFileDialog1.FileName))
                    return;

                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                RestoreSubtitleListviewIndexes();
            }
        }

        private bool LoadAlternateSubtitleFile(string fileName)
        {
            if (!File.Exists(fileName))
                return false;

            if (Path.GetExtension(fileName).ToLower() == ".sub" && IsVobSubFile(fileName, false))
                return false;

            var fi = new FileInfo(fileName);
            if (fi.Length > 1024 * 1024 * 10) // max 10 mb
            {
                if (MessageBox.Show(string.Format(_language.FileXIsLargerThan10Mb + Environment.NewLine +
                                                    Environment.NewLine +
                                                    _language.ContinueAnyway,
                                                    fileName), Title, MessageBoxButtons.YesNoCancel) != DialogResult.Yes)
                    return false;
            }

            Encoding encoding;
            _subtitleAlternate = new Subtitle();
            _subtitleAlternateFileName = fileName;
            SubtitleFormat format = _subtitleAlternate.LoadSubtitle(fileName, out encoding, null);
            if (format == null)
                return false;

            if (format.IsFrameBased)
                _subtitleAlternate.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
            else
                _subtitleAlternate.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);

            SetupAlternateEdit();
            return true;
        }

        private void SetupAlternateEdit()
        {
            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate.Paragraphs.Count > 1)
            {
                InsertMissingParagraphs(_subtitle, _subtitleAlternate);
                InsertMissingParagraphs(_subtitleAlternate, _subtitle);

                buttonUnBreak.Visible = false;
                buttonUndoListViewChanges.Visible = false;
                buttonSplitLine.Visible = false;

                textBoxListViewText.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                textBoxListViewText.Width = (groupBoxEdit.Width - (textBoxListViewText.Left + 10)) / 2;
                textBoxListViewTextAlternate.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                textBoxListViewTextAlternate.Left = textBoxListViewText.Left + textBoxListViewText.Width + 3;
                textBoxListViewTextAlternate.Width = textBoxListViewText.Width;
                textBoxListViewTextAlternate.Visible = true;
                labelAlternateText.Text = Configuration.Settings.Language.General.OriginalText;
                labelAlternateText.Visible = true;
                labelAlternateCharactersPerSecond.Visible = true;
                labelTextAlternateLineLengths.Visible = true;
                labelAlternateSingleLine.Visible = true;
                labelTextAlternateLineTotal.Visible = true;

                labelCharactersPerSecond.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelCharactersPerSecond.Width);
                labelTextLineTotal.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelTextLineTotal.Width);
                Main_Resize(null, null);
                _changeAlternate = false;

                SetTitle();
            }

            SubtitleListview1.ShowAlternateTextColumn(Configuration.Settings.Language.General.OriginalText);
            SubtitleListview1.AutoSizeAllColumns(this);
        }

        private void InsertMissingParagraphs(Subtitle masterSubtitle, Subtitle insertIntoSubtitle)
        {
            int index = 0;
            foreach (Paragraph p in masterSubtitle.Paragraphs)
            {

                Paragraph insertParagraph = Utilities.GetOriginalParagraph(index, p, insertIntoSubtitle.Paragraphs);
                if (insertParagraph == null)
                {
                    insertParagraph = new Paragraph(p);
                    insertParagraph.Text = string.Empty;
                    insertIntoSubtitle.InsertParagraphInCorrectTimeOrder(insertParagraph);
                }
                index++;
            }
            insertIntoSubtitle.Renumber(1);
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        private void OpenVideo(string fileName)
        {            
            if (File.Exists(fileName))
            {
                FileInfo fi = new FileInfo(fileName);
                if (fi.Length < 1000)
                    return;

                Cursor = Cursors.WaitCursor;
                VideoFileName = fileName;
                if (mediaPlayer.VideoPlayer != null)
                {
                    mediaPlayer.Pause();
                    mediaPlayer.VideoPlayer.DisposeVideoPlayer();
                }
                _endSeconds = -1;

                VideoInfo videoInfo = ShowVideoInfo(fileName);
                toolStripComboBoxFrameRate.Text = videoInfo.FramesPerSecond.ToString();

                Utilities.InitializeVideoPlayerAndContainer(fileName, videoInfo, mediaPlayer, VideoLoaded, VideoEnded);
                mediaPlayer.Volume = 0;
                labelVideoInfo.Text = Path.GetFileName(fileName) + " " + videoInfo.Width + "x" + videoInfo.Height + " " + videoInfo.VideoCodec;
                                
                string peakWaveFileName = GetPeakWaveFileName(fileName);
                string spectrogramFolder = GetSpectrogramFolder(fileName);
                if (File.Exists(peakWaveFileName))
                {
                    audioVisualizer.WavePeaks = new WavePeakGenerator(peakWaveFileName);
                    audioVisualizer.ResetSpectrogram();
                    audioVisualizer.InitializeSpectrogram(spectrogramFolder);
                    toolStripComboBoxWaveForm_SelectedIndexChanged(null, null);
                    audioVisualizer.WavePeaks.GenerateAllSamples();
                    audioVisualizer.WavePeaks.Close();
                    SetWaveFormPosition(0, 0, 0);
                    timerWaveForm.Start();
                }
                Cursor = Cursors.Default;

                SetUndockedWindowsTitle();
            }
        }

        private void SetWaveFormPosition(double startPositionSeconds, double currentVideoPositionSeconds, int subtitleIndex)
        {
            if (SubtitleListview1.IsAlternateTextColumnVisible && Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable)
                audioVisualizer.SetPosition(startPositionSeconds, _subtitleAlternate, currentVideoPositionSeconds, -1);
            else
                audioVisualizer.SetPosition(startPositionSeconds, _subtitle, currentVideoPositionSeconds, subtitleIndex);
        }

        void VideoLoaded(object sender, EventArgs e)
        {
            mediaPlayer.Stop();
            mediaPlayer.Volume = Configuration.Settings.General.VideoPlayerDefaultVolume;
            timer1.Start();

            trackBarWaveFormPosition.Maximum = (int)mediaPlayer.Duration;

            if (_videoLoadedGoToSubPosAndPause)
            {
                Application.DoEvents();
                _videoLoadedGoToSubPosAndPause = false;
                GotoSubPositionAndPause();
            }
        }

        void VideoEnded(object sender, EventArgs e)
        {
            mediaPlayer.Pause();
        }

        private VideoInfo ShowVideoInfo(string fileName)
        {
            return Utilities.GetVideoInfo(fileName, delegate { Application.DoEvents(); });
        }

        private void TryToFindAndOpenVideoFile(string fileNameNoExtension)
        {
            string movieFileName = null;

            foreach (string extension in Utilities.GetMovieFileExtensions())
            {
                movieFileName = fileNameNoExtension + extension;
                if (File.Exists(movieFileName))
                    break;
            }

            if (movieFileName != null && File.Exists(movieFileName))
            {
                OpenVideo(movieFileName);
            }
            else if (fileNameNoExtension.Contains("."))
            {
                fileNameNoExtension = fileNameNoExtension.Substring(0, fileNameNoExtension.LastIndexOf("."));
                TryToFindAndOpenVideoFile(fileNameNoExtension);
            }
        }

        internal void GoBackSeconds(double seconds)
        {
            if (mediaPlayer != null)
            {
                if (mediaPlayer.CurrentPosition > seconds)
                    mediaPlayer.CurrentPosition -= seconds;
                else
                    mediaPlayer.CurrentPosition = 0;
                ShowSubtitle();
            }
        }

        private void ButtonStartHalfASecondBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(0.5);
        }

        private void ButtonStartThreeSecondsBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(3.0);
        }

        private void ButtonStartOneMinuteBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(60);
        }

        private void ButtonStartHalfASecondAheadClick(object sender, EventArgs e)
        {
            GoBackSeconds(-0.5);
        }

        private void ButtonStartThreeSecondsAheadClick(object sender, EventArgs e)
        {
            GoBackSeconds(-3);
        }

        private void ButtonStartOneMinuteAheadClick(object sender, EventArgs e)
        {
            GoBackSeconds(-60);
        }

        private void videoTimer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer != null)
            {
                if (!mediaPlayer.IsPaused)
                {
                    mediaPlayer.RefreshProgressBar();
                    ShowSubtitle();
                }
            }
        }

        private void videoModeHiddenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HideVideoPlayer();
        }

        private void createadjustLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowVideoPlayer();
        }

        private void HideVideoPlayer()
        {
            if (mediaPlayer != null)
                mediaPlayer.Pause();

            splitContainer1.Panel2Collapsed = true;
            splitContainerMain.Panel2Collapsed = true;
            Main_Resize(null, null);
        }

        private void ShowVideoPlayer()
        {
            if (_isVideoControlsUnDocked)
            {
                ShowHideUnDockedVideoControls();
            }
            else
            {
                if (toolStripButtonToggleVideo.Checked && toolStripButtonToggleWaveForm.Checked)
                {
                    splitContainer1.Panel2Collapsed = false;
                    MoveVideoUp();
                }
                else
                {
                    splitContainer1.Panel2Collapsed = true;
                    MoveVideoDown();
                }

                splitContainerMain.Panel2Collapsed = false;
                if (toolStripButtonToggleVideo.Checked)
                {
                    if (audioVisualizer.Visible)
                    {
                        audioVisualizer.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                    }
                    else
                    {
                        panelVideoPlayer.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                    }
                }
                else if (audioVisualizer.Visible)
                {
                    audioVisualizer.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                }
                audioVisualizer.Width = groupBoxVideo.Width - (audioVisualizer.Left + 10);

                checkBoxSyncListViewWithVideoWhilePlaying.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                panelWaveFormControls.Left = audioVisualizer.Left;
                trackBarWaveFormPosition.Left = panelWaveFormControls.Left + panelWaveFormControls.Width + 5;
                trackBarWaveFormPosition.Width = audioVisualizer.Left + audioVisualizer.Width - trackBarWaveFormPosition.Left + 5;
            }

            if (mediaPlayer.VideoPlayer == null && !string.IsNullOrEmpty(_fileName))
                TryToFindAndOpenVideoFile(Path.Combine(Path.GetDirectoryName(_fileName), Path.GetFileNameWithoutExtension(_fileName)));
            Main_Resize(null, null);
        }

        private void ShowHideUnDockedVideoControls()
        {
            if (_videoPlayerUnDocked == null || _videoPlayerUnDocked.IsDisposed)
                UnDockVideoPlayer();
            _videoPlayerUnDocked.Visible = false;
            if (toolStripButtonToggleVideo.Checked)
            {
                _videoPlayerUnDocked.Show(this);
                if (_videoPlayerUnDocked.WindowState == FormWindowState.Minimized)
                    _videoPlayerUnDocked.WindowState = FormWindowState.Normal;
            }

            if (_waveFormUnDocked == null || _waveFormUnDocked.IsDisposed)
                UnDockWaveForm();
            _waveFormUnDocked.Visible = false;
            if (toolStripButtonToggleWaveForm.Checked)
            {
                _waveFormUnDocked.Show(this);
                if (_waveFormUnDocked.WindowState == FormWindowState.Minimized)
                    _waveFormUnDocked.WindowState = FormWindowState.Normal;
            }

            if (toolStripButtonToggleVideo.Checked || toolStripButtonToggleWaveForm.Checked)
            {                
                if (_videoControlsUnDocked == null || _videoControlsUnDocked.IsDisposed)
                    UnDockVideoButtons();
                _videoControlsUnDocked.Visible = false;
                _videoControlsUnDocked.Show(this);
            }
            else
            {
                if (_videoControlsUnDocked != null && !_videoControlsUnDocked.IsDisposed)
                    _videoControlsUnDocked.Visible = false;
            }
        }

        private void MoveVideoUp()
        {
            if (splitContainer1.Panel2.Controls.Count == 0)
            {
                var control = panelVideoPlayer;
                groupBoxVideo.Controls.Remove(control);
                splitContainer1.Panel2.Controls.Add(control);
            }
            panelVideoPlayer.Top = 0;
            panelVideoPlayer.Left = 0;
            panelVideoPlayer.Height = splitContainer1.Panel2.Height - 2;
            panelVideoPlayer.Width = splitContainer1.Panel2.Width - 2;
        }

        private void MoveVideoDown()
        {
            if (splitContainer1.Panel2.Controls.Count > 0)
            {
                var control = panelVideoPlayer;
                splitContainer1.Panel2.Controls.Clear();
                groupBoxVideo.Controls.Add(control);
            }
            panelVideoPlayer.Top = 32;
            panelVideoPlayer.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
            panelVideoPlayer.Height = groupBoxVideo.Height - (panelVideoPlayer.Top + 5);
            panelVideoPlayer.Width = groupBoxVideo.Width - (panelVideoPlayer.Left + 5);
        }

        private void FixLargeFonts()
        {
            Graphics graphics = this.CreateGraphics();
            SizeF textSize = graphics.MeasureString(buttonPlayPrevious.Text, this.Font);
            if (textSize.Height > buttonPlayPrevious.Height - 4)
            {
                int newButtonHeight = (int)(textSize.Height + 7 + 0.5);
                Utilities.SetButtonHeight(this, newButtonHeight, 1);

                // List view
                SubtitleListview1.InitializeTimeStampColumWidths(this);
                int adjustUp = 8;
                SubtitleListview1.Height = SubtitleListview1.Height - adjustUp;
                groupBoxEdit.Top = groupBoxEdit.Top - adjustUp;
                groupBoxEdit.Height = groupBoxEdit.Height + adjustUp;
                numericUpDownDuration.Left = timeUpDownStartTime.Left + timeUpDownStartTime.Width;
                numericUpDownDuration.Width = numericUpDownDuration.Width + 5;
                labelDuration.Left = numericUpDownDuration.Left - 3;
                labelAutoDuration.Left = labelDuration.Left - (labelAutoDuration.Width - 5);

                // Video controls - Create
                timeUpDownVideoPosition.Left = labelVideoPosition.Left + labelVideoPosition.Width;
                int buttonWidth = labelVideoPosition.Width + timeUpDownVideoPosition.Width;
                buttonInsertNewText.Width = buttonWidth;
                buttonBeforeText.Width = buttonWidth;
                buttonGotoSub.Width = buttonWidth;
                buttonSetStartTime.Width = buttonWidth;
                buttonSetEnd.Width = buttonWidth;
                int FKeyLeft = buttonInsertNewText.Left + buttonInsertNewText.Width;
                labelCreateF9.Left = FKeyLeft;
                labelCreateF10.Left = FKeyLeft;
                labelCreateF11.Left = FKeyLeft;
                labelCreateF12.Left = FKeyLeft;
                buttonForward1.Left = buttonInsertNewText.Left + buttonInsertNewText.Width - buttonForward1.Width;
                numericUpDownSec1.Width = buttonInsertNewText.Width - (numericUpDownSec1.Left + buttonForward1.Width);
                buttonForward2.Left = buttonInsertNewText.Left + buttonInsertNewText.Width - buttonForward2.Width;
                numericUpDownSec2.Width = buttonInsertNewText.Width - (numericUpDownSec2.Left + buttonForward2.Width);

                // Video controls - Adjust
                timeUpDownVideoPositionAdjust.Left = labelVideoPosition2.Left + labelVideoPosition2.Width;
                buttonSetStartAndOffsetRest.Width = buttonWidth;
                buttonSetEndAndGoToNext.Width = buttonWidth;
                buttonAdjustSetStartTime.Width = buttonWidth;
                buttonAdjustSetEndTime.Width = buttonWidth;
                buttonAdjustPlayBefore.Width = buttonWidth;
                buttonAdjustGoToPosAndPause.Width = buttonWidth;
                labelAdjustF9.Left = FKeyLeft;
                labelAdjustF10.Left = FKeyLeft;
                labelAdjustF11.Left = FKeyLeft;
                labelAdjustF12.Left = FKeyLeft;
                buttonAdjustSecForward1.Left = buttonInsertNewText.Left + buttonInsertNewText.Width - buttonAdjustSecForward1.Width;
                numericUpDownSecAdjust1.Width = buttonInsertNewText.Width - (numericUpDownSecAdjust2.Left + buttonAdjustSecForward1.Width);
                buttonAdjustSecForward2.Left = buttonInsertNewText.Left + buttonInsertNewText.Width - buttonAdjustSecForward2.Width;
                numericUpDownSecAdjust2.Width = buttonInsertNewText.Width - (numericUpDownSecAdjust2.Left + buttonAdjustSecForward2.Width);

                tabControl1_SelectedIndexChanged(null, null);
            }            
        }

        private void Main_Resize(object sender, EventArgs e)
        {            
            panelVideoPlayer.Invalidate();

            if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                textBoxListViewText.Width = (groupBoxEdit.Width - (textBoxListViewText.Left + 10)) / 2;
                textBoxListViewTextAlternate.Left = textBoxListViewText.Left + textBoxListViewText.Width + 3;
                labelAlternateText.Left = textBoxListViewTextAlternate.Left;

                textBoxListViewTextAlternate.Width = textBoxListViewText.Width;

                labelAlternateCharactersPerSecond.Left = textBoxListViewTextAlternate.Left + (textBoxListViewTextAlternate.Width - labelAlternateCharactersPerSecond.Width);
                labelTextAlternateLineLengths.Left = textBoxListViewTextAlternate.Left;
                labelAlternateSingleLine.Left = labelTextAlternateLineLengths.Left + labelTextAlternateLineLengths.Width;
                labelTextAlternateLineTotal.Left = textBoxListViewTextAlternate.Left + (textBoxListViewTextAlternate.Width - labelTextAlternateLineTotal.Width); 
            }

            labelCharactersPerSecond.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelCharactersPerSecond.Width);
            labelTextLineTotal.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelTextLineTotal.Width);
            SubtitleListview1.AutoSizeAllColumns(this);
        }

        private void PlayCurrent()
        {
            if (_subtitleListViewIndex >= 0)
            {
                GotoSubtitleIndex(_subtitleListViewIndex);
                textBoxListViewText.Focus();
                ReadyAutoRepeat();
                PlayPart(_subtitle.Paragraphs[_subtitleListViewIndex]);
            }
        }

        private void ReadyAutoRepeat()
        {
            if (checkBoxAutoRepeatOn.Checked)
            {
                _repeatCount = int.Parse(comboBoxAutoRepeat.Text);
            }
            else
            {
                _repeatCount = -1;
            }
            labelStatus.Text = _language.VideoControls.Playing;
        }

        private void Next()
        {
            int newIndex = _subtitleListViewIndex + 1;
            if (newIndex < _subtitle.Paragraphs.Count)
            {
                foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    item.Selected = false;
                SubtitleListview1.Items[newIndex].Selected = true;
                SubtitleListview1.Items[newIndex].EnsureVisible();
                textBoxListViewText.Focus();
                textBoxListViewText.SelectAll();
                _subtitleListViewIndex = newIndex;
                GotoSubtitleIndex(newIndex);
                ShowSubtitle();
                PlayCurrent();
            }
        }

        private void PlayPrevious()
        {
            if (_subtitleListViewIndex > 0)
            {
                int newIndex = _subtitleListViewIndex - 1;
                foreach (ListViewItem item in SubtitleListview1.SelectedItems)
                    item.Selected = false;
                SubtitleListview1.Items[newIndex].Selected = true;
                SubtitleListview1.Items[newIndex].EnsureVisible();
                textBoxListViewText.Focus();
                textBoxListViewText.SelectAll();
                GotoSubtitleIndex(newIndex);
                ShowSubtitle();
                _subtitleListViewIndex = newIndex;
                PlayCurrent();
            }
        }

        private void GotoSubtitleIndex(int index)
        {
            if (mediaPlayer != null && mediaPlayer.VideoPlayer != null && mediaPlayer.Duration > 0)
            {
                mediaPlayer.CurrentPosition = _subtitle.Paragraphs[index].StartTime.TotalSeconds;
            }
        }

        private void PlayPart(Paragraph paragraph)
        {
            if (mediaPlayer != null && mediaPlayer.VideoPlayer != null)
            {
                double startSeconds = paragraph.StartTime.TotalSeconds;
                if (startSeconds > 0.2)
                    startSeconds -= 0.2; // go a little back

                _endSeconds = paragraph.EndTime.TotalSeconds;
                if (mediaPlayer.Duration > _endSeconds + 0.2)
                    _endSeconds += 0.2; // go a little forward

                mediaPlayer.CurrentPosition = startSeconds;
                ShowSubtitle();
                mediaPlayer.Play();
            }
        }

        private void buttonSetStartTime_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count == 1)
            {
                timeUpDownStartTime.MaskedTextBox.TextChanged -= MaskedTextBox_TextChanged;
                int index = SubtitleListview1.SelectedItems[0].Index;
                Paragraph oldParagraph = new Paragraph(_subtitle.Paragraphs[index]);
                double videoPosition = mediaPlayer.CurrentPosition;

                timeUpDownStartTime.TimeCode = new TimeCode(TimeSpan.FromSeconds(videoPosition));

                var duration = _subtitle.Paragraphs[index].Duration.TotalMilliseconds;

                _subtitle.Paragraphs[index].StartTime.TotalMilliseconds = TimeSpan.FromSeconds(videoPosition).TotalMilliseconds;
                _subtitle.Paragraphs[index].EndTime.TotalMilliseconds = _subtitle.Paragraphs[index].StartTime.TotalMilliseconds + duration;
                SubtitleListview1.SetStartTime(index, _subtitle.Paragraphs[index]);
                SubtitleListview1.SetDuration(index, _subtitle.Paragraphs[index]);
                timeUpDownStartTime.TimeCode = _subtitle.Paragraphs[index].StartTime;
                timeUpDownStartTime.MaskedTextBox.TextChanged += MaskedTextBox_TextChanged;
                UpdateOriginalTimeCodes(oldParagraph);
                _change = true;
            }
        }

        private void buttonSetEndTime_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count == 1)
            {
                int index = SubtitleListview1.SelectedItems[0].Index;
                double videoPosition = mediaPlayer.CurrentPosition;

                _subtitle.Paragraphs[index].EndTime = new TimeCode(TimeSpan.FromSeconds(videoPosition));
                SubtitleListview1.SetStartTime(index, _subtitle.Paragraphs[index]);
                SubtitleListview1.SetDuration(index, _subtitle.Paragraphs[index]);

                if (index + 1 < _subtitle.Paragraphs.Count)
                {
                    SubtitleListview1.SelectedItems[0].Selected = false;
                    SubtitleListview1.Items[index + 1].Selected = true;
                    _subtitle.Paragraphs[index + 1].StartTime = new TimeCode(TimeSpan.FromSeconds(videoPosition));
                    SubtitleListview1.AutoScrollOffset.Offset(0, index * 16);
                    SubtitleListview1.EnsureVisible(Math.Min(SubtitleListview1.Items.Count - 1, index + 5));
                }
                else
                {
                    numericUpDownDuration.Value = (decimal)(_subtitle.Paragraphs[index].Duration.TotalSeconds);
                }
            }
        }

        private void buttonSetEnd_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count == 1)
            {
                int index = SubtitleListview1.SelectedItems[0].Index;
                double videoPosition = mediaPlayer.CurrentPosition;

                _subtitle.Paragraphs[index].EndTime = new TimeCode(TimeSpan.FromSeconds(videoPosition));
                SubtitleListview1.SetStartTime(index, _subtitle.Paragraphs[index]);
                SubtitleListview1.SetDuration(index, _subtitle.Paragraphs[index]);

                numericUpDownDuration.Value = (decimal)(_subtitle.Paragraphs[index].Duration.TotalSeconds);
            }
        }

        private void buttonInsertNewText_Click(object sender, EventArgs e)
        {
            mediaPlayer.Pause();

            var newParagraph = InsertNewTextAtVideoPosition();

            textBoxListViewText.Focus();
            timerAutoDuration.Start();

            ShowStatus(string.Format(_language.VideoControls.NewTextInsertAtX, newParagraph.StartTime.ToShortString()));
        }

        private Paragraph InsertNewTextAtVideoPosition()
        {
            // current movie pos
            double totalMilliseconds = mediaPlayer.CurrentPosition * 1000.0;

            int startNumber = 1;
            if (_subtitle.Paragraphs.Count > 0)
                startNumber = _subtitle.Paragraphs[0].Number;

            TimeCode tc = new TimeCode(TimeSpan.FromMilliseconds(totalMilliseconds));
            MakeHistoryForUndo(_language.BeforeInsertSubtitleAtVideoPosition + "  " + tc.ToString());

            // find index where to insert
            int index = 0;
            foreach (Paragraph p in _subtitle.Paragraphs)
            {
                if (p.StartTime.TotalMilliseconds > totalMilliseconds)
                    break;
                index++;
            }

            // create and insert
            var newParagraph = new Paragraph("", totalMilliseconds, totalMilliseconds + 2000);
            if (GetCurrentSubtitleFormat().IsFrameBased)
            {
                newParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                newParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
            }
            _subtitle.Paragraphs.Insert(index, newParagraph);

            _subtitleListViewIndex = -1;
            _subtitle.Renumber(startNumber);
            SubtitleListview1.Fill(_subtitle.Paragraphs);
            SubtitleListview1.SelectIndexAndEnsureVisible(index);
            return newParagraph;
        }

        private void timerAutoDuration_Tick(object sender, EventArgs e)
        {
            labelAutoDuration.Visible = !labelAutoDuration.Visible;

            double duration = Utilities.GetDisplayMillisecondsFromText(textBoxListViewText.Text) * 1.4;
            numericUpDownDuration.Value = (decimal)(duration / 1000.0);

            // update _subtitle + listview
            if (SubtitleListview1.SelectedItems.Count > 0)
            {
                int firstSelectedIndex = SubtitleListview1.SelectedItems[0].Index;
                Paragraph currentParagraph = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                currentParagraph.EndTime.TotalMilliseconds = currentParagraph.StartTime.TotalMilliseconds + duration;
                SubtitleListview1.SetDuration(firstSelectedIndex, currentParagraph);
            }
        }

        private void buttonBeforeText_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count > 0)
            {
                int index = SubtitleListview1.SelectedItems[0].Index;

                mediaPlayer.Pause();
                double pos = _subtitle.Paragraphs[index].StartTime.TotalSeconds;
                if (pos > 1)
                    mediaPlayer.CurrentPosition = (_subtitle.Paragraphs[index].StartTime.TotalSeconds) - 0.5;
                else
                    mediaPlayer.CurrentPosition = _subtitle.Paragraphs[index].StartTime.TotalSeconds;
                mediaPlayer.Play();
            }
        }

        private void GotoSubPositionAndPause()
        {
            if (SubtitleListview1.SelectedItems.Count > 0)
            {
                int index = SubtitleListview1.SelectedItems[0].Index;

                mediaPlayer.Pause();
                mediaPlayer.CurrentPosition = _subtitle.Paragraphs[index].StartTime.TotalSeconds;
                ShowSubtitle();

                double startPos = mediaPlayer.CurrentPosition - 1;
                if (startPos < 0)
                    startPos = 0;

                SetWaveFormPosition(startPos, mediaPlayer.CurrentPosition, index);
            }
        }

        private void buttonGotoSub_Click(object sender, EventArgs e)
        {
            GotoSubPositionAndPause();
        }

        private void buttonOpenVideo_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(openFileDialog1.InitialDirectory) && !string.IsNullOrEmpty(_fileName))
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(_fileName);
            openFileDialog1.Title = Configuration.Settings.Language.General.OpenVideoFileTitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetVideoFileFilter();
            openFileDialog1.FileName = string.Empty;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(openFileDialog1.FileName);
                OpenVideo(openFileDialog1.FileName);
            }
        }

        private void toolStripButtonToggleVideo_Click(object sender, EventArgs e)
        {
            toolStripButtonToggleVideo.Checked = !toolStripButtonToggleVideo.Checked;
            panelVideoPlayer.Visible = toolStripButtonToggleVideo.Checked;
            mediaPlayer.BringToFront();
//            labelSubtitle.BringToFront();
            if (!toolStripButtonToggleVideo.Checked && !toolStripButtonToggleWaveForm.Checked)
            {
                if (_isVideoControlsUnDocked)
                    ShowHideUnDockedVideoControls();
                else
                    HideVideoPlayer();
            }
            else
            {
                ShowVideoPlayer();
            }
            Configuration.Settings.General.ShowVideoPlayer = toolStripButtonToggleVideo.Checked;
            Refresh();
        }

        private void toolStripButtonToggleWaveForm_Click(object sender, EventArgs e)
        {
            toolStripButtonToggleWaveForm.Checked = !toolStripButtonToggleWaveForm.Checked;
            audioVisualizer.Visible = toolStripButtonToggleWaveForm.Checked;
            trackBarWaveFormPosition.Visible = toolStripButtonToggleWaveForm.Checked;
            panelWaveFormControls.Visible = toolStripButtonToggleWaveForm.Checked;
            if (!toolStripButtonToggleWaveForm.Checked && !toolStripButtonToggleVideo.Checked)
            {
                if (_isVideoControlsUnDocked)
                    ShowHideUnDockedVideoControls();
                else
                    HideVideoPlayer();
            }
            else
            {
                ShowVideoPlayer();
            }
            Configuration.Settings.General.ShowWaveForm = toolStripButtonToggleWaveForm.Checked;
            Refresh();
        }

        public void ShowEarlierOrLater(double adjustMilliseconds, bool onlySelected)
        {
            TimeCode tc = new TimeCode(TimeSpan.FromMilliseconds(adjustMilliseconds));
            MakeHistoryForUndo(_language.BeforeShowSelectedLinesEarlierLater  + ": " + tc.ToString());

            double frameRate = CurrentFrameRate;            
            SubtitleListview1.BeginUpdate();
            for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                if (SubtitleListview1.Items[i].Selected || !onlySelected)
                {
                    Paragraph p = _subtitle.GetParagraphOrDefault(i);
                    if (p != null)
                    {
                        p.StartTime.TotalMilliseconds += adjustMilliseconds;
                        p.EndTime.TotalMilliseconds += adjustMilliseconds;
                        SubtitleListview1.SetStartTime(i, p);
                    }
                }
            }
            _change = true;
            SubtitleListview1.EndUpdate();
            if (_subtitle.WasLoadedWithFrameNumbers)
                _subtitle.CalculateFrameNumbersFromTimeCodesNoCheck(frameRate);
            RefreshSelectedParagraph();
            UpdateSourceView();
        }

        private void UpdateSourceView()
        {
            if (tabControlSubtitle.SelectedIndex == TabControlSourceView)
                ShowSource();
        }

        private void toolStripMenuItemAdjustAllTimes_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count > 1)
            {
                ShowSelectedLinesEarlierlaterToolStripMenuItemClick(null, null);
            }
            else
            {
                if (_subtitle != null && _subtitle.Paragraphs.Count > 1)
                {
                    mediaPlayer.Pause();

                    if (_showEarlierOrLater != null && !_showEarlierOrLater.IsDisposed)
                    {
                        _showEarlierOrLater.WindowState = FormWindowState.Normal;
                        _showEarlierOrLater.Focus();
                        return;
                    }

                    _showEarlierOrLater = new ShowEarlierLater();
                    if (!_formPositionsAndSizes.SetPositionAndSize(_showEarlierOrLater))
                    {
                        _showEarlierOrLater.Top = this.Top + 100;
                        _showEarlierOrLater.Left = this.Left + (this.Width /2)  - (_showEarlierOrLater.Width / 3);
                    }
                    SaveSubtitleListviewIndexes();
                    _showEarlierOrLater.Initialize(ShowEarlierOrLater, _formPositionsAndSizes, false);
                    _showEarlierOrLater.Show(this);
                }
                else
                {
                    MessageBox.Show(_language.NoSubtitleLoaded, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer != null && mediaPlayer.VideoPlayer != null)
            {
                if (!mediaPlayer.IsPaused)
                {
                    timeUpDownVideoPosition.Enabled = false;
                    timeUpDownVideoPositionAdjust.Enabled = false;

                    if (_endSeconds >= 0 && mediaPlayer.CurrentPosition > _endSeconds && !AutoRepeatContinueOn)
                    {
                        mediaPlayer.Pause();
                        mediaPlayer.CurrentPosition = _endSeconds + EndDelay;
                        _endSeconds = -1;
                    }

                    if (AutoRepeatContinueOn) 
                    {
                        if (_endSeconds >= 0 && mediaPlayer.CurrentPosition > _endSeconds && checkBoxAutoRepeatOn.Checked)
                        {
                            mediaPlayer.Pause();
                            _endSeconds = -1;

                            if (checkBoxAutoRepeatOn.Checked && _repeatCount > 0)
                            {
                                if (_repeatCount == 1)
                                    labelStatus.Text = _language.VideoControls.RepeatingLastTime;
                                else
                                    labelStatus.Text = string.Format(Configuration.Settings.Language.Main.VideoControls.RepeatingXTimesLeft, _repeatCount);

                                _repeatCount--;
                                if (_subtitleListViewIndex >= 0 && _subtitleListViewIndex < _subtitle.Paragraphs.Count)
                                    PlayPart(_subtitle.Paragraphs[_subtitleListViewIndex]);
                            }
                            else if (checkBoxAutoContinue.Checked)
                            {
                                _autoContinueDelayCount = int.Parse(comboBoxAutoContinue.Text);
                                if (_repeatCount == 1)
                                    labelStatus.Text = _language.VideoControls.AutoContinueInOneSecond;
                                else
                                    labelStatus.Text = string.Format(Configuration.Settings.Language.Main.VideoControls.AutoContinueInXSeconds, _autoContinueDelayCount);
                                timerAutoContinue.Start();
                            }
                        }
                    }
                }
                else
                {
                    timeUpDownVideoPosition.Enabled = true;
                    timeUpDownVideoPositionAdjust.Enabled = true;
                }
                timeUpDownVideoPosition.TimeCode = new TimeCode(TimeSpan.FromMilliseconds(mediaPlayer.CurrentPosition * 1000.0));
                timeUpDownVideoPositionAdjust.TimeCode = new TimeCode(TimeSpan.FromMilliseconds(mediaPlayer.CurrentPosition * 1000.0));
                mediaPlayer.RefreshProgressBar();
                int index = ShowSubtitle();
                if (index != -1 && checkBoxSyncListViewWithVideoWhilePlaying.Checked)
                {
                    if ((DateTime.Now.Ticks - _lastTextKeyDownTicks) > 10000 * 700) // only if last typed char was entered > 700 milliseconds 
                    {
                        if (_endSeconds <= 0 || !checkBoxAutoRepeatOn.Checked)
                        {
                            SubtitleListview1.BeginUpdate();
                            if (index + 2 < SubtitleListview1.Items.Count)
                                SubtitleListview1.EnsureVisible(index + 2);
                            SubtitleListview1.SelectIndexAndEnsureVisible(index);
                            SubtitleListview1.EndUpdate();
                        }
                    }
                }

                trackBarWaveFormPosition.ValueChanged -= trackBarWaveFormPosition_ValueChanged;
                int value = (int)mediaPlayer.CurrentPosition;
                if (value > trackBarWaveFormPosition.Maximum)
                    value = trackBarWaveFormPosition.Maximum;
                if (value < trackBarWaveFormPosition.Minimum)
                    value = trackBarWaveFormPosition.Minimum;
                trackBarWaveFormPosition.Value = value;
                trackBarWaveFormPosition.ValueChanged += trackBarWaveFormPosition_ValueChanged;
            }
        }

        private void StopAutoDuration()
        {
            timerAutoDuration.Stop();
            labelAutoDuration.Visible = false;
        }

        private void textBoxListViewText_Leave(object sender, EventArgs e)
        {
            StopAutoDuration();
        }

        private void timerAutoContinue_Tick(object sender, EventArgs e)
        {
            _autoContinueDelayCount--;

            if (_autoContinueDelayCount == 0)
            {
                timerAutoContinue.Stop();

                if (timerStillTyping.Enabled)
                {
                    labelStatus.Text = _language.VideoControls.StillTypingAutoContinueStopped;
                }
                else
                {
                    labelStatus.Text = string.Empty;
                    Next();
                }
            }
            else
            {
                if (_repeatCount == 1)
                    labelStatus.Text = _language.VideoControls.AutoContinueInOneSecond;
                else
                    labelStatus.Text = string.Format(Configuration.Settings.Language.Main.VideoControls.AutoContinueInXSeconds, _autoContinueDelayCount);
            }
        }

        private void timerStillTyping_Tick(object sender, EventArgs e)
        {
            timerStillTyping.Stop();
        }

        private void textBoxListViewText_MouseMove(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control && MouseButtons == System.Windows.Forms.MouseButtons.Left)
            {
                if (!string.IsNullOrEmpty(textBoxListViewText.SelectedText))
                    textBoxListViewText.DoDragDrop(textBoxListViewText.SelectedText, DragDropEffects.Copy);
                else
                    textBoxListViewText.DoDragDrop(textBoxListViewText.Text, DragDropEffects.Copy);
            }
            else if (AutoRepeatContinueOn && !textBoxSearchWord.Focused)
            {
                string selectedText = textBoxListViewText.SelectedText;
                if (!string.IsNullOrEmpty(selectedText))
                {
                    selectedText = selectedText.Trim();
                    selectedText = selectedText.TrimEnd('.');
                    selectedText = selectedText.TrimEnd(',');
                    selectedText = selectedText.TrimEnd('!');
                    selectedText = selectedText.TrimEnd('?');
                    selectedText = selectedText.Trim();
                    if (!string.IsNullOrEmpty(selectedText) && selectedText != textBoxSearchWord.Text)
                    {
                        textBoxSearchWord.Text = Utilities.RemoveHtmlTags(selectedText);
                    }
                }
            }
        }

        private void textBoxListViewText_KeyUp(object sender, KeyEventArgs e)
        {
            textBoxListViewText_MouseMove(sender, null);
        }

        private void buttonGoogleIt_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.google.com/search?q=" + Utilities.UrlEncode(textBoxSearchWord.Text));
        }

        private void buttonGoogleTranslateIt_Click(object sender, EventArgs e)
        {
            string languageId = Utilities.AutoDetectGoogleLanguage(_subtitle);
            System.Diagnostics.Process.Start("http://translate.google.com/#auto|" + languageId + "|" + Utilities.UrlEncode(textBoxSearchWord.Text));
        }

        private void ButtonPlayCurrentClick(object sender, EventArgs e)
        {
            PlayCurrent();
        }

        private void buttonPlayNext_Click(object sender, EventArgs e)
        {
            Next();
        }

        private void buttonPlayPrevious_Click(object sender, EventArgs e)
        {
            PlayPrevious();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            _endSeconds = -1;
            timerAutoContinue.Stop();
            mediaPlayer.Pause();
            labelStatus.Text = string.Empty;
        }

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            toolStripMenuItemOpenContainingFolder.Visible = !string.IsNullOrEmpty(_fileName) && File.Exists(_fileName);

            bool subtitleLoaded = _subtitle != null && _subtitle.Paragraphs.Count > 0;
            openOriginalToolStripMenuItem.Visible = subtitleLoaded;
            if (subtitleLoaded && Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
            {
                saveOriginalToolStripMenuItem.Visible = true;
                saveOriginalAstoolStripMenuItem.Visible = true;
                removeOriginalToolStripMenuItem.Visible = true;
            }
            else
            {
                saveOriginalToolStripMenuItem.Visible = false;
                saveOriginalAstoolStripMenuItem.Visible = false;
                removeOriginalToolStripMenuItem.Visible = false;
            }
            toolStripSeparator20.Visible = subtitleLoaded;
        }

        private void toolStripMenuItemOpenContainingFolder_Click(object sender, EventArgs e)
        {
            string folderName = Path.GetDirectoryName(_fileName);
            if (Utilities.IsRunningOnMono())
            {
                System.Diagnostics.Process.Start(folderName);
            }
            else 
            {
                string argument = @"/select, " + _fileName;
                System.Diagnostics.Process.Start("explorer.exe", argument);
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {           
            if (tabControlButtons.SelectedIndex == 0)
            {
                tabControlButtons.Width = groupBoxTranslateSearch.Left + groupBoxTranslateSearch.Width + 10;
                Configuration.Settings.VideoControls.LastActiveTab = "Translate";
            }
            else if (tabControlButtons.SelectedIndex == 1)
            {
                tabControlButtons.Width = buttonInsertNewText.Left + buttonInsertNewText.Width + 35;
                Configuration.Settings.VideoControls.LastActiveTab = "Create";
            }
            else if (tabControlButtons.SelectedIndex == 2)
            {
                tabControlButtons.Width = buttonInsertNewText.Left + buttonInsertNewText.Width + 35;
                Configuration.Settings.VideoControls.LastActiveTab = "Adjust";
            }

            if (!_isVideoControlsUnDocked)
            {
                if (toolStripButtonToggleWaveForm.Checked)
                    audioVisualizer.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                if (!toolStripButtonToggleWaveForm.Checked && toolStripButtonToggleVideo.Checked)
                {
                    panelVideoPlayer.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                    panelVideoPlayer.Width = groupBoxVideo.Width - (panelVideoPlayer.Left + 10);
                }

                audioVisualizer.Width = groupBoxVideo.Width - (audioVisualizer.Left + 10);
                panelWaveFormControls.Left = audioVisualizer.Left;
                trackBarWaveFormPosition.Left = panelWaveFormControls.Left + panelWaveFormControls.Width + 5;
                trackBarWaveFormPosition.Width = groupBoxVideo.Width - (trackBarWaveFormPosition.Left + 10);
                this.Main_Resize(null, null);
                checkBoxSyncListViewWithVideoWhilePlaying.Left = tabControlButtons.Left + tabControlButtons.Width + 5;
                Refresh();
            }
            else if (_videoControlsUnDocked != null && !_videoControlsUnDocked.IsDisposed)
            {
                _videoControlsUnDocked.Width = tabControlButtons.Width + 20;
                _videoControlsUnDocked.Height = tabControlButtons.Height + 65;
            }
        }

        private void buttonSecBack1_Click(object sender, EventArgs e)
        {
            GoBackSeconds((double)numericUpDownSec1.Value);
        }

        private void buttonForward1_Click(object sender, EventArgs e)
        {
            GoBackSeconds(-(double)numericUpDownSec1.Value);
        }

        private void ButtonSetStartAndOffsetRestClick(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count == 1)
            {
                bool oldSync = checkBoxSyncListViewWithVideoWhilePlaying.Checked;
                checkBoxSyncListViewWithVideoWhilePlaying.Checked = false;

                timeUpDownStartTime.MaskedTextBox.TextChanged -= MaskedTextBox_TextChanged;
                int index = SubtitleListview1.SelectedItems[0].Index;
                double videoPosition = mediaPlayer.CurrentPosition;
                var tc = new TimeCode(TimeSpan.FromSeconds(videoPosition));
                timeUpDownStartTime.TimeCode = tc;

                MakeHistoryForUndo(_language.BeforeSetStartTimeAndOffsetTheRest + "  " +_subtitle.Paragraphs[index].Number.ToString() + " - " + tc.ToString());

                double offset = _subtitle.Paragraphs[index].StartTime.TotalMilliseconds - tc.TotalMilliseconds;
                for (int i = index; i < SubtitleListview1.Items.Count; i++)
                {
                    _subtitle.Paragraphs[i].StartTime = new TimeCode(TimeSpan.FromMilliseconds(_subtitle.Paragraphs[i].StartTime.TotalMilliseconds - offset));
                    _subtitle.Paragraphs[i].EndTime = new TimeCode(TimeSpan.FromMilliseconds(_subtitle.Paragraphs[i].EndTime.TotalMilliseconds - offset));
                    SubtitleListview1.SetStartTime(i, _subtitle.Paragraphs[i]);
                }

                if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                {
                    Paragraph original = Utilities.GetOriginalParagraph(index, _subtitle.Paragraphs[index], _subtitleAlternate.Paragraphs);
                    if (original != null)
                    {
                        index = _subtitleAlternate.GetIndex(original);
                        for (int i = index; i < _subtitleAlternate.Paragraphs.Count; i++)
                        {
                            _subtitleAlternate.Paragraphs[i].StartTime = new TimeCode(TimeSpan.FromMilliseconds(_subtitleAlternate.Paragraphs[i].StartTime.TotalMilliseconds - offset));
                            _subtitleAlternate.Paragraphs[i].EndTime = new TimeCode(TimeSpan.FromMilliseconds(_subtitleAlternate.Paragraphs[i].EndTime.TotalMilliseconds - offset));
                        }
                    }
                }
                checkBoxSyncListViewWithVideoWhilePlaying.Checked = oldSync;
                timeUpDownStartTime.MaskedTextBox.TextChanged += MaskedTextBox_TextChanged;                
            }
        }

        private void buttonSetEndAndGoToNext_Click(object sender, EventArgs e)
        {
            if (SubtitleListview1.SelectedItems.Count == 1)
            {
                int index = SubtitleListview1.SelectedItems[0].Index;
                double videoPosition = mediaPlayer.CurrentPosition;

                _subtitle.Paragraphs[index].EndTime = new TimeCode(TimeSpan.FromSeconds(videoPosition));
                SubtitleListview1.SetDuration(index, _subtitle.Paragraphs[index]);
                numericUpDownDuration.Value = (decimal)(_subtitle.Paragraphs[index].Duration.TotalSeconds);

                if (index + 1 < _subtitle.Paragraphs.Count)
                {
                    SubtitleListview1.Items[index].Selected = false;
                    SubtitleListview1.Items[index + 1].Selected = true;
                    _subtitle.Paragraphs[index + 1].StartTime = new TimeCode(TimeSpan.FromSeconds(videoPosition+0.001));
                    SubtitleListview1.SetStartTime(index + 1, _subtitle.Paragraphs[index + 1]);
                    SubtitleListview1.AutoScrollOffset.Offset(0, index * 16);
                    SubtitleListview1.EnsureVisible(Math.Min(SubtitleListview1.Items.Count - 1, index + 5));
                }
            }
        }

        private void buttonAdjustSecBack_Click(object sender, EventArgs e)
        {
            GoBackSeconds((double)numericUpDownSecAdjust1.Value);
        }

        private void buttonAdjustSecForward_Click(object sender, EventArgs e)
        {
            GoBackSeconds(-(double)numericUpDownSecAdjust1.Value);
        }

        private void Main_Shown(object sender, EventArgs e)
        {
            toolStripButtonToggleVideo.Checked = !Configuration.Settings.General.ShowVideoPlayer;
            toolStripButtonToggleVideo_Click(null, null);

            _timerAutoSave.Tick += TimerAutoSaveTick;
            if (Configuration.Settings.General.AutoBackupSeconds > 0)
            {
                _timerAutoSave.Interval = 1000 * Configuration.Settings.General.AutoBackupSeconds; // take backup every x second if changes were made
                _timerAutoSave.Start();
            }
            toolStripMenuItemPlayRateNormal_Click(null, null);
            Main_Resize(null, null);

            SetPositionFromXYString(Configuration.Settings.General.UndockedVideoPosition, "VideoPlayerUnDocked");
            SetPositionFromXYString(Configuration.Settings.General.UndockedWaveformPosition, "WaveFormUnDocked");
            SetPositionFromXYString(Configuration.Settings.General.UndockedVideoControlsPosition, "VideoControlsUndocked");
            if (Configuration.Settings.General.Undocked && Configuration.Settings.General.StartRememberPositionAndSize)
            {
                Configuration.Settings.General.Undocked = false;
                undockVideoControlsToolStripMenuItem_Click(null, null);
            }

            toolStripButtonLockCenter.Checked = Configuration.Settings.General.WaveFormCenter;
            audioVisualizer.Locked = toolStripButtonLockCenter.Checked;

            numericUpDownSec1.Value = (decimal) (Configuration.Settings.General.SmallDelayMilliseconds / 1000.0);
            numericUpDownSec2.Value = (decimal) (Configuration.Settings.General.LargeDelayMilliseconds / 1000.0);

            numericUpDownSecAdjust1.Value = (decimal)(Configuration.Settings.General.SmallDelayMilliseconds / 1000.0);
            numericUpDownSecAdjust2.Value = (decimal)(Configuration.Settings.General.LargeDelayMilliseconds / 1000.0);

            SetShortcuts();
            LoadPlugins();

            if (Configuration.Settings.General.StartInSourceView)
            {
                textBoxSource.Focus();
            }
            else
            {
                SubtitleListview1.Focus();
                int index = FirstSelectedIndex;
                if (index > 0 && SubtitleListview1.Items.Count > index)
                {
                    SubtitleListview1.Focus();
                    SubtitleListview1.Items[index].Focused = true;
                }
            }
        }

        private void SetPositionFromXYString(string positionAndSize, string name)
        {
            string[] parts = positionAndSize.Split(';');
            if (parts.Length == 4)
            {
                try
                {
                    int x = int.Parse(parts[0]);
                    int y = int.Parse(parts[1]);
                    int w = int.Parse(parts[2]);
                    int h = int.Parse(parts[3]);
                    _formPositionsAndSizes.AddPositionAndSize(new PositionAndSize() { Left = x, Top = y, Size = new Size(w, h), Name = name });
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine(exception.Message);
                }
            }
        }

        private void SetShortcuts()
        {
            newToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainFileNew);
            openToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainFileOpen);
            saveToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainFileSave);
            saveAsToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainFileSaveAs);
            findToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainEditFind);
            findNextToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainEditFindNext);
            replaceToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainEditReplace);
            gotoLineNumberToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainEditGoToLineNumber);

            fixToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainToolsFixCommonErrors);

            showhideVideoToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainVideoShowHideVideo);
            _toggleVideoDockUndock = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainVideoToggleVideoControls);

            toolStripMenuItemAdjustAllTimes.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainSynchronizationAdjustTimes);
            italicToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainListViewItalic);
            italicToolStripMenuItem1.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainTextBoxItalic);
            _mainAdjustInsertViaEndAutoStartAndGoToNext = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainAdjustViaEndAutoStartAndGoToNext);
        }

        private void LoadPlugins()
        {
            string path = Path.Combine(Configuration.BaseDirectory, "Plugins");
            if (!Directory.Exists(path))
                return;
            string[] pluginFiles = Directory.GetFiles(path, "*.DLL");

            int filePluginCount = 0;
            int toolsPluginCount = 0;
            int syncPluginCount = 0;
            foreach (string pluginFileName in pluginFiles)
            {
                Type pluginType = null;
                System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFile(pluginFileName);
                string objectName = Path.GetFileNameWithoutExtension(pluginFileName);
                if (assembly != null)
                {
                    try
                    {
                        pluginType = assembly.GetType("SubtitleEdit." + objectName);
                        object pluginObject = Activator.CreateInstance(pluginType);
                        System.Reflection.PropertyInfo pi = pluginType.GetProperty("Name");
                        string name = (string)pi.GetValue(pluginObject, null);
                        pi = pluginType.GetProperty("Version");
                        string version = (string)pi.GetValue(pluginObject, null);
                        pi = pluginType.GetProperty("ActionType");
                        string actionType = (string)pi.GetValue(pluginObject, null);
                        System.Reflection.MethodInfo mi = pluginType.GetMethod("DoAction");

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(actionType) && mi != null)
                        {
                            ToolStripMenuItem item = new ToolStripMenuItem();
                            item.Name = "Plugin" + toolsPluginCount.ToString();
                            item.Text = name;
                            item.Tag = pluginFileName;

                            pi = pluginType.GetProperty("ShortCut");
                            if (pi != null)
                                item.ShortcutKeys = Utilities.GetKeys((string)pi.GetValue(pluginObject, null));

                            if (string.Compare(actionType, "File", true) == 0)
                            {
                                if (filePluginCount == 0)
                                    fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.Count - 2, new ToolStripSeparator());
                                item.Click += PluginToolClick;
                                fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.Count - 2, item);
                                filePluginCount++;
                            }
                            else if (string.Compare(actionType, "Tool", true) == 0)
                            {
                                if (toolsPluginCount == 0)
                                    toolsToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
                                item.Click += PluginToolClick;
                                toolsToolStripMenuItem.DropDownItems.Add(item);
                                toolsPluginCount++;
                            }
                            else if (string.Compare(actionType, "Sync", true) == 0)
                            {
                                if (syncPluginCount == 0)
                                    toolStripMenuItemSyncronization.DropDownItems.Add(new ToolStripSeparator());
                                item.Click += PluginToolClick;
                                toolStripMenuItemSyncronization.DropDownItems.Add(item);
                                syncPluginCount++;
                            }
                        }

                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show("Error loading plugin:" + pluginFileName + ": " + exception.Message);
                    }
                    finally
                    {
                        assembly = null;
                    }
                }
            }
        }

        void PluginToolClick(object sender, EventArgs e)
        {
            try
            {
                ToolStripItem item = (ToolStripItem) sender;
                Type pluginType = null;
                System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFile(item.Tag.ToString());
                if (assembly != null)
                {
                    string objectName = Path.GetFileNameWithoutExtension(item.Tag.ToString());
                    pluginType = assembly.GetType("SubtitleEdit." + objectName);
                    object pluginObject = Activator.CreateInstance(pluginType);
                    System.Reflection.MethodInfo mi = pluginType.GetMethod("DoAction");

                    System.Reflection.PropertyInfo pi = pluginType.GetProperty("Name");
                    string name = (string)pi.GetValue(pluginObject, null);
                    pi = pluginType.GetProperty("Version");
                    string version = (string)pi.GetValue(pluginObject, null);


                    Subtitle temp = new Subtitle(_subtitle);
                    string text = temp.ToText(new SubRip());
                    string pluginResult = (string)mi.Invoke(pluginObject, new object[] { this, text, 25.0, _fileName, "", "" });

                    if (!string.IsNullOrEmpty(pluginResult) && pluginResult.Length > 10 && text != pluginResult)
                    {
                        _subtitle.MakeHistoryForUndo(string.Format("Before running plugin: {0} {1}", name, version), GetCurrentSubtitleFormat(), _fileDateTime);
                        string[] lineArray = pluginResult.Split(Environment.NewLine.ToCharArray());
                        List<string> lines = new List<string>();
                        foreach (string line in lineArray)
                            lines.Add(line);
                        new SubRip().LoadSubtitle(_subtitle, lines, _fileName);
                        SaveSubtitleListviewIndexes();
                        SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                        RestoreSubtitleListviewIndexes();
                        ShowSource();
                        _change = true;
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }      

        void TimerAutoSaveTick(object sender, EventArgs e)
        {
            string currentText = _subtitle.ToText(GetCurrentSubtitleFormat());
            if (_textAutoSave != null && _subtitle.Paragraphs.Count > 0)
            { 
                if (currentText != _textAutoSave && currentText.Trim().Length > 0)
                {
                    if (!Directory.Exists(Configuration.AutoBackupFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(Configuration.AutoBackupFolder);
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show("Unable to create backup directory " + Configuration.AutoBackupFolder + ": " + exception.Message);
                        }
                    }
                    string title = string.Empty;
                    if (!string.IsNullOrEmpty(_fileName))
                        title = "_" + Path.GetFileNameWithoutExtension(_fileName);
                    string fileName = string.Format("{0}{1:0000}-{2:00}-{3:00}_{4:00}-{5:00}-{6:00}{7}{8}", Configuration.AutoBackupFolder, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, title, GetCurrentSubtitleFormat().Extension);
                    File.WriteAllText(fileName, currentText);
                }
            }
            _textAutoSave = currentText;                
        }

        private void mediaPlayer_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                string fileName = files[0];
                string ext = Path.GetExtension(fileName).ToLower();               
                if (Utilities.GetVideoFileFilter().Contains(ext))
                {
                    OpenVideo(fileName);
                }
                else
                {
                    MessageBox.Show(string.Format(_language.DropFileXNotAccepted, fileName));
                }
            }
            else
            {
                MessageBox.Show(_language.DropOnlyOneFile);
            }
        }

        private void mediaPlayer_DragEnter(object sender, DragEventArgs e)
        {
            // make sure they're actually dropping files (not text or anything else)
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void buttonSecBack2_Click(object sender, EventArgs e)
        {
            GoBackSeconds((double)numericUpDownSec2.Value);
        }

        private void buttonForward2_Click(object sender, EventArgs e)
        {
            GoBackSeconds(-(double)numericUpDownSec2.Value);
        }

        private void buttonAdjustSecBack2_Click(object sender, EventArgs e)
        {
            GoBackSeconds((double)numericUpDownSecAdjust2.Value);
        }

        private void buttonAdjustSecForward2_Click(object sender, EventArgs e)
        {
            GoBackSeconds(-(double)numericUpDownSecAdjust2.Value);
        }

        private void translatepoweredByMicrosoftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TranslateViaGoogle(false, false);
        }

        public static string Sha256Hash(string value)
        {
            System.Security.Cryptography.SHA256Managed hasher = new System.Security.Cryptography.SHA256Managed();
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hash = hasher.ComputeHash(bytes);
            return Convert.ToBase64String(hash, 0, hash.Length);
        }

        private string GetPeakWaveFileName(string videoFileName)
        {
            string dir = Configuration.WaveFormsFolder.TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FileInfo fi = new FileInfo(videoFileName);
            string wavePeakName = Sha256Hash(Path.GetFileName(videoFileName) + fi.Length.ToString() + fi.CreationTimeUtc.ToShortDateString()) + ".wav";
            wavePeakName = wavePeakName.Replace("=", string.Empty).Replace("/", string.Empty).Replace(",", string.Empty).Replace("?", string.Empty).Replace("*", string.Empty).Replace("+", string.Empty).Replace("\\", string.Empty);
            wavePeakName = Path.Combine(dir, wavePeakName);
            return wavePeakName;            
        }

        private string GetSpectrogramFolder(string videoFileName)
        {
            string dir = Configuration.SpectrogramsFolder.TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FileInfo fi = new FileInfo(videoFileName);
            string name = Sha256Hash(Path.GetFileName(videoFileName) + fi.Length.ToString() + fi.CreationTimeUtc.ToShortDateString());
            name = name.Replace("=", string.Empty).Replace("/", string.Empty).Replace(",", string.Empty).Replace("?", string.Empty).Replace("*", string.Empty).Replace("+", string.Empty).Replace("\\", string.Empty);
            name = Path.Combine(dir, name);
            return name;
        }

        private void AudioWaveForm_Click(object sender, EventArgs e)
        {
            if (audioVisualizer.WavePeaks == null)
            {                
                if (string.IsNullOrEmpty(_videoFileName))
                {
                    buttonOpenVideo_Click(sender, e);
                    if (string.IsNullOrEmpty(_videoFileName))
                        return;
                }
                
                AddWareForm addWaveForm = new AddWareForm();
                string peakWaveFileName = GetPeakWaveFileName(_videoFileName);
                string spectrogramFolder = GetSpectrogramFolder(_videoFileName);
                addWaveForm.Initialize(_videoFileName, spectrogramFolder);
                if (addWaveForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    addWaveForm.WavePeak.WritePeakSamples(peakWaveFileName);
                    var audioPeakWave = new WavePeakGenerator(peakWaveFileName);
                    audioPeakWave.GenerateAllSamples();
                    audioPeakWave.Close();
                    audioVisualizer.WavePeaks = audioPeakWave;
                    if (addWaveForm.SpectrogramBitmaps != null)
                        audioVisualizer.InitializeSpectrogram(addWaveForm.SpectrogramBitmaps, spectrogramFolder);
                    timerWaveForm.Start();
                }
            }
        }

        private void timerWaveForm_Tick(object sender, EventArgs e)
        {
            if (audioVisualizer.Visible && mediaPlayer.VideoPlayer != null && audioVisualizer.WavePeaks != null)
            {
                int index = -1;
                if (SubtitleListview1.SelectedItems.Count > 0)
                    index = SubtitleListview1.SelectedItems[0].Index;

                if (audioVisualizer.Locked)
                {
                    double startPos = mediaPlayer.CurrentPosition - ((audioVisualizer.EndPositionSeconds - audioVisualizer.StartPositionSeconds) / 2.0);
                    if (startPos < 0)
                        startPos = 0;
                    SetWaveFormPosition(startPos, mediaPlayer.CurrentPosition, index);
                }
                else if (mediaPlayer.CurrentPosition > audioVisualizer.EndPositionSeconds || mediaPlayer.CurrentPosition < audioVisualizer.StartPositionSeconds)
                {
                    double startPos = mediaPlayer.CurrentPosition - 0.01;
                    if (startPos < 0)
                        startPos = 0;
                    audioVisualizer.ClearSelection();
                    SetWaveFormPosition(startPos, mediaPlayer.CurrentPosition, index);
                }
                else
                {
                    SetWaveFormPosition(audioVisualizer.StartPositionSeconds, mediaPlayer.CurrentPosition, index);
                }

                bool paused = mediaPlayer.IsPaused;
                toolStripButtonWaveFormPause.Visible = !paused;
                toolStripButtonWaveFormPlay.Visible = paused;
            }
            else
            {
                toolStripButtonWaveFormPlay.Visible = true;
                toolStripButtonWaveFormPause.Visible = false;
            }
        }

        private void addParagraphHereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            audioVisualizer.ClearSelection();
            Paragraph newParagraph = new Paragraph(audioVisualizer.NewSelectionParagraph);
            if (newParagraph == null)
                return;

            mediaPlayer.Pause();

            int startNumber = 1;
            if (_subtitle.Paragraphs.Count > 0)
                startNumber = _subtitle.Paragraphs[0].Number;

            // find index where to insert
            int index = 0;
            foreach (Paragraph p in _subtitle.Paragraphs)
            {
                if (p.StartTime.TotalMilliseconds > newParagraph.StartTime.TotalMilliseconds)
                    break;
                index++;
            }

            // create and insert
            if (GetCurrentSubtitleFormat().IsFrameBased)
            {
                newParagraph.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                newParagraph.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
            }
            _subtitle.Paragraphs.Insert(index, newParagraph);
            _change = true;

            _subtitleListViewIndex = -1;
            _subtitle.Renumber(startNumber);
            SubtitleListview1.Fill(_subtitle.Paragraphs);
            SubtitleListview1.SelectIndexAndEnsureVisible(index);

            textBoxListViewText.Focus();
            audioVisualizer.NewSelectionParagraph = null;

            ShowStatus(string.Format(_language.VideoControls.NewTextInsertAtX, newParagraph.StartTime.ToShortString()));
            audioVisualizer.Invalidate();
        }

        private void mergeWithPreviousToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = _subtitle.GetIndex(audioVisualizer.RightClickedParagraph);
            if (index >= 0)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
                MergeBeforeToolStripMenuItemClick(null, null); 
            }
            audioVisualizer.Invalidate();
        }

        private void deleteParagraphToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = _subtitle.GetIndex(audioVisualizer.RightClickedParagraph);
            if (index >= 0)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
                ToolStripMenuItemDeleteClick(null, null);
            }
            audioVisualizer.Invalidate();
        }

        private void splitToolStripMenuItem1_Click(object sender, EventArgs e)
        {            
            int index = _subtitle.GetIndex(audioVisualizer.RightClickedParagraph);
            if (index >= 0)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
                SplitSelectedParagraph(_audioWaveFormRightClickSeconds, null);
            }
            audioVisualizer.Invalidate();
        }

        private void mergeWithNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = _subtitle.GetIndex(audioVisualizer.RightClickedParagraph);
            if (index >= 0)
            {
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
                MergeAfterToolStripMenuItemClick(null, null);
            }
            audioVisualizer.Invalidate();
        }

        private void buttonWaveFormZoomIn_Click(object sender, EventArgs e)
        {
            if (audioVisualizer.WavePeaks != null && audioVisualizer.Visible)
            {
                audioVisualizer.ZoomFactor += 0.1;
            }
        }

        private void buttonWaveFormZoomOut_Click(object sender, EventArgs e)
        {
            if (audioVisualizer.WavePeaks != null && audioVisualizer.Visible)
            {
                audioVisualizer.ZoomFactor -= 0.1;
            }
        }

        private void buttonWaveFormZoomReset_Click(object sender, EventArgs e)
        {
            if (audioVisualizer.WavePeaks != null && audioVisualizer.Visible)
            {
                audioVisualizer.ZoomFactor = 1.0;
            }
        }

        private void toolStripMenuItemWaveFormPlaySelection_Click(object sender, EventArgs e)
        {
            if (mediaPlayer != null && mediaPlayer.VideoPlayer != null)
            {
                Paragraph p = audioVisualizer.NewSelectionParagraph;
                if (p == null)
                    p = audioVisualizer.RightClickedParagraph;

                if (p != null)
                {
                    mediaPlayer.CurrentPosition = p.StartTime.TotalSeconds;
                    Utilities.ShowSubtitle(_subtitle.Paragraphs, mediaPlayer);
                    mediaPlayer.Play();
                    _endSeconds = p.EndTime.TotalSeconds;
                }
            }
        }

        private void toolStripButtonWaveFormZoomIn_Click(object sender, EventArgs e)
        {
            if (audioVisualizer.WavePeaks != null && audioVisualizer.Visible)
            {
                audioVisualizer.ZoomFactor += 0.1;
                SelectZoomTextInComboBox();
            }
        }

        private void toolStripButtonWaveFormZoomOut_Click(object sender, EventArgs e)
        {
            if (audioVisualizer.WavePeaks != null && audioVisualizer.Visible)
            {
                audioVisualizer.ZoomFactor -= 0.1;
                SelectZoomTextInComboBox();
            }
        }

        private void toolStripComboBoxWaveForm_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBoxZoomItem item = toolStripComboBoxWaveForm.SelectedItem as ComboBoxZoomItem;
            if (item != null)
            {
                audioVisualizer.ZoomFactor = item.ZoomFactor;
            }
        }

        private void SelectZoomTextInComboBox()
        {
            int i = 0;
            foreach (object obj in toolStripComboBoxWaveForm.Items)
            {
                ComboBoxZoomItem item = obj as ComboBoxZoomItem;
                if (Math.Abs(audioVisualizer.ZoomFactor - item.ZoomFactor) < 0.001)
                {
                    toolStripComboBoxWaveForm.SelectedIndex = i;
                    return;
                }
                i++;
            }
        }

        private void toolStripButtonWaveFormPause_Click(object sender, EventArgs e)
        {
            mediaPlayer.Pause();
        }

        private void toolStripButtonWaveFormPlay_Click(object sender, EventArgs e)
        {
            mediaPlayer.Play();
        }

        private void toolStripButtonLockCenter_Click(object sender, EventArgs e)
        {
            toolStripButtonLockCenter.Checked = !toolStripButtonLockCenter.Checked;
            audioVisualizer.Locked = toolStripButtonLockCenter.Checked;
            Configuration.Settings.General.WaveFormCenter = audioVisualizer.Locked;
        }

        private void trackBarWaveFormPosition_ValueChanged(object sender, EventArgs e)
        {
            mediaPlayer.CurrentPosition = trackBarWaveFormPosition.Value;
        }

        private void buttonCustomUrl_Click(object sender, EventArgs e)
        {
            string url = Configuration.Settings.VideoControls.CustomSearchUrl;
            if (!string.IsNullOrEmpty(url))
            {
                if (url.Contains("{0}"))
                {
                    url = string.Format(url, Utilities.UrlEncode(textBoxSearchWord.Text));
                }
                System.Diagnostics.Process.Start(url);
            }
        }

        private void showhideWaveFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripButtonToggleWaveForm_Click(null, null);
        }

        private void AudioWaveForm_DragEnter(object sender, DragEventArgs e)
        {
            // make sure they're actually dropping files (not text or anything else)
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void AudioWaveForm_DragDrop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(_videoFileName))
                buttonOpenVideo_Click(null, null);
            if (_videoFileName == null)
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                string fileName = files[0];
                string ext = Path.GetExtension(fileName).ToLower();
                if (ext != ".wav")
                {
                    MessageBox.Show(string.Format(".Wav only!", fileName));
                    return;
                }

                AddWareForm addWaveForm = new AddWareForm();
                addWaveForm.InitializeViaWaveFile(fileName);
                if (addWaveForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string peakWaveFileName = GetPeakWaveFileName(_videoFileName);
                    addWaveForm.WavePeak.WritePeakSamples(peakWaveFileName);
                    var audioPeakWave = new WavePeakGenerator(peakWaveFileName);
                    audioPeakWave.GenerateAllSamples();
                    audioVisualizer.WavePeaks = audioPeakWave;
                    timerWaveForm.Start();
                }               
            }
            else
            {
                MessageBox.Show(_language.DropOnlyOneFile);
            }
        }

        private void toolStripMenuItemImportBluRaySup_Click(object sender, EventArgs e)
        {
            if (ContinueNewOrExit())
            {
                openFileDialog1.Title = _language.OpenBluRaySupFile;
                openFileDialog1.FileName = string.Empty;
                openFileDialog1.Filter = _language.BluRaySupFiles + "|*.sup";
                if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
                {
                    ImportAndOcrBluRaySup(openFileDialog1.FileName);
                }
            }
        }

        private void ImportAndOcrBluRaySup(string fileName)
        {
            StringBuilder log = new StringBuilder();
            var subtitles = BluRaySupParser.ParseBluRaySup(fileName, log);
            if (subtitles.Count > 0)
            {
                var vobSubOcr = new VobSubOcr();
                vobSubOcr.Initialize(subtitles, Configuration.Settings.VobSubOcr);
                vobSubOcr.FileName = Path.GetFileName(fileName);
                if (vobSubOcr.ShowDialog(this) == DialogResult.OK)
                {
                    MakeHistoryForUndo(_language.BeforeImportingBluRaySupFile);
                    FileNew();
                    _subtitle.Paragraphs.Clear();
                    SetCurrentFormat(new SubRip().FriendlyName);
                    _subtitle.WasLoadedWithFrameNumbers = false;
                    _subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);
                    foreach (Paragraph p in vobSubOcr.SubtitleFromOcr.Paragraphs)
                    {
                        _subtitle.Paragraphs.Add(p);
                    }

                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    _change = true;
                    _subtitleListViewIndex = -1;
                    SubtitleListview1.FirstVisibleIndex = -1;
                    SubtitleListview1.SelectIndexAndEnsureVisible(0);

                    _fileName = Path.ChangeExtension(vobSubOcr.FileName, ".srt");
                    SetTitle();
                    _converted = true;

                    Configuration.Settings.Save();
                }
            }
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            if (textBoxListViewTextAlternate.Focused)
                textBoxListViewTextAlternate.SelectAll();
            else
                textBoxListViewText.SelectAll();
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (textBoxListViewTextAlternate.Focused)
                textBoxListViewTextAlternate.Cut();
            else
                textBoxListViewText.Cut();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (textBoxListViewTextAlternate.Focused)
                textBoxListViewTextAlternate.Copy();
            else               
                textBoxListViewText.Copy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (textBoxListViewTextAlternate.Focused)
                textBoxListViewTextAlternate.Paste();
            else
                textBoxListViewText.Paste();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (textBoxListViewTextAlternate.Focused)
                textBoxListViewTextAlternate.DeselectAll();
            else
                textBoxListViewText.DeselectAll();
        }

        private void normalToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TextBox tb;
            if (textBoxListViewTextAlternate.Focused)
                tb = textBoxListViewTextAlternate;
            else
                tb = textBoxListViewText;

            string text = tb.SelectedText;
            int selectionStart = tb.SelectionStart;
            text = Utilities.RemoveHtmlTags(text);
            tb.SelectedText = text;
            tb.SelectionStart = selectionStart;
            tb.SelectionLength = text.Length;
        }

        private void TextBoxListViewToggleTag(string tag)
        {
            TextBox tb;
            if (textBoxListViewTextAlternate.Focused)
                tb = textBoxListViewTextAlternate;
            else
                tb = textBoxListViewText;

            string text = tb.SelectedText;
            int selectionStart = tb.SelectionStart;

            if (text.Contains("<" + tag + ">"))
            {
                text = text.Replace("<" + tag + ">", string.Empty);
                text = text.Replace("</" + tag + ">", string.Empty);
            }
            else
            {
                text = string.Format("<{0}>{1}</{0}>", tag, text);
            }

            tb.SelectedText = text;
            tb.SelectionStart = selectionStart;
            tb.SelectionLength = text.Length;
        }

        private void boldToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TextBoxListViewToggleTag("b");
        }

        private void italicToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TextBoxListViewToggleTag("i");
        }

        private void underlineToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TextBoxListViewToggleTag("u");
        }

        private void colorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TextBox tb;
            if (textBoxListViewTextAlternate.Focused)
                tb = textBoxListViewTextAlternate;
            else
                tb = textBoxListViewText;

            //color
            string text = tb.SelectedText;
            int selectionStart = tb.SelectionStart;

            if (colorDialog1.ShowDialog(this) == DialogResult.OK)
            { 
                string color = Utilities.ColorToHex(colorDialog1.Color);
                bool done = false;
                string s = text;
                if (s.StartsWith("<font "))
                {
                    int start = s.IndexOf("<font ");
                    if (start >= 0)
                    {
                        int end = s.IndexOf(">", start);
                        if (end > 0)
                        {
                            string f = s.Substring(start, end - start);
                            if (f.Contains(" face=") && !f.Contains(" color="))
                            {
                                start = s.IndexOf(" face=", start);
                                s = s.Insert(start, string.Format(" color=\"{0}\"", color));
                                text = s;
                                done = true;
                            }
                            else if (f.Contains(" color="))
                            {
                                int colorStart = f.IndexOf(" color=");
                                if (s.IndexOf("\"", colorStart + " color=".Length + 1) > 0)
                                    end = s.IndexOf("\"", colorStart + " color=".Length + 1);
                                s = s.Substring(0, colorStart) + string.Format(" color=\"{0}", color) + s.Substring(end);
                                text = s;
                                done = true;
                            }
                        }
                    }
                }

                if (!done)
                    text = string.Format("<font color=\"{0}\">{1}</font>", color, text);

                tb.SelectedText = text;
                tb.SelectionStart = selectionStart;
                tb.SelectionLength = text.Length;
            }
        }

        private void fontNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TextBox tb;
            if (textBoxListViewTextAlternate.Focused)
                tb = textBoxListViewTextAlternate;
            else
                tb = textBoxListViewText;

            // font name
            string text = tb.SelectedText;
            int selectionStart = tb.SelectionStart;

            if (fontDialog1.ShowDialog(this) == DialogResult.OK)
            {
                bool done = false;

                string s = text;
                if (s.StartsWith("<font "))
                {
                    int start = s.IndexOf("<font ");
                    if (start >= 0)
                    {
                        int end = s.IndexOf(">", start);
                        if (end > 0)
                        {
                            string f = s.Substring(start, end - start);
                            if (f.Contains(" color=") && !f.Contains(" face="))
                            {
                                start = s.IndexOf(" color=", start);
                                s = s.Insert(start, string.Format(" face=\"{0}\"", fontDialog1.Font.Name));
                                text = s;
                                done = true;
                            }
                            else if (f.Contains(" face="))
                            {
                                int faceStart = f.IndexOf(" face=");
                                if (s.IndexOf("\"", faceStart + " face=".Length + 1) > 0)
                                    end = s.IndexOf("\"", faceStart + " face=".Length + 1);
                                s = s.Substring(0, faceStart) + string.Format(" face=\"{0}", fontDialog1.Font.Name) + s.Substring(end);
                                text = s;
                                done = true;
                            }
                        }
                    }
                }
                if (!done)
                    text = string.Format("<font face=\"{0}\">{1}</font>", fontDialog1.Font.Name, s);

                tb.SelectedText = text;
                tb.SelectionStart = selectionStart;
                tb.SelectionLength = text.Length;
            }
        }

        public void SetSubtitle(Subtitle subtitle, string message)
        {
            _subtitle = subtitle;
            SubtitleListview1.Fill(subtitle, _subtitleAlternate);
            _change = true;
            ShowStatus(message);
        }

        #region Networking
        private void startServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _networkSession = new NikseWebServiceSession(_subtitle, _subtitleAlternate, TimerWebServiceTick, OnUpdateUserLogEntries);
            NetworkStart networkNew = new NetworkStart();
            networkNew.Initialize(_networkSession, _fileName);
            if (networkNew.ShowDialog(this) == DialogResult.OK)
            {
                if (GetCurrentSubtitleFormat().GetType() == typeof(AdvancedSubStationAlpha) || GetCurrentSubtitleFormat().GetType() == typeof(SubStationAlpha))
                {
                    SubtitleListview1.HideExtraColumn();
                }

                _networkSession.AppendToLog(string.Format(_language.XStartedSessionYAtZ, _networkSession.CurrentUser.UserName, _networkSession.SessionId, DateTime.Now.ToLongTimeString()));
                toolStripStatusNetworking.Visible = true;
                toolStripStatusNetworking.Text = _language.NetworkMode;
                EnableDisableControlsNotWorkingInNetworkMode(false);
                SubtitleListview1.ShowExtraColumn(_language.UserAndAction);
                SubtitleListview1.AutoSizeAllColumns(this);
                TimerWebServiceTick(null, null);
            }
            else
            {
                _networkSession = null;
            }
        }

        private void joinSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _networkSession = new NikseWebServiceSession(_subtitle, _subtitleAlternate, TimerWebServiceTick, OnUpdateUserLogEntries);
            NetworkJoin networkJoin = new NetworkJoin();
            networkJoin.Initialize(_networkSession);

            if (networkJoin.ShowDialog(this) == DialogResult.OK)
            {
                _subtitle = _networkSession.Subtitle;
                _subtitleAlternate = _networkSession.OriginalSubtitle;
                if (_subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                    SubtitleListview1.ShowAlternateTextColumn(Configuration.Settings.Language.General.OriginalText);
                _fileName = networkJoin.FileName;
                SetTitle();
                Text = Title;
                toolStripStatusNetworking.Visible = true;
                toolStripStatusNetworking.Text = _language.NetworkMode;
                EnableDisableControlsNotWorkingInNetworkMode(false);
                _networkSession.AppendToLog(string.Format(_language.XStartedSessionYAtZ, _networkSession.CurrentUser.UserName, _networkSession.SessionId, DateTime.Now.ToLongTimeString()));
                SubtitleListview1.ShowExtraColumn(_language.UserAndAction);
                SubtitleListview1.AutoSizeAllColumns(this);
                _subtitleListViewIndex = -1;
                _oldSelectedParagraph = null;
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                SubtitleListview1.SelectIndexAndEnsureVisible(0);
                _change = true;
                TimerWebServiceTick(null, null);                
            }
            else
            {
                _networkSession = null;
            }
        }

        private void EnableDisableControlsNotWorkingInNetworkMode(bool enabled)
        {
            //Top menu
            newToolStripMenuItem.Enabled = enabled;
            openToolStripMenuItem.Enabled = enabled;
            reopenToolStripMenuItem.Enabled = enabled;
            toolStripMenuItemOpenContainingFolder.Enabled = enabled;
            toolStripMenuItemCompare.Enabled = enabled;
            toolStripMenuItemImportDvdSubtitles.Enabled = enabled;
            toolStripMenuItemSubIdx.Enabled = enabled;
            toolStripMenuItemImportBluRaySup.Enabled = enabled;
            matroskaImportStripMenuItem.Enabled = enabled;
            toolStripMenuItemManualAnsi.Enabled = enabled;
            toolStripMenuItemImportText.Enabled = enabled;
            toolStripMenuItemImportTimeCodes.Enabled = enabled;

            showHistoryforUndoToolStripMenuItem.Enabled = enabled;
            multipleReplaceToolStripMenuItem.Enabled = enabled;

            toolsToolStripMenuItem.Enabled = enabled;

            toolStripMenuItemSyncronization.Enabled = enabled;

            toolStripMenuItemAutoTranslate.Enabled = enabled;

            //Toolbar
            toolStripButtonFileNew.Enabled = enabled;
            toolStripButtonFileOpen.Enabled = enabled;
            toolStripButtonVisualSync.Enabled = enabled;       
     
            // textbox source
            textBoxSource.ReadOnly = !enabled;            
        }

        internal void TimerWebServiceTick(object sender, EventArgs e)
        {
            if (_networkSession == null)
                return;

            List<int> deleteIndices = new List<int>();
            NetworkGetSendUpdates(deleteIndices, 0, null);
        }

        private void NetworkGetSendUpdates(List<int> deleteIndices, int insertIndex, Paragraph insertParagraph)
        {
            _networkSession.TimerStop();

            bool doReFill = false;
            bool updateListViewStatus = false;
            SubtitleListview1.SelectedIndexChanged -= SubtitleListview1_SelectedIndexChanged;
            string message = string.Empty;

            int numberOfLines = 0;
            List<SeNetworkService.SeUpdate> updates = null;
            try
            {
                updates = _networkSession.GetUpdates(out message, out numberOfLines);
            }
            catch (Exception exception)
            {
                MessageBox.Show(string.Format(_language.NetworkUnableToConnectToServer, exception.Message));
                _networkSession.TimerStop();
                if (_networkChat != null && !_networkChat.IsDisposed)
                {
                    _networkChat.Close();
                    _networkChat = null;
                }
                _networkSession = null;
                EnableDisableControlsNotWorkingInNetworkMode(true);
                toolStripStatusNetworking.Visible = false;
                SubtitleListview1.HideExtraColumn();
                _networkChat = null;
                return;
            }
            int currentSelectedIndex = -1;
            if (SubtitleListview1.SelectedItems.Count > 0)
                currentSelectedIndex = SubtitleListview1.SelectedItems[0].Index;
            int oldCurrentSelectedIndex = currentSelectedIndex; 
            if (message == "OK")
            {
                foreach (var update in updates)
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        if (!update.Text.Contains(Environment.NewLine))
                            update.Text = update.Text.Replace("\n", Environment.NewLine);
//                        update.Text = HttpUtility.HtmlDecode(update.Text).Replace("<br />", Environment.NewLine);
                        update.Text = Utilities.HtmlDecode(update.Text).Replace("<br />", Environment.NewLine);
                    }
                    if (update.User.Ip != _networkSession.CurrentUser.Ip || update.User.UserName != _networkSession.CurrentUser.UserName)
                    {
                        if (update.Action == "USR")
                        {
                            _networkSession.Users.Add(update.User);
                            if (_networkChat != null && !_networkChat.IsDisposed)
                            {
                                _networkChat.AddUser(update.User);
                            }
                            _networkSession.AppendToLog(string.Format(_language.NetworkNewUser, update.User.UserName, update.User.Ip ));
                        }
                        else if (update.Action == "MSG")
                        {
                            _networkSession.ChatLog.Add(new NikseWebServiceSession.ChatEntry() { User = update.User, Message = update.Text });
                            if (_networkChat == null || _networkChat.IsDisposed)
                            {
                                _networkChat = new NetworkChat();
                                _networkChat.Initialize(_networkSession);
                                _networkChat.Show(this);
                            }
                            else
                            {
                                _networkChat.AddChatMessage(update.User, update.Text);
                            }
                            _networkSession.AppendToLog(string.Format(_language.NetworkMessage, update.User.UserName, update.User.Ip, update.Text));
                        }
                        else if (update.Action == "DEL")
                        {
                            doReFill = true;
                            _subtitle.Paragraphs.RemoveAt(update.Index);
                            if (_networkSession.LastSubtitle != null)
                                _networkSession.LastSubtitle.Paragraphs.RemoveAt(update.Index);
                            _networkSession.AppendToLog(string.Format(_language.NetworkDelete, update.User.UserName , update.User.Ip, update.Index.ToString()));
                            _networkSession.AdjustUpdateLogToDelete(update.Index);
                            _change = true;

                            if (deleteIndices.Count > 0)
                            {
                                for (int i = deleteIndices.Count - 1; i >= 0; i--)
                                {
                                    int index = deleteIndices[i];
                                    if (index == update.Index)
                                        deleteIndices.RemoveAt(i);
                                    else if (index > update.Index)
                                        deleteIndices[i] = index - 1;
                                }
                            }

                            if (insertIndex > update.Index)
                                insertIndex--;
                            if (currentSelectedIndex >= 0 && currentSelectedIndex > update.Index)
                                currentSelectedIndex--;
                        }
                        else if (update.Action == "INS")
                        {
                            doReFill = true;
                            Paragraph p = new Paragraph(update.Text, update.StartMilliseconds, update.EndMilliseconds);
                            _subtitle.Paragraphs.Insert(update.Index, p);
                            if (_networkSession.LastSubtitle != null)
                                _networkSession.LastSubtitle.Paragraphs.Insert(update.Index, new Paragraph(p));
                            _networkSession.AppendToLog(string.Format(_language.NetworkInsert, update.User.UserName, update.User.Ip, update.Index.ToString(), update.Text.Replace(Environment.NewLine, Configuration.Settings.General.ListViewLineSeparatorString)));
                            _networkSession.AddToWsUserLog(update.User, update.Index, update.Action, false);
                            updateListViewStatus = true;
                            _networkSession.AdjustUpdateLogToInsert(update.Index);
                            _change = true;

                            if (deleteIndices.Count > 0)
                            {
                                for (int i = deleteIndices.Count - 1; i >= 0; i--)
                                {
                                    int index = deleteIndices[i];
                                    if (index > update.Index)
                                        deleteIndices[i] = index +1;
                                }
                            }
                            if (insertIndex > update.Index)
                                insertIndex++;
                            if (currentSelectedIndex >= 0 && currentSelectedIndex > update.Index)
                                currentSelectedIndex++;
                        }
                        else if (update.Action == "UPD")
                        {
                            updateListViewStatus = true;
                            Paragraph p = _subtitle.GetParagraphOrDefault(update.Index);
                            if (p != null)
                            {
                                p.StartTime.TotalMilliseconds = update.StartMilliseconds;
                                p.EndTime.TotalMilliseconds = update.EndMilliseconds;
                                p.Text = update.Text;
                                SubtitleListview1.SetTimeAndText(update.Index, p);
                                _networkSession.AppendToLog(string.Format(_language.NetworkUpdate, update.User.UserName, update.User.Ip, update.Index.ToString(), update.Text.Replace(Environment.NewLine, Configuration.Settings.General.ListViewLineSeparatorString)));
                                _networkSession.AddToWsUserLog(update.User, update.Index, update.Action, true);
                                updateListViewStatus = true;
                            }
                            if (_networkSession.LastSubtitle != null)
                            {
                                p = _networkSession.LastSubtitle.GetParagraphOrDefault(update.Index);
                                if (p != null)
                                {
                                    p.StartTime.TotalMilliseconds = update.StartMilliseconds;
                                    p.EndTime.TotalMilliseconds = update.EndMilliseconds;
                                    p.Text = update.Text;
                                }
                            }
                            _change = true;
                        }
                        else if (update.Action == "BYE")
                        {
                            if (_networkChat != null && !_networkChat.IsDisposed)
                                _networkChat.RemoveUser(update.User);

                            SeNetworkService.SeUser removeUser = null;
                            foreach (var user in _networkSession.Users)
                            {
                                if (user.UserName == update.User.UserName)
                                {
                                    removeUser = user;
                                    break;
                                }
                            }
                            if (removeUser != null)
                                _networkSession.Users.Remove(removeUser);

                            _networkSession.AppendToLog(string.Format(_language.NetworkByeUser, update.User.UserName, update.User.Ip));
                        }
                        else
                        {
                            _networkSession.AppendToLog("UNKNOWN ACTION: " + update.Action + " by " + update.User.UserName + " (" + update.User.Ip + ")");
                        }
                    }
                }
                if (numberOfLines != _subtitle.Paragraphs.Count)
                {
                    _subtitle = _networkSession.ReloadSubtitle();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    UpdateListviewWithUserLogEntries();
                    _networkSession.LastSubtitle = new Subtitle(_subtitle);
                    _oldSelectedParagraph = null;
                    SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                    _networkSession.TimerStart();
                    RefreshSelectedParagraph();
                    return;
                }
                if (deleteIndices.Count > 0)
                {
                    deleteIndices.Sort();
                    deleteIndices.Reverse();
                    foreach (int i in deleteIndices)
                    {
                        _subtitle.Paragraphs.RemoveAt(i);
                        if (_networkSession != null && _networkSession.LastSubtitle != null && i < _networkSession.LastSubtitle.Paragraphs.Count)
                            _networkSession.LastSubtitle.Paragraphs.RemoveAt(i);
                    }

                    _networkSession.DeleteLines(deleteIndices);
                    doReFill = true;
                }
                if (insertIndex >= 0 && insertParagraph != null)
                {
                    _subtitle.Paragraphs.Insert(insertIndex, insertParagraph);
                    if (_networkSession != null && _networkSession.LastSubtitle != null && insertIndex < _networkSession.LastSubtitle.Paragraphs.Count)
                        _networkSession.LastSubtitle.Paragraphs.Insert(insertIndex, insertParagraph);
                    _networkSession.InsertLine(insertIndex, insertParagraph);
                    doReFill = true;
                }
                _networkSession.CheckForAndSubmitUpdates(updates); // updates only (no inserts/deletes)
            }
            else
            {
                MessageBox.Show(message);
                LeaveSessionToolStripMenuItemClick(null, null);
                SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
                return;
            }
            if (doReFill)
            {
                _subtitle.Renumber(1);
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                UpdateListviewWithUserLogEntries();

                if (oldCurrentSelectedIndex != currentSelectedIndex)
                {
                    _oldSelectedParagraph = null;
                    _subtitleListViewIndex = currentSelectedIndex;
                    SubtitleListview1.SelectIndexAndEnsureVisible(_subtitleListViewIndex);
                }
                else if (_oldSelectedParagraph != null)
                {
                    Paragraph p = _subtitle.GetFirstAlike(_oldSelectedParagraph);
                    if (p != null)
                    {
                        _subtitleListViewIndex = _subtitle.GetIndex(p);
                        SubtitleListview1.SelectIndexAndEnsureVisible(_subtitleListViewIndex);
                    }
                }
            }
            else if (updateListViewStatus)
            {
                UpdateListviewWithUserLogEntries();
            }
            _networkSession.LastSubtitle = new Subtitle(_subtitle);
            SubtitleListview1.SelectedIndexChanged += SubtitleListview1_SelectedIndexChanged;
            _networkSession.TimerStart();
        }

        private void UpdateListviewWithUserLogEntries()
        {
            SubtitleListview1.BeginUpdate();
            foreach (UpdateLogEntry entry in _networkSession.UpdateLog)
                SubtitleListview1.SetExtraText(entry.Index, entry.ToString(), Utilities.GetColorFromUserName(entry.UserName));
            SubtitleListview1.EndUpdate();
        }

        private void LeaveSessionToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_networkSession != null)
            {
                _networkSession.TimerStop();
                _networkSession.Leave();
            }
            if (_networkChat != null && !_networkChat.IsDisposed)
            {
                _networkChat.Close();
                _networkChat = null;
            }
            _networkSession = null;
            EnableDisableControlsNotWorkingInNetworkMode(true);
            toolStripStatusNetworking.Visible = false;
            SubtitleListview1.HideExtraColumn();
            _networkChat = null;

            if ((GetCurrentSubtitleFormat().GetType() == typeof(AdvancedSubStationAlpha) || GetCurrentSubtitleFormat().GetType() == typeof(SubStationAlpha)) && _networkSession == null)
            {
                SubtitleListview1.ShowExtraColumn("Style");
                SubtitleListview1.DisplayExtraFromExtra = true;
            }
        }

        private void toolStripMenuItemNetworking_DropDownOpening(object sender, EventArgs e)
        {
            startServerToolStripMenuItem.Visible = _networkSession == null;
            joinSessionToolStripMenuItem.Visible = _networkSession == null;
            showSessionKeyLogToolStripMenuItem.Visible = _networkSession != null;
            leaveSessionToolStripMenuItem.Visible = _networkSession != null;
            chatToolStripMenuItem.Visible = _networkSession != null;
        }

        internal void OnUpdateUserLogEntries(object sender, EventArgs e)
        {
            UpdateListviewWithUserLogEntries();
        }

        private void toolStripStatusNetworking_Click(object sender, EventArgs e)
        {
            showSessionKeyLogToolStripMenuItem_Click(null, null);
        }

        private void showSessionKeyLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NetworkLogAndInfo networkLog = new NetworkLogAndInfo();
            networkLog.Initialize(_networkSession);
            networkLog.ShowDialog(this);
        }

        private void chatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_networkSession != null)
            {
                if (_networkChat == null || _networkChat.IsDisposed)
                {
                    _networkChat = new NetworkChat();
                    _networkChat.Initialize(_networkSession);
                    _networkChat.Show(this);
                }
                else
                {
                    _networkChat.WindowState = FormWindowState.Normal;
                }
            }
        }
        #endregion

        private void UnDockVideoPlayer()
        {
            bool firstUndock = _videoPlayerUnDocked != null && !_videoPlayerUnDocked.IsDisposed;

            _videoPlayerUnDocked = new VideoPlayerUnDocked(this, _formPositionsAndSizes, mediaPlayer);
            _formPositionsAndSizes.SetPositionAndSize(_videoPlayerUnDocked);

            if (firstUndock)
            {
                Configuration.Settings.General.UndockedVideoPosition = _videoPlayerUnDocked.Left.ToString() + ";" + _videoPlayerUnDocked.Top.ToString() + ";" + _videoPlayerUnDocked.Width + ";" + _videoPlayerUnDocked.Height;
                //if (_waveFormUnDocked != null && !_waveFormUnDocked.IsDisposed)
                //    Configuration.Settings.General.UndockedWaveformPosition = _waveFormUnDocked.Left.ToString() + ";" + _waveFormUnDocked.Top.ToString() + ";" + _waveFormUnDocked.Width + ";" + _waveFormUnDocked.Height;
                //if (_videoControlsUnDocked != null && !_videoControlsUnDocked.IsDisposed)
                //    Configuration.Settings.General.UndockedVideoControlsPosition = _videoControlsUnDocked.Left.ToString() + ";" + _videoControlsUnDocked.Top.ToString() + ";" + _videoControlsUnDocked.Width + ";" + _videoControlsUnDocked.Height;
            }

            Control control = null;
            if (splitContainer1.Panel2.Controls.Count == 0)
            {
                control = panelVideoPlayer;
                groupBoxVideo.Controls.Remove(control);
            }
            else if (splitContainer1.Panel2.Controls.Count > 0)
            {
                control = panelVideoPlayer;
                splitContainer1.Panel2.Controls.Clear();
            }
            if (control != null)
            {
                control.Top = 0;
                control.Left = 0;
                control.Width = _videoPlayerUnDocked.PanelContainer.Width;
                control.Height = _videoPlayerUnDocked.PanelContainer.Height;
                _videoPlayerUnDocked.PanelContainer.Controls.Add(control);
            }
        }

        public void ReDockVideoPlayer(Control control)
        {
            groupBoxVideo.Controls.Add(control);
        }

        private void UnDockWaveForm()
        {
            _waveFormUnDocked = new WaveFormUnDocked(this, _formPositionsAndSizes);
            _formPositionsAndSizes.SetPositionAndSize(_waveFormUnDocked);
            
            var control = audioVisualizer;
            groupBoxVideo.Controls.Remove(control);
            control.Top = 0;
            control.Left = 0;
            control.Width = _waveFormUnDocked.PanelContainer.Width;
            control.Height = _waveFormUnDocked.PanelContainer.Height - panelWaveFormControls.Height;
            _waveFormUnDocked.PanelContainer.Controls.Add(control);

            var control2 = (Control)panelWaveFormControls;
            groupBoxVideo.Controls.Remove(control2);
            control2.Top = control.Height;
            control2.Left = 0;
            _waveFormUnDocked.PanelContainer.Controls.Add(control2);

            var control3 = (Control)trackBarWaveFormPosition;
            groupBoxVideo.Controls.Remove(control3);
            control3.Top = control.Height;
            control3.Left = control2.Width +2;
            control3.Width = _waveFormUnDocked.PanelContainer.Width - control3.Left;
            _waveFormUnDocked.PanelContainer.Controls.Add(control3);
        }

        public void ReDockWaveForm(Control waveForm, Control buttons, Control trackBar)
        {
            groupBoxVideo.Controls.Add(waveForm);
            waveForm.Top = 30;
            waveForm.Height = groupBoxVideo.Height - (waveForm.Top + buttons.Height + 10);

            groupBoxVideo.Controls.Add(buttons);
            buttons.Top = waveForm.Top + waveForm.Height + 5;

            groupBoxVideo.Controls.Add(trackBar);
            trackBar.Top = buttons.Top;
        }

        private void UnDockVideoButtons()
        {
            _videoControlsUnDocked = new VideoControlsUndocked(this, _formPositionsAndSizes);
            _formPositionsAndSizes.SetPositionAndSize(_videoControlsUnDocked);
            var control = tabControlButtons;
            groupBoxVideo.Controls.Remove(control);
            control.Top = 25;
            control.Left = 0;
            _videoControlsUnDocked.PanelContainer.Controls.Add(control);

            groupBoxVideo.Controls.Remove(checkBoxSyncListViewWithVideoWhilePlaying);
            _videoControlsUnDocked.PanelContainer.Controls.Add(checkBoxSyncListViewWithVideoWhilePlaying);
            checkBoxSyncListViewWithVideoWhilePlaying.Top = 5;
            checkBoxSyncListViewWithVideoWhilePlaying.Left = 5;

            splitContainerMain.Panel2Collapsed = true;
            splitContainer1.Panel2Collapsed = true;
        }

        public void ReDockVideoButtons(Control videoButtons, Control checkBoxSyncSubWithVideo)
        {
            groupBoxVideo.Controls.Add(videoButtons);
            videoButtons.Top = 12;
            videoButtons.Left = 5;

            groupBoxVideo.Controls.Add(checkBoxSyncSubWithVideo);
            checkBoxSyncSubWithVideo.Top = 11;
            checkBoxSyncSubWithVideo.Left = videoButtons.Left + videoButtons.Width + 5;
        }

        private void undockVideoControlsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Configuration.Settings.General.Undocked)
                return;

            Configuration.Settings.General.Undocked = true;

            UnDockVideoPlayer();
            if (toolStripButtonToggleVideo.Checked)
            {
                _videoPlayerUnDocked.Show(this);
                if (_videoPlayerUnDocked.Top < -999 || _videoPlayerUnDocked.Left < -999)
                {
                    _videoPlayerUnDocked.WindowState = FormWindowState.Minimized;
                    _videoPlayerUnDocked.Top = Top + 40;
                    _videoPlayerUnDocked.Left = Math.Abs(Left - 20);
                    _videoPlayerUnDocked.Width = 600;
                    _videoPlayerUnDocked.Height = 400;
                }
            }

            UnDockWaveForm();
            if (toolStripButtonToggleWaveForm.Checked)
            {
                _waveFormUnDocked.Show(this);
                if (_waveFormUnDocked.Top < -999 || _waveFormUnDocked.Left < -999)
                {
                    _waveFormUnDocked.WindowState = FormWindowState.Minimized;
                    _waveFormUnDocked.Top = Top + 60;
                    _waveFormUnDocked.Left = Math.Abs(Left - 15);
                    _waveFormUnDocked.Width = 600;
                    _waveFormUnDocked.Height= 200;
                }
            }

            UnDockVideoButtons();
            _videoControlsUnDocked.Show(this);
            if (_videoControlsUnDocked.Top < -999 || _videoControlsUnDocked.Left < -999)
            {
                _videoControlsUnDocked.WindowState = FormWindowState.Minimized;
                _videoControlsUnDocked.Top = Top + 40;
                _videoControlsUnDocked.Left = Math.Abs(Left - 10);
                _videoControlsUnDocked.Width = tabControlButtons.Width + 20;
                _videoControlsUnDocked.Height = tabControlButtons.Height + 65;
            }

            _isVideoControlsUnDocked = true;
            SetUndockedWindowsTitle();

            undockVideoControlsToolStripMenuItem.Visible = false;
            redockVideoControlsToolStripMenuItem.Visible = true;

            tabControl1_SelectedIndexChanged(null, null);
        }

        public void redockVideoControlsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Configuration.Settings.General.Undocked)
                return;

            SaveUndockedPositions();

            Configuration.Settings.General.Undocked = false;

            if (_videoControlsUnDocked != null && !_videoControlsUnDocked.IsDisposed)
            {
                var control = _videoControlsUnDocked.PanelContainer.Controls[0];
                var controlCheckBox = _videoControlsUnDocked.PanelContainer.Controls[1];
                _videoControlsUnDocked.PanelContainer.Controls.Clear();
                ReDockVideoButtons(control, controlCheckBox);
                _videoControlsUnDocked.Close();
                _videoControlsUnDocked = null;
            }

            if (_waveFormUnDocked != null && !_waveFormUnDocked.IsDisposed)
            {
                var controlWaveForm = _waveFormUnDocked.PanelContainer.Controls[0];
                var controlButtons = _waveFormUnDocked.PanelContainer.Controls[1];
                var controlTrackBar = _waveFormUnDocked.PanelContainer.Controls[2];
                _waveFormUnDocked.PanelContainer.Controls.Clear();
                ReDockWaveForm(controlWaveForm, controlButtons, controlTrackBar);
                _waveFormUnDocked.Close();
                _waveFormUnDocked = null;
            }

            if (_videoPlayerUnDocked != null && !_videoPlayerUnDocked.IsDisposed)
            {
                var control = _videoPlayerUnDocked.PanelContainer.Controls[0];
                _videoPlayerUnDocked.PanelContainer.Controls.Remove(control);
                ReDockVideoPlayer(control);
                _videoPlayerUnDocked.Close();
                _videoPlayerUnDocked = null;
            }

            _isVideoControlsUnDocked = false;
            _videoPlayerUnDocked = null;
            _waveFormUnDocked = null;
            _videoControlsUnDocked = null;
            ShowVideoPlayer();

            audioVisualizer.Visible = toolStripButtonToggleWaveForm.Checked;
            trackBarWaveFormPosition.Visible = toolStripButtonToggleWaveForm.Checked;
            panelWaveFormControls.Visible = toolStripButtonToggleWaveForm.Checked;
            if (!toolStripButtonToggleVideo.Checked)
                HideVideoPlayer();
            
            mediaPlayer.Invalidate();
            this.Refresh();

            undockVideoControlsToolStripMenuItem.Visible = true;
            redockVideoControlsToolStripMenuItem.Visible = false;
        }

        internal void SetWaveFormToggleOff()
        {
            toolStripButtonToggleWaveForm.Checked = false;
        }

        internal void SetVideoPlayerToggleOff()
        {
            toolStripButtonToggleVideo.Checked = false;
        }

        private void toolStripMenuItemInsertSubtitle_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = _languageGeneral.OpenSubtitle;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetOpenDialogFilter();
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                if (!File.Exists(openFileDialog1.FileName))
                    return;

                var fi = new FileInfo(openFileDialog1.FileName);
                if (fi.Length > 1024 * 1024 * 10) // max 10 mb
                {
                    if (MessageBox.Show(string.Format(_language.FileXIsLargerThan10Mb + Environment.NewLine +
                                                      Environment.NewLine +
                                                      _language.ContinueAnyway,
                                                      openFileDialog1.FileName), Title, MessageBoxButtons.YesNoCancel) != DialogResult.Yes)
                        return;
                }

                MakeHistoryForUndo(string.Format(_language.BeforeInsertLine, openFileDialog1.FileName));

                Encoding encoding = null;
                Subtitle subtitle = new Subtitle();
                SubtitleFormat format = subtitle.LoadSubtitle(openFileDialog1.FileName, out encoding, encoding);

                if (format != null)
                {
                    SaveSubtitleListviewIndexes();
                    if (format.IsFrameBased)
                        subtitle.CalculateTimeCodesFromFrameNumbers(CurrentFrameRate);
                    else
                        subtitle.CalculateFrameNumbersFromTimeCodes(CurrentFrameRate);

                    if (Configuration.Settings.General.RemoveBlankLinesWhenOpening)
                        subtitle.RemoveEmptyLines();

                    int index = FirstSelectedIndex;
                    if (index < 0)
                        index = 0;
                    foreach (Paragraph p in subtitle.Paragraphs)
                    {
                        _subtitle.Paragraphs.Insert(index, new Paragraph(p));
                        index++;                        
                    }

                    if (Configuration.Settings.General.AllowEditOfOriginalSubtitle && _subtitleAlternate != null && _subtitleAlternate.Paragraphs.Count > 0)
                    {
                        index = FirstSelectedIndex;
                        if (index < 0)
                            index = 0;
                        Paragraph current = _subtitle.GetParagraphOrDefault(index);
                        if (current != null)
                        {
                            Paragraph original = Utilities.GetOriginalParagraph(index, current, _subtitleAlternate.Paragraphs);
                            if (original != null)
                            {
                                index = _subtitleAlternate.GetIndex(original);
                                foreach (Paragraph p in subtitle.Paragraphs)
                                {
                                    _subtitleAlternate.Paragraphs.Insert(index, new Paragraph(p));
                                    index++;
                                }
                                _changeAlternate = subtitle.Paragraphs.Count > 0;
                            }
                        }
                    }

                    _change = subtitle.Paragraphs.Count > 0;
                    _subtitle.Renumber(1);
                    ShowSource();
                    SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                    RestoreSubtitleListviewIndexes();
                }
            }
        }

        private void insertLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InsertBefore();
        }

        private void closeVideoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mediaPlayer.VideoPlayer.DisposeVideoPlayer();
            mediaPlayer.VideoPlayer = null;
            timer1.Stop();
            _videoFileName = null;
            _videoAudioTrackNumber = -1;
            labelVideoInfo.Text = Configuration.Settings.Language.General.NoVideoLoaded;
            audioVisualizer.WavePeaks = null;
            audioVisualizer.ResetSpectrogram();
            audioVisualizer.Invalidate();            
        }

        private void toolStripMenuItemVideo_DropDownOpening(object sender, EventArgs e)
        {
            if (_isVideoControlsUnDocked)
            {
                redockVideoControlsToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainVideoToggleVideoControls);
                undockVideoControlsToolStripMenuItem.ShortcutKeys = Keys.None;
            }
            else
            {
                undockVideoControlsToolStripMenuItem.ShortcutKeys = Utilities.GetKeys(Configuration.Settings.Shortcuts.MainVideoToggleVideoControls);
                redockVideoControlsToolStripMenuItem.ShortcutKeys = Keys.None;
            }

            closeVideoToolStripMenuItem.Visible = !string.IsNullOrEmpty(_videoFileName);
            toolStripMenuItemSetAudioTrack.Visible = false;
            if (mediaPlayer.VideoPlayer != null && mediaPlayer.VideoPlayer is Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic)
            {
                var libVlc = (Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic)mediaPlayer.VideoPlayer;
                int numberOfTracks = libVlc.AudioTrackCount;
                _videoAudioTrackNumber = libVlc.AudioTrackNumber;
                if (numberOfTracks > 1)
                {
                    toolStripMenuItemSetAudioTrack.DropDownItems.Clear();
                    for (int i = 0; i < numberOfTracks; i++)
                    {
                        toolStripMenuItemSetAudioTrack.DropDownItems.Add((i + 1).ToString(), null, ChooseAudioTrack);
                        if (i == _videoAudioTrackNumber)
                            toolStripMenuItemSetAudioTrack.DropDownItems[toolStripMenuItemSetAudioTrack.DropDownItems.Count - 1].Select();
                    }
                    toolStripMenuItemSetAudioTrack.Visible = true;
                }
            }
        }

        private void ChooseAudioTrack(object sender, EventArgs e)
        {
            if (mediaPlayer.VideoPlayer != null && mediaPlayer.VideoPlayer is Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic)
            {
                var libVlc = (Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic)mediaPlayer.VideoPlayer;
                var item = sender as ToolStripItem;

                int number = int.Parse(item.Text);
                number--;
                libVlc.AudioTrackNumber = number;
                _videoAudioTrackNumber = number;
            }
        }

        private void textBoxListViewTextAlternate_TextChanged(object sender, EventArgs e)
        {
            if (_subtitleAlternate == null || _subtitleAlternate.Paragraphs.Count < 1)
                return;

            if (_subtitleListViewIndex >= 0)
            {
                Paragraph original = Utilities.GetOriginalParagraph(_subtitleListViewIndex, _subtitle.Paragraphs[_subtitleListViewIndex], _subtitleAlternate.Paragraphs);
                if (original != null)
                {
                    string text = textBoxListViewTextAlternate.Text.TrimEnd();

                    // update _subtitle + listview
                    original.Text = text;
                    UpdateListViewTextInfo(labelTextAlternateLineLengths, labelAlternateSingleLine, labelTextAlternateLineTotal, labelAlternateCharactersPerSecond, original);
                    SubtitleListview1.SetAlternateText(_subtitleListViewIndex, text);
                    _changeAlternate = true;
                }
            }
        }

        private void textBoxListViewTextAlternate_KeyDown(object sender, KeyEventArgs e)
        {
            if (_subtitleAlternate == null || _subtitleAlternate.Paragraphs.Count < 1)
                return;

            int numberOfNewLines = textBoxListViewTextAlternate.Text.Length - textBoxListViewTextAlternate.Text.Replace(Environment.NewLine, " ").Length;

            Utilities.CheckAutoWrap(textBoxListViewTextAlternate, e, numberOfNewLines);

            if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None && numberOfNewLines > 1)
            {
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.R)
            {
                if (textBoxListViewTextAlternate.Text.Length > 0)
                    textBoxListViewTextAlternate.Text = Utilities.AutoBreakLine(textBoxListViewTextAlternate.Text);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.U)
            {
                textBoxListViewTextAlternate.Text = Utilities.UnbreakLine(textBoxListViewTextAlternate.Text);
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.I)
            {
                if (textBoxListViewTextAlternate.SelectionLength == 0)
                {
                    string tag = "i";
                    if (textBoxListViewTextAlternate.Text.Contains("<" + tag + ">"))
                    {
                        textBoxListViewTextAlternate.Text = textBoxListViewTextAlternate.Text.Replace("<" + tag + ">", string.Empty);
                        textBoxListViewTextAlternate.Text = textBoxListViewTextAlternate.Text.Replace("</" + tag + ">", string.Empty);
                    }
                    else
                    {
                        textBoxListViewTextAlternate.Text = string.Format("<{0}>{1}</{0}>", tag, textBoxListViewTextAlternate.Text);
                    }
                }
                else
                {
                   TextBoxListViewToggleTag("i");
                }
            }
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.D)
            {
                textBoxListViewTextAlternate.SelectionLength = 0;
                e.SuppressKeyPress = true;
            }

            // last key down in text
            _lastTextKeyDownTicks = DateTime.Now.Ticks;
        }

        private void openOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenAlternateSubtitle();
        }

        private void saveOriginalAstoolStripMenuItem_Click(object sender, EventArgs e)
        {
            SubtitleFormat currentFormat = GetCurrentSubtitleFormat();
            Utilities.SetSaveDialogFilter(saveFileDialog1, currentFormat);

            saveFileDialog1.Title = _language.SaveOriginalSubtitleAs;
            saveFileDialog1.DefaultExt = "*" + currentFormat.Extension;
            saveFileDialog1.AddExtension = true;               
 
            if (!string.IsNullOrEmpty(_videoFileName))
                saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(_videoFileName);
            else
                saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(_subtitleAlternateFileName);

            if (!string.IsNullOrEmpty(openFileDialog1.InitialDirectory))
                saveFileDialog1.InitialDirectory = openFileDialog1.InitialDirectory;

            DialogResult result = saveFileDialog1.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                _subtitleAlternateFileName = saveFileDialog1.FileName;
                SaveOriginalSubtitle(GetCurrentSubtitleFormat());
            }
        }

        private void saveOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {                  
            if (string.IsNullOrEmpty(_subtitleAlternateFileName))
            {
                saveOriginalAstoolStripMenuItem_Click(null, null);
                return;
            }

            try
            {
                SaveOriginalSubtitle(GetCurrentSubtitleFormat());
            }
            catch
            {
                MessageBox.Show(string.Format(_language.UnableToSaveSubtitleX, _subtitleAlternateFileName));
            }
        }

        private void removeOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueNewOrExitAlternate())
            {
                RemoveAlternate(true);
            }
        }

        private void RemoveAlternate(bool removeFromListView)
        {
            if (removeFromListView)
            {
                SubtitleListview1.HideAlternateTextColumn();
                SubtitleListview1.AutoSizeAllColumns(this);
                _subtitleAlternate = new Subtitle();
                _subtitleAlternateFileName = null;
            }

            buttonUnBreak.Visible = true;
            buttonUndoListViewChanges.Visible = true;
            textBoxListViewTextAlternate.Visible = false;
            labelAlternateText.Visible = false;
            labelAlternateCharactersPerSecond.Visible = false;
            labelTextAlternateLineLengths.Visible = false;
            labelAlternateSingleLine.Visible = false;
            labelTextAlternateLineTotal.Visible = false;
            textBoxListViewText.Width = (groupBoxEdit.Width - (textBoxListViewText.Left + 8 + buttonUnBreak.Width));
            textBoxListViewText.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            labelCharactersPerSecond.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelCharactersPerSecond.Width);
            labelTextLineTotal.Left = textBoxListViewText.Left + (textBoxListViewText.Width - labelTextLineTotal.Width);

            SetTitle();
        }

        private void toolStripMenuItemSpellCheckMain_DropDownOpening(object sender, EventArgs e)
        {
            if (Configuration.Settings.General.SpellChecker.ToLower().Contains("word"))
            {
                toolStripSeparator9.Visible = false;
                GetDictionariesToolStripMenuItem.Visible = false;
                addWordToNamesetcListToolStripMenuItem.Visible = false;
            }
            else
            {
                toolStripSeparator9.Visible = true;
                GetDictionariesToolStripMenuItem.Visible = true;
                addWordToNamesetcListToolStripMenuItem.Visible = true;
            }
        }

        private void toolStripMenuItemPlayRateSlow_Click(object sender, EventArgs e)
        {
            if (mediaPlayer.VideoPlayer != null)
            {
                toolStripMenuItemPlayRateSlow.Checked = true;
                toolStripMenuItemPlayRateNormal.Checked = false;
                toolStripMenuItemPlayRateFast.Checked = false;
                toolStripMenuItemPlayRateVeryFast.Checked = false;
                mediaPlayer.VideoPlayer.PlayRate = 0.8;
                toolStripSplitButtonPlayRate.Image = imageListPlayRate.Images[1];
            }
        }

        private void toolStripMenuItemPlayRateNormal_Click(object sender, EventArgs e)
        {
            if (mediaPlayer.VideoPlayer != null)
            {
                toolStripMenuItemPlayRateSlow.Checked = false;
                toolStripMenuItemPlayRateNormal.Checked = true;
                toolStripMenuItemPlayRateFast.Checked = false;
                toolStripMenuItemPlayRateVeryFast.Checked = false;
                mediaPlayer.VideoPlayer.PlayRate = 1.0;
                toolStripSplitButtonPlayRate.Image = imageListPlayRate.Images[0];
            }
        }

        private void toolStripMenuItemPlayRateFast_Click(object sender, EventArgs e)
        {
            if (mediaPlayer.VideoPlayer != null)
            {
                toolStripMenuItemPlayRateSlow.Checked = false;
                toolStripMenuItemPlayRateNormal.Checked = false;
                toolStripMenuItemPlayRateFast.Checked = true;
                toolStripMenuItemPlayRateVeryFast.Checked = false;
                mediaPlayer.VideoPlayer.PlayRate = 1.2;
                toolStripSplitButtonPlayRate.Image = imageListPlayRate.Images[1];
            }
        }

        private void veryFastToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mediaPlayer.VideoPlayer != null)
            {
                toolStripMenuItemPlayRateSlow.Checked = false;
                toolStripMenuItemPlayRateNormal.Checked = false;
                toolStripMenuItemPlayRateFast.Checked = false;
                toolStripMenuItemPlayRateVeryFast.Checked = true;
                mediaPlayer.VideoPlayer.PlayRate = 1.6;
                toolStripSplitButtonPlayRate.Image = imageListPlayRate.Images[1];
            }            
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            Main_Resize(null, null);
        }

        private void buttonSplitLine_Click(object sender, EventArgs e)
        {
            SplitSelectedParagraph(null, null);
        }

        ///////////// 3.2 /////////////

        private void toolStripMenuItemCopySourceText_Click(object sender, EventArgs e)
        {
            Subtitle selectedLines = new Subtitle(_subtitle);
            selectedLines.Paragraphs.Clear();
            foreach (int index in SubtitleListview1.SelectedIndices)
                selectedLines.Paragraphs.Add(_subtitle.Paragraphs[index]);            
            Clipboard.SetText(selectedLines.ToText(GetCurrentSubtitleFormat()));
        }

        public void PlayPause()
        {
            mediaPlayer.TogglePlayPause();
        }

        public void SetCurrentViaEndPositionAndGotoNext(int index)
        {
            Paragraph p = _subtitle.GetParagraphOrDefault(index);
            if (p == null)
                return;

            if (mediaPlayer.VideoPlayer == null || string.IsNullOrEmpty(_videoFileName))
            {
                MessageBox.Show(Configuration.Settings.Language.General.NoVideoLoaded);
                return;
            }

            //if (autoDuration)
            //{ 
            //    //TODO: auto duration
            //    //TODO: search for start via wave file (must only be minor adjustment)
            //}

            // current movie pos
            double durationTotalMilliseconds = p.Duration.TotalMilliseconds;
            double totalMillisecondsEnd = mediaPlayer.CurrentPosition * 1000.0;

            TimeCode tc = new TimeCode(TimeSpan.FromMilliseconds(totalMillisecondsEnd - durationTotalMilliseconds));
            MakeHistoryForUndo(_language.BeforeSetEndAndVideoPosition + "  " + tc.ToString());
            _makeHistory = false;

            p.StartTime.TotalMilliseconds = totalMillisecondsEnd - durationTotalMilliseconds;
            p.EndTime.TotalMilliseconds = totalMillisecondsEnd;

            timeUpDownStartTime.TimeCode = p.StartTime;
            decimal durationInSeconds = (decimal)(p.Duration.TotalSeconds);
            if (durationInSeconds >= numericUpDownDuration.Minimum && durationInSeconds <= numericUpDownDuration.Maximum)
                numericUpDownDuration.Value = durationInSeconds;

            SubtitleListview1.SelectIndexAndEnsureVisible(index+1);
            ShowStatus(string.Format(_language.VideoControls.AdjustedViaEndTime, p.StartTime.ToShortString()));
            audioVisualizer.Invalidate();
            _makeHistory = true;
        }

        private void editSelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < SubtitleListview1.Items.Count; i++)
                SubtitleListview1.Items[i].Selected = true;
        }

        private void toolStripMenuItemSplitTextAtCursor_Click(object sender, EventArgs e)
        {
            TextBox tb =textBoxListViewText;
            if (textBoxListViewTextAlternate.Focused)
                tb = textBoxListViewTextAlternate;

            int? pos = null;
            if (tb.SelectionStart > 2 && tb.SelectionStart < tb.Text.Length - 2)
                pos = tb.SelectionStart;
            SplitSelectedParagraph(null, pos);
        }

        private void contextMenuStripTextBoxListView_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TextBox tb = textBoxListViewText;
            if (textBoxListViewTextAlternate.Focused)
                tb = textBoxListViewTextAlternate;
            toolStripMenuItemSplitTextAtCursor.Visible = tb.Text.Length > 5 && tb.SelectionStart > 2 && tb.SelectionStart < tb.Text.Length - 2;

            if (GetCurrentEncoding() == Encoding.Default || _subtitleListViewIndex == -1)
            {
                toolStripMenuItemInsertUnicodeSymbol.Visible = false;
                toolStripSeparator26.Visible = false;
            }
            else
            {
                toolStripMenuItemInsertUnicodeSymbol.Visible = true;
                toolStripSeparator26.Visible = true;
            }
        }

        private void toolStripMenuItemExportPngXml_Click(object sender, EventArgs e)
        {
            ExportPngXml exportBdnXmlPng = new ExportPngXml();
            exportBdnXmlPng.Initialize(_subtitle);
            exportBdnXmlPng.ShowDialog(this);
        }

        private void tabControlSubtitle_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (tabControlSubtitle.SelectedIndex != TabControlSourceView && textBoxSource.Text.Trim().Length > 1)
            {
                Subtitle temp = new Subtitle(_subtitle);
                SubtitleFormat format = temp.ReloadLoadSubtitle(new List<string>(textBoxSource.Lines), null);
                if (format == null)
                    e.Cancel = true;
            }
        }

        private void toolStripComboBoxFrameRate_TextChanged(object sender, EventArgs e)
        {
            Configuration.Settings.General.CurrentFrameRate = CurrentFrameRate;
        }

        private void toolStripMenuItemGoogleMicrosoftTranslateSelLine_Click(object sender, EventArgs e)
        {
            int firstSelectedIndex = FirstSelectedIndex;
            if (firstSelectedIndex >= 0)
            {
                Paragraph p = _subtitle.GetParagraphOrDefault(firstSelectedIndex);
                if (p != null)
                {
                    string defaultFromLanguage = Utilities.AutoDetectGoogleLanguage(_subtitle);
                    string defaultToLanguage = defaultFromLanguage;
                    if (_subtitleAlternate != null)
                     {
                         Paragraph o = Utilities.GetOriginalParagraph(firstSelectedIndex, p, _subtitleAlternate.Paragraphs);
                         if (o != null)
                         {
                             p = o;
                             defaultFromLanguage = Utilities.AutoDetectGoogleLanguage(_subtitleAlternate);
                         }
                     }
                     Cursor = Cursors.WaitCursor;
                     if (_googleOrMicrosoftTranslate == null || _googleOrMicrosoftTranslate.IsDisposed)
                     {
                         _googleOrMicrosoftTranslate = new GoogleOrMicrosoftTranslate();
                         _googleOrMicrosoftTranslate.InitializeFromLanguage(defaultFromLanguage, defaultToLanguage);
                     }
                     _googleOrMicrosoftTranslate.Initialize(p);
                    Cursor = Cursors.Default;
                    if (_googleOrMicrosoftTranslate.ShowDialog() == DialogResult.OK)
                    {
                        textBoxListViewText.Text = _googleOrMicrosoftTranslate.TranslatedText;
                    }
                }
            }
        }

        private void numericUpDownSec1_ValueChanged(object sender, EventArgs e)
        {
            Configuration.Settings.General.SmallDelayMilliseconds = (int)(numericUpDownSec1.Value * 1000);
        }

        private void numericUpDownSec2_ValueChanged(object sender, EventArgs e)
        {
            Configuration.Settings.General.LargeDelayMilliseconds = (int)(numericUpDownSec2.Value * 1000);
        }

        private void numericUpDownSecAdjust1_ValueChanged(object sender, EventArgs e)
        {
            Configuration.Settings.General.SmallDelayMilliseconds = (int)(numericUpDownSecAdjust1.Value * 1000);
        }

        private void numericUpDownSecAdjust2_ValueChanged(object sender, EventArgs e)
        {
            Configuration.Settings.General.LargeDelayMilliseconds = (int)(numericUpDownSecAdjust2.Value * 1000);
        }

        private void toolStripMenuItemMakeEmptyFromCurrent_Click(object sender, EventArgs e)
        {
            if (ContinueNewOrExit())
            {
                _subtitleAlternate = new Subtitle(_subtitle);
                _subtitleAlternateFileName = null;
                int oldIndex = FirstSelectedIndex;
                if (oldIndex < 0)
                    oldIndex = 0;

                foreach (Paragraph p in _subtitle.Paragraphs)
                {
                    p.Text = "-";
                }
                SubtitleListview1.ShowAlternateTextColumn(Configuration.Settings.Language.General.OriginalText);
                _subtitleListViewIndex = -1;
                SubtitleListview1.Fill(_subtitle, _subtitleAlternate);
                SubtitleListview1.SelectIndexAndEnsureVisible(oldIndex);
                textBoxListViewText.Focus();
                Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable = true;

                _subtitleAlternateFileName = _fileName;
                _fileName = null;
                SetupAlternateEdit();
                _changeAlternate = _change;
                _change = true;
            }
        }

        private void toolStripMenuItemShowOriginalInPreview_Click(object sender, EventArgs e)
        {
            toolStripMenuItemShowOriginalInPreview.Checked = !toolStripMenuItemShowOriginalInPreview.Checked;
            Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable = toolStripMenuItemShowOriginalInPreview.Checked;
        }

        private void toolStripMenuItemVideo_DropDownClosed(object sender, EventArgs e)
        {
            redockVideoControlsToolStripMenuItem.ShortcutKeys = Keys.None; 
            undockVideoControlsToolStripMenuItem.ShortcutKeys = Keys.None;

        }

        private void toolsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (_subtitle != null && _subtitle.Paragraphs.Count > 0 && _networkSession != null)
            {
                toolStripSeparator23.Visible = true;
                toolStripMenuItemMakeEmptyFromCurrent.Visible = _subtitle != null && _subtitle.Paragraphs.Count > 0 && !SubtitleListview1.IsAlternateTextColumnVisible;
                toolStripMenuItemShowOriginalInPreview.Checked = Configuration.Settings.General.ShowOriginalAsPreviewIfAvailable;
            }
            else
            {
                toolStripSeparator23.Visible = false;
                toolStripMenuItemMakeEmptyFromCurrent.Visible = false;
                toolStripMenuItemShowOriginalInPreview.Checked = false;
            }
            toolStripMenuItemShowOriginalInPreview.Visible = SubtitleListview1.IsAlternateTextColumnVisible;
        }

        private void contextMenuStripWaveForm_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (audioVisualizer.IsSpectrogramAvailable)
            {
                if (audioVisualizer.ShowSpectrogram && audioVisualizer.ShowWaveform)
                {
                    showWaveformAndSpectrogramToolStripMenuItem.Visible = false;
                    showOnlyWaveformToolStripMenuItem.Visible = true;
                    showOnlySpectrogramToolStripMenuItem.Visible = true;
                }
                else if (audioVisualizer.ShowSpectrogram)
                {
                    showWaveformAndSpectrogramToolStripMenuItem.Visible = true;
                    showOnlyWaveformToolStripMenuItem.Visible = true;
                    showOnlySpectrogramToolStripMenuItem.Visible = false;
                }
                else
                {
                    showWaveformAndSpectrogramToolStripMenuItem.Visible = true;
                    showOnlyWaveformToolStripMenuItem.Visible = false;
                    showOnlySpectrogramToolStripMenuItem.Visible = true;
                }
            }
            else
            {
                toolStripSeparator24.Visible = false;
                showWaveformAndSpectrogramToolStripMenuItem.Visible = false;
                showOnlyWaveformToolStripMenuItem.Visible = false;
                showOnlySpectrogramToolStripMenuItem.Visible = false;
            }
        }

        private void showWaveformAndSpectrogramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            audioVisualizer.ShowSpectrogram = true;
            audioVisualizer.ShowWaveform = true;
        }

        private void showOnlyWaveformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            audioVisualizer.ShowSpectrogram = false;
            audioVisualizer.ShowWaveform = true;
        }

        private void showOnlySpectrogramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            audioVisualizer.ShowSpectrogram = true;
            audioVisualizer.ShowWaveform = false;
        }

        private void splitContainerMain_SplitterMoved(object sender, SplitterEventArgs e)
        {
            mediaPlayer.Refresh();
        }

        private void FindDoubleLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = FirstSelectedIndex+1; i < _subtitle.Paragraphs.Count; i++)
            {
                var current = _subtitle.GetParagraphOrDefault(i);
                var next = _subtitle.GetParagraphOrDefault(i+1);
                if (current != null && next != null)
                {
                    if (current.Text.Trim().ToLower() == next.Text.Trim().ToLower())
                    {
                        SubtitleListview1.SelectIndexAndEnsureVisible(i);
                        SubtitleListview1.Items[i + 1].Selected = true;
                        break;
                    }
                }
            }
        }

        private void textBoxListViewTextAlternate_MouseMove(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control && MouseButtons == System.Windows.Forms.MouseButtons.Left)
            {
                if (!string.IsNullOrEmpty(textBoxListViewTextAlternate.SelectedText))
                    textBoxListViewTextAlternate.DoDragDrop(textBoxListViewTextAlternate.SelectedText, DragDropEffects.Copy);
                else
                    textBoxListViewTextAlternate.DoDragDrop(textBoxListViewTextAlternate.Text, DragDropEffects.Copy);
            }
        }

        private void textBoxListViewText_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TextBox tb = (sender as TextBox);
            string selText = tb.SelectedText;
            if (!string.IsNullOrEmpty(selText))
            {
                for (int i = 0; i < 5; i++)
                {
                    selText = selText.TrimEnd('.');
                    selText = selText.TrimEnd('!');
                    selText = selText.TrimEnd('?');
                    selText = selText.TrimEnd(',');
                    selText = selText.TrimEnd(')');
                    selText = selText.TrimEnd(':');
                    selText = selText.TrimEnd(';');
                    selText = selText.TrimEnd(' ');
                }
                tb.SelectionLength = selText.Length;
                if ((selText.StartsWith("(") || selText.StartsWith("[")) && selText.Length > 1)
                {
                    int l = tb.SelectionLength -1;
                    tb.SelectionStart++;
                    tb.SelectionLength = l;
                }
            }
        }

        private void textBoxListViewTextAlternate_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            textBoxListViewText_MouseDoubleClick(sender, e);
        }

    }
}