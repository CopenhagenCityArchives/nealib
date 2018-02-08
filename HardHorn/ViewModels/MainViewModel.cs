using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using System.ComponentModel;
using HardHorn.Analysis;
using HardHorn.Statistics;
using HardHorn.Archiving;
using System.IO;
using System.Collections.ObjectModel;
using HardHorn.Logging;
using System.Dynamic;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using HardHorn.Utility;

namespace HardHorn.ViewModels
{

    class MainViewModel : PropertyChangedBase, ILogger
    {
        #region Properties
        ArchiveVersionViewModel _selectedArchiveVersionViewModel;
        public ArchiveVersionViewModel SelectedArchiveVersionViewModel
        {
            get { return _selectedArchiveVersionViewModel; }
            set {
                if (value == null)
                {
                    WindowTitle = "HardHorn";
                }
                else
                {
                    WindowTitle = string.Format("HardHorn - {0}", value.ArchiveVersion.Id);
                }
                _selectedArchiveVersionViewModel = value;
                NotifyOfPropertyChange("SelectedArchiveVersionViewModel");
            }
        }
        public ObservableCollection<ArchiveVersionViewModel> ArchiveVersionViewModels { get; private set; }
        public ObservableCollection<Tuple<LogLevel, DateTime, string>> LogItems { get; set; }

        public TestSelection SelectedTests { get; set; }
        string _windowTitle = "HardHorn";
        public string WindowTitle { get { return _windowTitle; } set { _windowTitle = value; NotifyOfPropertyChange("WindowTitle"); } }

        public ObservableCollection<string> RecentLocations { get { return Properties.Settings.Default.RecentLocations; } }

        bool _loadingTableIndex = false;
        public bool LoadingTableIndex
        {
            get { return _loadingTableIndex; }
            set { _loadingTableIndex = value; NotifyOfPropertyChange("LoadingTableIndex"); }
        }

        private bool _testLoaded = false;
        public bool TestLoaded
        {
            get { return _testLoaded; }
            set { _testLoaded = value; NotifyOfPropertyChange("TestLoaded"); }
        }

        Dictionary<Type, ErrorViewModelBase> LoadingErrorViewModelIndex { get; set; }

        string _statusText = "";
        public string StatusText { get { return _statusText; } set { _statusText = value; NotifyOfPropertyChange("StatusText"); } }
        #endregion

        #region Constructors
        public MainViewModel()
        {
            ArchiveVersionViewModels = new ObservableCollection<ArchiveVersionViewModel>();
            LoadingErrorViewModelIndex = new Dictionary<Type, ErrorViewModelBase>();
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();

            Log("Så er det dælme tid til at teste datatyper!", LogLevel.SECTION);
        }
        #endregion

        #region Methods
        public void Log(string msg, LogLevel level = LogLevel.NORMAL)
        {
            LogItems.Add(new Tuple<LogLevel, DateTime, string>(level, DateTime.Now, msg));

            if (level == LogLevel.SECTION || level == LogLevel.ERROR)
            {
                StatusText = msg;
            }
        }

        public void OnArchiveVersionException(Exception ex)
        {
            ErrorViewModelBase errorViewModel;

            if (!LoadingErrorViewModelIndex.ContainsKey(ex.GetType()))
            {
                if (ex is ArchiveVersionColumnParsingException)
                {
                    errorViewModel = new ColumnParsingErrorViewModel();
                }
                else if (ex is ArchiveVersionColumnTypeParsingException)
                {
                    errorViewModel = new ColumnTypeParsingErrorViewModel();
                }
                else
                {
                    return;
                }

                LoadingErrorViewModelIndex[ex.GetType()] = errorViewModel;
                //Application.Current.Dispatcher.Invoke(() => ErrorViewModels.Add(errorViewModel));

            }
            else
            {
                errorViewModel = LoadingErrorViewModelIndex[ex.GetType()];
            }

            Application.Current.Dispatcher.Invoke(() => errorViewModel.Add(ex));
        }
        #endregion

        #region Actions
        public void Close(ArchiveVersionViewModel avViewModel)
        {
            if (avViewModel.TestRunning)
                avViewModel.StopTest();
            ArchiveVersionViewModels.Remove(avViewModel);
            avViewModel = null;
        }

        public void NewTest()
        {
            if (SelectedArchiveVersionViewModel != null)
            {
                SelectedArchiveVersionViewModel.StartTest();
            }
        }

        public void StopTest()
        {
            if (SelectedArchiveVersionViewModel != null)
            {
                SelectedArchiveVersionViewModel.StopTest();
            }
        }

        public void SaveTableIndex()
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Filter = "Xml|*.xml|Alle filtyper|*.*";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    //ArchiveVersion.TableIndex.ToXml().Save(dialog.FileName);
                }
            }
        }

        public async void LoadLocation(string location)
        {
            if (Directory.Exists(location) && File.Exists(Path.Combine(location, "Indices", "tableIndex.xml")))
            {
                Log(string.Format("Indlæser tabeller fra '{0}'", location), LogLevel.SECTION);
                try
                {
                    var logger = new ProgressLogger(this);
                    ArchiveVersionViewModel vm = await Task.Run(() =>
                    {
                        var av = ArchiveVersion.Load(location, logger, OnArchiveVersionException);
                        return new ArchiveVersionViewModel(av, this as ILogger);
                    });

                    ArchiveVersionViewModels.Add(vm);
                    SelectedArchiveVersionViewModel = vm;

                    // Add to recent locations
                    if (Properties.Settings.Default.RecentLocations == null)
                    {
                        Properties.Settings.Default.RecentLocations = new ObservableCollection<string>();
                    }

                    var index = -1;
                    for (int i = 0; i < Properties.Settings.Default.RecentLocations.Count; i++)
                    {
                        var loc = Properties.Settings.Default.RecentLocations[i];
                        if (loc.ToLower() == location.ToLower())
                        {
                            index = i;
                            break;
                        }
                    }
                    if (index != -1)
                    {
                        Properties.Settings.Default.RecentLocations.RemoveAt(index);
                    }
                    if (Properties.Settings.Default.RecentLocations.Count < 5)
                    {
                        Properties.Settings.Default.RecentLocations.Add(null);
                    }

                    for (int i = Properties.Settings.Default.RecentLocations.Count - 1; i > 0; i--)
                    {
                        Properties.Settings.Default.RecentLocations[i] = Properties.Settings.Default.RecentLocations[i - 1];
                    }
                    Properties.Settings.Default.RecentLocations[0] = location;

                    Properties.Settings.Default.Save();
                    NotifyOfPropertyChange("RecentLocations");

                }
                catch (Exception ex)
                {
                    Log("En undtagelse forekom under indlæsningen af arkiveringsversionen, med følgende besked: " + ex.Message, LogLevel.ERROR);
                    return;
                }
                finally
                {
                    LoadingTableIndex = false;
                }
            } 
            else
            {
                Log(string.Format("Placeringen '{0}' er ikke en gyldig arkiveringsversion.", location), LogLevel.ERROR);
            }
        }

        public void SelectLocation()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    LoadLocation(dialog.SelectedPath);
                }
            }
        }

        public void Exit()
        {
            Application.Current.Shutdown();
        }
        #endregion
    }
}
