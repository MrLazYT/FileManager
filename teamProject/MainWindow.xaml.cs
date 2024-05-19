﻿using Microsoft.VisualBasic;
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

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeRestoreButton_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }

        private void PathTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    model.Path = PathTextBox.Text;
                    Directory.SetCurrentDirectory(model.Path);
                    UpdateItems();
                }
                catch (DirectoryNotFoundException)
                {
                    MessageBox.Show("Вказано невірний шлях.", "Помилка шляху", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Непердбачена помилка шляху: {ex.Message}", "Помилка шляху", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        private void UpdateItems()
        {
            model.ClearItems();

            string[] directories = Directory.GetDirectories(model.Path);
            string[] files = Directory.GetFiles(model.Path);

            UpdateItemsByType(directories);
            UpdateItemsByType(files, "file");
            UpdateButtonState();

            ItemsListBox.ItemsSource = model.Items;
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
                long itemSize = 0;
                DItem dItem = new DItem();

                if (type == "directory")
                {
                    itemSize = GetFolderSize(itemPath);
                    dItem = new DDirectory(itemName, itemDate, itemSize);
                }
                else if (type == "file")
                {
                    itemSize = new FileInfo(itemPath).Length;
                    dItem = new DFile(itemName, itemDate, itemSize);
                }

                model.AddItem(dItem);
            }
        }

        private long GetFolderSize(string curDirectoryPath)
        {
            long size = 0;
            try
            {
                string[] directories = Directory.GetDirectories(curDirectoryPath);
                string[] files = Directory.GetFiles(curDirectoryPath);

                foreach (string directoryPath in directories)
                {
                    size += GetFolderSize(directoryPath);
                }

                foreach (string filePath in files)
                {
                    size += new FileInfo(filePath).Length;
                }

                
            }
            catch (Exception ex) { }
            return size;

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

        private void OpenDirectory(DItem dObject)
        {
            model.PushBackPath(model.Path); 
            model.Path = Path.Combine(model.Path, dObject.Name);
            Directory.SetCurrentDirectory(model.Path);
            openedDirectory = Directory.GetCurrentDirectory();
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
                Directory.SetCurrentDirectory(model.Path);
                openedDirectory = Directory.GetCurrentDirectory();
            }
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
           
            if (model.CanGoForward())
            {
                string nextPath = model.PopForwardPath();

              
                if (Directory.Exists(nextPath))
                {
                    model.Path = nextPath;
                    UpdateItems();
                    Directory.SetCurrentDirectory(model.Path);
                    openedDirectory = Directory.GetCurrentDirectory();
                }
                else
                {
            
                    model.RemoveForwardPath(nextPath);
                    NextBtn.IsEnabled = false;
                    MessageBox.Show("Цей шлях більше не існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string phrase = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(phrase))
            {
                UpdateItems();
            }
            else
            {
                SearchDirectories(model.Path, phrase); 
            }
        }
        private async void SearchDirectories(string rootPath, string phrase)
        {
            List<string> foundItems = new List<string>();
            await Task.Run(() =>
            {
                try
                {
                    foundItems.AddRange(Directory.EnumerateFileSystemEntries(rootPath, "*.*", SearchOption.AllDirectories)
                                                 .Where(path => Path.GetFileName(path).Contains(phrase, StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to access {rootPath}: {ex.Message}");
                }
            });

            UpdateItemsByType(foundItems.ToArray());
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
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            UpdateItems();
            Directory.SetCurrentDirectory(model.Path);
            openedDirectory = Directory.GetCurrentDirectory();
            pasteItem.IsEnabled = false;
        }
        private void UpDate_btn(object sender, RoutedEventArgs e)
        {
            UpdateItems();
            pasteItem.IsEnabled = false;
        }

        private void Home_btn(object sender, RoutedEventArgs e)
        {
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
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
        public void RemoveForwardPath(string path)
        {
            if (forwardPathHistory.Contains(path))
            {
                var tempStack = new Stack<string>(forwardPathHistory.Count);
                while (forwardPathHistory.Count > 0)
                {
                    var currentPath = forwardPathHistory.Pop();
                    if (currentPath != path)
                    {
                        tempStack.Push(currentPath);
                    }
                }
                while (tempStack.Count > 0)
                {
                    forwardPathHistory.Push(tempStack.Pop());
                }
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
        private List<string> Units = new List<string>()
        {
            "B", "KB", "MB", "GB"
        };

        public string Name { get; set; }
        public DateTime Date { get; set; }
        public long Size { get; set; }
        public string SizeString { get; set; }

        public DItem() { }

        public DItem(string name, DateTime date, long size)
        {
            Name = name;
            Date = date;

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

    [AddINotifyPropertyChangedInterface]
    public class DDirectory : DItem
    {
        public DDirectory(string name, DateTime date, long size) : base(name, date, size) { }
    }

    [AddINotifyPropertyChangedInterface]
    public class DFile : DItem
    {   
        public DFile(string name, DateTime date, long size) : base(name, date, size) { }
    }
}