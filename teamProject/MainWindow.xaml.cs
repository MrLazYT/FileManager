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
        private string _soursDirectory;
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
            string username = Environment.UserName;

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

        private async void UpdateItems()
        {
            model.ClearItems();

            string[] directories = Directory.GetDirectories(model.Path);
            string[] files = Directory.GetFiles(model.Path);

            await UpdateItemsByTypeAsync(directories);
            await UpdateItemsByTypeAsync(files, "file");
            UpdateDirectoriesSize();
            UpdateButtonState();

            ItemsListBox.ItemsSource = model.Items;
        }

        private async void UpdateDirectoriesSize()
        {
            try
            {
                foreach (DItem dItem in ItemsListBox.Items)
                {
                    if (dItem is DDirectory)
                    {
                        string itemPath = Path.Combine(model.Path, dItem.Name);
                        long itemSize = await GetItemsSizeAsync(itemPath);
                        dItem.UpdateSize(itemSize);
                    }
                }
            }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"Непередбачена помилка: {ex.Message}", "Помилка розрахунку розміру папки.", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateButtonState()
        {
            DirectoryInfo parentDir = Directory.GetParent(model.Path);
            BackBtn.IsEnabled = (parentDir != null);

            
            NextBtn.IsEnabled = model.CanGoForward();
        }
        private Task UpdateItemsByTypeAsync(string[] items, string type = "directory")
        {
            return Task.Run(() =>
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

                    Dispatcher.Invoke(() =>
                    {
                        model.AddItem(dItem);
                    });
                }
            });
        }

        private Task<long> GetItemsSizeAsync(string curDirectoryPath)
        {
            return Task.Run(async () =>
            {
                long size = 0;

                try
                {
                    string[] directories = Directory.GetDirectories(curDirectoryPath);
                    string[] files = Directory.GetFiles(curDirectoryPath);

                    foreach (string directoryPath in directories)
                    {
                        size += await GetItemsSizeAsync(directoryPath);
                    }

                    foreach (string filePath in files)
                    {
                        size += new FileInfo(filePath).Length;
                    }


                }
                catch { }

                return size;
            });
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
            try
            {
                model.PushBackPath(model.Path);
                model.Path = Path.Combine(model.Path, dObject.Name);
                Directory.SetCurrentDirectory(model.Path);
                openedDirectory = Directory.GetCurrentDirectory();
                model.forwardPathHistory.Clear();
                UpdateItems();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Ви не маєте доступу до цієї папки.", "Неавторизований доступ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (DirectoryNotFoundException)
            {
                MessageBox.Show("Не можливо знайти шлях до папки.", "Невірне розташування", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Непередбачена помилка: {ex.Message}", "Помилка шляху папки");
            }
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

            UpdateItemsByTypeAsync(foundItems.ToArray());
           
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
                string filePath = "";
                foreach (DItem selectedFile in selectedFiles)
                {
                    filePath = Path.Combine(Directory.GetCurrentDirectory(), selectedFile.Name);
                    fileListForClipboard.Add(filePath);
                }
                _soursDirectory = filePath;
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
                    else if (Directory.Exists(fileToPaste))
                    {
                        var sourceeDirectoryName = new DirectoryInfo(fileToPaste).Name;
                        var destinationPath = Path.Combine(openedDirectory, sourceeDirectoryName);
                        var uniqueDirectoryName = GetUniqueFileName(sourceeDirectoryName);

                        if (openedDirectory == _soursDirectory)
                        {
                            MessageBox.Show("Не можливо вставити кореневу папку в саму ж себе");
                        }
                        else{
                            Directory.CreateDirectory(uniqueDirectoryName);
                            CopyDirectory(fileToPaste, destinationPath);
                        }


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

        private void MyFolder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DDirectory selectedDir = (DDirectory)FolderListBox.SelectedItem;
            
            if (e.ClickCount >= 2 && model.Path != selectedDir.Path)
            {
                model.Path = selectedDir.Path;

                UpdateItems();
            }
        }

        private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

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
    }

    [AddINotifyPropertyChangedInterface]
    public class Model
    {
        private ObservableCollection<DItem> items;
        private ObservableCollection<DDirectory> myFolders;
        private Stack<string> backPathHistory = new Stack<string>(); 
        public Stack<string> forwardPathHistory = new Stack<string>(); 

        public string Path { get; set; }
        public IEnumerable<DItem> Items => items;

        public IEnumerable<DDirectory> MyFolders => myFolders;

        public Model()
        {
            items = new ObservableCollection<DItem>();
            myFolders = new ObservableCollection<DDirectory>()
            {
                new DDirectory("Робочий стіл", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
                new DDirectory("Завантаження", @$"C:\Users\{Environment.UserName}\Downloads"),
                new DDirectory("Документи", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                new DDirectory("Зображення", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
                new DDirectory("Відео", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
                new DDirectory("Музика", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            };
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
        public string SizeString { get; set; } = "Розрахунок...";

        public DItem() { }

        public DItem(string name, DateTime date)
        {
            Name = name;
            Date = date;
        }

        public void UpdateSize(long size)
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

    [AddINotifyPropertyChangedInterface]
    public class DDirectory : DItem
    {
        public string Path { get; set; }

        public DDirectory(string name, DateTime date) : base(name, date) { }
        
        public DDirectory(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DFile : DItem
    {   
        public DFile(string name, DateTime date, long size) : base(name, date)
        {
            UpdateSize(size);
        }
    }
}