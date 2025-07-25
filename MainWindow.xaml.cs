using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Shapes;
using static KontextDatasetHelper.Utils;


namespace KontextDatasetHelper
{
    public partial class MainWindow : Window
    {
        private string INFO =
            "- Ensure you have Python installed and added to PATH.\n" +
            "- Ensure you have Pillow (python package) installed and accessible globally.\n" +
            "- Create a workspace folder for your dataset management - let's call it ROOT.\n" +
            "- Create (literaly named) 'base' and 'ref' folders inside ROOT.\n" +
            "- Place A COPY OF YOUR 'base' images in 'base' folder and A COPY OF YOUR 'ref' images in 'ref' folder.\n" +
            "- Ensure all images you copied are in either one of the supported formats: '.png', '.jpg', '.jpeg', '.webp', '.bmp', '.gif' .\n" +
            "- Ensure your image pairs have the same name and same extension!! - This is critical! -\n" +
            "- Ensure you don't have images with the same name but different extensions (ex: ROOT\\base\\1.jpeg + ROOT\\base\\1.png).\n" +
            "- Copy the 'prepare_images_for_project.bat', 'prepare_images_for_project.py' and 'Kontext_LoRA_Dataset_Helper.exe' to ROOT.\n\n" +
            "- Run 'prepare_images_for_project.bat' (will simply execute the .py script).\n" +
            "  The .py script will do the following:\n" +
            "  - Converts all .bmp/.gif/.webp images in 'base' and 'ref' to .png.\n" +
            "  - Downscales all images to a maximum width/height of 2048 while preserving aspect ratio and retaining as much quality as possible - if necessary.\n" +
            "  - Ensures each image pair has the exact same resolution by downscaling the highest resolution image to the resolution of the other - if necessary.\n" +
            "  (Images will be overwritten so make sure you still have the originals saved elsewhere)\n\n" +
            "- Finally, run Kontext_LoRA_Dataset_Helper.exe\n\n" +
            "Notes:\n" +
            "  - Converting .webp images and downscaling to a maximum width/height of 2048 is necessary simply because WPF does not support .webp nor images with higher dimensions.\n" +
            "  - Most LoRA trainers do not support .bmp so we will be converting those to .png as well.\n" +
            "  - Result images (the edited one and its pairing) will be saved as RGB .png at 'ROOT\\final\\{base/ref}' - when the 'save' button is pressed.\n" +
            "  - IMPORTANT: The program skips (on launch) any image pair for pairs whose base image filename already exists in 'ROOT\\final\\base'.\n\n\n" +
            "Are you ready to proceed?";

        string[] CAPTIONS_INFO = new string[]
        {
            "# Place your possible captions here separated by new lines.",
            "# Empty lines and trimmed lines that start with '#' or '/' will be ignored.",
            "# This feature is only meant to be used if you plan on only using a very small amount of possible captions for all your image pairs.",
            "# In that case - since you are already inspecting the images here - might as well set the captions too.",
            "# Once you write the captions here you can pick which one to apply in the DropDownMenu next to the 'Set Captions' button.",
            "# The selected caption will be written upon pressing the 'save' button.",
            "# TIP1: If you want to bind a name of a caption to the DropDownMenu then you can do so like this:",
            "#        [NAME:YOUR CAPTION NAME HERE] YOUR ACTUAL CAPTION HERE",
            "# TIP2: Use Ctrl/Shift/Alt + Number/NumpadNumber as shortcuts for caption selection by index (order).",
            "# (Press the 'Set Captions' button again to close the textbox)", "", ""
        };


        private string _rootDirectory = "";
        private string _deletedDirectory = "";
        private string _baseImageDirectory = "";
        private string _refImageDirectory = "";
        private string _storeDirectory = "";
        private string _storeBaseImageDirectory = "";
        private string _storeRefImageDirectory = "";
        private string _storeCaptionsDirectory = "";
        
        public string BaseImagePath { get; private set; }
        public string RefImagePath { get; private set; }
        public string MergedImageStorePath { get; private set; }
        public string OtherImagePath { get; private set; }
        public string OtherImageStorePath { get; private set; }
        public string CaptionStorePath { get; private set; }


        private string _captionsPath = "";
        public static List<string> _captions = new List<string>();
        private List<string> _previousCaptions = new List<string>(); // updated only through combobox updater
        private string _currentCaption = "";
        private int _currentDropDownItemsCount = 0;

        private List<string> _imageFileNames;
        private int _currentIndex = 0;

        // These store the actual pixel data of the masks, original resolution
        private WriteableBitmap _baseMaskBitmap;
        private WriteableBitmap _refMaskBitmap;

        // these are only for the visual feedback of the brush size when the user hovers the mouse over the canvas
        // An ellipse is generated on the mouse move event and assigned to these which in turn are added to the respective canvas and immediately turned null and removed
        // from the canvas on mouse leave event
        private Ellipse _currentFeedbackEllipseBase; 
        private Ellipse _currentFeedbackEllipseRef;

        private Point _lastMousePosition;
        private bool _isDrawing = false;

        private BitmapSource _currentBaseImageOriginal;
        private BitmapSource _currentRefImageOriginal;
        private BitmapSource _currentMergedImage;
        private BitmapSource _currentBaseDiffImage;
        private BitmapSource _currentRefDiffImage;
        private BitmapSource _currentMergedDiffImage;

        private BitmapSource _currentSavedMergeImage;


        private bool _isMainBase = false;
        private bool _isWritting = false;
        private bool _isUpdatingMerge = false;
        private bool _isAnalyzingMerge = false;
        private bool _isUpdatingSaveButton = false;

        private bool _DrawnMask = false;

        // Get a reference to the UI thread's dispatcher.
        // You can get this from any UI element, e.g., a Window or the Application itself.
        // For simplicity, using Application.Current.Dispatcher is often suitable.
        System.Windows.Threading.Dispatcher uiDispatcher = System.Windows.Application.Current.Dispatcher;

        private bool _isInitialized = false;

        private SolidColorBrush _background1 = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF000000"));
        private SolidColorBrush _background2 = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2A2A2A"));
        private SolidColorBrush _background3 = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF525252"));
        private SolidColorBrush _savedButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00AB38"));
        private SolidColorBrush _foregroundOrange = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF6900"));

        public SolidColorBrush Background1 { get { return _background1; } }
        public SolidColorBrush Background2 { get { return _background2; } }
        public SolidColorBrush Background3 { get { return _background3; } }
        public SolidColorBrush ForeGroundOrange { get { return _foregroundOrange; } }


        Stack<Memory> undoStack = new Stack<Memory>();
        Stack<Memory> redoStack = new Stack<Memory>();


        public class CaptionItem
        {
            public string Content { get; set; } // What's displayed
            public string Value { get; set; }   // The actual caption string or identifier

            public CaptionItem() { }

            public CaptionItem(int i, string text)
            {
                text = text.Trim();
                string lowerTrimmedText = text.ToLower();

                if (lowerTrimmedText.StartsWith("[name:"))
                {
                    int closingBracketIndex = text.IndexOf(']');
                    if (closingBracketIndex != -1)
                    {
                        Content = text.Substring(6, closingBracketIndex - 6).Trim();
                        Value = text.Substring(closingBracketIndex + 1).Trim();
                        return;
                    }
                }

                Content = $"Caption #{i} (" + _captions[i].Substring(0, _captions[i].Length >= 30 ? 30 : _captions[i].Length) + (_captions[i].Length > 30 ? " ...)" : ")");
                Value = _captions[i];
            }
        }

        public class Memory
        {
            public WriteableBitmap _baseMaskBitmap;
            public WriteableBitmap _refMaskBitmap;
            public Memory(WriteableBitmap baseMaskBitmap, WriteableBitmap refMaskBitmap)
            {
                _baseMaskBitmap = baseMaskBitmap;
                _refMaskBitmap = refMaskBitmap;
            }
            public Memory Clone()
            {
                return new Memory(CloneWriteableBitmap(_baseMaskBitmap), CloneWriteableBitmap(_refMaskBitmap));
            }
        }



        public MainWindow()
        {
            this.DataContext = this; // required for color binding
            this.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/Images/titlebar.png"));
            this.ContentRendered += MainWindow_ContentRendered;
            this.SizeChanged += (_, __) => UpdateMergedClip();

            InitializeComponent();
            InitializeDirectories();
            InitializeCaptions();
            LoadImagePairNames();
            LoadCurrentImagePair();
        }



        




        #region Application & Window Lifecycle Management
        private void Window_Loaded(object sender, RoutedEventArgs e) // apply margin on startup
        {
            AdjustMarginsForWindowState();
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            _isInitialized = true;

            string substr = _isMainBase ? "Ref" : "Base";
            ShowMergeDiffButton.Content = ShowMergeDiffButton.Content.ToString().Contains("Show") ? $"Show Merge-{substr} Difference" : $"Hide Merge-{substr} Difference";
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            AdjustMarginsForWindowState();
        }

        private void AdjustMarginsForWindowState()
        {
            if (WindowState == WindowState.Maximized)
            {
                TitleBarGrid.Margin = new Thickness(0, 8, 0, 0);
            }
            else
            {
                TitleBarGrid.Margin = new Thickness(0);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender.ToString() != "ImageMaskApp.MainWindow")
            {
                DragMove();
            }
        }

        private void buttonMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // Note: Windows reserves a few pixels (typically 7–8px) for the resize border (called the non-client area)
        // We need to compensate for this by setting a custom TitleBarGrid Margin based on WindowState - otherwise the titlebar.png and the bar itself will be clipped
        private void buttonMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                maximizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Images/maximize.png"));

            }
            else
            {
                this.WindowState = WindowState.Maximized;
                maximizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Images/restore.png"));
            }
            AdjustMarginsForWindowState();
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion



        #region Image & Data Loading/Initialization
        private void InitializeDirectories()
        {
            _rootDirectory = new FileInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).Directory.FullName;

            if (MessageBox.Show(INFO, "INSTRUCTIONS", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                Environment.Exit(0);
            }

            _deletedDirectory = _rootDirectory + "\\deleted";
            _baseImageDirectory = _rootDirectory + "\\base";
            _refImageDirectory = _rootDirectory + "\\ref";

            if (!Directory.Exists(_baseImageDirectory) || !Directory.Exists(_refImageDirectory))
            {
                if (Directory.Exists(@"O:\SD\DATASETS\test")) // hardcoded a test dataset to make it easier for me to debug
                {
                    _rootDirectory = @"O:\SD\DATASETS\test";
                    _deletedDirectory = _rootDirectory + "\\deleted";
                    _baseImageDirectory = _rootDirectory + "\\base";
                    _refImageDirectory = _rootDirectory + "\\ref";
                }
                else
                {
                    MessageBox.Show("You are missing at least one of the following directories:\n" +
                        _baseImageDirectory + "\n" +
                        _refImageDirectory + "\n\n" +
                        "Please follow the instructions properly and ensure those directories exist.\nThe program will now terminate", "WARNING", MessageBoxButton.OK);
                    Environment.Exit(0);
                }   
            }

            _storeDirectory = _rootDirectory + "\\final";

            _storeBaseImageDirectory = _storeDirectory + "\\base";
            _storeRefImageDirectory = _storeDirectory + "\\ref";
            _storeCaptionsDirectory = _storeDirectory + "\\captions";

            foreach (string dir in new string[]
            {
                _deletedDirectory,
                _deletedDirectory + "\\base",
                _deletedDirectory + "\\ref",

                _storeDirectory,
                _storeBaseImageDirectory,
                _storeRefImageDirectory,
                _storeCaptionsDirectory,
            })
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private void InitializeCaptions()
        {
            _captionsPath = _rootDirectory + "\\captions.txt";
            if (!System.IO.File.Exists(_captionsPath) || System.IO.File.ReadAllText(_captionsPath).Trim() == "")
            {
                System.IO.File.WriteAllLines(_captionsPath, CAPTIONS_INFO, System.Text.Encoding.UTF8);
            }
            CaptionInputTextBox.Text = System.IO.File.ReadAllText(_captionsPath, System.Text.Encoding.UTF8);
            UpdateCaptions();
        }

        private void UpdatePathProperties()
        {
            BaseImagePath = System.IO.Path.Combine(_baseImageDirectory, _imageFileNames[_currentIndex]);
            RefImagePath = System.IO.Path.Combine(_refImageDirectory, _imageFileNames[_currentIndex]);

            string filename = System.IO.Path.GetFileNameWithoutExtension(_imageFileNames[_currentIndex]) + ".png";
            MergedImageStorePath = System.IO.Path.Combine(_isMainBase ? _storeBaseImageDirectory : _storeRefImageDirectory, filename);

            OtherImagePath = _isMainBase ? BaseImagePath : RefImagePath;
            OtherImageStorePath = (_isMainBase ? _storeRefImageDirectory : _storeBaseImageDirectory) + "\\" + filename;

            CaptionStorePath = System.IO.Path.Combine(_storeCaptionsDirectory, System.IO.Path.GetFileNameWithoutExtension(_imageFileNames[_currentIndex]) + ".txt");

            TitleBarTextBox.Text = _imageFileNames[_currentIndex];
        }

        private void UpdateCaptions()
        {
            _captions = CaptionInputTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.Trim() != "" && !l.Trim().StartsWith("#") && !l.Trim().StartsWith("/"))
                .ToList();

            if (CaptionInputTextBox.Text.Trim() == "")
            {
                System.IO.File.WriteAllLines(_captionsPath, CAPTIONS_INFO, System.Text.Encoding.UTF8);
                CaptionInputTextBox.Text = System.IO.File.ReadAllText(_captionsPath, System.Text.Encoding.UTF8);
            }
            else
            {
                System.IO.File.WriteAllText(_captionsPath, CaptionInputTextBox.Text, System.Text.Encoding.UTF8);
            }

            UpdateComboBoxItems();
        }

        private void LoadImagePairNames()
        {
            List<string> baseFiles = new List<string>();
            List<string> refFiles = new List<string>();
            List<string> storeBaseFiles = new List<string>();

            foreach (string validExt in new string[] { ".jpg", ".jpeg", ".png" })
            {
                baseFiles.AddRange(Directory.GetFiles(_baseImageDirectory, "*" + validExt, SearchOption.TopDirectoryOnly)
                                    .Select(System.IO.Path.GetFileName)
                                    .ToList()
                                    .ConvertAll(fn => fn.ToLower()));
                refFiles.AddRange(Directory.GetFiles(_refImageDirectory, "*" + validExt, SearchOption.TopDirectoryOnly)
                                    .Select(System.IO.Path.GetFileName)
                                    .ToList()
                                    .ConvertAll(fn => fn.ToLower()));
                storeBaseFiles.AddRange(Directory.GetFiles(_storeBaseImageDirectory, "*" + validExt, SearchOption.TopDirectoryOnly)
                                    .Select(System.IO.Path.GetFileName)
                                    .ToList()
                                    .ConvertAll(fn => fn.ToLower()));
            }

            _imageFileNames = baseFiles.Intersect(refFiles).OrderBy(f => f)
                .Where(f => 
                    !storeBaseFiles.Contains(System.IO.Path.GetFileNameWithoutExtension(f) + ".jpg") &&
                    !storeBaseFiles.Contains(System.IO.Path.GetFileNameWithoutExtension(f) + ".jpeg") &&
                    !storeBaseFiles.Contains(System.IO.Path.GetFileNameWithoutExtension(f) + ".png")
                    ).ToList();

            if (!_imageFileNames.Any())
            {
                MessageBox.Show("No matching image pairs found in the specified directories.");
                // Optionally, disable buttons or close the application here
                Environment.Exit(0);
            }
        }

        private void LoadCurrentImagePair()
        {
            if (_imageFileNames == null || _imageFileNames.Count == 0 || _currentIndex < 0 || _currentIndex >= _imageFileNames.Count)
            {
                // Clear displays if no images
                BaseImageDisplay.Source = null;
                RefImageDisplay.Source = null;
                MergedImageDisplay.Source = null;

                BaseDiffImageDisplay.Source = null;
                RefDiffImageDisplay.Source = null;
                MergedDiffImageDisplay.Source = null;

                _currentBaseImageOriginal = null;
                _currentRefImageOriginal = null;
                _currentMergedImage = null;

                _currentBaseDiffImage = null;
                _currentRefDiffImage = null;
                _currentMergedDiffImage = null;

                _baseMaskBitmap = null;
                _refMaskBitmap = null;
                return;
            }

            UpdatePathProperties();

            if (File.Exists(BaseImagePath) && File.Exists(RefImagePath))
            {
                _currentBaseImageOriginal = LoadBitmap(BaseImagePath);
                _currentRefImageOriginal = LoadBitmap(RefImagePath);

                if (_currentBaseImageOriginal.PixelWidth != _currentRefImageOriginal.PixelWidth || _currentBaseImageOriginal.PixelHeight != _currentRefImageOriginal.PixelHeight)
                {
                    MessageBox.Show($"Detected mismatched resolution pair for: {_imageFileNames[_currentIndex]}. Skipping to next.");
                    Next_Click(null, null); // Try next pair
                    return;
                }

                BaseImageDisplay.Source = _currentBaseImageOriginal;
                RefImageDisplay.Source = _currentRefImageOriginal;

                // Initialize masks with same dimensions as original images, all transparent (Alpha = 0)
                _baseMaskBitmap = new WriteableBitmap(_currentBaseImageOriginal.PixelWidth, _currentBaseImageOriginal.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                _refMaskBitmap = new WriteableBitmap(_currentRefImageOriginal.PixelWidth, _currentRefImageOriginal.PixelHeight, 96, 96, PixelFormats.Bgra32, null);

                BaseMaskDisplay.Source = _baseMaskBitmap;
                RefMaskDisplay.Source = _refMaskBitmap;

                BaseOrRefComparerDisplay.Source = _isMainBase ? _currentBaseImageOriginal : _currentRefImageOriginal;

                UpdateMergeAndDiff();

                UpdateMergedClip(true);
            }
            else
            {
                MessageBox.Show($"Could not find pair for: {_imageFileNames[_currentIndex]}. Skipping to next.");
                // Handle error or skip to next pair
                Next_Click(null, null); // Try next pair
            }
        }
        #endregion



        #region UI Element Interaction & Display Updates
        private void ApplyAndSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBaseImageOriginal == null || _currentRefImageOriginal == null)
            {
                MessageBox.Show("No images loaded.");
                return;
            }

            while (_isUpdatingMerge || _isAnalyzingMerge) // both of these are running in a separate thread so its safe to do Thread.Sleep() here
            {
                System.Threading.Thread.Sleep(25);
            }


            if (_currentMergedImage == null)
            {
                MessageBox.Show("Merged image not yet generated. Please wait a bit before clicking save.");
                return;
            }

            bool success = false;
            try
            {
                SaveBitmapAsRgb(_currentMergedImage, MergedImageStorePath);
                // also write the other image instead of copying (even though its unchanged) - to ensure same PixelFormat for both
                SaveBitmapAsRgb(_isMainBase ? _currentRefImageOriginal : _currentBaseImageOriginal, OtherImageStorePath);

                if (_currentCaption != "")
                {
                    System.IO.File.WriteAllText(CaptionStorePath, _currentCaption, System.Text.Encoding.UTF8);
                }
                else // delete stored caption when it exists and current caption is empty string
                {
                    if (System.IO.File.Exists(CaptionStorePath))
                    {
                        System.IO.File.Delete(CaptionStorePath);
                    }
                }

                success = true;
                SaveButton.Background = _savedButtonBackground;
                SaveButton.Content = "Saved";
                SaveButton.Foreground = new SolidColorBrush(Colors.Yellow);

                _currentSavedMergeImage = CloneBitmapSource(_currentMergedImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }

            if (!success)
            {
                SaveButton.Background = _background3;
                SaveButton.Content = "Apply and Save";
                SaveButton.Foreground = new SolidColorBrush(Colors.White);

                foreach (string file in new string[]
                {
                    MergedImageStorePath,
                    OtherImageStorePath,
                    CaptionStorePath
                })
                {
                    try
                    {
                        System.IO.File.Delete(file);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.Yes == MessageBox.Show("This pair will be moved to \\deleted folder.\n\nAre you sure about this?", "Move to \\deleted ?", MessageBoxButton.YesNo))
            {
                System.IO.File.Move(BaseImagePath, _deletedDirectory + "\\base\\" + System.IO.Path.GetFileName(BaseImagePath));
                System.IO.File.Move(RefImagePath, _deletedDirectory + "\\ref\\" + System.IO.Path.GetFileName(RefImagePath));
                Next_Click(null, null);
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            _currentSavedMergeImage = null;
            ClearMemory();
            ClearMasks(false);
            _DrawnMask = false;

            _currentIndex++;
            if (_currentIndex >= _imageFileNames.Count)
            {
                _currentIndex = 0; // Loop back to start
            }
            LoadCurrentImagePair();
            UpdateSaveButton();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            _currentSavedMergeImage = null;
            ClearMemory();
            ClearMasks(false);
            _DrawnMask = false;

            _currentIndex--;
            if (_currentIndex < 0)
            {
                _currentIndex = _imageFileNames.Count - 1; // Loop to end
            }
            LoadCurrentImagePair();
            UpdateSaveButton();
        }

        private void ClearMasks_Click(object sender, RoutedEventArgs e)
        {
            ClearMasks();
        }

        private void ShowBaseDifference_Click(object sender, RoutedEventArgs e)
        {
            string newContent = ShowBaseDiffButton.Content.ToString().Contains("Show") ? "Hide Base-Ref Difference" : "Show Base-Ref Difference";
            ShowBaseDiffButton.Content = newContent;
            ShowBaseDiffButton.Foreground = new SolidColorBrush(newContent.Contains("Show") ? Colors.White : Colors.Green);
            UpdateDiffImages();
        }

        private void ShowRefDifference_Click(object sender, RoutedEventArgs e)
        {
            string newContent = ShowRefDiffButton.Content.ToString().Contains("Show") ? "Hide Ref-Base Difference" : "Show Ref-Base Difference";
            ShowRefDiffButton.Content = newContent;
            ShowRefDiffButton.Foreground = new SolidColorBrush(newContent.Contains("Show") ? Colors.White : Colors.Green);
            UpdateDiffImages();
        }

        private void ShowMergeDifference_Click(object sender, RoutedEventArgs e)
        {
            string substr = _isMainBase ? "Ref" : "Base";
            string newContent = ShowMergeDiffButton.Content.ToString().Contains("Show") ? $"Hide Merge-{substr} Difference" : $"Show Merge-{substr} Difference";
            ShowMergeDiffButton.Content = newContent;
            ShowMergeDiffButton.Foreground = new SolidColorBrush(newContent.Contains("Show") ? Colors.White : Colors.Green);
            UpdateDiffImages();
        }

        private void OnDiffThresValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitialized)
            {
                UpdateDiffImages();
            }
        }

        private void SetMainImg_Click(object sender, RoutedEventArgs e)
        {
            string newContent = SetMainImgButtonTextBlock.Text.Contains("Main is: Base") ? "Main is: Ref\n(Draw on Base)" : "Main is: Base\n(Draw on Ref)";
            SetMainImgButtonTextBlock.Text = newContent;
            _isMainBase = newContent.Contains("Main is: Base");

            BaseOrRefComparerDisplay.Source = _isMainBase ? _currentRefImageOriginal : _currentBaseImageOriginal;

            UpdatePathProperties();
            UpdateMergeAndDiff();

            string substr = _isMainBase ? "Ref" : "Base";
            ShowMergeDiffButton.Content = ShowMergeDiffButton.Content.ToString().Contains("Show") ? $"Show Merge-{substr} Difference" : $"Hide Merge-{substr} Difference";
        }

        private void SetCaptions_Click(object sender, RoutedEventArgs e)
        {
            UpdateCaptions();

            if (CaptionInputTextBox.Visibility == Visibility.Collapsed && !_isWritting)
            {
                _isWritting = true;
                CaptionInputTextBox.Visibility = Visibility.Visible;
                CaptionInputTextBox.Focus();
            }
            else
            {
                _isWritting = false;
                CaptionInputTextBox.Visibility = Visibility.Collapsed;
            }

            UpdateSaveButton();
        }

        private bool CaptionsChanged()
        {
            if (_previousCaptions.Count != _captions.Count) { return true; }
            for (int i = 0; i < _captions.Count; i++)
            {
                if (_captions[i] != _previousCaptions[i]) { return true; }
            }
            return false;
        }

        private void UpdateComboBoxItems(int selectedIndex = -1)
        {
            if (CaptionsChanged() || captionComboBox.Items.Count == 0 || selectedIndex != -1)
            {
                _previousCaptions.Clear();
                _previousCaptions.AddRange(_captions);

                captionComboBox.Items.Clear(); // Clear existing items
                captionComboBox.Items.Add(new CaptionItem { Content = "None", Value = "" }); // Add 'None' as the first option

                for (int i = 0; i < _captions.Count; i++)
                {
                    captionComboBox.Items.Add(new CaptionItem(i, _captions[i]));
                }

                _currentDropDownItemsCount = _captions.Count + 1;
                
                captionComboBox.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex > _captions.Count ? _captions.Count : selectedIndex;
            }

            UpdateSaveButton();
        }

        private void CaptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (captionComboBox.SelectedItem == null)
            {
                _currentCaption = "";
            }
            else
            {
                CaptionItem ci = captionComboBox.SelectedItem as CaptionItem;
                _currentCaption = ci.Value;
            }

            UpdateSaveButton();
        }

        /// <summary>
        /// Checks if the current pair has been saved based on current caption settings and masks.
        /// </summary>
        private async void UpdateSaveButton()
        {
            if (_isInitialized)
            {
                _isUpdatingSaveButton = true;

                if (System.IO.File.Exists(MergedImageStorePath) && System.IO.File.Exists(OtherImageStorePath))
                {
                    bool identicalAndSameCaptions = ((_currentCaption == "" && !System.IO.File.Exists(CaptionStorePath)) || (System.IO.File.Exists(CaptionStorePath) && _currentCaption == System.IO.File.ReadAllText(CaptionStorePath)))
                        && await AreImagesIdenticalAsync(_currentMergedImage, _currentSavedMergeImage);

                    SaveButton.Background = identicalAndSameCaptions ? _savedButtonBackground : _background3;
                    SaveButton.Content = "Saved";
                    SaveButton.Foreground = new SolidColorBrush(Colors.Yellow);
                }
                else
                {
                    SaveButton.Background = _background3;
                    SaveButton.Content = "Apply and Save";
                    SaveButton.Foreground = new SolidColorBrush(Colors.White);
                }

                _isUpdatingSaveButton = false;
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void Undo()
        {
            LoadLastMemory();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }

        private void Redo()
        {
            if (redoStack.Count > 0)
            {
                Memory redoMem = redoStack.Pop();
                undoStack.Push(redoMem);
                LoadMemory(redoMem);
            }
        }

        private void OnMinRegionValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateAutoMask(false);
        }

        private void OnColorToleranceValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateAutoMask(false);
        }

        private void AutoMask_Click(object sender, RoutedEventArgs e)
        {
            UpdateAutoMask(true);
        }

        private void InvertedDiff_Click(object sender, RoutedEventArgs e)
        {
            UpdateAutoMask(true, true);
        }

        private void InvertBaseMask_Click(object sender, RoutedEventArgs e)
        {
            InvertCurrentMask(true);
        }

        private void InvertRefMask_Click(object sender, RoutedEventArgs e)
        {
            InvertCurrentMask(false);
        }

        private void ToggleMasks_Click(object sender, RoutedEventArgs e)
        {
            bool turnVisible = BaseMaskDisplay.Visibility == Visibility.Hidden;
            BaseMaskDisplay.Visibility = turnVisible ? Visibility.Visible : Visibility.Hidden;
            RefMaskDisplay.Visibility = turnVisible ? Visibility.Visible : Visibility.Hidden;
            ToggleMasksButton.Content = turnVisible ? "Hide Masks" : "Show Masks";
            ToggleMasksButton.Foreground = new SolidColorBrush(turnVisible ? Colors.White : Colors.Red);
        }

        // This is the core logic for CTRL + Mouse Wheel
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Check if the Control key is pressed
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Prevent the default scrolling behavior
                e.Handled = true;

                double delta = e.Delta > 0 ? 1 : -1; // +1 for scroll up, -1 for scroll down

                // Adjust the slider's value
                // Ensure the new value stays within the slider's min and max
                double newValue = BrushSizeSlider.Value + (delta * 3); // multiply by 3 to update faster
                newValue = newValue < BrushSizeSlider.Minimum ? BrushSizeSlider.Minimum
                    : newValue > BrushSizeSlider.Maximum ? BrushSizeSlider.Maximum
                    : newValue;

                BrushSizeSlider.Value = newValue;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (CaptionInputTextBox.Visibility != Visibility.Visible 
                && (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Alt))
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.Z)
                    {
                        Undo();

                        // Optionally, if you want to prevent the event from being handled by other controls
                        // or the default behavior (like undo in a TextBox), you can set e.Handled = true;
                        //e.Handled = true;
                    }
                    else if (e.Key == Key.Y)
                    {
                        Redo();
                    }
                }
                
                // these will work with ctrl/shift/alt
                if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                {
                    UpdateComboBoxItems(0);
                }
                else if (e.Key == Key.D1 || e.Key == Key.NumPad1)
                {
                    UpdateComboBoxItems(1);
                }
                else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
                {
                    UpdateComboBoxItems(2);
                }
                else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
                {
                    UpdateComboBoxItems(3);
                }
                else if (e.Key == Key.D4 || e.Key == Key.NumPad4)
                {
                    UpdateComboBoxItems(4);
                }
                else if (e.Key == Key.D5 || e.Key == Key.NumPad5)
                {
                    UpdateComboBoxItems(5);
                }
                else if (e.Key == Key.D6 || e.Key == Key.NumPad6)
                {
                    UpdateComboBoxItems(6);
                }
                else if (e.Key == Key.D7 || e.Key == Key.NumPad7)
                {
                    UpdateComboBoxItems(7);
                }
                else if (e.Key == Key.D8 || e.Key == Key.NumPad8)
                {
                    UpdateComboBoxItems(8);
                }
                else if (e.Key == Key.D9 || e.Key == Key.NumPad9)
                {
                    UpdateComboBoxItems(9);
                }
            }
        }

        private void ImageDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isDrawing)
            {
                // Sync canvas size with image size
                if (sender == BaseImageDisplay)
                {
                    BaseMaskCanvas.Width = BaseImageDisplay.ActualWidth;
                    BaseMaskCanvas.Height = BaseImageDisplay.ActualHeight;
                }
                if (sender == RefImageDisplay)
                {
                    RefMaskCanvas.Width = RefImageDisplay.ActualWidth;
                    RefMaskCanvas.Height = RefImageDisplay.ActualHeight;
                }

                ClearMemory();
                ClearMasks();
            }
        }

        private void MergedImageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateMergedClip();
        }

        private void UpdateMergedClip(bool reset = false)
        {
            if (reset)
            {
                MergedImageSlider.Value = 0; // this will trigger MergedImageSlider_ValueChanged() if current value differs from 0
            }

            if (!_isInitialized || BaseOrRefComparerDisplay.Source == null || MergedImageDisplay.Source == null) return;

            double gridWidth = MergedImageGrid.ActualWidth;
            double gridHeight = MergedImageGrid.ActualHeight;

            double sourceWidth = MergedImageDisplay.Source.Width;
            double sourceHeight = MergedImageDisplay.Source.Height;

            double imageAspect = sourceWidth / sourceHeight;
            double containerAspect = gridWidth / gridHeight;

            double visibleWidth, visibleHeight;
            double offsetX = 0, offsetY = 0;

            if (imageAspect > containerAspect)
            {
                // Image fills width, has vertical letterboxing
                visibleWidth = gridWidth;
                visibleHeight = gridWidth / imageAspect;
                offsetY = (gridHeight - visibleHeight) / 2;
            }
            else
            {
                // Image fills height, has horizontal letterboxing
                visibleHeight = gridHeight;
                visibleWidth = gridHeight * imageAspect;
                offsetX = (gridWidth - visibleWidth) / 2;
            }

            double MergedSliderLineLeft = MergedImageGrid.ActualWidth * MergedImageSlider.Value - (MergedSliderLine.ActualWidth / 2);
            MergedSliderLineLeft = MergedSliderLineLeft < 0 ? 0 : MergedSliderLineLeft;

            ClipRectangle.Rect = new Rect(0, 0, MergedSliderLineLeft - offsetX < 0 ? 0 : MergedSliderLineLeft - offsetX, visibleHeight);
            MergedSliderLine.Margin = new Thickness(MergedSliderLineLeft, 0, 0, 0);

            // make black vertical rectangle transparent when slider value = 0 / 1
            MergedSliderLine.Visibility = MergedImageSlider.Value == 0 || MergedImageSlider.Value == 1 ? Visibility.Hidden : Visibility.Visible;
        }
        #endregion



        #region Masking & Drawing Operations (Canvas Interactions)
        private void MaskCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            Canvas canvas = sender as Canvas;
            _lastMousePosition = e.GetPosition(canvas);
            DrawMaskStroke(canvas, e.GetPosition(canvas));
        }

        private void MaskCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Canvas canvas = sender as Canvas;

            Point currentMousePosition = e.GetPosition(canvas);

            double brushSize = BrushSizeSlider.Value;

            Ellipse ellipse = new Ellipse
            {
                Width = brushSize,
                Height = brushSize,
                Fill = new SolidColorBrush(_isDrawing ? Colors.Red : Colors.Yellow) { Opacity = _isDrawing ? 1 : 0.5 },
                Margin = new Thickness(currentMousePosition.X - brushSize / 2, currentMousePosition.Y - brushSize / 2, 0, 0)
            };

            if (ellipse != null)
            {
                if ((string)canvas.Tag == "Base")
                {
                    if (_currentFeedbackEllipseBase == null)
                    {
                        _currentFeedbackEllipseBase = ellipse;
                        canvas.Children.Add(_currentFeedbackEllipseBase);
                    }
                    else // when not null -> should already be assigned to canvas and so just update
                    {
                        CopyEllipseMainProperties(_currentFeedbackEllipseBase, ellipse);
                    }
                }
                else
                {
                    if (_currentFeedbackEllipseRef == null)
                    {
                        _currentFeedbackEllipseRef = ellipse;
                        canvas.Children.Add(_currentFeedbackEllipseRef);
                    }
                    else // when not null -> should already be assigned to canvas and so just update
                    {
                        CopyEllipseMainProperties(_currentFeedbackEllipseRef, ellipse);
                    }
                }
            }

            if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
            {
                DrawMaskStroke(canvas, currentMousePosition);
                _lastMousePosition = currentMousePosition;
            }
        }

        private void MaskCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            StoppedDrawing(sender as Canvas);
        }

        private void MaskCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            Canvas canvas = sender as Canvas;
            if ((string)canvas.Tag == "Base")
            {
                canvas.Children.Remove(_currentFeedbackEllipseBase);
                _currentFeedbackEllipseBase = null;
            }
            else
            {
                canvas.Children.Remove(_currentFeedbackEllipseRef);
                _currentFeedbackEllipseRef = null;
            }
            StoppedDrawing(sender as Canvas);
        }

        private void DrawMaskStroke(Canvas canvas, Point currentMousePosition)
        {
            bool isBase = (string)canvas.Tag == "Base";

            WriteableBitmap targetMaskBitmap = isBase ? _baseMaskBitmap : _refMaskBitmap;
            BitmapSource originalImage = isBase ? _currentBaseImageOriginal : _currentRefImageOriginal;


            if (targetMaskBitmap == null || originalImage == null) return;

            double brushSize = BrushSizeSlider.Value;
            // Map display coordinates to original image coordinates
            // Ensure you're mapping to the *actual* displayed size of the original image
            System.Windows.Controls.Image imageControl = isBase ? BaseImageDisplay : RefImageDisplay;
            Point imagePoint = GetImageRelativePoint(imageControl, currentMousePosition); // This is good

            int originalX = (int)imagePoint.X;
            int originalY = (int)imagePoint.Y;

            // Use originalImage.PixelWidth/Height for scaling based on actual image content
            double actualImageWidth = imageControl.RenderSize.Width;
            double actualImageHeight = imageControl.RenderSize.Height;
            double brushScale = Math.Max(originalImage.PixelWidth / actualImageWidth, originalImage.PixelHeight / actualImageHeight);
            int originalBrushRadius = (int)(brushSize / 2 * brushScale);

            // Update the actual mask bitmap (pixel data)
            UpdateMaskBitmap(targetMaskBitmap, originalX, originalY, originalBrushRadius, isBase);


            if (isBase)
            {
                BaseMaskDisplay.Source = targetMaskBitmap; // re-assign to force update
            }
            else
            {
                RefMaskDisplay.Source = targetMaskBitmap; // re-assign to force update
            }
        }

        private void StoppedDrawing(Canvas canvas)
        {
            if (_isDrawing)
            {
                _DrawnMask = true;
                _isDrawing = false;
                UpdateMergeAndDiff();
            }
        }

        private void CopyEllipseMainProperties(Ellipse copyTo, Ellipse copyFrom)
        {
            copyTo.Width = copyFrom.Width;
            copyTo.Height = copyFrom.Height;
            copyTo.Fill = copyFrom.Fill;
            copyTo.Margin = copyFrom.Margin;
        }
        #endregion



        #region Image Processing & Mask Application Logic
        private void UpdateDiffImages(bool forceUpdate = false)
        {
            bool showBaseDiff = ShowBaseDiffButton.Content.ToString().Contains("Hide");
            bool showRefDiff = ShowRefDiffButton.Content.ToString().Contains("Hide");
            bool showMergedDiff = ShowMergeDiffButton.Content.ToString().Contains("Hide");

            bool forceNull = _currentBaseImageOriginal == null || _currentRefImageOriginal == null || _currentMergedImage == null;

            if (showBaseDiff || forceUpdate)
            {
                _currentBaseDiffImage = forceNull ? null : GenerateDifferenceImage(_currentBaseImageOriginal, _currentRefImageOriginal, (int)DiffThresSlider.Value);
                BaseDiffImageDisplay.Source = _currentBaseDiffImage;
            }
            if (showRefDiff || forceUpdate)
            {
                _currentRefDiffImage = forceNull ? null : GenerateDifferenceImage(_currentRefImageOriginal, _currentBaseImageOriginal, (int)DiffThresSlider.Value);
                RefDiffImageDisplay.Source = _currentRefDiffImage;
            }
            if (showMergedDiff || forceUpdate)
            {
                _currentMergedDiffImage = forceNull ? null : GenerateDifferenceImage(_isMainBase ? _currentRefImageOriginal : _currentBaseImageOriginal, _currentMergedImage, (int)DiffThresSlider.Value);
                MergedDiffImageDisplay.Source = _currentMergedDiffImage;
            }

            BaseDiffImageDisplay.Visibility = showBaseDiff ? Visibility.Visible : Visibility.Hidden;
            RefDiffImageDisplay.Visibility = showRefDiff ? Visibility.Visible : Visibility.Hidden;
            MergedDiffImageDisplay.Visibility = showMergedDiff ? Visibility.Visible : Visibility.Hidden;
        }

        private async void UpdateMergeAndDiff(bool saveMemory = true)
        {
            if (_currentBaseImageOriginal == null || _currentRefImageOriginal == null || _baseMaskBitmap == null || _refMaskBitmap == null)
            {
                MergedImageDisplay.Source = null;
                return;
            }

            while (_isAnalyzingMerge)
            {
                await Task.Delay(25);
            }

            _isUpdatingMerge = true;

            int width = _currentBaseImageOriginal.PixelWidth;
            int height = _currentBaseImageOriginal.PixelHeight;

            byte[] baseMaskPixelsForBackground = new byte[width * height * 4];
            byte[] refMaskPixelsForBackground = new byte[width * height * 4];

            _baseMaskBitmap.CopyPixels(new Int32Rect(0, 0, width, height), baseMaskPixelsForBackground, width * 4, 0);
            _refMaskBitmap.CopyPixels(new Int32Rect(0, 0, width, height), refMaskPixelsForBackground, width * 4, 0);

            BitmapSource mergedImage = await Task.Run(() =>
            {
                WriteableBitmap tempMergedBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

                byte[] basePixels = new byte[width * height * 4];
                _currentBaseImageOriginal.CopyPixels(new Int32Rect(0, 0, width, height), basePixels, width * 4, 0);

                byte[] refPixels = new byte[width * height * 4];
                _currentRefImageOriginal.CopyPixels(new Int32Rect(0, 0, width, height), refPixels, width * 4, 0);

                byte[] baseMaskPixels = baseMaskPixelsForBackground;
                byte[] refMaskPixels = refMaskPixelsForBackground;

                tempMergedBitmap.Lock();
                IntPtr backBuffer = tempMergedBitmap.BackBuffer;
                int stride = tempMergedBitmap.BackBufferStride;

                unsafe
                {
                    byte* pMerged = (byte*)backBuffer;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int pixelIndex = y * stride + x * 4; // Index for current pixel (B, G, R, A)

                            // Get alpha values for masks
                            bool isBaseMaskActive = baseMaskPixels[pixelIndex + 3] > 0; // Alpha channel for mask
                            bool isRefMaskActive = refMaskPixels[pixelIndex + 3] > 0;   // Alpha channel for mask

                            if (!_isMainBase)
                            {
                                if ((!isRefMaskActive && !isBaseMaskActive)
                                || (isRefMaskActive && isBaseMaskActive)
                                || (isRefMaskActive && !isBaseMaskActive))
                                {
                                    pMerged[pixelIndex + 0] = refPixels[pixelIndex + 0]; // B
                                    pMerged[pixelIndex + 1] = refPixels[pixelIndex + 1]; // G
                                    pMerged[pixelIndex + 2] = refPixels[pixelIndex + 2]; // R
                                    pMerged[pixelIndex + 3] = 255; // A - 255 = opaque
                                }
                                else
                                {
                                    pMerged[pixelIndex + 0] = basePixels[pixelIndex + 0]; // B
                                    pMerged[pixelIndex + 1] = basePixels[pixelIndex + 1]; // G
                                    pMerged[pixelIndex + 2] = basePixels[pixelIndex + 2]; // R
                                    pMerged[pixelIndex + 3] = 255; // A - 255 = opaque
                                }
                            }
                            else
                            {
                                if ((!isRefMaskActive && !isBaseMaskActive)
                                || (isRefMaskActive && isBaseMaskActive)
                                || (!isRefMaskActive && isBaseMaskActive))
                                {
                                    pMerged[pixelIndex + 0] = basePixels[pixelIndex + 0]; // B
                                    pMerged[pixelIndex + 1] = basePixels[pixelIndex + 1]; // G
                                    pMerged[pixelIndex + 2] = basePixels[pixelIndex + 2]; // R
                                    pMerged[pixelIndex + 3] = 255; // A - 255 = opaque
                                }
                                else
                                {
                                    pMerged[pixelIndex + 0] = refPixels[pixelIndex + 0]; // B
                                    pMerged[pixelIndex + 1] = refPixels[pixelIndex + 1]; // G
                                    pMerged[pixelIndex + 2] = refPixels[pixelIndex + 2]; // R
                                    pMerged[pixelIndex + 3] = 255; // A - 255 = opaque
                                }
                            }

                        }
                    }
                }
                tempMergedBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height)); // Mark the entire bitmap as dirty
                tempMergedBitmap.Unlock();
                tempMergedBitmap.Freeze();
                return tempMergedBitmap;
            });

            _currentMergedImage = mergedImage;
            MergedImageDisplay.Source = _currentMergedImage;

            _isUpdatingMerge = false;


            UpdateDiffImages();
            UpdateSaveButton();
            if (saveMemory)
            {
                UpdateMemoryCache();
            }
        }

        private void ClearMasks(bool updateMergeAndDiff = true, bool saveMemory = true)
        {
            _baseMaskBitmap = null;
            _refMaskBitmap = null;

            BaseMaskDisplay.Source = null;
            RefMaskDisplay.Source = null;

            // Initialize masks with same dimensions as original images, all transparent (Alpha = 0)
            _baseMaskBitmap = new WriteableBitmap(_currentBaseImageOriginal.PixelWidth, _currentBaseImageOriginal.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
            _refMaskBitmap = new WriteableBitmap(_currentRefImageOriginal.PixelWidth, _currentRefImageOriginal.PixelHeight, 96, 96, PixelFormats.Bgra32, null);

            if (updateMergeAndDiff)
            {
                UpdateMergeAndDiff(saveMemory);
            }
        }

        /// <summary>
        /// Compares two images pixel by pixel.
        /// </summary>
        /// <returns>True if all pixels match, false otherwise.</returns>
        private async Task<bool> AreImagesIdenticalAsync(BitmapSource img, BitmapSource img2)
        {
            if (img == null || img2 == null)
            {
                return false;
            }

            while (_isUpdatingMerge)
            {
                await Task.Delay(25);
            }

            _isAnalyzingMerge = true;

            bool samePixels = await Task.Run(() =>
            {
                // Ensure both images have the same dimensions and pixel format for a valid comparison
                if (img.PixelWidth != img2.PixelWidth ||
                    img.PixelHeight != img2.PixelHeight ||
                    img.Format != img2.Format)
                {
                    return false;
                }

                int width = img.PixelWidth;
                int height = img.PixelHeight;
                int stride = img.PixelWidth * img.Format.BitsPerPixel / 8; // Calculate stride (bytes per row)

                // Create pixel arrays for both images
                byte[] pixels1 = new byte[height * stride];
                byte[] pixels2 = new byte[height * stride];

                // Copy pixel data from both BitmapSources to byte arrays
                img.CopyPixels(pixels1, stride, 0);
                img2.CopyPixels(pixels2, stride, 0);

                // Compare pixel data byte by byte
                for (int i = 0; i < pixels1.Length; i++)
                {
                    if (pixels1[i] != pixels2[i])
                    {
                        return false; // Mismatch found
                    }
                }

                return true;
            });

            _isAnalyzingMerge = false;
            return samePixels;
        }

        private void UpdateAutoMask(bool isClick, bool isInvertedDiffMask = false)
        {
            if (_isInitialized)
            {
                ClearMasks(false);

                if (!isInvertedDiffMask)
                {
                    int imageRes = (int)(_currentBaseImageOriginal.Width * _currentBaseImageOriginal.Height);

                    int minRegionSizePercentage = (int)MinRegionSlider.Value;
                    byte colorTolerance = (byte)((int)ColorToleranceSlider.Value);

                    int minRegionSize = (int)(minRegionSizePercentage * imageRes / 100);

                    BitmapSource map = GetDifferenceMap(_isMainBase ? _currentBaseImageOriginal : _currentRefImageOriginal, _isMainBase ? _currentRefImageOriginal : _currentBaseImageOriginal, minRegionSize, colorTolerance);

                    CopyPixels(map, _isMainBase ? _refMaskBitmap : _baseMaskBitmap, 1);
                }
                else // new feature: apply inverted diff as mask
                {
                    UpdateDiffImages(true);
                    CopyPixels(_isMainBase ? _currentRefDiffImage : _currentBaseDiffImage, _isMainBase ? _refMaskBitmap : _baseMaskBitmap, 2);
                }

                if (_isMainBase)
                {
                    RefMaskDisplay.Source = _refMaskBitmap;
                }
                else
                {
                    BaseMaskDisplay.Source = _baseMaskBitmap;
                }

                UpdateMergeAndDiff(isClick);
            }
        }
        
        private void InvertCurrentMask(bool isBase)
        {
            Utils.InvertCurrentMask(isBase ? _baseMaskBitmap : _refMaskBitmap);
            if (isBase)
            {
                
                BaseMaskDisplay.Source = _baseMaskBitmap; // re-assign to force update
            }
            else
            {
                RefMaskDisplay.Source = _refMaskBitmap; // re-assign to force update
            }
            UpdateMergeAndDiff(true);
        }
        #endregion



        #region Memory Management & Caching
        private void ClearMemory()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        private void UpdateMemoryCache()
        {
            if (_DrawnMask)
            {
                undoStack.Push(new Memory(CloneWriteableBitmap(_baseMaskBitmap), CloneWriteableBitmap(_refMaskBitmap)));

                if (undoStack.Count > 30) undoStack = new Stack<Memory>(undoStack.Reverse().Take(30));

                // Once you draw a new stroke, Redo is no longer valid
                redoStack.Clear();
            }
        }

        private void LoadMemory(Memory mem)
        {
            _baseMaskBitmap = null;
            _refMaskBitmap = null;

            BaseMaskDisplay.Source = null;
            RefMaskDisplay.Source = null;

            _baseMaskBitmap = CloneWriteableBitmap(mem._baseMaskBitmap);
            _refMaskBitmap = CloneWriteableBitmap(mem._refMaskBitmap);

            BaseMaskDisplay.Source = _baseMaskBitmap;
            RefMaskDisplay.Source = _refMaskBitmap;

            UpdateMergeAndDiff(false);
        }

        private void LoadLastMemory()
        {
            if (undoStack.Count >= 1)
            {
                // Current state to redo stack
                Memory current = undoStack.Pop();
                redoStack.Push(current);

                if (undoStack.Count > 0)
                {
                    Memory previous = undoStack.Peek();
                    LoadMemory(previous);
                    return;
                }
            }
            ClearMasks(true, false);
        }




        #endregion

        
    }
}

