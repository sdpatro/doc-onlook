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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace doc_onlook
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    

    public sealed partial class MainPage : Page
    {

        Workspace workspace;
        public MainPage()
        {
            InitializeComponent();
            FillWorkspace();
            RunTCPListener();
        }

        public async void FillWorkspace()
        {
            UpdateLocalList();
            workspace = new Workspace(WorkspacePivot);
            StorageFile file = await GetLocalFile("hello", ".html");
            workspace.ShowDoc(file);
        }

        class Workspace
        {
            private List<string> workspaceList { get; set; }
            private Pivot workspacePivot { get; set; }

            public void addToList(string fileName)
            {
                workspaceList.Add(fileName);
                
                workspacePivot.Items.Add(CreateWorkspaceItem(fileName));
            }

            private object CreateWorkspaceItem(string fileName)
            {
                PivotItem newItem = new PivotItem();
                newItem.Header = fileName;
                newItem.Content = new Grid();
                Grid grid = (Grid)newItem.Content;
                grid.Children.Add(new WebView());

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
                addToList("hello");
            }
        };



        // Fill the local files list:
        public async void UpdateLocalList()
        {
            List<StorageFile> items = await GetLocalFiles();
            LocalListView.ItemsSource = items;
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
            StorageFile localFile = await GetLocalFile(fileName, fileType);
            workspace.ShowDoc(localFile);
        }

        public async void DisplayDoc(StorageFile localFile)
        {
            var read = await FileIO.ReadTextAsync(localFile);
            var CurrentFileBuffer = read;
            workspace.ShowDoc(localFile);
        }

        public async Task<StorageFile> GetLocalFile(string fileName, string fileType)
        {
            try
            {
                StorageFile localFile = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName+fileType);
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

        private async void DeleteLocalFile(string name)
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.GetFileAsync(name+".html");
                await file.DeleteAsync();
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
            DeleteLocalFile(name.Text);
        }

        private void NewTabBtn_Tapped_1(object sender, TappedRoutedEventArgs e)
        {
            workspace.addToList("something");
        }
    }
}
