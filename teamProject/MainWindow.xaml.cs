using Microsoft.VisualBasic;
using Microsoft.Win32;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace teamProject
{
    public partial class MainWindow : Window
    {
        private Model model;
        private string openedDirectory;
        private string _soursDirectory;
        private string homeDirectory;
        private bool isMove = false;
        public MainWindow()
        {
            InitializeComponent();
            model = new Model();
            this.DataContext = model;
            openedDirectory = null!;
            _soursDirectory = null!;

            GetDefaultPath();
            UpdateItems();
        }

        private void GetDefaultPath()
        {
            string username = Environment.UserName;
            model.Path = Directory.GetCurrentDirectory();
            openedDirectory = model.Path;
            homeDirectory = model.Path; ;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Пошук")
            {
                SearchTextBox.Text = "";
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Пошук";
            }
        }

        private void GetDefaultPath()
        {
            string username = Environment.UserName;
            model.Path = Directory.GetCurrentDirectory();
            openedDirectory = model.Path;
            homeDirectory = model.Path; ;
        }
       
        private void ItemGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                DItem dItem = (DItem)ItemsListBox.SelectedItem;

                if (dItem is DDrive)
                {
                    OpenDrive(dItem);
                }
                else if (dItem is DDirectory)
                {
                    OpenDirectory(dItem);
                }
                else if (dItem is DFile)
                {
                    OpenFile(dItem);
                }
            }
        }

        private void OpenDrive(DItem dItem)
        {
            model.PushBackPath(model.Path);
            model.Path = ((DDrive)dItem).Path;
            Directory.SetCurrentDirectory(model.Path);
            openedDirectory = Directory.GetCurrentDirectory();
            model.forwardPathHistory.Clear();

            UpdateItems();
        }

        private void OpenDirectory(DItem dItem)
        {
            try
            {
                model.PushBackPath(model.Path);
                model.Path = Path.Combine(model.Path, dItem.Name);
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

        private async void UpdateItems()
        {
            if (model.Path == "Диски")
            {
                UpdateDrives();
            }
            else
            {
                model.ClearItems();

                string[] directories = Directory.GetDirectories(model.Path);
                string[] files = Directory.GetFiles(model.Path);

                await UpdateItemsByTypeAsync(directories);
                await UpdateItemsByTypeAsync(files);

                ItemsListBox.ItemsSource = model.Items;

                UpdateItemsSize();
                UpdateButtonState();
            }
        }

        private void UpdateDrives()
        {
            model.ClearItems();
            
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                DDrive dDrive = new DDrive(drive);

                model.AddItem(dDrive);
            }

            model.Path = "Диски";
        }

        private Task UpdateItemsByTypeAsync(string[] items)
        {
            return Task.Run(async () =>
            {
                foreach (string itemPath in items)
                {
                    await UpdateItemByTypeAsync(itemPath);
                }
            });
        }

        private Task UpdateItemByTypeAsync(string itemPath)
        {
            return Task.Run(async () =>
            {
                string itemName = Path.GetFileName(itemPath);
                List<string> vanishedItems = new List<string>()
                { "$recycle.bin", "$windows.~ws", "$winreagent", "config.msi", "documents and settings",
                  "system volume information", "recovery", "msocache", "$av_asw", "boot",
                  "dumpstack.log", "dumpstack.log.tmp", "hiberfil.sys", "pagefile.sys", "swapfile.sys", "vfcompat.dll",
                  "bootmgr", "bootnxt", "boottel.dat", "autoexec.bat", "bootsect.bak", "config.sys", "io.sys",
                  "msdos.sys", "wfnei" };

                DateTime itemDate = Directory.GetLastWriteTime($"{itemPath}");
                DItem dItem = new DItem();

                if (Directory.Exists(itemPath) &&
                    !vanishedItems.Contains(itemName.ToLower()))
                {
                    dItem = new DDirectory(itemName, itemDate);

                    await GetItemsSizeAsync(itemPath, dItem);
                }
                else if (File.Exists(itemPath) &&
                         !vanishedItems.Contains(itemName.ToLower()))
                {
                    long itemSize = new FileInfo(itemPath).Length;

                    dItem = new DFile(itemName, itemDate, itemSize);
                }

                AddItem(dItem);
            });
        }

        private void AddItem(DItem dItem)
        {
            if (dItem.Name != null)
            {
                Dispatcher.Invoke(() =>
                {
                    model.AddItem(dItem);
                });
            }
        }

        private void UpdateItemsSize()
        {
            try
            {
                foreach (DItem dItem in model.Items)
                {
                    UpdateItemSize(dItem);
                }
            }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"Непередбачена помилка: {ex.Message}", "Помилка розрахунку розміру папки.", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdateItemSize(DItem dItem)
        {
            if (dItem is DDirectory)
            {
                string itemPath = Path.Combine(model.Path, dItem.Name);
                long itemSize = await GetItemsSizeAsync(itemPath, dItem);
                dItem.UpdateItemSize(itemSize);
            }
        }

        private Task<long> GetItemsSizeAsync(string curDirectoryPath, DItem dItem)
        {
            return Task.Run(async () =>
            {
                long size = 0;
                DirectoryInfo dirInfo = new DirectoryInfo(curDirectoryPath);

                if (ItemsListBox.Items.Contains(dItem))
                {
                    try
                    {
                        size = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        try
                        {
                            string[] directories = Directory.GetDirectories(curDirectoryPath);
                            string[] files = Directory.GetFiles(curDirectoryPath);

                            foreach (string directoryPath in directories)
                            {
                                size += await GetItemsSize(directoryPath, dItem);
                            }

                            foreach (string filePath in files)
                            {
                                size += new FileInfo(filePath).Length;
                            }
                        }
                        catch { }
                    }
                }

                return size;
            });
        }

        private async Task<long> GetItemsSize(string curDirectoryPath, DItem dItem)
        {
            long size = 0;
            DirectoryInfo dirInfo = new DirectoryInfo(curDirectoryPath);

            try
            {
                size = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    string[] directories = Directory.GetDirectories(curDirectoryPath);
                    string[] files = Directory.GetFiles(curDirectoryPath);

                    foreach (string directoryPath in directories)
                    {
                        size += await GetItemsSize(directoryPath, dItem);
                    }

                    foreach (string filePath in files)
                    {
                        size += new FileInfo(filePath).Length;
                    }
                }
                catch { }
            }

            return size;
        }

        private void UpdateButtonState()
        {
            DirectoryInfo parentDir = Directory.GetParent(model.Path)!;

            BackBtn.IsEnabled = (parentDir != null);
            NextBtn.IsEnabled = model.CanGoForward();
        }

        private void OpenFile(DItem dItem)
        {
            string filePath = Path.Combine(model.Path, dItem.Name);
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();

            startInfo.UseShellExecute = true;
            startInfo.FileName = filePath;
            process.StartInfo = startInfo;

            process.Start();
        }
        
        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo parentDir = Directory.GetParent(model.Path)!;
            
            if (parentDir != null)
            {
                model.PushForwardPath(model.Path); 
                model.Path = parentDir.FullName;
                Directory.SetCurrentDirectory(model.Path);
                openedDirectory = Directory.GetCurrentDirectory();
                
                UpdateItems();
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
                    Directory.SetCurrentDirectory(model.Path);
                    openedDirectory = Directory.GetCurrentDirectory();
                    
                    UpdateItems();
                }
                else
                {
                    model.RemoveForwardPath(nextPath);
                    NextBtn.IsEnabled = false;
                    
                    MessageBox.Show("Цей шлях більше не існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
         
        private async void SearchDirectories(string rootPath, string phrase)
        {
            List<string> foundItems = new List<string>();
            
            await Task.Run(() =>
            {
                try
                {
                    foundItems.AddRange(Directory
                        .EnumerateFileSystemEntries(rootPath, "*.*", SearchOption.AllDirectories)
                        .Where(path => Path.GetFileName(path)
                        .Contains(phrase, StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to access {rootPath}: {ex.Message}");
                }
            });

            await UpdateItemsByTypeAsync(foundItems.ToArray());
           
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
                    
                    if (PathTextBox.Text.Contains(Path.GetFileName(fileToPaste)))
                    {
                        MessageBox.Show("Не можливо вставити папку саму в себе");
                        continue;
                    }

                    if (File.Exists(fileToPaste))
                    {
                        var fileName = Path.GetFileName(fileToPaste);
                        var destinationPath = Path.Combine(openedDirectory, fileName);
                        var uniqueFileName = GetUniqueFileName(fileName);

                        File.Copy(fileToPaste, uniqueFileName);
                        if (isMove)
                        {
                            File.Delete(fileToPaste);
                            isMove = false;
                        }
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
                        else
                        {
                            Directory.CreateDirectory(uniqueDirectoryName);

                            CopyDirectory(fileToPaste, destinationPath);
                            if (isMove)
                            {
                                Directory.Delete(fileToPaste, true);
                                isMove = false;
                            }
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

        private void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {

            var directory = new DirectoryInfo(sourceDirectoryName);
            var dirs = directory.GetDirectories();
            var files = directory.GetFiles();

            Directory.CreateDirectory(destinationDirectoryName);

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
                DFile selectedFile = (DFile)ItemsListBox.SelectedItem;

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
                    if (isMove)
                    {
                        Delete_Click(sender, e);
                    }
                    UpdateItems();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка перейменування файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Mov_Click(object sender, RoutedEventArgs e)
        {
            isMove = true;
            Copy_Click(sender, e);
            // код Семена || P.S. Вже не Семена
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            Directory.SetCurrentDirectory(model.Path);
            openedDirectory = Directory.GetCurrentDirectory();
            pasteItem.IsEnabled = false;
            
            UpdateItems();
        }

        private void UpDate_btn(object sender, RoutedEventArgs e)
        {
            pasteItem.IsEnabled = false;
            
            UpdateItems();
        }

        private void Home_btn(object sender, RoutedEventArgs e)
        {
            model.Path = homeDirectory;
            Directory.SetCurrentDirectory(model.Path);
            openedDirectory = Directory.GetCurrentDirectory();

            UpdateItems();
        }

        private void MyFolder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DDirectory selectedDir = (DDirectory)FolderListBox.SelectedItem;

            if (e.ClickCount >= 2 && model.Path != selectedDir.Path)
            {
                if (selectedDir.Path == "<=Discs=>")
                {
                    Directory.SetCurrentDirectory(selectedDir.Path);
                    openedDirectory =  Directory.GetCurrentDirectory();
                    UpdateDrives();
                }
                else
                {
                    model.Path = selectedDir.Path;
                    Directory.SetCurrentDirectory(selectedDir.Path);
                    openedDirectory = Directory.GetCurrentDirectory();
                    UpdateItems();
                }
            }
        }

        private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

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
            Path = null!;

            items = new ObservableCollection<DItem>();
            myFolders = new ObservableCollection<DDirectory>()
            {
                new DDirectory("Диски", "<=Discs=>"),
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
            return backPathHistory.Count > 0 ? backPathHistory.Pop() : null!;
        }

        public void PushForwardPath(string path)
        {
            forwardPathHistory.Push(path);
        }

        public string PopForwardPath()
        {
            return forwardPathHistory.Count > 0 ? forwardPathHistory.Pop() : null!;
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
            "Байт", "КБ", "МБ", "ГБ", "ТБ", "ПТ"
        };

        public string Name { get; set; }
        public string Date { get; set; }
        public long Size { get; set; }
        public string SizeString { get; set; } = "Розрахунок...";

        public DItem()
        {
            Name = null!;
        }

        public DItem(string name, DateTime date)
        {
            Name = name;
            Date = date.ToShortDateString();
        }

        public void UpdateItemSize(long size)
        {
            Size = size;
            SizeString = UpdateSize(Size);
        }

        public string UpdateSize(long size)
        {
            long bytes = size;
            string remainderString = "";
            int unitIndex = ConvertUnit(ref size);
            long roundedBytes = size;

            for (int i = 0; i < unitIndex; i++)
            {
                roundedBytes *= 1024;
            }

            long byteDifference = (bytes - roundedBytes);

            if (byteDifference > 0)
            {
                ConvertUnit(ref byteDifference);
                int remainder = (int)((100 / 1024.0) * byteDifference);
                
                remainderString = $",{remainder}";
            }

            return $"{size}{remainderString} {Units[unitIndex]}";
        }

        public int ConvertUnit(ref long unit)
        {
            int unitIndex = 0;

            while (unit >= 1024)
            {
                unit /= 1024;
                unitIndex++;
            }

            return unitIndex;
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DDrive : DItem
    {
        public string Path { get; set; }
        public long TotalSpace { get; set; }
        public string TotalSpaceString { get; set; } = "";
        public long FreeSpace { get; set; }
        public string FreeSpaceString { get; set; } = "";
        public double PercentSize { get; set; }

        public DDrive(DriveInfo driveInfo)
        {
            Name = $"Локальний диск ({driveInfo.Name.Substring(0, driveInfo.Name.Length - 1)})";
            Path = driveInfo.Name;
            TotalSpace = driveInfo.TotalSize;
            FreeSpace = driveInfo.AvailableFreeSpace;
            PercentSize = 100 - ((FreeSpace / TotalSpace) * 100);
            
            SizeString = "";
            TotalSpaceString = UpdateSize(TotalSpace);
            FreeSpaceString = UpdateSize(FreeSpace);
            Date = $"{FreeSpaceString} вільно з {TotalSpaceString}";
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DDirectory : DItem
    {
        public string Path { get; set; }

        public DDirectory(string name, DateTime date) : base(name, date)
        {
            Path = null!;
        }
        
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
            UpdateItemSize(size);
        }
    }
}