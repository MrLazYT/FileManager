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

        private void OpenDirectory(DItem dObject)
        {
            model.Path = Path.Combine(model.Path, dObject.Name);

            UpdateItems();
        }

        private void UpdateItems()
        {
            model.ClearItems();

            string[] directories = Directory.GetDirectories(model.Path);
            string[] files = Directory.GetFiles(model.Path);

            UpdateItemsByType(directories);
            UpdateItemsByType(files, "file");
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

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    [AddINotifyPropertyChangedInterface]
    public class Model
    {
        private ObservableCollection<DItem> items;

        public IEnumerable<DItem> Items => items;
        public string Path { get; set; }
        public string TotalSize { get; set; }
        public string ItemCount { get; set; }

        public Model()
        {
            items = new ObservableCollection<DItem>();
        }

        public void AddItemRange(IEnumerable<DDirectory> directories)
        {
            foreach (DDirectory directory in directories)
            {
                AddItem(directory);
            }
        }

        public void AddItem(DItem item)
        {
            items.Add(item);
        }

        public void ClearItems()
        {
            items.Clear();
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
        private List<string> Units = new List<string>()
        {
            "B", "KB", "MB", "GB"
        };

        public long Size { get; set; }
        public string SizeString { get; set; }
        
        public DFile(string name, DateTime date, long size) : base(name, date)
        {
            int unitIndex = 0;

            Size = size;
            
            while (Size >= 1024)
            {
                Size = Size / 1024;
                unitIndex++;
            }
            
            SizeString = $"{Size} {Units[unitIndex]}";
        }
    }
}