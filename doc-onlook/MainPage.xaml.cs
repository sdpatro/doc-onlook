using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.Storage.Search;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using System.Net;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.UI.ViewManagement;
using Newtonsoft.Json;
using Windows.Networking.Connectivity;
using Windows.Data.Pdf;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Ideafixxxer.CsvParser;
using System.IO;
using Windows.ApplicationModel.Background;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace doc_onlook
{
    
    public sealed partial class MainPage : Page
    {

        Workspace workspace;
        List<StorageFile> localFilesList;
        List<StorageFile> localFilesList_queried;
        List<FileItem> fileItemList;
        List<string> _matchingNamesList;
        List<string> fileNameList;
        StreamSocketListener _listener;
        string IP_info;
        int _preSelectedIndex;
        StorageFile _preSelectedItem;


        public MainPage()
        {
            InitializeComponent();
            InitializeData();

            FillWorkspace();
            RunTCPListener();
        }

        public void FillWorkspace()
        {
            UpdateLocalList();
            workspace = new Workspace(WorkspacePivot);
            WorkspacePivot.Focus(FocusState.Pointer);
            
        }

        private void InitializeIPInfo()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();

            if (icp != null && icp.NetworkAdapter != null)
            {
                var hostNamesList = NetworkInformation.GetHostNames();
                string domainName = null;
                string v4Name = null;
                foreach (var entry in hostNamesList)
                {
                    if (entry.Type==Windows.Networking.HostNameType.DomainName && domainName==null)
                    {
                        domainName = entry.CanonicalName;
                    }
                    if(entry.Type == Windows.Networking.HostNameType.Ipv4 && v4Name == null)
                    {
                        v4Name = entry.CanonicalName;
                    }
                }
                if (domainName!=null && v4Name!=null)
                {
                    IPInfo.Text = domainName + " " + v4Name;
                    IP_info = IPInfo.Text;
                }
                else
                {
                    IPInfo.Text = "Couldn't initialize IP information";
                }
            }
            else
            {
                IPInfo.Text = "Couldn't initialize IP information";
            }
        }

        private void InitializeData()
        {
            localFilesList = new List<StorageFile>();
            localFilesList_queried = new List<StorageFile>();
            fileItemList = new List<FileItem>();

            IDictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("name", "hello");
            dict.Add("type", ".html");
            dict.Add("data", "<html><body>Hello doc-onlook</body></html>");
            WriteUniqueFileToLocal(dict);

            fileNameList = new List<string>();
            _matchingNamesList = new List<string>();
            InitializeIPInfo();

            RegisterBackgroundTask();
        }

        private bool RegisterBackgroundTask()
        {
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();
            builder.Name = "Background listener";
            builder.TaskEntryPoint = "BackgroundListener.SampleBackgroundTask";
            // Run every 1 minute if the device is on AC power 
            IBackgroundTrigger trigger = new MaintenanceTrigger(60, false);
            builder.SetTrigger(trigger);
            IBackgroundTaskRegistration task = builder.Register();
            return true;
        }
        

        class FileItem
        {
            public StorageFile file
            {
                get; set;
            }
            public string dateCreated
            {
                get; set;
            }
            public string fileDisplayName
            {
                get; set;
            }
            public string fileType
            {
                get; set;
            }

            public FileItem(StorageFile file)
            {
                this.file = file;
                dateCreated = file.DateCreated.LocalDateTime.ToString();
                fileDisplayName = file.DisplayName;
                fileType = file.FileType;
            }
            public static implicit operator FileItem(StorageFile s)
            {
                return new FileItem(s);
            }
            public static explicit operator StorageFile(FileItem f)
            {
                if (f == null)
                {
                    return null;
                }
                return f.file;
            }
        }

        class Workspace
        {
            private List<string> workspaceList { get; set; }
            private Pivot workspacePivot { get; set; }
            PdfDocument _pdfDocument;
            StackPanel _pdfStack;

            public void AddToList(StorageFile file)
            {
                Debug.WriteLine("AddToList: "+file.DisplayName+file.FileType);
                PivotItem item = CreateWorkspaceItem(file.DisplayName + file.FileType);
                workspacePivot.Items.Add(item);

                SetPivotItemContent(workspacePivot.Items.Count-1, file);
            }

            public int InList(string name)
            {
                for (var i=0; i<workspacePivot.Items.Count; i++)
                {
                    string s = GetPivotFileName((PivotItem)workspacePivot.Items[i]);
                    if (s == name)
                    {
                        return i;
                    }
                }
                return -1;
            }

            private PivotItem CreateWorkspaceItem(string fileName)
            {
                PivotItem newItem = new PivotItem();
                newItem.Height = workspacePivot.Height;
                newItem.Header = CreatePivotItemHeader(fileName.Split('.')[0], fileName.Split('.')[1]);

                newItem.Content = new StackPanel();
                StackPanel stackPanel = (StackPanel)newItem.Content;
                stackPanel.Height = newItem.Height;
                stackPanel.Children.Add(new Image());

                return newItem;
            }

            private StackPanel CreatePivotItemHeader(string fileDisplayName, string fileType)
            {
                StackPanel headerStack = new StackPanel();
                headerStack.Orientation = Orientation.Horizontal;
                TextBlock displayBlock = new TextBlock();
                displayBlock.Text = fileDisplayName;
                TextBlock typeBlock = new TextBlock();
                typeBlock.Text = fileType;
                typeBlock.FontSize = 12;
                headerStack.Children.Add(displayBlock);
                headerStack.Children.Add(typeBlock);
                return headerStack;
            }

            public void DeleteFromList(string s)
            {
                Debug.WriteLine("DeleteFromList");
                List<int> itemIndex = new List<int>();
                foreach(PivotItem item in workspacePivot.Items)
                {
                    if(GetPivotFileName(item) == s)
                    {
                        workspaceList.Remove(s);
                        workspacePivot.Items.Remove(item);
                    }
                }
            }


            private string GetPivotFileName(PivotItem pivotItem)
            {
                Debug.WriteLine("GetPivotFileName");
                string fileName = null;
                if(pivotItem != null)
                {
                    StackPanel stackHeader = (StackPanel)pivotItem.Header;
                    string fileDisplayName = ((TextBlock)stackHeader.Children[0]).Text;
                    string fileType = ((TextBlock)stackHeader.Children[1]).Text;
                    fileName = fileDisplayName + fileType;
                }
                return fileName;
            }
            
            public void RemoveCurrent()
            {

                if (workspacePivot.Items.Count > 1)
                {
                    int prevSelectedIndex = workspacePivot.SelectedIndex;
                    int newSelectedIndex = (prevSelectedIndex - 1);
                    if(newSelectedIndex == -1)
                    {
                        newSelectedIndex = workspacePivot.Items.Count - 2;
                    }
                    workspacePivot.Items.RemoveAt(prevSelectedIndex);
                    workspacePivot.SelectedIndex = newSelectedIndex;
                }
            }

            public void SetCurrent(int index)
            {
                if (index < workspaceList.Count)
                {
                    workspacePivot.SelectedIndex = index;
                }
            }

            public int GetPivotItemCount()
            {
                return workspacePivot.Items.Count;
            }

            private async void NotifyUser(string message)
            {
                MessageDialog dialog = new MessageDialog(message);
                await dialog.ShowAsync();
            }

            private void SetScrollViewerProperties(ScrollViewer scrollView, StackPanel stackPanel, string type)
            {
                scrollView.Height = stackPanel.Height;
                scrollView.Width = stackPanel.Width;

                scrollView.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollView.ZoomMode = ZoomMode.Enabled;

                switch(type)
                {
                    case "PDF": scrollView.ViewChanged += PdfScrollView_ViewChanged;
                        break;
                }
                
            }

            private ProgressRing CreateProgressRing()
            {
                ProgressRing progressRing = new ProgressRing();
                progressRing.Height = 100;
                progressRing.Width = 100;
                progressRing.IsActive = true;
                return progressRing;
            }

            private async void LoadPdfImage(int i, StackPanel imageContainer, Image img)
            {
                try
                {
                    if (imageContainer.Children.Count < 2)
                    {
                        Debug.WriteLine("Loading " + i.ToString());
                        imageContainer.Children.Add(CreateProgressRing());
                        PdfPage pdfPage = _pdfDocument.GetPage((uint)i);
                        var stream = new InMemoryRandomAccessStream();
                        PdfPageRenderOptions options = new PdfPageRenderOptions();
                        options.BitmapEncoderId = BitmapEncoder.JpegXREncoderId;
                        options.DestinationHeight = (uint)(0.8 * pdfPage.Dimensions.ArtBox.Height);
                        options.DestinationWidth = (uint)(0.8 * pdfPage.Dimensions.ArtBox.Width);
                        await pdfPage.RenderToStreamAsync(stream, options);
                        BitmapImage pdfImg = new BitmapImage();
                        pdfImg.SetSource(stream);
                        img.Source = pdfImg;
                        imageContainer.Height = pdfImg.PixelHeight;
                        imageContainer.Width = pdfImg.PixelWidth;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("LoadPdfImage: "+e.ToString());
                }
            }

            private void PdfScrollView_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
            {
                if (e.IsIntermediate == false)
                {
                    ScrollViewer pdfScrollView = (ScrollViewer)sender;
                    StackPanel pdfStackPanel = (StackPanel)pdfScrollView.Content;

                    var ttv = pdfScrollView.TransformToVisual(Window.Current.Content);
                    Point scrollViewCoords = ttv.TransformPoint(new Point(0, 0));

                    if ((pdfScrollView).Content != null)
                    {
                        for (var i = 0; i < pdfStackPanel.Children.Count; i++)
                        {
                            StackPanel imageContainer = (StackPanel)pdfStackPanel.Children[i];
                            var ttv2 = imageContainer.TransformToVisual(Window.Current.Content);
                            Point imageCoords = ttv2.TransformPoint(new Point(0, 0));
                            var img = ((Image)(imageContainer).Children[0]);

                            double imageBottom = imageCoords.Y + imageContainer.ActualHeight;
                            double imageTop = imageCoords.Y;
                            double scrollViewTop = scrollViewCoords.Y;
                            double scrollViewBottom = scrollViewCoords.Y+pdfScrollView.ActualHeight;

                            if(imageTop<scrollViewBottom && imageTop>scrollViewTop)
                            {
                                LoadPdfImage(i, imageContainer, img);
                                if (i<pdfStackPanel.Children.Count-1)
                                {
                                    LoadPdfImage(i + 1, (StackPanel)pdfStackPanel.Children[i + 1], ((Image)((StackPanel)pdfStackPanel.Children[i + 1]).Children[0]));
                                }
                                if (i>0)
                                {
                                    LoadPdfImage(i - 1, (StackPanel)pdfStackPanel.Children[i - 1], ((Image)((StackPanel)pdfStackPanel.Children[i - 1]).Children[0]));
                                }
                            }
                        }
                    }
                }
            }

            StackPanel CreateImageContainer(double height, double width)
            {
                StackPanel imageContainer = new StackPanel();
                imageContainer.Height = height;
                imageContainer.Width = width;
                Border myBorder2 = new Border();
                imageContainer.Background = new SolidColorBrush(Windows.UI.Colors.White);
                imageContainer.Margin = new Thickness(1);
                imageContainer.Children.Add(new Image());
                return imageContainer;
            }

            private UIElement SetContentType(StackPanel stackPanel, string type, uint count)
            {
                switch (type)
                {
                    case ".html":

                        stackPanel.Children.Clear();
                        WebView webView = new WebView();
                        webView.DOMContentLoaded += DOMContentLoaded;
                        stackPanel.Children.Add(webView);
                        return stackPanel.Children[0];

                    case ".jpg":
                        stackPanel.Children.Clear();
                        ScrollViewer scrollView = new ScrollViewer();
                        SetScrollViewerProperties(scrollView, stackPanel,"JPEG");

                        Image image = new Image();
                        image.SizeChanged += ScrollViewImage_Loaded;
                        image.Stretch = Stretch.Uniform;

                        scrollView.Content = image;                        
                        stackPanel.Children.Add(scrollView);           
                        return image;

                    case ".pdf":
                        stackPanel.Children.Clear();
                        ScrollViewer pdfScrollView = new ScrollViewer();
                        pdfScrollView.Width = stackPanel.Width;

                        SetScrollViewerProperties(pdfScrollView, stackPanel,"PDF");
                        stackPanel.Children.Add(pdfScrollView);
                        StackPanel pdfStackPanel = new StackPanel();
                        pdfScrollView.Content = pdfStackPanel;
                        pdfStackPanel.Width = pdfScrollView.Width;
                        return stackPanel;

                    case ".csv":
                        stackPanel.Children.Clear();
                        ScrollViewer csvScrollView = new ScrollViewer();
                        SetScrollViewerProperties(csvScrollView, stackPanel, "CSV");
                        stackPanel.Children.Add(csvScrollView);
                        ListView csvListView = new ListView();
                        csvScrollView.Content = csvListView;
                        return stackPanel;

                    case ".txt":
                        stackPanel.Children.Clear();
                        ScrollViewer txtScrollView = new ScrollViewer();
                        SetScrollViewerProperties(txtScrollView, stackPanel, "TXT");
                        stackPanel.Children.Add(txtScrollView);
                        TextBlock txtBlock = new TextBlock();
                        txtScrollView.Content = txtBlock;
                        return stackPanel;

                    default:
                        return null;
                }
            }

            private void ScrollViewImage_Loaded(object sender, RoutedEventArgs e)
            {
                Image image = (Image)sender;
                ScrollViewer scrollView = (ScrollViewer)image.Parent;
                StackPanel stackPanel = (StackPanel)scrollView.Parent;

                if (image.ActualHeight > image.ActualWidth)
                {
                    scrollView.ZoomToFactor((float)scrollView.ActualHeight / (float)image.ActualHeight);
                }
                else
                {
                    scrollView.ZoomToFactor((float)scrollView.ViewportWidth / (float)image.ActualWidth);
                }

                scrollView.Height = ((PivotItem)(workspacePivot.Items[workspacePivot.SelectedIndex])).ActualHeight - 50; 
            }

            private void PdfScrollViewImage_Loaded(object sender, RoutedEventArgs e)
            {
                Image image = (Image)sender;
                PivotItem pivotItem = (PivotItem)workspacePivot.Items[workspacePivot.SelectedIndex];
                ScrollViewer pdfScrollView = (ScrollViewer)(((StackPanel)pivotItem.Content).Children[0]);

                if (image.ActualHeight > image.ActualWidth)
                {
                    pdfScrollView.ZoomToFactor((float)pdfScrollView.ActualHeight / (float)image.ActualHeight);
                }
                else
                {
                    pdfScrollView.ZoomToFactor((float)pdfScrollView.ViewportWidth / (float)image.ActualWidth);
                }
                NotifyUser(pdfScrollView.ActualHeight + "," + image.ActualHeight);
                pdfScrollView.Height = ((PivotItem)(workspacePivot.Items[workspacePivot.SelectedIndex])).ActualHeight - 50;
            }

            private void DOMContentLoaded(WebView webView, WebViewDOMContentLoadedEventArgs args)
            {
                try
                {
                    webView.Height = ((PivotItem)(workspacePivot.Items[workspacePivot.SelectedIndex])).ActualHeight - 50;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("DOMContentLoaded " + e.ToString());
                }
            }
            
            private void LoadPdf(PdfDocument pdfDocument, StackPanel stackPanel, ScrollViewer pdfScrollView)
            {
                _pdfDocument = pdfDocument;
                _pdfStack = stackPanel;
                for (uint i = 0; i < pdfDocument.PageCount; i++)
                {
                    stackPanel.Children.Add(CreateImageContainer(pdfDocument.GetPage(i).Dimensions.ArtBox.Height, pdfDocument.GetPage(i).Dimensions.ArtBox.Width));
                }
                LoadPdfImage(0, (StackPanel)stackPanel.Children[0], (Image)((StackPanel)(stackPanel.Children[0])).Children[0]);
                pdfScrollView.Loaded += PdfScrollView_Loaded;
            }

            private void PdfScrollView_Loaded(object sender, RoutedEventArgs e)
            {
                var pdfScrollView = (ScrollViewer)sender;
                var panelWidth = ((StackPanel)pdfScrollView.Content).ActualWidth;
                pdfScrollView.ZoomToFactor((float)pdfScrollView.ActualWidth / (float)panelWidth);
            }

            public async void SetPivotItemContent(int index, StorageFile file)
            {
                try
                {
                    Debug.WriteLine("SetPivotItemContent");
                    PivotItem pivotItem = (PivotItem)workspacePivot.Items[index];
                    
                    StackPanel stackPanel = (StackPanel)pivotItem.Content;
                    pivotItem.Header = CreatePivotItemHeader(file.DisplayName.Split('.')[0],file.FileType);

                    switch (file.FileType)
                    {
                        case ".html":
                            Debug.WriteLine("SetPivotItemContent .html");
                            WebView webView = (WebView)SetContentType(stackPanel, ".html", 0);
                            var buffer = await FileIO.ReadTextAsync(file);
                            webView.NavigateToString(buffer);
                            break;
                        case ".jpg":
                            Debug.WriteLine("SetPivotItemContent .jpg");
                            Image image = (Image)SetContentType(stackPanel, ".jpg", 0);
                            var fileStream = await file.OpenAsync(FileAccessMode.Read);
                            var img = new BitmapImage();
                            img.SetSource(fileStream);
                            image.Source = img;
                            break;
                        case ".pdf":
                            Debug.WriteLine("SetPivotItemContent .pdf");
                            PdfDocument pdfDocument = await PdfDocument.LoadFromFileAsync(file);
                            stackPanel = (StackPanel)SetContentType(stackPanel, ".pdf", pdfDocument.PageCount);
                            LoadPdf(pdfDocument, ((StackPanel)(((ScrollViewer)(stackPanel.Children[0])).Content)), (ScrollViewer)(stackPanel.Children[0]));
                            break;
                        case ".csv":
                            Debug.WriteLine("SetPivotItemContent .csv");                            
                            stackPanel = (StackPanel)SetContentType(stackPanel, ".csv", 0);
                            LoadCsv((ListView)(((ScrollViewer)(stackPanel.Children[0])).Content),file);
                            break;
                        case ".txt":
                            Debug.WriteLine("SetPivotItemContent .txt");
                            stackPanel = (StackPanel)SetContentType(stackPanel, ".txt", 0);
                            LoadTxt((TextBlock)(((ScrollViewer)stackPanel.Children[0]).Content), file);
                            break;
                        default:
                            NotifyUser("Error: File extension not found.");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
                
            }

            private async void LoadTxt(TextBlock textBlock, StorageFile file)
            {
                string txtContent = await FileIO.ReadTextAsync(file);
                textBlock.Text = txtContent;
            }

            private async void LoadCsv(ListView csvListView,StorageFile file)
            {
                string csvText = await FileIO.ReadTextAsync(file);
                TextReader reader = new StringReader(csvText);
                CsvParser csvParser = new CsvParser();
                string[][] results = csvParser.Parse(reader);
                List<string> resultsList = new List<string>();
                foreach(string[] result in results)
                {
                    string resultListItem = "";
                    foreach(string resultColValue in result)
                    {
                        resultListItem += resultColValue + "    ";
                    }
                    resultsList.Add(resultListItem);
                }
                csvListView.ItemsSource = resultsList;               
            }

            public void ShowDoc(StorageFile file)
            {
                Debug.WriteLine("ShowDoc");
                SetPivotItemContent(workspacePivot.SelectedIndex, file);
            }

            public string GetCurrentDoc()
            {
                Debug.WriteLine("GetCurrentDoc");
                StackPanel header = (StackPanel)(((PivotItem)(workspacePivot.SelectedItem)).Header);
                string fileDisplayName = ((TextBlock)(header.Children[0])).Text;
                string fileType = ((TextBlock)(header.Children[1])).Text;
                return fileDisplayName + fileType;
            }

            public Workspace(Pivot workspacePivot)
            {
                var bounds = Window.Current.Bounds;

                this.workspacePivot = workspacePivot;
                this.workspacePivot.Height = bounds.Height;
                workspaceList = new List<string>();
            }
        };


        // Fill the local files list:
        public async void UpdateLocalList()
        {

            var preSelectedFile = (StorageFile)((FileItem)(LocalListView.SelectedItem));
            string preSelectedFileName = "";
            if (preSelectedFile != null)
            {
                preSelectedFileName = preSelectedFile.DisplayName + preSelectedFile.FileType;
            }

            Debug.WriteLine("UpdateLocalList");
            localFilesList = await GetLocalFiles();
            fileNameList.Clear();

            var i = 0;
            var nextSelectedIndex = 0;

            foreach (StorageFile file in localFilesList)
            {
                if (fileNameList.IndexOf(file.DisplayName) == -1)
                {
                    fileNameList.Add(file.DisplayName);
                }
                if ((file.DisplayName + file.FileType) == preSelectedFileName)
                {
                    nextSelectedIndex = i;
                }
                i++;
            }


            LocalListView.ItemsSource = null;
            fileItemList.Clear();
            foreach(StorageFile item in localFilesList)
            {
                fileItemList.Add(new FileItem(item));
            }
            LocalListView.ItemsSource = fileItemList;
            LocalListView.SelectedIndex = nextSelectedIndex;

        }

        public async void UpdateLocalList_Thread()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                UpdateLocalList();
            });
        }
        

        public async Task<List<StorageFile>> GetLocalFiles()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFileQueryResult queryResult =  localFolder.CreateFileQuery();
            IReadOnlyList<StorageFile> fileList = await queryResult.GetFilesAsync();
            List<StorageFile> localList = new List<StorageFile>();
            foreach (StorageFile file in fileList)
            {
                localList.Add(file);
            }
            return localList;
        }

        async private void SaveToDeviceBtn_Click(object sender, RoutedEventArgs args)
        {

            if(LocalListView.SelectionMode == ListViewSelectionMode.Single)
            {
                try
                {
                    FileSavePicker savePicker = new FileSavePicker();
                    savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    StorageFile file = (StorageFile)((FileItem)(LocalListView.SelectedItem));
                    savePicker.FileTypeChoices.Add(file.DisplayType, new List<string>() { file.FileType });
                    savePicker.SuggestedFileName = file.DisplayName;

                    StorageFile newFile = await savePicker.PickSaveFileAsync();
                    if (file != null)
                    {
                        await file.CopyAndReplaceAsync(newFile);
                        NotifyUser("File copied successfully");
                    }
                    else
                    {
                        MessageDialog dialog = new MessageDialog("Operation cancelled.");
                        await dialog.ShowAsync();
                    }
                }
                catch (Exception e)
                {
                    MessageDialog dialog = new MessageDialog("Something occured: " + e.ToString());
                    await dialog.ShowAsync();
                    return;
                }
            }
            else
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    try
                    {
                        var folderPicker = new FolderPicker();
                        folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                        folderPicker.FileTypeFilter.Add(".jpeg");
                        folderPicker.ViewMode = PickerViewMode.Thumbnail;
                        folderPicker.SettingsIdentifier = "FolderPicker";
                        var folder = await folderPicker.PickSingleFolderAsync();
                        var i = 1;
                        foreach (FileItem file in LocalListView.SelectedItems)
                        {
                            StorageFile storageItem = (StorageFile)file;
                            await storageItem.CopyAsync(folder);
                            i++;
                        }
                        NotifyUser("All files saved successfully.");
                    }
                    catch (Exception e)
                    {
                        NotifyUser("Something occured: " + e.ToString());
                        return;
                    }
                });
                
            }

        }

        public async Task<StorageFile> GetLocalFile(string fileName)
        {
            try
            {
                StorageFile localFile = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                return localFile;
            }
            catch(Exception exc){
                Debug.WriteLine("Exception: " + exc.ToString());
                return null;
            }
            
        }

        public async void RunTCPListener()
        {
            _listener = new StreamSocketListener();
            await _listener.BindServiceNameAsync("2112");
            _listener.ConnectionReceived += OnConnection;
        }

        public async void NotifyUser(string Message)
        {
            MessageDialog dialog = new MessageDialog(Message);
            await dialog.ShowAsync();
        }

        public async void NotifyUser_Thread(string Message)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                MessageDialog dialog = new MessageDialog(Message);
                dialog.ShowAsync();
            });
        }        

        public async void UpdateFileReceptionStatus(string status)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                FileReceptionIndicator.Text = status;
            });
        }


        public int GetContentLength(string content)
        {
            Regex regex = new Regex("Content-Length: ([0-9]*)");
            Match match = regex.Match(content);
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 0;   
        }

        public IDictionary<string,string> ParseContent(string content)
        {
            Regex r = new Regex("([A-Za-z]*)=([^&]*)");
            IDictionary<string, string> contentDict = new Dictionary<string,string>();
            
            Match m = r.Match(content);
            while (m.Success)
            {
                contentDict.Add(new KeyValuePair<string, string>(WebUtility.UrlDecode(m.Groups[1].Value), WebUtility.UrlDecode(m.Groups[2].Value)));
                m = m.NextMatch();
            }

            return contentDict;

        }

        public async void WriteFileToLocal(IDictionary<string,string> ContentData)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            
            StorageFile newFile = await localFolder.CreateFileAsync(ContentData["name"] + ContentData["type"], CreationCollisionOption.GenerateUniqueName);

            switch (ContentData["type"])
            {
                case ".csv":
                case ".txt":
                case ".html": using (IRandomAccessStream textStream = await newFile.OpenAsync(FileAccessMode.ReadWrite))
                              {
                                    using (DataWriter textWriter = new DataWriter(textStream))
                                    {
                                        textWriter.WriteString(ContentData["data"]);
                                        await textWriter.StoreAsync();
                                    }
                              }
                            break;
                case ".pdf":
                case ".jpg":   using (IRandomAccessStream textStream = await newFile.OpenAsync(FileAccessMode.ReadWrite))
                                {
                                    using (DataWriter textWriter = new DataWriter(textStream))
                                    {
                                        var bytes = Convert.FromBase64String(ContentData["data"]);
                                        textWriter.WriteBytes(bytes);
                                        await textWriter.StoreAsync();
                                    }
                                }
                            break;

                default: NotifyUser_Thread("Can't write file");
                    break;
            }

            
            
        }

        public async void WriteUniqueFileToLocal(IDictionary<string, string> ContentData)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            StorageFile newFile = await localFolder.CreateFileAsync(ContentData["name"] + ContentData["type"], CreationCollisionOption.ReplaceExisting);

            using (IRandomAccessStream textStream = await newFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (DataWriter textWriter = new DataWriter(textStream))
                {
                    textWriter.WriteString(ContentData["data"]);
                    await textWriter.StoreAsync();
                }
            }

        }

        private string BuildPostResponse(string responseType, string fileSize, string timeTaken)
        {
            string responseString = "HTTP/1.1 200 OK";
            responseString += "\r\nAccess-Control-Allow-Origin: *";
            responseString += "\r\nContent-Type: application/x-www-form-urlencoded; charset=UTF-8";
            responseString += "\r\n";
            responseString += "\r\n";

            Dictionary<string, string> dict = new Dictionary<string, string>();

            switch (responseType)
            {
                case "FIND_DEVICE": dict["Message"] = "SUCCESS";
                    dict["DeviceInfo"] = IP_info;
                    break;

                case "SEND_FILE":
                    dict["Message"] = "SUCCESS";
                    dict["FileSize"] = fileSize;
                    dict["TimeTaken"] = timeTaken;
                    break;
            }
            
            responseString += JsonConvert.SerializeObject(dict);
            return responseString;
        }


        private async void OnConnection( StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            try
            {
                using (IInputStream inStream = args.Socket.InputStream)
                {

                    UpdateFileReceptionStatus("Received a connection.");
                    DataReader reader = new DataReader(inStream);
                    reader.InputStreamOptions = InputStreamOptions.Partial;
                    int contentLength = 0;
                    uint numReadBytes;
                    string totalContent = "";
                    DateTime timeStart = DateTime.Now;

                    IOutputStream outStream = args.Socket.OutputStream;

                    do
                    {
                        numReadBytes = await reader.LoadAsync(1 << 20);

                        if (numReadBytes > 0)
                        {
                            byte[] tmpBuf = new byte[numReadBytes];
                            reader.ReadBytes(tmpBuf);
                            string result = Encoding.UTF8.GetString(tmpBuf).TrimEnd('\0');
                            string[] contents = Regex.Split(result, "\r\n\r\n");
                            if (GetContentLength(result) != 0)
                            {
                                contentLength = GetContentLength(result);
                            }
                            string content;
                            if (contents.Length > 1)
                            {
                                content = contents[1];
                            }
                            else
                            {
                                content = contents[0];
                            }
                            totalContent += content;
                            UpdateFileReceptionStatus("Receiving: " + (totalContent.Length * 100 / contentLength) + "%");
                            if (totalContent.Length == contentLength)
                            {

                                UpdateFileReceptionStatus("Processing the stuff received...");
                                var ContentData = ParseContent(totalContent);
                                string param_fileSize = "";
                                string param_timeTaken = "";
                                if (ContentData["action"] == "FIND_DEVICE")
                                {
                                    UpdateFileReceptionStatus("Pinged by a sender.");
                                }
                                else
                                {
                                    param_fileSize = (ContentData["data"].Length).ToString();
                                    TimeSpan diff = DateTime.Now - timeStart;
                                    param_timeTaken = diff.TotalSeconds.ToString();
                                    UpdateFileReceptionStatus("Writing to your storage...");
                                    WriteFileToLocal(ContentData);
                                    UpdateFileReceptionStatus("Done!");
                                    UpdateLocalList_Thread();
                                }

                                IBuffer replyBuff = Encoding.ASCII.GetBytes(BuildPostResponse(ContentData["action"],param_fileSize,param_timeTaken)).AsBuffer();
                                await outStream.WriteAsync(replyBuff);
                                reader.DetachStream();
                                args.Socket.Dispose();
                                break;
                            }
                        }
                    } while (true);
                }
            }
            catch(Exception e)
            {
                NotifyUser_Thread(e.StackTrace);
            }
        }

        private void NewTabBtn_Tapped_1(object sender, TappedRoutedEventArgs e)
        {
            if(LocalListView.SelectionMode == ListViewSelectionMode.Single)
            {
                workspace.AddToList((StorageFile)((FileItem)(LocalListView.SelectedItem)));
            }
            else
            {
                foreach (FileItem fileItem in LocalListView.SelectedItems)
                {
                    StorageFile file = (StorageFile)fileItem;
                    string fileName = file.DisplayName + file.FileType;
                    if (workspace.InList(fileName) == -1)
                    {
                        workspace.AddToList(file);
                    }
                }

                LocalListView.SelectionMode = ListViewSelectionMode.Single;
                LocalListView.SelectedIndex = _preSelectedIndex;
            }
        }

        private void CloseTabBtn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            workspace.RemoveCurrent();
        }

        private void DragPanelStatus(bool status)
        {
            if (status == true)
            {
                DragPanel.Opacity = 0.7;
                DragPanel.SetValue(Canvas.ZIndexProperty, 1);
            }
            else
            {
                DragPanel.Opacity = 0;
                DragPanel.SetValue(Canvas.ZIndexProperty, -1);
            }
        }

        private void WorkspacePivot_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void WorkspacePivot_Drop(object sender, DragEventArgs e)
        {
            var fileName = await e.DataView.GetTextAsync();
            StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
            workspace.AddToList(file);
            WorkspacePivot.SelectedIndex = WorkspacePivot.Items.Count - 1;
            DragPanelStatus(false);
        }

        private void LocalListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            DragPanelStatus(false);
        }
        private void ListView_DragStarting(object sender, DragItemsStartingEventArgs e)
        {
            e.Data.SetText(((StorageFile)e.Items[0]).DisplayName+((StorageFile)e.Items[0]).FileType);
            e.Data.RequestedOperation = DataPackageOperation.Copy;
            DragPanelStatus(true);
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _matchingNamesList.Clear();
            foreach(string name in fileNameList)
            {
                if ((name.ToLower()).Contains((SuggestBox.Text).ToLower()))
                {
                    _matchingNamesList.Add(name);
                }
            }
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                sender.ItemsSource = null;
                sender.ItemsSource = _matchingNamesList;
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var submittedName = args.QueryText;
            localFilesList_queried.Clear();
            foreach(StorageFile file in localFilesList)
            {
                if (file.DisplayName.Contains(submittedName))
                    localFilesList_queried.Add(file);
            }
            LocalListView.ItemsSource = null;
            LocalListView.ItemsSource = localFilesList_queried;
            ((SymbolIcon)LoadAll.Content).Symbol = Symbol.Back;
        }

        private async void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            var selectedName = args.SelectedItem.ToString();
            foreach (StorageFile storageFileItem in localFilesList)
            {
                if (storageFileItem.DisplayName == selectedName)
                {
                    WorkspacePivot.Focus(FocusState.Pointer);
                    StorageFile file = await GetLocalFile(selectedName+storageFileItem.FileType);
                    workspace.ShowDoc(file);
                    break;
                }
            }
        }

        private void LoadAll_Tapped(object sender, TappedRoutedEventArgs e)
        {
            UpdateLocalList();
            ((SymbolIcon)LoadAll.Content).Symbol = Symbol.Refresh;
        }

        private void ShareMailBtn_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += ShareMailHandler;
            DataTransferManager.ShowShareUI();
        }

        private async void ShareMailHandler(DataTransferManager sender, DataRequestedEventArgs e)
        {
            DataRequest request = e.Request;
            DataRequestDeferral deferral = request.GetDeferral();

            if(LocalListView.SelectionMode == ListViewSelectionMode.Single)
            {

                StorageFile file = (StorageFile)((FileItem)(LocalListView.SelectedItem));
                request.Data.Properties.Title = file.DisplayName;
                request.Data.Properties.Description = "Share the current file via DocOnlook.";

                switch (file.FileType)
                {
                    case ".txt":
                    case ".html":
                        string HTMLContent;
                        HTMLContent = await FileIO.ReadTextAsync(file);
                        string htmlFormat = HtmlFormatHelper.CreateHtmlFormat(HTMLContent);
                        request.Data.SetHtmlFormat(htmlFormat);
                        break;
                    case ".png":
                    case ".jpg":
                        RandomAccessStreamReference imageStreamRef = RandomAccessStreamReference.CreateFromFile(file);
                        request.Data.SetBitmap(imageStreamRef);
                        break;
                    case ".csv":
                    case ".pdf":
                        List<StorageFile> storageList = new List<StorageFile>();
                        storageList.Add(file);
                        request.Data.SetStorageItems(storageList);
                        break;
                }
            }
            else
            {
                List<StorageFile> fileList = new List<StorageFile>();
                foreach(FileItem fileItem in LocalListView.SelectedItems)
                {
                    StorageFile file = (StorageFile)fileItem;
                    fileList.Add(file);
                    request.Data.Properties.Title += file.DisplayName + "; ";
                }
                request.Data.Properties.Description = "Share files via DocOnlook.";
                request.Data.SetStorageItems(fileList);
            }
           
            deferral.Complete();
        }

        private void ShowDebugInfoBtn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            PivotItem pivotItem = (PivotItem)WorkspacePivot.Items[0];
            StackPanel stackPanel = (StackPanel)pivotItem.Content;
            Image image = (Image)(((ScrollViewer)stackPanel.Children[0]).Content);
            NotifyUser(image.Height + " " + image.Width + " " + image.ActualHeight + " " + image.ActualWidth);
        }

        private async void DeleteBtn_Tapped(object sender, TappedRoutedEventArgs args)
        {
            Debug.WriteLine("DeleteBtn");
            if (LocalListView.SelectionMode == ListViewSelectionMode.Single)
            {
                StorageFile file = (StorageFile)((FileItem)(LocalListView.SelectedItem));
                try
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    workspace.DeleteFromList(file.DisplayName + file.FileType);
                }
                catch (Exception e)
                {
                    NotifyUser("Sorry, we couldn't delete the file: " + e.Message + ", " + e.HelpLink);
                    return;
                }
                NotifyUser("File deleted successfully.");
                UpdateLocalList();
            }
            else
            {
                int count = LocalListView.SelectedItems.Count;
                try
                {
                    List<string> deleteFileNames = new List<string>();
                    foreach (FileItem fileItem in LocalListView.SelectedItems)
                    {
                        StorageFile file = (StorageFile)fileItem;
                        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        workspace.DeleteFromList(file.DisplayName + file.FileType);
                    }
                }
                catch (Exception e)
                {
                    NotifyUser("Sorry, we couldn't delete the files: " + e.Message + ", " + e.HelpLink);
                    return;
                }
                NotifyUser(count + " file(s) deleted successfully.");
                LocalListView.SelectionMode = ListViewSelectionMode.Single;
                UpdateLocalList();
            }
        }

        private async void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if(LocalListView.SelectionMode == ListViewSelectionMode.Single)
            {
                StorageFile file = (StorageFile)((FileItem)(LocalListView.SelectedItem));
                await Launcher.LaunchFileAsync(file, new LauncherOptions
                {
                    DisplayApplicationPicker = true,
                    DesiredRemainingView = ViewSizePreference.UseHalf
                });
                LauncherOptions options = new LauncherOptions();
                return;
            }
        }

        private void LocalListView_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            Debug.WriteLine("SelectionChanged");
            if (LocalListView.ItemsSource!=null && LocalListView.SelectionMode!=ListViewSelectionMode.Multiple && LocalListView.SelectedIndex!=-1)
            {
                try
                {
                    FileItem fileItem = (FileItem)((FileItem)(LocalListView.SelectedItem));
                    var file = (StorageFile)((FileItem)((FileItem)(LocalListView.SelectedItem)));
                    if (workspace.GetPivotItemCount() == 0)
                    {
                        workspace.AddToList(file);
                    }
                    else if (workspace.GetCurrentDoc() != (file.DisplayName + file.FileType))
                    {
                        Debug.WriteLine("GetCurrentDoc: " + workspace.GetCurrentDoc());
                        workspace.ShowDoc(file);
                    }
                }
                catch (Exception e)
                {
                    NotifyUser("Sorry, we couldn't open the file: " + e.Message + ", " + e.HelpLink);
                }
            }
        }

        public class PdfPageListItem
        {
            public BitmapImage BmImage { get; set; }
        }

        private void testOutput_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var pdfScrollView = (ScrollViewer)(((StackPanel)(((PivotItem)(WorkspacePivot.SelectedItem)).Content)).Children[0]);
            NotifyUser(pdfScrollView.ActualWidth.ToString());
        }

        private void WorkspacePivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Pivot_SelectionChanged");
            if (WorkspacePivot.Items.Count > 0)
            {
                var header = (StackPanel)(((PivotItem)WorkspacePivot.SelectedItem).Header);
                var fileDisplayName = ((TextBlock)header.Children[0]).Text;
                var fileType = ((TextBlock)header.Children[1]).Text;
                string fileName = fileDisplayName + fileType;
                foreach (FileItem fileItem in LocalListView.Items)
                {
                    StorageFile storageItem = (StorageFile)fileItem;
                    if (storageItem.DisplayName + storageItem.FileType == fileName)
                    {
                        LocalListView.SelectedIndex = LocalListView.Items.IndexOf(fileItem);
                    }
                }
            }
        }

        private void MultiSelect_Click(object sender, RoutedEventArgs e)
        {
            if (LocalListView.SelectionMode == ListViewSelectionMode.Single)
            {
                _preSelectedIndex = LocalListView.SelectedIndex;
                LocalListView.SelectionMode = ListViewSelectionMode.Multiple;
                LocalListView.SelectedIndex = _preSelectedIndex;
            }
            else
            {
                LocalListView.SelectionMode = ListViewSelectionMode.Single;
                LocalListView.SelectedIndex = _preSelectedIndex;
            }
        }

        private void Collapse_Click(object sender, RoutedEventArgs e)
        {

        }

        private void FileCommandBar_Opening(object sender, object e)
        {
            CommandBar bar = (CommandBar)sender;
            AppBarToggleButton collapseBtn = (AppBarToggleButton)bar.Content;
            collapseBtn.Label = "Toggle files";
        }

        private void FileCommandBar_Closed(object sender, object e)
        {
            CommandBar bar = (CommandBar)sender;
            AppBarToggleButton collapseBtn = (AppBarToggleButton)bar.Content;
            collapseBtn.Label = "";
        }

    }
    
}
