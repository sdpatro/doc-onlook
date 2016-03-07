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
        

        public MainPage()
        {
            
            InitializeData();
            InitializeComponent();
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
        }

        class Workspace
        {
            private List<string> workspaceList { get; set; }
            private Pivot workspacePivot { get; set; }

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
                newItem.Header = fileName;
                newItem.Content = new StackPanel();
                StackPanel stackPanel = (StackPanel)newItem.Content;
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

            private UIElement SetContentType(StackPanel stackPanel, string type)
            {
                switch (type)
                {
                    case ".html": WebView webView = new WebView();
                        webView.Height = 500;
                            stackPanel.Children[0] = webView;
                        return stackPanel.Children[0];
                    case ".jpg":
                        ScrollViewer scrollView = new ScrollViewer();

                        scrollView.Height = 500;
                        scrollView.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                        scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        scrollView.ZoomMode = ZoomMode.Enabled;

                        Image image = new Image();
                        image.SizeChanged += ScrollViewImage_Loaded;
                        image.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
                        scrollView.Content = image;
                        
                        stackPanel.Children[0] = scrollView;           
                        return image;
                    default:
                        return null;
                }
            }

            private void ScrollViewImage_Loaded(object sender, RoutedEventArgs e)
            {
                Image image = (Image)sender;
                ScrollViewer scrollView = (ScrollViewer)image.Parent;

                if(image.ActualHeight > image.ActualWidth)
                {
                    scrollView.ZoomToFactor((float)scrollView.ViewportHeight / (float)image.ActualHeight);
                }
                else
                {
                    scrollView.ZoomToFactor((float)scrollView.ViewportWidth / (float)image.ActualWidth);
                }
            }

            public async void SetPivotItemContent(int index, StorageFile file)
            {
                PivotItem pivotItem = (PivotItem)workspacePivot.Items[index];
                
                StackPanel stackPanel = (StackPanel)pivotItem.Content;
                pivotItem.Header = file.DisplayName;

                switch (file.FileType)
                {
                    case ".html":
                                WebView webView = (WebView)SetContentType(stackPanel,".html");
                                var buffer = await FileIO.ReadTextAsync(file);
                                webView.NavigateToString(buffer);
                                break;
                    case ".jpg":
                                Image image = (Image)SetContentType(stackPanel,".jpg");
                                var fileStream = await file.OpenAsync(FileAccessMode.Read);
                                var img = new BitmapImage();
                                img.SetSource(fileStream);
                                image.Source = img;
                                break;
                    default: NotifyUser("Error: File extension not found.");
                        break;
                }
                
            }

            public void ShowDoc(StorageFile file)
            {
                SetPivotItemContent(workspacePivot.SelectedIndex, file);
            }

            public Workspace(Pivot workspacePivot)
            {
                this.workspacePivot = workspacePivot;
                workspaceList = new List<string>();
            }
        };



        // Fill the local files list:
        public async void UpdateLocalList()
        {
            localFilesList = await GetLocalFiles();
            foreach (StorageFile file in localFilesList)
            {
                if(fileNameList.IndexOf(file.DisplayName) == -1)
                {
                    fileNameList.Add(file.DisplayName);
                }
            }
            LocalListView.ItemsSource = localFilesList;
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

        private async void FileListItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            StackPanel fileItem = (StackPanel)sender;
            string fileName = ((TextBlock)fileItem.Children[0]).Text;
            string fileType = ((TextBlock)fileItem.Children[1]).Text;
            
            StorageFile localFile = await GetLocalFile(fileName+fileType);
            workspace.ShowDoc(localFile);
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
            StreamSocketListener listener = new StreamSocketListener();
            await listener.BindServiceNameAsync("2112");
            listener.ConnectionReceived += OnConnection;
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


        private async void OnConnection( StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            using (IInputStream inStream = args.Socket.InputStream)
            {
                UpdateFileReceptionStatus("Receving new file...");
                DataReader reader = new DataReader(inStream);
                reader.InputStreamOptions = InputStreamOptions.Partial;
                int contentLength = 0;
                uint numReadBytes;
                string totalContent = "";

                IOutputStream outStream = args.Socket.OutputStream;

                do
                {
                    numReadBytes = await reader.LoadAsync(1 << 20);  

                    if (numReadBytes > 0)
                    {
                        byte[] tmpBuf = new byte[numReadBytes];
                        reader.ReadBytes(tmpBuf);
                        string result = Encoding.UTF8.GetString(tmpBuf).TrimEnd('\0');
                        string[] contents = Regex.Split(result,"\r\n\r\n");
                        if (GetContentLength(result)!= 0)
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
                        UpdateFileReceptionStatus("Receiving: "+(totalContent.Length*100 / contentLength) + "%");
                        if (totalContent.Length == contentLength)
                        {
                            var ContentData = ParseContent(totalContent);
                            UpdateFileReceptionStatus("File received.");
                            WriteFileToLocal(ContentData);
                            UpdateLocalList_Thread();
                            string responseString = "HTTP/1.1 200 OK";
                            tmpBuf = Encoding.ASCII.GetBytes(responseString);
                            IBuffer replyBuff = tmpBuf.AsBuffer();
                            await outStream.WriteAsync(replyBuff);
                            break;
                        }
                        
                    }
                } while (numReadBytes > 0);
            }
        }

        private async void DeleteLocalFile(string name)
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.GetFileAsync(name);
                await file.DeleteAsync();
                fileNameList.Remove(name);
                NotifyUser("File deleted.");
                
                UpdateLocalList();
            }
            catch(Exception e)
            {
                NotifyUser("Exception: " + e.ToString());
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
                DragPanel.Opacity = 0.2;
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

        private void DeleteBtn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            PivotItem pivotItem = (PivotItem)WorkspacePivot.Items[WorkspacePivot.SelectedIndex];
            string fileName = (string)pivotItem.Header;
            DeleteLocalFile(fileName);
        }
    }
}
