using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.Storage.Pickers;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace doc_onlook
{
    
    public sealed partial class MainPage : Page
    {

        Workspace workspace;
        List<StorageFile> localFilesList;
        List<StorageFile> localFilesList_queried;
        List<string> _matchingNamesList;
        List<string> fileNameList;
        StreamSocketListener _listener;
        string IP_info;
        
        
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
            workspace.AddToList("hello.html");

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

            IDictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("name", "hello");
            dict.Add("type", ".html");
            dict.Add("data", "<html><body>Hello doc-onlook</body></html>");
            WriteUniqueFileToLocal(dict);

            fileNameList = new List<string>();
            _matchingNamesList = new List<string>();

            InitializeIPInfo();
        }

        class Workspace
        {
            private List<string> workspaceList { get; set; }
            private Pivot workspacePivot { get; set; }
            PdfDocument _pdfDocument;
            StackPanel _pdfStack;

            public async void AddToList(string fileName)
            {
                workspaceList.Add(fileName);
                PivotItem item = CreateWorkspaceItem(fileName);
                workspacePivot.Items.Add(item);

                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                SetPivotItemContent(workspaceList.Count - 1, file);
            }


            private PivotItem CreateWorkspaceItem(string fileName)
            {
                PivotItem newItem = new PivotItem();
                newItem.Height = workspacePivot.Height;
                newItem.Header = fileName;

                newItem.Content = new StackPanel();
                StackPanel stackPanel = (StackPanel)newItem.Content;
                stackPanel.Height = newItem.Height;
                stackPanel.Children.Add(new Image());

                return newItem;
            }

            public void RemoveFromList(string fileName)
            {
                workspaceList.Remove(fileName);
            }
            
            public void RemoveCurrent()
            {
                if (workspaceList.Count > 1)
                {
                    workspaceList.RemoveAt(workspacePivot.SelectedIndex);
                    workspacePivot.Items.RemoveAt(workspacePivot.SelectedIndex);
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
                return workspaceList.Count;
            }

            private async void NotifyUser(string message)
            {
                MessageDialog dialog = new MessageDialog(message);
                await dialog.ShowAsync();
            }

            private void SetScrollViewerProperties(ScrollViewer scrollView, StackPanel stackPanel)
            {
                scrollView.Height = stackPanel.Height;
                scrollView.Width = stackPanel.Width;

                scrollView.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollView.ZoomMode = ZoomMode.Enabled;

                scrollView.ViewChanged += PdfScrollView_ViewChanged;
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
                if (imageContainer.Children.Count<2)
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
                                if (i<pdfStackPanel.Children.Count)
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
                        SetScrollViewerProperties(scrollView, stackPanel);

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

                        SetScrollViewerProperties(pdfScrollView, stackPanel);
                        stackPanel.Children.Add(pdfScrollView);
                        StackPanel pdfStackPanel = new StackPanel();
                        pdfScrollView.Content = pdfStackPanel;
                        pdfStackPanel.Width = pdfScrollView.Width;
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
            
            private async void LoadPdf(PdfDocument pdfDocument, StackPanel stackPanel)
            {
                _pdfDocument = pdfDocument;
                _pdfStack = stackPanel;
                for (uint i = 0; i < pdfDocument.PageCount; i++)
                {
                    stackPanel.Children.Add(CreateImageContainer(pdfDocument.GetPage(i).Dimensions.ArtBox.Height, pdfDocument.GetPage(i).Dimensions.ArtBox.Width));
                }
            }

            public async void SetPivotItemContent(int index, StorageFile file)
            {
                try
                {
                    Debug.WriteLine("SetPivotItemContent");
                    PivotItem pivotItem = (PivotItem)workspacePivot.Items[index];
                    
                    StackPanel stackPanel = (StackPanel)pivotItem.Content;
                    pivotItem.Header = file.DisplayName;

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
                            LoadPdf(pdfDocument, ((StackPanel)(((ScrollViewer)(stackPanel.Children[0])).Content)));
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

            public void ShowDoc(StorageFile file)
            {
                Debug.WriteLine("ShowDoc");
                SetPivotItemContent(workspacePivot.SelectedIndex, file);
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
            Debug.WriteLine("UpdateLocalList");
            localFilesList = await GetLocalFiles();
            fileNameList.Clear();
            foreach (StorageFile file in localFilesList)
            {
                if (fileNameList.IndexOf(file.DisplayName) == -1)
                {
                    fileNameList.Add(file.DisplayName);
                }
            }
            LocalListView.ItemsSource = null;
            LocalListView.ItemsSource = localFilesList;
            LocalListView.SelectedIndex = 0;
        }

        public async void UpdateLocalList_Thread()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                List<StorageFile> items = await GetLocalFiles();
                LocalListView.ItemsSource = items;
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

        async private void SaveToDeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                StorageFile file = await GetLocalFile((string)((PivotItem)WorkspacePivot.SelectedItem).Header);
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
            catch(Exception exc)
            {
                MessageDialog dialog = new MessageDialog("Something occured: "+exc.ToString());
                await dialog.ShowAsync();
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
                default: NotifyUser("Can't write file");
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

        private async void DeleteLocalFile(string name)
        {
            Debug.WriteLine("DeleteLocalFile");
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.GetFileAsync(name);
                await file.DeleteAsync();
                fileNameList.Remove(name);
                UpdateLocalList();
            }
            catch(Exception e)
            {
                Debug.WriteLine("Delete Local File: "+ e.ToString());
            }
            
        }

        private void NewTabBtn_Tapped_1(object sender, TappedRoutedEventArgs e)
        {
            workspace.AddToList("hello.html");
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
            workspace.AddToList(fileName);
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
            

            var selectedItem = (PivotItem)WorkspacePivot.SelectedItem;
            string fileName = (string)selectedItem.Header;
            StorageFile file = await GetLocalFile(fileName);

            char[] splitter = { '.' };
            string[] contents = fileName.Split(splitter);
            request.Data.Properties.Title = contents[0];
            request.Data.Properties.Description = "Share the current file via DocOnlook.";
            
            switch (file.FileType)
            {
                case ".html":
                            string HTMLContent;
                            HTMLContent = await FileIO.ReadTextAsync(file);
                            string htmlFormat = HtmlFormatHelper.CreateHtmlFormat(HTMLContent);
                            request.Data.SetHtmlFormat(htmlFormat);
                            break;
                case ".jpg":
                            RandomAccessStreamReference imageStreamRef = RandomAccessStreamReference.CreateFromFile(file);
                            request.Data.SetBitmap(imageStreamRef);
                            break;
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

        private void DeleteBtn_Tapped(object sender, TappedRoutedEventArgs args)
        {
            Debug.WriteLine("DeleteBtn");
            StorageFile file = (StorageFile)LocalListView.SelectedItem;
            try{
                DeleteLocalFile(file.DisplayName + file.FileType);
            }catch(Exception e)
            {
                NotifyUser("Sorry, we couldn't delete the file: " + e.Message + ", " + e.HelpLink);
            }
        }

        private async void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            StorageFile file = (StorageFile)LocalListView.SelectedItem;
            await Launcher.LaunchFileAsync(file, new LauncherOptions
            {
                DisplayApplicationPicker = true,
                DesiredRemainingView = ViewSizePreference.UseHalf
            });

            return;

        }

        private void LocalListView_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (LocalListView.ItemsSource != null)
            {
                Debug.WriteLine("SelectionChanged");
                try
                {
                    var file = (StorageFile)LocalListView.SelectedItem;
                    workspace.ShowDoc(file);
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
    }
    
}
