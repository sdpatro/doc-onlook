using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
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
using Windows.Foundation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace doc_onlook
{
    
    public sealed partial class MainPage : Page
    {

        Workspace workspace;
        List<StorageFile> localFilesList;
        List<StorageFile> localFilesList_queried;
        List<string> fileNameList;
        List<string> indexedWords;
        string _type;

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
                newItem.Content = new Grid();
                Grid grid = (Grid)newItem.Content;
                WebView webView = new WebView();
                grid.Children.Add(webView);

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

            public async void SetPivotItemContent(int index, StorageFile file)
            {
                PivotItem pivotItem = (PivotItem)workspacePivot.Items[index];
                Grid grid = (Grid)pivotItem.Content;
                pivotItem.Header = file.DisplayName;
                WebView webView = (WebView)grid.Children[0];
                var buffer = await FileIO.ReadTextAsync(file);
                webView.NavigateToString(buffer);
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
                savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
                savePicker.SuggestedFileName = "New Document";

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    CachedFileManager.DeferUpdates(file);
                    await FileIO.WriteTextAsync(file, file.Name);
                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == FileUpdateStatus.Complete)
                    {
                        MessageDialog dialog = new MessageDialog("File Saved.");
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        MessageDialog dialog = new MessageDialog("We couldn't save the file.");
                        await dialog.ShowAsync();
                    }
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
                Debug.WriteLine("Match "+m.Groups[1].Value+" "+m.Groups[2].Value);
                contentDict.Add(new KeyValuePair<string, string>(WebUtility.UrlDecode(m.Groups[1].Value), WebUtility.UrlDecode(m.Groups[2].Value)));
                m = m.NextMatch();
            }

            return contentDict;

        }

        public async void WriteFileToLocal(IDictionary<string,string> ContentData)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            
            StorageFile newFile = await localFolder.CreateFileAsync(ContentData["name"] + ContentData["type"], CreationCollisionOption.GenerateUniqueName);

            using (IRandomAccessStream textStream = await newFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (DataWriter textWriter = new DataWriter(textStream))
                {
                    textWriter.WriteString(ContentData["data"]);
                    await textWriter.StoreAsync();
                }
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
                        Debug.WriteLine("Result: " + result);
                        string[] contents = Regex.Split(result,"\r\n\r\n");
                        if (GetContentLength(result)!= 0)
                        {
                            contentLength = GetContentLength(result);
                            Debug.WriteLine("Total content length: " + contentLength);
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
                            Debug.WriteLine("Read all data: "+totalContent);
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
            Debug.WriteLine("Finished reading");
        }

        private async void DeleteLocalFile(string name, string type)
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.GetFileAsync(name+type);
                await file.DeleteAsync();
                fileNameList.Remove(name + type);
                NotifyUser("File deleted.");
                
                UpdateLocalList();
            }
            catch(Exception e)
            {
                NotifyUser("Exception: " + e.ToString());
            }
        }

        private void FileListItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            StackPanel stackElement = (StackPanel)sender;
            TextBlock name = (TextBlock)stackElement.Children[0];
            TextBlock type = (TextBlock)stackElement.Children[1];
            DeleteLocalFile(name.Text,type.Text);
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
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                sender.ItemsSource = fileNameList;
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

        private async void ShareMailBtn_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += ShareHtmlHandler;
            DataTransferManager.ShowShareUI();
        }

        private async void ShareHtmlHandler(DataTransferManager sender, DataRequestedEventArgs e)
        {
            DataRequest request = e.Request;
            DataRequestDeferral deferral = request.GetDeferral();
            

            var selectedItem = (PivotItem)WorkspacePivot.SelectedItem;
            string fileName = (string)selectedItem.Header;
            StorageFile file = await GetLocalFile(fileName);
            string HTMLContent;

            HTMLContent = await FileIO.ReadTextAsync(file);
            char[] splitter = { '.' };
            string[] contents = fileName.Split(splitter);
            request.Data.Properties.Title = contents[0];
            request.Data.Properties.Description = "Share the current file via DocOnlook.";
            string htmlFormat = HtmlFormatHelper.CreateHtmlFormat(HTMLContent);
            request.Data.SetHtmlFormat(htmlFormat);
            deferral.Complete();
        }

        
    }
}
