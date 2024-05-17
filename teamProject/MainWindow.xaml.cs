using Microsoft.VisualBasic;
using Microsoft.Win32;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace teamProject
{
    public partial class MainWindow : Window
    {
        private Model model;
        private string openedDirectory;
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

            model.Path = Directory.GetCurrentDirectory();
            openedDirectory = model.Path;
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
            Directory.SetCurrentDirectory(model.Path);
            openedDirectory = Directory.GetCurrentDirectory();
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
        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*|Folder (*)|*";
            saveFileDialog.FilterIndex = 3;

            string currentDirectory = openedDirectory;
            saveFileDialog.InitialDirectory = currentDirectory;

            bool? res = saveFileDialog.ShowDialog();
            if (res == true)
            {
                try
                {
                    string uniqueFileName = GetUniqueFileName(saveFileDialog.FileName);
                    if (saveFileDialog.FilterIndex != 3)
                    {
                        File.Create(uniqueFileName);
                    }
                    else
                    {
                        Directory.CreateDirectory(uniqueFileName);
                    }
                    UpdateItems();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка створення файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedFiles = ItemsListBox.SelectedItems;
                var fileListForClipboard = new StringCollection();

                foreach (DItem selectedFile in selectedFiles)
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), selectedFile.Name);
                    fileListForClipboard.Add(filePath);
                }

                Clipboard.SetFileDropList(fileListForClipboard);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка копіювання файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            pasteItem.IsEnabled = true;
            
        }
        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filesToPaste = Clipboard.GetFileDropList();
                string[] fileArray = new string[filesToPaste.Count];
                filesToPaste.CopyTo(fileArray, 0);

                foreach (var fileToPaste in fileArray)
                {
                    if (File.Exists(fileToPaste))
                    {
                        var fileName = Path.GetFileName(fileToPaste);
                        var destinationPath = Path.Combine(openedDirectory, fileName);
                        var uniqueFileName = GetUniqueFileName(fileName);
                        File.Copy(fileToPaste, uniqueFileName);
                    }
                    else if(Directory.Exists(fileToPaste))
                    {
                        var sourceeDirectoryName = new DirectoryInfo(fileToPaste).Name;
                        var destinationPath = Path.Combine(openedDirectory, sourceeDirectoryName);
                        var uniqueDirectoryName = GetUniqueFileName(sourceeDirectoryName);
                        Directory.CreateDirectory(uniqueDirectoryName);
                        CopyDirectory(fileToPaste, destinationPath);
                    }

                }
                UpdateItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка вставки файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            var directory = new DirectoryInfo(sourceDirectoryName);
            var dirs = directory.GetDirectories();

            Directory.CreateDirectory(destinationDirectoryName);

            var files = directory.GetFiles();
            foreach (var file in files)
            {
                var temqPath = Path.Combine(destinationDirectoryName, file.Name);
                file.CopyTo(temqPath, false);
            }

            foreach (var subDir in dirs)
            {
                var tempPath = Path.Combine(destinationDirectoryName, subDir.Name);
                CopyDirectory(subDir.FullName, tempPath);
            }
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedFiles = ItemsListBox.SelectedItems;
                var selectedItems = new List<DItem>(ItemsListBox.SelectedItems.Cast<DItem>());
                foreach (DItem selectedFile in selectedItems)
                {
                    var filePath = Path.Combine(openedDirectory, selectedFile.Name);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    else if (Directory.Exists(filePath))
                    {
                        Directory.Delete(filePath, true);
                    }
                    UpdateItems();
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Помилка видалення файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DFile selectedFile = ItemsListBox.SelectedItem as DFile;
                if (selectedFile == null)
                {
                    MessageBox.Show("Виберіть файл для перейменування.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var oldFilePath = Path.Combine(openedDirectory, selectedFile.Name);

                var newFileName = Interaction.InputBox("Введіть нову назву файлу:", "Перейменувати файл", selectedFile.Name);

                if (!string.IsNullOrWhiteSpace(newFileName))
                {
                    var newFilePath = Path.Combine(openedDirectory, newFileName);

                    File.Move(oldFilePath, newFilePath);
                    UpdateItems();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка перейменування файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        //------------------------------------------------------------
        private void Mov_Click(object sender, RoutedEventArgs e)
        {
            // код Семена
        }
        //------------------------------------------------------------
        private string GetUniqueFileName(string fileName)
        {
            string directoryPath = Directory.GetCurrentDirectory();
            string extension = Path.GetExtension(fileName);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string newName = fileName;
            if (File.Exists(Path.Combine(directoryPath, newName)))
            {
                int count = 2;
                while (File.Exists(Path.Combine(directoryPath, newName)))
                {
                    newName = $"{fileNameWithoutExtension}({count}){extension}";
                    count++;
                }
            }
            return newName;
        }
        //
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            UpdateItems();
            pasteItem.IsEnabled = false;
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
        public long Size { get; set; }
        
        public DFile(string name, DateTime date, long size) : base(name, date)
        {
            Size = size / 1024 / 1024;
        }
    }
}