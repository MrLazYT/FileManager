using PropertyChanged;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace teamProject
{
    public partial class MainWindow : Window
    {
        private Model model;

        public MainWindow()
        {
            InitializeComponent();
            model = new Model();
            this.DataContext = model;

            GetDefaultPath();
            UpdateItems();
        }

        private void GetDefaultPath()
        {
            string compAndUserNames = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string username = compAndUserNames.Split('\\')[1];

            model.Path = $"C:/Users/{username}/Downloads";
        }
        private void FetchFolders()
        {

        }

        private void ItemGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                DItem dObject = (DItem)ItemsListBox.SelectedItem;

                if (dObject is DDirectory)
                {
                    OpenDirectory(dObject);
                }
                else if (dObject is DFile)
                {
                    OpenFile(dObject);
                }
            }
        }

        

        private void UpdateItems()
        {
            model.ClearItems();

            string[] directories = Directory.GetDirectories(model.Path);
            string[] files = Directory.GetFiles(model.Path);

            UpdateItemsByType(directories);
            UpdateItemsByType(files, "file");
            UpdateButtonState();
        }
        private void UpdateButtonState()
        {
            DirectoryInfo parentDir = Directory.GetParent(model.Path);
            BackBtn.IsEnabled = (parentDir != null);

            
            NextBtn.IsEnabled = model.CanGoForward();
        }
        private void UpdateItemsByType(string[] items, string type = "directory")
        {
            foreach (string itemPath in items)
            {
                string itemName = Path.GetFileName(itemPath);
                DateTime itemDate = Directory.GetLastWriteTime($"{itemPath}");
                DItem dItem = new DItem();

                if (type == "directory")
                {
                    dItem = new DDirectory(itemName, itemDate);
                }
                else if (type == "file")
                {
                    long itemSize = new FileInfo(itemPath).Length;
                    dItem = new DFile(itemName, itemDate, itemSize);
                }

                model.AddItem(dItem);
            }
        }
        //
        private void OpenFile(DItem dObject)
        {
            string filePath = Path.Combine(model.Path, dObject.Name);
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();

            startInfo.UseShellExecute = true;
            startInfo.FileName = filePath;
            process.StartInfo = startInfo;

            process.Start();
        }

        private void OpenDirectory(DItem dObject)
        {
            model.PushBackPath(model.Path); 
            model.Path = Path.Combine(model.Path, dObject.Name);

            model.forwardPathHistory.Clear();
            UpdateItems();
        }
        

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo parentDir = Directory.GetParent(model.Path);
            if (parentDir != null)
            {
                model.PushForwardPath(model.Path); 
                model.Path = parentDir.FullName;
                UpdateItems();
               
            }
            
        }


        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (model.CanGoForward())
            {
                string nextPath = model.PopForwardPath();
                model.Path = nextPath;
                UpdateItems();
              
            }
            
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

    [AddINotifyPropertyChangedInterface]
    public class Model
    {
        private ObservableCollection<DItem> items;
        private Stack<string> backPathHistory = new Stack<string>(); 
        public Stack<string> forwardPathHistory = new Stack<string>(); 

        public IEnumerable<DItem> Items => items;
        public string Path { get; set; }

        public Model()
        {
            items = new ObservableCollection<DItem>();
        }

        public void AddItem(DItem item)
        {
            items.Add(item);
        }

        public void ClearItems()
        {
            items.Clear();
        }

        
        public void PushBackPath(string path)
        {
            backPathHistory.Push(path);
        }

        public string PopBackPath()
        {
            return backPathHistory.Count > 0 ? backPathHistory.Pop() : null;
        }

        public void PushForwardPath(string path)
        {
            forwardPathHistory.Push(path);
        }

        public string PopForwardPath()
        {
            return forwardPathHistory.Count > 0 ? forwardPathHistory.Pop() : null;
        }

        public bool CanGoBack() 
        {
            return backPathHistory.Count > 0;
        }

        public bool CanGoForward() 
        {
            return forwardPathHistory.Count > 0;
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DItem
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }

        public DItem() { }

        public DItem(string name, DateTime date)
        {
            Name = name;
            Date = date;
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DDirectory : DItem
    {
        public DDirectory(string name, DateTime date) : base(name, date) { }
    }

    [AddINotifyPropertyChangedInterface]
    public class DFile : DItem
    {
        public long Size { get; set; }
        
        public DFile(string name, DateTime date, long size) : base(name, date)
        {
            Size = size / 1024 / 1024;
        }
    }
}