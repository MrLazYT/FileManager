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
using System.Windows.Shapes;

namespace teamProject
{
    public partial class MainWindow : Window
    {
        private Model model;
        private string openedDirectory;
        private string _soursDirectory;
        private string homeDirectory;
        private bool isMove = false;
        List<CancellationTokenSource> tokens;

        public MainWindow()
        {
            InitializeComponent();
            model = new Model();
            this.DataContext = model;
            openedDirectory = null!;
            _soursDirectory = null!;
            homeDirectory = null!;
            tokens = new List<CancellationTokenSource>();

            GetDefaultPath();
            UpdateItems();
        }

        private void GetDefaultPath()
        {
            //string username = Environment.UserName;
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

        private void ItemGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                foreach (CancellationTokenSource tokenSource in tokens)
                {
                    tokenSource.Cancel();
                }

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
                model.Path = dItem.Path;
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

        private void UpdateItems()
        {
            if (model.Path == "Диски")
            {
                UpdateDrives();
            }
            else
            {
                UpdateMyFolders();
                UpdateDirectory();
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

            UpdateTotalItemsCount();
            UpdateTotalItemsSize();
        }

        private void UpdateMyFolders()
        {
            bool isInMyFolderPath = false;

            foreach (DDirectory myFolder in model.MyFolders)
            {
                if (model.Path == myFolder.Path)
                {
                    isInMyFolderPath = true;
                    FolderListBox.SelectedItem = myFolder;

                    break;
                }
            }

            if (!isInMyFolderPath)
            {
                FolderListBox.SelectedItem = null;
            }
        }

        private async void UpdateDirectory()
        {
            model.ClearItems();

            try
            {
                string[] directories = Directory.GetDirectories(model.Path);
                string[] files = Directory.GetFiles(model.Path);
                
                await UpdateItemsByTypeAsync(directories);
                await UpdateItemsByTypeAsync(files);

                ItemsListBox.ItemsSource = model.Items;
                UpdateItemsSize();
                UpdateTotalItemsCount();
                UpdateTotalItemsSize();
                UpdateButtonState();
            }
            catch
            {
                MessageBox.Show("Неможливо отримати доступ до папки", "Помилка відкриття папки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                string itemName = System.IO.Path.GetFileName(itemPath);
                List<string> vanishedItems = new List<string>()
                { "$recycle.bin", "$windows.~ws", "$winreagent", "config.msi", "documents and settings",
                  "system volume information", "recovery", "msocache", "$av_asw", "boot", "application data",
                  "dumpstack.log", "dumpstack.log.tmp", "hiberfil.sys", "pagefile.sys", "swapfile.sys", "vfcompat.dll",
                  "bootmgr", "bootnxt", "boottel.dat", "autoexec.bat", "bootsect.bak", "config.sys", "io.sys",
                  "msdos.sys", "wfnei" };

                DateTime itemDate = Directory.GetLastWriteTime($"{itemPath}");
                DItem dItem = new DItem();

                if (Directory.Exists(itemPath) &&
                    !vanishedItems.Contains(itemName.ToLower()))
                {
                    dItem = await UpdateDirectory(itemPath);
                }
                else if (File.Exists(itemPath) &&
                         !vanishedItems.Contains(itemName.ToLower()))
                {
                    dItem = UpdateFile(itemPath);
                }

                AddItem(dItem);
            });
        }

        private Task<DDirectory> UpdateDirectory(string dirPath)
        {
            return Task.Run(() =>
            {
                string dirName = System.IO.Path.GetFileName(dirPath);
                DateTime dirDate = Directory.GetLastWriteTime($"{dirPath}");
                DDirectory dDirectory = new DDirectory(dirName, dirDate, dirPath, "Assets/folder.png");

                return dDirectory;
            });
        }

        private DFile UpdateFile(string itemPath)
        {
            string itemName = System.IO.Path.GetFileName(itemPath);
            DateTime itemDate = Directory.GetLastWriteTime($"{itemPath}");
            long itemSize = new FileInfo(itemPath).Length;
            return new DFile(itemName, itemDate, itemPath, itemSize, GetImageForFile(itemName));
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
                string itemPath = System.IO.Path.Combine(model.Path, dItem.Name);
                List<string> strings = new List<string>()
                {
                    "Users", "ProgramData", "All Users", "Default", "Windows"
                };

                if (strings.Contains(dItem.Name))
                {
                    dItem.SizeString = "Невідомо";
                }
                else
                {
                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    tokens.Add(tokenSource);

                    long itemSize = await GetItemsSizeAsync(dItem, tokenSource.Token);
                    dItem.UpdateItemSize(itemSize);
                }
            }
        }

        private Task<long> GetItemsSizeAsync(DItem dItem, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                if (token.IsCancellationRequested)
                {
                    return 0;
                }

                long size = 0;

                try
                {
                    size = GetItemsSizeFast(dItem);
                }
                catch
                {
                    size = await GetItemsSizeLong(dItem.Path, token);
                }

                return size;
            });
        }

        private long GetItemsSizeFast(DItem dItem)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dItem.Path);
            long size = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

            UpdateDirectorySize(size);

            return size;
        }

        private async Task<long> GetItemsSizeLong(string curDirectoryPath, CancellationToken token)
        {
            long size = 0;
            string[] directories = new string[] { };
            string[] files = new string[] { };

            if (token.IsCancellationRequested)
            {
                return 0;
            }

            try
            {
                directories = Directory.GetDirectories(curDirectoryPath);
                files = Directory.GetFiles(curDirectoryPath);
            }
            catch
            {
                return 0;
            }

            foreach (string directoryPath in directories)
            {
                size += await GetItemsSizeLong(directoryPath, token);

                if (token.IsCancellationRequested)
                {
                    return 0;
                }
            }

            foreach (string filePath in files)
            {
                if (token.IsCancellationRequested)
                {
                    return 0;
                }

                size += new FileInfo(filePath).Length;
            }

            UpdateDirectorySize(size);

            return size;
        }

        private void UpdateButtonState()
        {
            DirectoryInfo parentDir = Directory.GetParent(model.Path)!;

            BackBtn.IsEnabled = (parentDir != null);
            NextBtn.IsEnabled = model.CanGoForward();
        }

        private void UpdateTotalItemsCount()
        {
            model.ItemCount = ItemsListBox.Items.Count;
        }

        private void UpdateTotalItemsSize()
        {
            long totalSize = 0;

            foreach (DItem dItem in model.Items)
            {
                if (dItem is DFile)
                {
                    totalSize += dItem.Size;
                }
                else if (dItem is DDrive)
                {
                    DDrive dDrive = (DDrive)dItem;

                    totalSize += dDrive.TotalSpace - dDrive.FreeSpace;
                }
            }

            model.UpdateTotalSize(totalSize);
        }

        private void UpdateDirectorySize(long size)
        {
            model.UpdateTotalSize(model.TotalSize + size);
        }

        private void OpenFile(DItem dItem)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();

            startInfo.UseShellExecute = true;
            startInfo.FileName = dItem.Path;
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
                        using (File.Create(uniqueFileName)) { }
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
                    filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), selectedFile.Name);
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

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filesToPaste = Clipboard.GetFileDropList();
                string[] fileArray = new string[filesToPaste.Count];
                filesToPaste.CopyTo(fileArray, 0);

                foreach (var fileToPaste in fileArray)
                {

                    if (PathTextBox.Text.Contains(System.IO.Path.GetFileName(fileToPaste)))
                    {
                        MessageBox.Show("Не можливо вставити папку саму в себе", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);

                        continue;
                    }

                    if (File.Exists(fileToPaste))
                    {
                        await PasteFileAsync(fileToPaste);
                    }
                    else if (Directory.Exists(fileToPaste))
                    {
                        await PasteDirectoryAsync(fileToPaste);
                    }
                }

                UpdateItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка вставки файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Task PasteFileAsync(string filePath)
        {
            return Task.Run(async () =>
            {
                var fileName = System.IO.Path.GetFileName(filePath);
                var destinationPath = System.IO.Path.Combine(openedDirectory, fileName);
                var uniqueFileName = GetUniqueFileName(fileName);

                File.Copy(filePath, uniqueFileName);

                if (isMove)
                {
                    await DeleteFileAsync(filePath);

                    isMove = false;
                }
            });
        }

        private Task PasteDirectoryAsync(string dirPath)
        {
            return Task.Run(async () =>
            {
                var sourceDirectoryName = new DirectoryInfo(dirPath).Name;
                var destinationPath = System.IO.Path.Combine(openedDirectory, sourceDirectoryName);
                var uniqueDirectoryName = GetUniqueFileName(sourceDirectoryName);

                if (openedDirectory == _soursDirectory)
                {
                    MessageBox.Show("Не можливо вставити папку саму в себе", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    Directory.CreateDirectory(uniqueDirectoryName);

                    await CopyDirectoryAsync(dirPath, destinationPath);

                    if (isMove)
                    {
                        await DeleteDirectoryAsync(dirPath);

                        isMove = false;
                    }
                }
            });
        }

        private string GetUniqueFileName(string fileName)
        {
            string directoryPath = Directory.GetCurrentDirectory();
            string extension = System.IO.Path.GetExtension(fileName);
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string newName = fileName;

            if (File.Exists(System.IO.Path.Combine(directoryPath, newName)))
            {
                int count = 2;

                while (File.Exists(System.IO.Path.Combine(directoryPath, newName)))
                {
                    newName = $"{fileNameWithoutExtension}({count}){extension}";
                    count++;
                }
            }

            return newName;
        }

        private Task CopyDirectoryAsync(string sourceDirectoryName, string destinationDirectoryName)
        {
            return Task.Run(async () =>
            {
                var directory = new DirectoryInfo(sourceDirectoryName);
                var dirs = directory.GetDirectories();
                var files = directory.GetFiles();

                Directory.CreateDirectory(destinationDirectoryName);

                foreach (var file in files)
                {
                    var tempPath = System.IO.Path.Combine(destinationDirectoryName, file.Name);
                    file.CopyTo(tempPath, false);
                }

                foreach (var subDir in dirs)
                {
                    var tempPath = System.IO.Path.Combine(destinationDirectoryName, subDir.Name);

                    await CopyDirectoryAsync(subDir.FullName, tempPath);
                }
            });
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = new List<DItem>(ItemsListBox.SelectedItems.Cast<DItem>());

            try
            {
                TryDeleteItems(selectedItems);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка видалення файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TryDeleteItems(List<DItem> Items)
        {
            foreach (DItem selectedFile in Items)
            {
                var itemPath = System.IO.Path.Combine(openedDirectory, selectedFile.Name);

                if (File.Exists(itemPath))
                {
                    await DeleteFileAsync(itemPath);
                }
                else if (Directory.Exists(itemPath))
                {
                    await DeleteDirectoryAsync(itemPath);
                    DeleteDirIfEmpty(itemPath);
                }

                UpdateItems();
            }
        }

        private Task DeleteFileAsync(string filePath)
        {
            return Task.Run(() =>
            {
                FileInfo fi = new FileInfo(filePath);

                fi.IsReadOnly = false;
                fi.Delete();
            });
        }

        private Task DeleteDirectoryAsync(string curDirPath)
        {
            return Task.Run(async () =>
            {
                DeleteDirectoryFiles(curDirPath);

                string[] directories = Directory.GetDirectories(curDirPath);

                foreach (string dirPath in directories)
                {
                    await DeleteDirectoryAsync(dirPath);
                }

                DeleteDirIfEmpty(curDirPath);
            });
        }

        private void DeleteDirectoryFiles(string dirPath)
        {
            string[] files = Directory.GetFiles(dirPath);

            foreach (string filePath in files)
            {
                FileInfo fi = new FileInfo(filePath);

                fi.IsReadOnly = false;
                fi.Delete();
            }
        }

        private void DeleteDirIfEmpty(string dirPath)
        {
            if (Directory.Exists(dirPath))
            {
                string[] dirFiles = Directory.GetFiles(dirPath);
                string[] dirDirectories = Directory.GetDirectories(dirPath);

                if (dirFiles.Length == 0 && dirDirectories.Length == 0)
                {
                    DirectoryInfo di = new DirectoryInfo(dirPath);
                    di.Delete();
                }
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ItemsListBox.SelectedItem is DFile selectedFile)
                {
                    var oldFilePath = System.IO.Path.Combine(openedDirectory, selectedFile.Name);
                    var newFileName = Interaction.InputBox("Введіть нову назву файлу:", "Перейменувати файл", selectedFile.Name);

                    if (!string.IsNullOrWhiteSpace(newFileName))
                    {
                        var newFilePath = System.IO.Path.Combine(openedDirectory, newFileName);

                        File.Move(oldFilePath, newFilePath);
                        UpdateItems();
                    }
                }
                else if (ItemsListBox.SelectedItem is DDirectory selectedDirectory)
                {
                    var oldDirectoryPath = System.IO.Path.Combine(openedDirectory, selectedDirectory.Name);
                    var newDirectoryName = Interaction.InputBox("Введіть нову назву папки:", "Перейменувати папку", selectedDirectory.Name);

                    if (!string.IsNullOrWhiteSpace(newDirectoryName))
                    {
                        var newDirectoryPath = System.IO.Path.Combine(openedDirectory, newDirectoryName);

                        if (!Directory.Exists(newDirectoryPath))
                        {
                            Directory.Move(oldDirectoryPath, newDirectoryPath);
                            UpdateItems();
                        }
                        else
                        {
                            MessageBox.Show("Папка з такою назвою вже існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Виберіть файл або папку для перейменування.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка перейменування: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                foreach (CancellationTokenSource tokenSource in tokens)
                {
                    tokenSource.Cancel();
                }

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

            string phrase = SearchTextBox.Text.Trim();

            if (phrase != "Пошук")
            {
                model.ClearItems();

                if (string.IsNullOrEmpty(phrase))
                {
                    UpdateItems();
                }
                else
                {
                    SearchDirectories(model.Path, phrase);
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
                        .EnumerateFileSystemEntries(rootPath, "*.*")
                        .Where(path => System.IO.Path.GetFileName(path)
                        .Contains(phrase, StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });

            await UpdateItemsByTypeAsync(foundItems.ToArray());

        }

        private async void SortFromAtoZ(string rootPath)
        {
            List<string> sortedItems = new List<string>();
            await Task.Run(() =>
            {
                try
                {
                    sortedItems.AddRange(Directory
                .EnumerateFileSystemEntries(rootPath, "*.*")
                .OrderBy(path => path));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });
            await UpdateItemsByTypeAsync(sortedItems.ToArray());
        }
        private async void SortFromZtoA(string rootPath)
        {
            List<string> sortedItems = new List<string>();
            await Task.Run(() =>
            {
                try
                {
                    sortedItems.AddRange(Directory
                .EnumerateFileSystemEntries(rootPath, "*.*")
                .OrderByDescending(path => path));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });
            await UpdateItemsByTypeAsync(sortedItems.ToArray());
        }
        
        private async void SortDate(string rootPath)
        {
            List<DItem> sortedItems = new List<DItem>();
            await Task.Run(() =>
            {
                try
                {
                    var items = Directory.EnumerateFileSystemEntries(rootPath, "*.*")
                        .Select(path =>
                        {
                            var itemName = System.IO.Path.GetFileName(path);
                            var itemDate = Directory.GetLastWriteTime(path);
                            if (Directory.Exists(path))
                            {
                                return new DDirectory(itemName, itemDate, path, "Assets/folder.png") as DItem;
                            }
                            else
                            {
                                var itemSize = new FileInfo(path).Length;
                                return new DFile(itemName, itemDate, path, itemSize, GetImageForFile(itemName)) as DItem;
                            }
                        })
                        .OrderBy(item => item.Date);

                     sortedItems.AddRange(items);
                    Dispatcher.Invoke(() =>
                    {
                        model.ClearItems();
                        foreach (var item in sortedItems)
                        {
                            model.AddItem(item);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });

           
        }
        private async void SortDateDesc(string rootPath)
        {
            List<DItem> sortedItems = new List<DItem>();
            await Task.Run(() =>
            {
                try
                {
                    var items = Directory.EnumerateFileSystemEntries(rootPath, "*.*")
                        .Select(path =>
                        {
                            var itemName = System.IO.Path.GetFileName(path);
                            var itemDate = Directory.GetLastWriteTime(path);
                            if (Directory.Exists(path))
                            {
                                return new DDirectory(itemName, itemDate, path, "Assets/folder.png") as DItem;
                            }
                            else
                            {
                                var itemSize = new FileInfo(path).Length;
                                return new DFile(itemName, itemDate, path, itemSize, GetImageForFile(itemName)) as DItem;
                            }
                        })
                        .OrderByDescending(item => item.Date);

                    sortedItems.AddRange(items);
                    Dispatcher.Invoke(() =>
                    {
                        model.ClearItems();
                        foreach (var item in sortedItems)
                        {
                            model.AddItem(item);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });
           
        }
        private async void SortSize(string rootPath)
        {
            List<DItem> sortedItems = new List<DItem>();
            await Task.Run(() =>
            {
                try
                {
                    var items = Directory.EnumerateFileSystemEntries(rootPath, "*.*")
                        .Select(path =>
                        {
                            var itemName = System.IO.Path.GetFileName(path);
                            var itemDate = Directory.GetLastWriteTime(path);
                            if (Directory.Exists(path))
                            {
                                return new DDirectory(itemName, itemDate, path, "Assets/folder.png") as DItem;
                            }
                            else
                            {
                                var itemSize = new FileInfo(path).Length;
                                return new DFile(itemName, itemDate, path, itemSize, GetImageForFile(itemName)) as DItem;
                            }
                        })
                        .OrderBy(item => item.Size);

                    sortedItems.AddRange(items);
                    Dispatcher.Invoke(() =>
                    {
                        model.ClearItems();
                        foreach (var item in sortedItems)
                        {
                            model.AddItem(item);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });
          
           
        }
        private async void SortSizeDesc(string rootPath)
        {
            List<DItem> sortedItems = new List<DItem>();
            await Task.Run(() =>
            {
                try
                {
                    var items = Directory.EnumerateFileSystemEntries(rootPath, "*.*")
                        .Select(path =>
                        {
                            var itemName = System.IO.Path.GetFileName(path);
                            var itemDate = Directory.GetLastWriteTime(path);
                            if (Directory.Exists(path))
                            {
                                return new DDirectory(itemName, itemDate, path, "Assets/folder.png") as DItem;
                            }
                            else
                            {
                                var itemSize = new FileInfo(path).Length;
                                return new DFile(itemName, itemDate, path, itemSize, GetImageForFile(itemName)) as DItem;
                            }
                        })
                        .OrderByDescending(item => item.Size);

                    sortedItems.AddRange(items);
                    Dispatcher.Invoke(() =>
                    {
                        model.ClearItems();
                        foreach (var item in sortedItems)
                        {
                            model.AddItem(item);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Неможливо доступитися до {rootPath}: {ex.Message}");
                }
            });
            
        }
        private void Sort_btn(object sender, RoutedEventArgs e)
        {
            if (SortButton.ContextMenu != null)
            {
                SortButton.ContextMenu.PlacementTarget = SortButton;
                SortButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                SortButton.ContextMenu.IsOpen = true;
            }

            
        }
       
        private void SortFromA(object sender, RoutedEventArgs e)
        {
            model.ClearItems();
            SortFromAtoZ(model.Path);
        }

        private void SortFromZ(object sender, RoutedEventArgs e)
        {
            model.ClearItems();
            SortFromZtoA(model.Path);
        }

        private void SortByDate(object sender, RoutedEventArgs e)
        {
            model.ClearItems();
            SortDate(model.Path);
        }

        private void SortBySize(object sender, RoutedEventArgs e)
        {
            model.ClearItems();
            SortSize(model.Path);
        }

        private void SortDateRev(object sender, RoutedEventArgs e)
        {
            model.ClearItems();
            SortDateDesc(model.Path);
        }
        private void SortSizeRev(object sender, RoutedEventArgs e)
        {
            model.ClearItems();
            SortSizeDesc(model.Path);
        }
        public string GetImageForFile(string fileName)
        {
            string extension = System.IO.Path.GetExtension(fileName).ToLower();
            string image = "";

            if (extension == ".txt")
            {
                image = "Assets/document.png";
            }
            else if (extension == ".json" || extension == ".xml" || extension == ".xaml" || extension == ".cs" || extension == ".cpp" || extension == ".html" || extension == ".css")
            {
                image = "Assets/dev-file.png";
            }
            else if (extension == ".ini" || extension == ".dll")
            {
                image = "Assets/sett-file.png";
            }
            else if (extension == ".exe")
            {
                image = "Assets/exe-file.png";
            }
            else if (extension == ".js")
            {
                image = "Assets/js-file.png";
            }
            else if (extension == ".java")
            {
                image = "Assets/java.png";
            }
            else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
            {
                image = "Assets/picture.png";
            }
            else if (extension == ".mp3" || extension == ".wav" || extension == ".ogg")
            {
                image = "Assets/music.png";
            }
            else if (extension == ".mp4" || extension == ".mkv" || extension == ".mpeg" || extension == ".avi")
            {
                image = "Assets/video.png";
            }
            else if (extension == ".zip" || extension == ".rar" || extension == ".7z")
            {
                image = "Assets/archive.png";
            }
            else if (extension == ".py")
            {
                image = "Assets/python.png";
            }
            else if (extension == ".doc" || extension == ".docx" || extension == ".docm" || extension == ".ppt" || extension == ".pptx" || extension == ".xls" || extension == ".xlsm" || extension == ".xlsx" || extension == ".accdb")
            {
                image = "Assets/mc-office.png";
            }
            else if (extension == ".psd" || extension == ".ai" || extension == ".indd" || extension == ".prproj")
            {
                image = "Assets/adobe.png";
            }
            else
            {
                image = "Assets/file.png";
            }

            return image;
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
        public int ItemCount { get; set; } = 0;
        public long TotalSize { get; set; } = 0;
        public string TotalSizeString { get; set; } = "0 МБ";
        public IEnumerable<DItem> Items => items;
        public IEnumerable<DDirectory> MyFolders => myFolders;

        private List<string> Units = new List<string>()
            {
                "Байт", "КБ", "МБ", "ГБ", "ТБ", "ПТ"
            };

        public Model()
        {
            Path = null!;

            items = new ObservableCollection<DItem>();
            myFolders = new ObservableCollection<DDirectory>()
            {
                new DDirectory("Диски", "<=Discs=>", "Assets/hdd.png"),
                new DDirectory("Робочий стіл", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Assets/computer.png"),
                new DDirectory("Завантаження", @$"C:\Users\{Environment.UserName}\Downloads", "Assets/download.png"),
                new DDirectory("Документи", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Assets/document.png"),
                new DDirectory("Зображення", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Assets/picture.png"),
                new DDirectory("Відео", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Assets/video.png"),
                new DDirectory("Музика", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Assets/music.png"),
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

        public void UpdateTotalSize(long size)
        {
            TotalSize = size;
            TotalSizeString = UpdateSize(size);
        }

        public string UpdateSize(long size)
        {
            string remainderString = "";
            long convertedSize = size;
            int unitIndex = ConvertUnit(ref convertedSize);
            long roundedSize = convertedSize;

            for (int i = 0; i < unitIndex; i++)
            {
                roundedSize *= 1024;
            }

            long byteDifference = (size - roundedSize);

            if (byteDifference > 0)
            {
                ConvertUnit(ref byteDifference);
                int remainder = (int)((100 / 1024.0) * byteDifference);

                remainderString = $",{remainder}";
            }

            return $"{convertedSize}{remainderString} {Units[unitIndex]}";
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
    public class DItem
    {
        private List<string> Units = new List<string>()
        {
            "Байт", "КБ", "МБ", "ГБ", "ТБ", "ПТ"
        };

        private const int MAX_NAME_SIZE = 25;

        public string Name { get; set; }
        public string Path { get; set; }
        public string Date { get; set; }
        public long Size { get; set; }
        public string SizeString { get; set; } = "Розрахунок...";
        public string IconPath { get; set; }
        public Visibility ProgressVisibility { get; set; }
        public int ProgressSize { get; set; }
        public double PercentSize { get; set; }
        public long UsedSpace { get; set; }

        public DItem()
        {
            Name = null!;
            Date = null!;
            Path = null!;
            IconPath = null!;
            ProgressVisibility = Visibility.Collapsed;
            ProgressSize = 155;
        }

        public DItem(string name, DateTime date, string path, string iconPath)
        {
            if (name.Length >= MAX_NAME_SIZE)
            {
                Name = $"{name.Substring(0, MAX_NAME_SIZE - 1)}...";
            }
            else
            {
                Name = name;
            }

            Date = DateUK.ConvertDate(date);
            Path = path;
            IconPath = iconPath;
            ProgressVisibility = Visibility.Collapsed;
            ProgressSize = 155;
        }

        public void UpdateItemSize(long size)
        {
            Size = size;
            SizeString = UpdateSize(size);
        }

        public string UpdateSize(long size)
        {
            string remainderString = "";
            long convertedSize = size;
            int unitIndex = ConvertUnit(ref convertedSize);
            long roundedSize = convertedSize;

            for (int i = 0; i < unitIndex; i++)
            {
                roundedSize *= 1024;
            }

            long byteDifference = (size - roundedSize);

            if (byteDifference > 0)
            {
                ConvertUnit(ref byteDifference);
                int remainder = (int)((100 / 1024.0) * byteDifference);

                remainderString = $",{remainder}";
            }

            return $"{convertedSize}{remainderString} {Units[unitIndex]}";
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
        public long TotalSpace { get; set; }
        public string TotalSpaceString { get; set; } = "";
        public long FreeSpace { get; set; }
        public string FreeSpaceString { get; set; } = "";

        public DDrive(DriveInfo driveInfo)
        {
            Name = $"Локальний диск ({driveInfo.Name.Substring(0, driveInfo.Name.Length - 1)})";
            Path = driveInfo.Name;
            TotalSpace = driveInfo.TotalSize;
            FreeSpace = driveInfo.AvailableFreeSpace;
            UsedSpace = TotalSpace - FreeSpace;
            PercentSize = ((double)UsedSpace / TotalSpace) * 100;
            ProgressVisibility = Visibility.Visible;
            SizeString = "";
            ProgressSize = 115;
            TotalSpaceString = UpdateSize(TotalSpace);
            FreeSpaceString = UpdateSize(FreeSpace);
            Date = $"{FreeSpaceString} вільно з {TotalSpaceString}";
            IconPath = "Assets/hdd.png";
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DDirectory : DItem
    {
        public DDirectory(string name, DateTime date, string path, string iconPath) : base(name, date, path, iconPath) { }

        public DDirectory(string name, string path, string iconPath)
        {
            Name = name;
            Path = path;
            IconPath = iconPath;
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class DFile : DItem
    {
        public DFile(string name, DateTime date, string path, long size, string iconPath) : base(name, date, path, iconPath)
        {
            Path = path;
            UpdateItemSize(size);
            IconPath = iconPath;
        }
    }
}