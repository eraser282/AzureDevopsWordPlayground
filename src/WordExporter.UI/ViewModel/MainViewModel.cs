using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WordExporter.Core;
using WordExporter.Core.Templates;
using WordExporter.Core.WordManipulation;
using WordExporter.Core.WorkItems;
using WordExporter.UI.Support;
using WordExporter.UI.ViewModel.SubModels;

namespace WordExporter.UI.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            //if (IsInDesignMode)
            //{
            //    // Code runs in Blend --> create design time data.
            //}
            //else
            //{
            //    // Code runs "for real"
            //}

            TemplateFolder = StatePersister.Instance.Load<String>("main.TemplateFolder") ?? @"C:\develop\GitHub\AzureDevopsWordPlayground\src\WordExporter\Templates";
            Connect = new RelayCommand(ConnectMethod);
            GetQueries = new RelayCommand(GetQueriesMethod);
            Export = new RelayCommand(ExportMethod);
            Dump = new RelayCommand(DumpMethod);
            GetIterations = new RelayCommand(GetIterationsMethod);
            Address = StatePersister.Instance.Load<String>("main.Address") ?? String.Empty;
            CredentialViewModel = new CredentialViewModel();
            CredentialViewModel.UserName = StatePersister.Instance.Load<String>("userName");
            UseNetworkCredential = "true".Equals(StatePersister.Instance.Load<String>("useNetworkCredential"), StringComparison.OrdinalIgnoreCase);
        }

        private Boolean _connected;

        public Boolean Connected
        {
            get
            {
                return _connected;
            }
            set
            {
                Set<Boolean>(() => this.Connected, ref _connected, value);
            }
        }

        private String _status;

        public String Status
        {
            get
            {
                return _status;
            }
            set
            {
                Set<String>(() => this.Status, ref _status, value);
            }
        }

        private String _address;

        public String Address
        {
            get
            {
                return _address;
            }
            set
            {
                Set<String>(() => this.Address, ref _address, value);
                StatePersister.Instance.Save("main.Address", value);
            }
        }

        private ObservableCollection<TeamProject> _teamProjects = new ObservableCollection<TeamProject>();

        public ObservableCollection<TeamProject> TeamProjects
        {
            get
            {
                return _teamProjects;
            }
            set
            {
                _teamProjects = value;
                RaisePropertyChanged(nameof(TeamProjects));
            }
        }

        private TeamProject _selectedTeamProject;

        public TeamProject SelectedTeamProject
        {
            get
            {
                return _selectedTeamProject;
            }
            set
            {
                Set<TeamProject>(() => this.SelectedTeamProject, ref _selectedTeamProject, value);
                GetIterationsMethod();
            }
        }

        private Boolean _useNetworkCredential;

        public Boolean UseNetworkCredential
        {
            get
            {
                return _useNetworkCredential;
            }
            set
            {
                Set<Boolean>(() => this.UseNetworkCredential, ref _useNetworkCredential, value);
            }
        }

        private CredentialViewModel _credentialViewModel;

        public CredentialViewModel CredentialViewModel
        {
            get
            {
                return _credentialViewModel;
            }
            set
            {
                Set<CredentialViewModel>(() => this.CredentialViewModel, ref _credentialViewModel, value);
            }
        }

        private ObservableCollection<QueryViewModel> _queries = new ObservableCollection<QueryViewModel>();

        public ObservableCollection<QueryViewModel> Queries
        {
            get
            {
                return _queries;
            }
            set
            {
                _queries = value;
                RaisePropertyChanged(nameof(Queries));
            }
        }

        private QueryViewModel _selectedQuery;

        public QueryViewModel SelectedQuery
        {
            get
            {
                return _selectedQuery;
            }
            set
            {
                Set<QueryViewModel>(() => this.SelectedQuery, ref _selectedQuery, value);
            }
        }

        private TemplateInfo _selectedTemplate;

        /// <summary>
        /// First template selected.
        /// </summary>
        public TemplateInfo SelectedTemplate
        {
            get
            {
                return _selectedTemplate;
            }
            set
            {
                Set<TemplateInfo>(() => this.SelectedTemplate, ref _selectedTemplate, value);
            }
        }

        private String _templateFolder;

        public String TemplateFolder
        {
            get
            {
                return _templateFolder;
            }
            set
            {
                Set<String>(() => this.TemplateFolder, ref _templateFolder, value);
                Templates.Clear();
                if (Directory.Exists(value))
                {
                    TemplateManager = new TemplateManager(TemplateFolder);
                    foreach (var template in TemplateManager.GetTemplateNames())
                    {
                        var wordTemplate = TemplateManager.GetWordDefinitionTemplate(template);
                        var info = new TemplateInfo(template, wordTemplate);
                        Templates.Add(info);
                        info.PropertyChanged += TemplatePropertyChanged;
                    }
                    StatePersister.Instance.Save("main.TemplateFolder", value);
                }
                else
                {
                    TemplateManager = null;
                }
            }
        }

        private void TemplatePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateSelectionOfTemplate();
        }

        private TemplateManager _templateManager;

        public TemplateManager TemplateManager
        {
            get
            {
                return _templateManager;
            }
            set
            {
                Set<TemplateManager>(() => this.TemplateManager, ref _templateManager, value);
            }
        }

        private ObservableCollection<TemplateInfo> _templates = new ObservableCollection<TemplateInfo>();

        public ObservableCollection<TemplateInfo> Templates
        {
            get
            {
                return _templates;
            }
            set
            {
                _templates = value;
                RaisePropertyChanged(nameof(Templates));
            }
        }

        private ObservableCollection<ParameterViewModel> _parameters = new ObservableCollection<ParameterViewModel>();

        public ObservableCollection<ParameterViewModel> Parameters
        {
            get
            {
                return _parameters;
            }
            set
            {
                _parameters = value;
                RaisePropertyChanged(nameof(Parameters));
            }
        }

        private ObservableCollection<ParameterViewModel> _arrayParameters = new ObservableCollection<ParameterViewModel>();

        public ObservableCollection<ParameterViewModel> ArrayParameters
        {
            get
            {
                return _arrayParameters;
            }
            set
            {
                _arrayParameters = value;
                RaisePropertyChanged(nameof(ArrayParameters));
            }
        }

        private ObservableCollection<IterationsViewModel> _iterations = new ObservableCollection<IterationsViewModel>();

        public ObservableCollection<IterationsViewModel> Iterations
        {
            get
            {
                return _iterations;
            }
            set
            {
                _iterations = value;
                RaisePropertyChanged(nameof(Iterations));
            }
        }

        private Boolean _generatePdf;

        public Boolean GeneratePdf
        {
            get
            {
                return _generatePdf;
            }
            set
            {
                Set<Boolean>(() => this.GeneratePdf, ref _generatePdf, value);
            }
        }

        private Boolean _showIterationParameters;

        public Boolean ShowIterationParameters
        {
            get
            {
                return _showIterationParameters;
            }
            set
            {
                Set<Boolean>(() => this.ShowIterationParameters, ref _showIterationParameters, value);
            }
        }

        private Boolean _normalizeFont;

        public Boolean NormalizeFont
        {
            get
            {
                return _normalizeFont;
            }
            set
            {
                Registry.Options.NormalizeFontInDescription = value;
                Set<Boolean>(() => this.NormalizeFont, ref _normalizeFont, value);
            }
        }

        public ICommand Connect { get; private set; }

        public ICommand GetQueries { get; private set; }

        public ICommand Export { get; private set; }

        public ICommand Dump { get; private set; }

        public ICommand GetIterations { get; private set; }

#pragma warning disable S3168 // "async" methods should not return "void"
        private async void ConnectMethod()
#pragma warning restore S3168 // "async" methods should not return "void"
        {
            try
            {
                Status = "Connecting";
                var connectionManager = new ConnectionManager();
                if (!UseNetworkCredential)
                {
                    await connectionManager.ConnectAsync(Address);
                }
                else
                {
                    await connectionManager.ConnectAsyncWithNetworkCredentials(
                        Address, new NetworkCredential(CredentialViewModel.UserName, CredentialViewModel.Password));
                    StatePersister.Instance.Save("userName", CredentialViewModel.UserName);
                    StatePersister.Instance.Save("password", EncryptionUtils.Encrypt(CredentialViewModel.Password));
                    StatePersister.Instance.Save("useNetworkCredential", UseNetworkCredential.ToString());
                }

                Status = "Connected, Retrieving Team Projects";
                var projectHttpClient = connectionManager.GetClient<ProjectHttpClient>();

                await GetTeamProjectAsync(projectHttpClient);
                Status = "Connected, List of team Project retrieved";
                Connected = true;
            }
            catch (Exception ex)
            {
                Status = $"Error during connection: {ex.Message}";
                Log.Error(ex, "Error during connection");
            }
        }

        private async Task GetTeamProjectAsync(ProjectHttpClient projectHttpClient)
        {
            // then - same as above.. iterate over the project references (with a hard-coded pagination of the first 10 entries only)
            var tpList = await Task.Run(() =>
            {
                List<TeamProject> tempUnorderedListOfTeamProjects = new List<TeamProject>();
                foreach (var projectReference in projectHttpClient.GetProjects(top: 100, skip: 0).Result)
                {
                    // and then get ahold of the actual project
                    var teamProject = projectHttpClient.GetProject(projectReference.Id.ToString()).Result;

                    tempUnorderedListOfTeamProjects.Add(teamProject);
                }
                return tempUnorderedListOfTeamProjects;
            });

            foreach (var teamProject in tpList.OrderBy(tp => tp.Name))
            {
                _teamProjects.Add(teamProject);
            }
        }

#pragma warning disable S3168 // "async" methods should not return "void"
        public async void GetQueriesMethod()
#pragma warning restore S3168 // "async" methods should not return "void"
        {
            WorkItemTrackingHttpClient witClient = ConnectionManager.Instance.GetClient<WorkItemTrackingHttpClient>();
            var queries = await witClient.GetQueriesAsync(SelectedTeamProject.Name, depth: 2, expand: QueryExpand.Wiql);
            Queries.Clear();
            await PopulateQueries(String.Empty, witClient, queries);
        }

        public void GetIterationsMethod()
        {
            Iterations.Clear();
            if (SelectedTeamProject == null)
            {
                return;
            }

            Status = "Getting iterations for team project " + SelectedTeamProject.Name;

            var itManager = new IterationManager(ConnectionManager.Instance);

            foreach (var iteration in itManager.GetAllIterationsForTeamProject(SelectedTeamProject.Name))
            {
                Iterations.Add(new IterationsViewModel(iteration));
            }
            Status = "All iteration loaded";
        }

        public void ExportMethod()
        {
            if (TemplateManager == null)
            {
                return;
            }

            if (!Templates.Any(t => t.IsSelected))
            {
                return;
            }

            if (ConnectionManager.Instance == null)
            {
                return;
            }

            try
            {
                InnerExecuteExport();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting template: {message}", ex.Message);
                Status = $"Error exporting template: {ex.Message}";
            }
        }

        public void DumpMethod()
        {
            if (TemplateManager == null)
            {
                return;
            }

            if (!Templates.Any(t => t.IsSelected))
            {
                return;
            }

            if (ConnectionManager.Instance == null)
            {
                return;
            }

            try
            {
                InnerExecuteDump();
                Status = $"Export Completed";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error exporting template: {message}", ex.Message);
                Status = $"Error exporting template: {ex.Message}";
            }
        }

        private void InnerExecuteDump()
        {
            foreach (var selectedTemplate in Templates.Where(t => t.IsSelected))
            {
                var fileName = Path.Combine(Path.GetTempPath(), selectedTemplate.TemplateName, Guid.NewGuid().ToString()) + ".txt";
                if (selectedTemplate.IsScriptTemplate)
                {
                    var executor = new TemplateExecutor(selectedTemplate.WordTemplateFolderManager);

                    //now we need to ask user parameter value
                    Dictionary<string, object> parameters = PrepareUserParameters();
                    executor.DumpWorkItem(fileName, ConnectionManager.Instance, SelectedTeamProject.Name, parameters);
                }
                else
                {
                    var selected = SelectedQuery?.Results?.Where(q => q.Selected).ToList();
                    if (selected == null || selected.Count == 0)
                    {
                        return;
                    }

                    var sb = new StringBuilder();
                    foreach (var workItemResult in selected)
                    {
                        var workItem = workItemResult.WorkItem;
                        var values = workItem.CreateDictionaryFromWorkItem();
                        foreach (var value in values)
                        {
                            sb.AppendLine($"{value.Key.PadRight(50, ' ')}={value.Value}");
                        }
                        File.WriteAllText(fileName, sb.ToString());
                    }
                }
                System.Diagnostics.Process.Start(fileName);
            }
        }

        private void InnerExecuteExport()
        {
            var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            foreach (var selectedTemplate in Templates.Where(t => t.IsSelected))
            {
                if (selectedTemplate.IsScriptTemplate)
                {
                    if (ArrayParameters.Any())
                    {
                        var arrayParameters = ArrayParameters.Select(p => new
                        {
                            Name = p.Name,
                            Values = p.Value?.Split(',', ';').ToList() ?? new List<string>()
                        })
                        .ToList();

                        Int32 maxParameterCount = arrayParameters.Max(p => p.Values.Count);
                        for (int i = 0; i < maxParameterCount; i++)
                        {
                            StringBuilder fileSuffix = new StringBuilder();
                            Dictionary<string, object> parameters = PrepareUserParameters();
                            foreach (var arrayParameter in arrayParameters)
                            {
                                var value = arrayParameter.Values.Count > i ? arrayParameter.Values[i] : String.Empty;
                                parameters[arrayParameter.Name] = value;
                                fileSuffix.Append(arrayParameter.Name);
                                fileSuffix.Append("_");
                                fileSuffix.Append(value);
                            }
                            var fileName = Path.Combine(baseFolder, selectedTemplate.TemplateName + "_" + DateTime.Now.ToString("dd_MM_yyyy hh mm")) + "_" + fileSuffix.ToString();
                            GenerateFileFromScriptTemplate(fileName, selectedTemplate, parameters);
                        }
                    }
                    else
                    {
                        var fileName = Path.Combine(baseFolder, selectedTemplate.TemplateName + "_" + DateTime.Now.ToString("dd_MM_yyyy hh mm"));
                        Dictionary<string, object> parameters = PrepareUserParameters();
                        GenerateFileFromScriptTemplate(fileName, selectedTemplate, parameters);
                    }
                }
                else
                {
                    var fileName = Path.Combine(baseFolder, selectedTemplate.TemplateName + "_" + DateTime.Now.ToString("dd_MM_yyyy hh mm")) + ".docx";
                    var selected = SelectedQuery?.Results?.Where(q => q.Selected).ToList();
                    if (selected == null || selected.Count == 0)
                    {
                        return;
                    }

                    var template = selectedTemplate.WordTemplateFolderManager;
                    using (WordManipulator manipulator = new WordManipulator(fileName, true))
                    {
                        foreach (var workItemResult in selected)
                        {
                            var workItem = workItemResult.WorkItem;
                            manipulator.InsertWorkItem(workItem, template.GetTemplateFor(workItem.Type.Name), true);
                        }
                    }
                    ManageGeneratedWordFile(fileName);
                }
            }
            Status = $"Export Completed";
        }

        private void GenerateFileFromScriptTemplate(string fileName, TemplateInfo selectedTemplate, Dictionary<string, object> parameters)
        {
            var executor = new TemplateExecutor(selectedTemplate.WordTemplateFolderManager);
            var finalFileName = executor.GenerateFile(fileName, ConnectionManager.Instance, SelectedTeamProject.Name, parameters);
            ManageGeneratedWordFile(finalFileName);
        }

        private void ManageGeneratedWordFile(string fileName)
        {
            if (fileName.EndsWith(".docx"))
            {
                using (WordAutomationHelper helper = new WordAutomationHelper(fileName, false))
                {
                    helper.UpdateAllTocs();
                    if (GeneratePdf)
                    {
                        var pdfFile = helper.ConvertToPdf();
                        if (!String.IsNullOrEmpty(pdfFile))
                        {
                            System.Diagnostics.Process.Start(pdfFile);
                        }
                    }
                }
            }
            System.Diagnostics.Process.Start(fileName);
        }

        private Dictionary<string, object> PrepareUserParameters()
        {
            Dictionary<string, Object> parameters = new Dictionary<string, object>();
            foreach (var parameter in Parameters)
            {
                if (parameter.Type == "iterations")
                {
                    List<String> iterations = Iterations
                        .Where(i => i.Selected)
                        .Select(i => $"[System.IterationPath] = '{i.Path}'").ToList();
                    parameters[parameter.Name] = $"({String.Join(" OR ", iterations)})";
                }
                else
                {
                    parameters[parameter.Name] = parameter.Value;
                }
            }
            //now some standard parameter
            parameters["CurrentDate"] = DateTime.Now.ToString("dd/MM/yyyy");
            parameters["CurrentUser"] = ConnectionManager.Instance.GetAuthenticatedUser();
            return parameters;
        }

        private async Task PopulateQueries(String actualPath, WorkItemTrackingHttpClient witClient, IEnumerable<QueryHierarchyItem> queries)
        {
            foreach (var query in queries)
            {
                if (query.IsFolder != true)
                {
                    Queries.Add(new QueryViewModel(this, actualPath, query));
                }
                if (query.HasChildren == true)
                {
                    var newPath = actualPath + '/' + query.Name;
                    if (query.Children == null)
                    {
                        //need to requery the store to grab reference to the query.
                        var queryReloaded = await witClient.GetQueryAsync(SelectedTeamProject.Id, query.Path, depth: 2, expand: QueryExpand.Wiql);
                        await PopulateQueries(newPath, witClient, queryReloaded.Children);
                    }
                    else
                    {
                        await PopulateQueries(newPath, witClient, query.Children);
                    }
                }
            }
        }

        private void UpdateSelectionOfTemplate()
        {
            Parameters.Clear();
            ArrayParameters.Clear();
            ShowIterationParameters = false;
            SelectedTemplate = Templates.FirstOrDefault(t => t.IsSelected);

            if (SelectedTemplate == null)
            {
                return;
            }

            foreach (var selectedTemplate in Templates.Where(t => t.IsSelected))
            {
                if (selectedTemplate.IsScriptTemplate)
                {
                    foreach (var parameter in selectedTemplate.Parameters)
                    {
                        if (parameter.AllowedValues?.Length > 0)
                        {
                            parameter.Value = parameter.AllowedValues[0];
                        }
                        if (!Parameters.Any(p => p.Name == parameter.Name))
                        {
                            Parameters.Add(parameter);
                        }
                        if (parameter.Type.Equals("iterations", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowIterationParameters = true;
                        }
                    }

                    foreach (var parameter in selectedTemplate.ArrayParameters)
                    {
                        if (!ArrayParameters.Any(p => p.Name == parameter.Key))
                        {
                            ArrayParameters.Add(new ParameterViewModel(parameter.Key, "", parameter.Value, null));
                        }
                    }
                }
            }
        }
    }
}