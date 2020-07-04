﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kontract;
using Kontract.Extensions;
using Kontract.Interfaces.FileSystem;
using Kontract.Interfaces.Managers;
using Kontract.Interfaces.Plugins.State;
using Kontract.Interfaces.Plugins.State.Archive;
using Kontract.Interfaces.Progress;
using Kontract.Models.Archive;
using Kontract.Models.IO;
using Kore.Factories;
using Kore.Managers.Plugins;
using Kuriimu2.WinForms.MainForms.Interfaces;
using Kuriimu2.WinForms.Properties;

namespace Kuriimu2.WinForms.MainForms.FormatForms
{
    public partial class ArchiveForm : UserControl, IArchiveForm
    {
        private readonly IInternalPluginManager _pluginManager;
        private readonly IStateInfo _stateInfo;
        private readonly IProgressContext _progressContext;

        private ISaveFiles SaveState => _stateInfo.State as ISaveFiles;
        private IArchiveState ArchiveState => _stateInfo.State as IArchiveState;

        public Func<OpenFileEventArgs, Task<bool>> OpenFilesDelegate { get; set; }
        public Func<SaveTabEventArgs, Task<bool>> SaveFilesDelegate { get; set; }
        public Action<IStateInfo> UpdateTabDelegate { get; set; }

        public ArchiveForm(IStateInfo loadedState, IInternalPluginManager pluginManager, IProgressContext progressContext)
        {
            InitializeComponent();

            // Populate image list
            imlFiles.Images.Add("tree-directory", Resources.tree_directory);
            imlFiles.Images.Add("tree-directory-open", Resources.tree_directory_open);
            imlFiles.Images.Add("tree-text-file", Resources.tree_text_file);
            imlFiles.Images.Add("tree-image-file", Resources.tree_image_file);
            imlFiles.Images.Add("tree-archive-file", Resources.tree_archive_file);
            imlFilesLarge.Images.Add("tree-directory", Resources.tree_directory_32);
            imlFilesLarge.Images.Add("tree-directory-open", Resources.tree_directory_open);
            imlFilesLarge.Images.Add("tree-text-file", Resources.tree_text_file_32);
            imlFilesLarge.Images.Add("tree-image-file", Resources.tree_image_file_32);
            imlFilesLarge.Images.Add("tree-archive-file", Resources.tree_archive_file_32);

            _stateInfo = loadedState;
            _progressContext = progressContext;

            _pluginManager = pluginManager;

            LoadDirectories();
            UpdateFormInternal();
        }

        #region Events

        #region tlsMain

        private void tsbSave_Click(object sender, EventArgs e)
        {
            Save(UPath.Empty);
        }

        private void tsbSaveAs_Click(object sender, EventArgs e)
        {
            SaveAs();
        }

        private void tsbFind_Click(object sender, EventArgs e)
        {
            Stub();
        }

        private void tsbProperties_Click(object sender, EventArgs e)
        {
            Stub();
        }

        #endregion

        #region tlsPreview

        private void tsbFileExtract_Click(object sender, EventArgs e)
        {
            ExtractSelectedFiles();
        }

        private void tsbFileReplace_Click(object sender, EventArgs e)
        {
            ReplaceSelectedFiles();
        }

        private void tsbFileRename_Click(object sender, EventArgs e)
        {
            RenameSelectedFiles();
        }

        private void tsbFileDelete_Click(object sender, EventArgs e)
        {
            DeleteSelectedFiles();
        }

        private void tsbFileOpen_Click(object sender, EventArgs e)
        {
            OpenSelectedFiles();
        }

        private void tsbFileProperties_Click(object sender, EventArgs e)
        {
            Stub();
        }

        #endregion

        #region mnuFiles

        // ReSharper disable ConditionIsAlwaysTrueOrFalse
        private void mnuFiles_Opening(object sender, CancelEventArgs e)
        {
            var selectedItem = lstFiles.SelectedItems.Count > 0 ? lstFiles.SelectedItems[0] : null;
            var afi = selectedItem?.Tag as ArchiveFileInfo;

            var canExtractFiles = true;
            var canReplaceFiles = ArchiveState is IReplaceFiles;
            var canRenameFiles = ArchiveState is IRenameFiles;
            var canDeleteFiles = ArchiveState is IRemoveFiles;

            extractFileToolStripMenuItem.Enabled = canExtractFiles;
            extractFileToolStripMenuItem.Text = canExtractFiles ? "E&xtract..." : "Extract is not supported";
            extractFileToolStripMenuItem.Tag = afi;

            replaceFileToolStripMenuItem.Enabled = canReplaceFiles;
            replaceFileToolStripMenuItem.Text = canReplaceFiles ? "&Replace..." : "Replace is not supported";
            replaceFileToolStripMenuItem.Tag = afi;

            renameFileToolStripMenuItem.Enabled = canRenameFiles;
            renameFileToolStripMenuItem.Text = canRenameFiles ? "Re&name..." : "Rename is not supported";
            renameFileToolStripMenuItem.Tag = afi;

            deleteFileToolStripMenuItem.Enabled = canDeleteFiles;
            deleteFileToolStripMenuItem.Text = canDeleteFiles ? "&Delete" : "Delete is not supported";
            deleteFileToolStripMenuItem.Tag = afi;

            openWithPluginToolStripMenuItem.DropDownItems.Clear();
            foreach (var pluginId in afi?.PluginIds ?? Array.Empty<Guid>())
            {
                var filePluginLoader = _pluginManager.GetFilePluginLoaders().FirstOrDefault(x => x.Exists(pluginId));
                var filePlugin = filePluginLoader?.GetPlugin(pluginId);

                if (filePlugin != null)
                {
                    var item = new ToolStripMenuItem(filePlugin.Metadata.Name)
                    {
                        Tag = (pluginId, afi)
                    };

                    item.Click += Item_Click;
                    openWithPluginToolStripMenuItem.DropDownItems.Add(item);
                }
            }

            openWithPluginToolStripMenuItem.Visible = openWithPluginToolStripMenuItem.DropDownItems.Count > 0;
        }

        private async void Item_Click(object sender, EventArgs e)
        {
            var tsi = (ToolStripMenuItem)sender;
            var info = ((Guid, ArchiveFileInfo))tsi.Tag;

            if (!await OpenAfi(info.Item2, info.Item1))
                MessageBox.Show($"File could not be opened with plugin '{info.Item1}''.",
                    "File not opened", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExtractSelectedFiles();
        }

        private void replaceFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReplaceSelectedFiles();
        }

        private void renameFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RenameSelectedFiles();
        }

        private void deleteFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedFiles();
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenSelectedFiles();
        }

        #endregion

        #region mnuDirectories

        private void mnuDirectories_Opening(object sender, CancelEventArgs e)
        {
            var fileName = ((UPath)treDirectories.SelectedNode.Text).GetName().Replace('.', '_');

            var canExtractFiles = true;
            var canReplaceFiles = ArchiveState is IReplaceFiles;
            var canDeleteFiles = ArchiveState is IRemoveFiles;
            var canAddFiles = ArchiveState is IAddFiles;

            extractDirectoryToolStripMenuItem.Enabled = canExtractFiles;
            extractDirectoryToolStripMenuItem.Text = canExtractFiles ? $"E&xtract {fileName}..." : "Extract is not supported";

            replaceDirectoryToolStripMenuItem.Enabled = canReplaceFiles;
            replaceDirectoryToolStripMenuItem.Text = canReplaceFiles ? $"&Replace {fileName}..." : "Replace is not supported";

            addDirectoryToolStripMenuItem.Enabled = canAddFiles;
            addDirectoryToolStripMenuItem.Text = canAddFiles ? $"&Add to {fileName}..." : "Add is not supported";

            deleteDirectoryToolStripMenuItem.Enabled = canDeleteFiles;
            deleteDirectoryToolStripMenuItem.Text = canDeleteFiles ? $"&Delete {fileName}..." : "Delete is not supported";
        }

        private void extractDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = treDirectories.SelectedNode;
            var selectedPath = UPath.Empty;

            while (node.Parent != null)
            {
                selectedPath = node.Text / selectedPath;
                node = node.Parent;
            }

            var treeFiles = CollectFilesFromTreeNode(treDirectories.SelectedNode).ToList();
            ExtractFiles(treeFiles, selectedPath);
        }

        private void replaceDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = treDirectories.SelectedNode;
            var selectedPath = UPath.Empty;

            while (node.Parent != null)
            {
                selectedPath = node.Text / selectedPath;
                node = node.Parent;
            }

            var treeFiles = CollectFilesFromTreeNode(treDirectories.SelectedNode).ToList();
            ReplaceMultipleFiles(treeFiles, selectedPath);

            UpdateFormInternal();
        }

        private void addDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!treDirectories.Focused)
                return;

            AddFiles();

            LoadDirectories();

            UpdateFormInternal();
        }

        private void deleteDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treDirectories.SelectedNode?.Tag is IEnumerable<ArchiveFileInfo>)
                DeleteFiles(treDirectories.SelectedNode?.Tag as IList<ArchiveFileInfo>);

            LoadDirectories();

            UpdateFormInternal();
        }

        #endregion

        #region treDirectories

        private void treDirectories_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Parent != null)
            {
                e.Node.ImageKey = "tree-directory";
                e.Node.SelectedImageKey = e.Node.ImageKey;
            }
        }

        private void treDirectories_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Parent != null)
            {
                e.Node.ImageKey = "tree-directory-open";
                e.Node.SelectedImageKey = e.Node.ImageKey;
            }
        }

        private void treDirectories_AfterSelect(object sender, TreeViewEventArgs e)
        {
            LoadFiles();

            UpdateFormInternal();
        }

        #endregion

        #region lstFiles

        private async void lstFiles_DoubleClick(object sender, EventArgs e)
        {
            var menuItem = lstFiles.SelectedItems[0];
            var afi = menuItem.Tag as ArchiveFileInfo;

            var pluginIds = afi.PluginIds ?? Array.Empty<Guid>();
            if (_pluginManager.GetFilePluginLoaders().Any(x => pluginIds.Any(x.Exists)))
            {
                var pluginId = pluginIds.First(x => _pluginManager.GetFilePluginLoaders().Any(y => y.Exists(x)));

                if (!await OpenAfi(afi, pluginId))
                    MessageBox.Show($"File couldn't be opened with preset plugin {pluginId}.",
                        "Opening error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                // Use automatic identification
                if (!await OpenAfi(afi))
                    MessageBox.Show("File couldn't be opened.",
                        "Opening error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #endregion

        #region Utilities

        #region General

        private void Stub()
        {
            MessageBox.Show("This method is not implemented yet.", "Not implemented", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private IEnumerable<ArchiveFileInfo> CollectSelectedFiles()
        {
            foreach (ListViewItem item in lstFiles.SelectedItems)
                yield return item.Tag as ArchiveFileInfo;
        }

        private IEnumerable<ArchiveFileInfo> CollectFilesFromTreeNode(TreeNode node)
        {
            if (node.Tag is IList<ArchiveFileInfo> files)
                foreach (var file in files)
                    yield return file;

            foreach (TreeNode childNode in node.Nodes)
                foreach (var file in CollectFilesFromTreeNode(childNode))
                    yield return file;
        }

        private void LoadDirectories()
        {
            treDirectories.BeginUpdate();
            treDirectories.Nodes.Clear();

            if (ArchiveState.Files == null)
                LoadFiles();
            else
            {
                var lookup = ArchiveState.Files.OrderBy(f => f.FilePath).ToLookup(f => f.FilePath.GetDirectory());

                // Build directory tree
                var root = treDirectories.Nodes.Add("root", _stateInfo.FilePath.FullName,
                    "tree-archive-file", "tree-archive-file");
                foreach (var path in lookup.Select(g => g.Key))
                {
                    path.Split()
                        .Aggregate<string, TreeNode>(root, (node, part) =>
                            node.Nodes[part] ?? node.Nodes.Add(part, part))
                        .Tag = lookup[path];
                }

                root.Expand();
                treDirectories.SelectedNode = root;
            }

            treDirectories.EndUpdate();
            treDirectories.Focus();
        }

        private void LoadFiles()
        {
            lstFiles.BeginUpdate();
            lstFiles.Items.Clear();

            if (treDirectories.SelectedNode?.Tag is IList<ArchiveFileInfo> files)
            {
                imlFiles.Images.Add("0", Resources.menu_new);

                foreach (var afi in files)
                {
                    var listViewItem = new ListViewItem(new[] { afi.FilePath.GetName(), afi.FileSize.ToString() },
                        "0", Color.Black, Color.Transparent, lstFiles.Font)
                    {
                        Tag = afi
                    };

                    lstFiles.Items.Add(listViewItem);
                }

                tslFileCount.Text = $"Files: {files.Count}";
            }

            lstFiles.EndUpdate();
        }

        //private Color StateToColor(ArchiveFileState state)
        //{
        //    Color result = Color.Black;

        //    switch (state)
        //    {
        //        case ArchiveFileState.Empty:
        //            result = Color.DarkGray;
        //            break;
        //        case ArchiveFileState.Added:
        //            result = Color.Green;
        //            break;
        //        case ArchiveFileState.Replaced:
        //            result = Color.Orange;
        //            break;
        //        case ArchiveFileState.Renamed:
        //            result = Color.Blue;
        //            break;
        //        case ArchiveFileState.Deleted:
        //            result = Color.Red;
        //            break;
        //    }

        //    return result;
        //}

        #endregion

        #region Updates

        public void UpdateForm()
        {
            LoadDirectories();
            LoadFiles();

            UpdateFormInternal();
        }

        private void UpdateFormInternal()
        {
            // Menu
            tsbSave.Enabled = ArchiveState is ISaveFiles;
            tsbSaveAs.Enabled = ArchiveState is ISaveFiles && _stateInfo.ParentStateInfo == null;
            // TODO: Property implementation
            //tsbProperties.Enabled = _archiveAdapter.FileHasExtendedProperties;

            // Toolbar
            tsbFileExtract.Enabled = true;
            tsbFileReplace.Enabled = ArchiveState is IReplaceFiles && ArchiveState is ISaveFiles;
            tsbFileRename.Enabled = ArchiveState is IRenameFiles && ArchiveState is ISaveFiles;
            tsbFileDelete.Enabled = ArchiveState is IRemoveFiles && ArchiveState is ISaveFiles;

            UpdateTabDelegate?.Invoke(_stateInfo);
        }

        #endregion

        #region Save

        private void SaveAs()
        {
            var sfd = new SaveFileDialog
            {
                FileName = _stateInfo.FilePath.GetName(),
                Filter = "All Files (*.*)|*.*"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
                Save(sfd.FileName);
        }

        public async void Save(UPath savePath)
        {
            if (savePath == UPath.Empty)
                savePath = _stateInfo.AbsoluteDirectory / _stateInfo.FilePath;

            await SaveFilesDelegate(new SaveTabEventArgs(_stateInfo, savePath));

            LoadDirectories();
            LoadFiles();

            UpdateFormInternal();
        }

        #endregion

        #region Extract

        private void ExtractSelectedFiles()
        {
            var selectedFiles = CollectSelectedFiles().ToList();
            ExtractFiles(selectedFiles, selectedFiles[0].FilePath.GetDirectory());
        }

        private async void ExtractFiles(IList<ArchiveFileInfo> files, UPath rootPath)
        {
            ContractAssertions.IsNotNull(files, nameof(files));

            UPath selectedPath0;
            var selectedFile0 = string.Empty;

            if (files.Count > 1)
            {
                // Extracting more than one file should choose a folder to extract to

                var fbd = new FolderBrowserDialog
                {
                    SelectedPath = Settings.Default.LastDirectory,
                    Description = $"Select where you want to extract {rootPath} to..."
                };

                if (fbd.ShowDialog() != DialogResult.OK)
                    return;

                selectedPath0 = fbd.SelectedPath;
            }
            else
            {
                // Extracting just one file should choose a folder and filename

                var fileName = files[0].FilePath.GetName();
                var extension = files[0].FilePath.GetExtensionWithDot();

                var sfd = new SaveFileDialog
                {
                    InitialDirectory = Settings.Default.LastDirectory,
                    FileName = fileName,
                    Filter = $@"{extension.ToUpper().TrimStart('.')} File (*{extension})|*{extension}"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                selectedPath0 = ((UPath)sfd.FileName).GetDirectory();
                selectedFile0 = ((UPath)sfd.FileName).GetName();
            }

            var destinationFileSystem = FileSystemFactory.CreatePhysicalFileSystem(selectedPath0, _stateInfo.StreamManager);

            var temporaryStreamProvider = _stateInfo.StreamManager.CreateTemporaryStreamProvider();
            var extractionPath = files.Count > 1 ? rootPath.GetDirectory() : rootPath;
            foreach (var afi in files)
            {
                var fileData = _stateInfo.StreamManager.WrapUndisposable(await afi.GetFileData(temporaryStreamProvider));

                var subPath = (UPath)afi.FilePath.ToRelative().FullName.Substring(extractionPath.FullName.Length);
                destinationFileSystem.CreateDirectory(subPath.GetDirectory());

                var filePath = subPath;
                if (selectedFile0 != string.Empty)
                    filePath = subPath.GetDirectory() / selectedFile0;

                destinationFileSystem.SetFileData(filePath, fileData);

                fileData.Close();
            }

            extractionPath = files.Count > 1 ? rootPath : files[0].FilePath.GetName();
            MessageBox.Show($"'{extractionPath}' extracted successfully.", "Extraction Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region Replace

        private void ReplaceSelectedFiles()
        {

            var selectedFiles = CollectSelectedFiles().ToList();

            ReplaceFiles(selectedFiles);

            UpdateFormInternal();
        }

        private void ReplaceFiles(IList<ArchiveFileInfo> files)
        {
            if (files.Count > 1)
                ReplaceMultipleFiles(files, files[0].FilePath.GetDirectory());
            else
                ReplaceSingleFile(files[0]);
        }

        private void ReplaceMultipleFiles(IList<ArchiveFileInfo> files, UPath rootPath)
        {
            var fbd = new FolderBrowserDialog
            {
                SelectedPath = Settings.Default.LastDirectory,
                Description = $"Select where you want to replace {rootPath} from..."
            };

            if (fbd.ShowDialog() != DialogResult.OK)
                return;

            var sourceFileSystem = FileSystemFactory.CreatePhysicalFileSystem(fbd.SelectedPath, _stateInfo.StreamManager);
            var destinationFileSystem = FileSystemFactory.CreateAfiFileSystem(ArchiveState, rootPath.ToAbsolute(), _stateInfo.StreamManager);

            var replaceCount = 0;
            foreach (var sourcePath in sourceFileSystem.EnumeratePaths(UPath.Root, "*", SearchOption.AllDirectories, SearchTarget.File))
            {
                if (!destinationFileSystem.FileExists(sourcePath))
                    continue;

                var newFileData = sourceFileSystem.OpenFile(sourcePath);
                if (!(ArchiveState is IReplaceFiles replaceState))
                    continue;

                var afi = files.First(f => f.FilePath == rootPath.ToAbsolute() / sourcePath.ToRelative());
                replaceState.ReplaceFile(afi, newFileData);
                replaceCount++;
            }

            MessageBox.Show($"Replaced {replaceCount} files in \"{rootPath}\" successfully.", "Replacement Result",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadFiles();
        }

        private void ReplaceSingleFile(ArchiveFileInfo file)
        {
            ContractAssertions.IsNotNull(file, nameof(file));

            var fileName = file.FilePath.GetName();

            var sfd = new OpenFileDialog
            {
                InitialDirectory = Settings.Default.LastDirectory,
                FileName = fileName,
                Filter = "All Files (*.*)|*.*"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            var sourceFileSystem = FileSystemFactory.CreatePhysicalFileSystem(((UPath)sfd.FileName).GetDirectory(), _stateInfo.StreamManager);
            var newFileData = sourceFileSystem.OpenFile(((UPath)sfd.FileName).GetName());

            if (!(ArchiveState is IReplaceFiles replaceState))
                return;

            replaceState.ReplaceFile(file, newFileData);

            MessageBox.Show($"Replaced {file.FilePath} with \"{sfd.FileName}\" successfully.", "Replacement Result",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadFiles();
        }

        //private void ReplaceFiles(IList<ArchiveFileInfo> files, UPath rootPath)
        //{
        //    ContractAssertions.IsNotNull(files, nameof(files));

        //    UPath selectedPath0;
        //    var selectedFile0 = string.Empty;

        //    if (files.Count > 1)
        //    {
        //        // Replacing more than one file should choose a folder to replace from

        //        var fbd = new FolderBrowserDialog
        //        {
        //            SelectedPath = Settings.Default.LastDirectory,
        //            Description = $"Select where you want to replace {rootPath} from..."
        //        };

        //        if (fbd.ShowDialog() != DialogResult.OK)
        //            return;

        //        selectedPath0 = fbd.SelectedPath;
        //    }
        //    else
        //    {
        //        // Replacing just one file should choose a folder and filename

        //        var fileName = files[0].FilePath.GetName();

        //        var sfd = new OpenFileDialog()
        //        {
        //            InitialDirectory = Settings.Default.LastDirectory,
        //            FileName = fileName,
        //            Filter = "All Files (*.*)|*.*"
        //        };

        //        if (sfd.ShowDialog() != DialogResult.OK)
        //            return;

        //        selectedPath0 = ((UPath)sfd.FileName).GetDirectory();
        //        selectedFile0 = ((UPath)sfd.FileName).GetName();
        //    }

        //    var sourceFileSystem = FileSystemFactory.CreatePhysicalFileSystem(selectedPath0, _stateInfo.StreamManager);

        //    var replaceCount = 0;
        //    foreach (var afi in files)
        //    {
        //        var subPath = (UPath)afi.FilePath.FullName.Substring(rootPath.FullName.Length);
        //        if (!sourceFileSystem.FileExists(subPath))
        //            continue;

        //        var newFileData = sourceFileSystem.OpenFile(subPath);
        //        (ArchiveState as IReplaceFiles)?.ReplaceFile(afi, newFileData);

        //        replaceCount++;
        //    }

        //    if (files.Count > 1)
        //        MessageBox.Show($"Replaced {replaceCount} files in \"{rootPath}\" successfully.",
        //            "Replacement Result",
        //            MessageBoxButtons.OK, MessageBoxIcon.Information);

        //    LoadFiles();
        //}

        #endregion

        #region Rename

        private void RenameSelectedFiles()
        {
            RenameFiles(CollectSelectedFiles().ToList());

            UpdateFormInternal();
        }

        private void RenameFiles(IList<ArchiveFileInfo> files)
        {
            ContractAssertions.IsNotNull(files, nameof(files));

            var canceledRenames = new List<ArchiveFileInfo>();
            foreach (var afi in files)
            {
                var inputBox = new InputBox($"Select a new filename for '{afi.FilePath.GetName()}'.",
                    "Rename file",
                    afi.FilePath.GetName());

                if (inputBox.ShowDialog() == DialogResult.OK)
                    (ArchiveState as IRenameFiles).Rename(afi, ((UPath)inputBox.InputText).GetName());
                else
                    canceledRenames.Add(afi);
            }

            if (canceledRenames.Count > 0)
            {
                var canceledRenameFiles = string.Join(Environment.NewLine, canceledRenames.Select(x => x.FilePath.GetName()));
                MessageBox.Show($"Following files were not renamed:{canceledRenameFiles}",
                    "Renaming error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            LoadFiles();
        }

        #endregion

        #region Delete

        private void DeleteSelectedFiles()
        {
            DeleteFiles(CollectSelectedFiles().ToList());

            LoadFiles();

            UpdateFormInternal();
        }

        private void DeleteFiles(IList<ArchiveFileInfo> files)
        {
            ContractAssertions.IsNotNull(files, nameof(files));

            if (files.Count <= 0)
                return;

            foreach (var afi in files)
                (ArchiveState as IRemoveFiles).RemoveFile(afi);
        }

        #endregion

        #region Open

        private void OpenSelectedFiles()
        {
            OpenFiles(CollectSelectedFiles().ToList());
        }

        private async void OpenFiles(List<ArchiveFileInfo> files)
        {
            ContractAssertions.IsNotNull(files, nameof(files));

            var notOpened = new List<ArchiveFileInfo>();
            foreach (var afi in files)
            {
                if (!await OpenAfi(afi))
                    notOpened.Add(afi);
            }

            var notOpenedFiles = string.Join(Environment.NewLine, notOpened.Select(x => x.FilePath.GetName()));
            if (notOpened.Count > 0)
                MessageBox.Show($"Following files were not opened:{notOpenedFiles}",
                    "Opening error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private Task<bool> OpenAfi(ArchiveFileInfo afi) => OpenAfi(afi, Guid.Empty);

        private Task<bool> OpenAfi(ArchiveFileInfo afi, Guid plugin)
        {
            var args = new OpenFileEventArgs(_stateInfo, afi, plugin);
            return OpenFilesDelegate?.Invoke(args) ?? Task.FromResult(false);
        }

        #endregion

        #region Add

        private void AddFiles()
        {
            var dlg = new FolderBrowserDialog
            {
                Description = $"Choose where you want to add from to {treDirectories.SelectedNode.FullPath}:"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            var fs = FileSystemFactory.CreatePhysicalFileSystem(dlg.SelectedPath, _stateInfo.StreamManager);
            AddRecursive(fs, UPath.Root);
        }

        private void AddRecursive(IFileSystem fileSystem, UPath currentPath)
        {
            foreach (var dir in fileSystem.EnumeratePaths(currentPath, "*", SearchOption.TopDirectoryOnly, SearchTarget.Directory))
                AddRecursive(fileSystem, dir);

            foreach (var file in fileSystem.EnumeratePaths(currentPath, "*", SearchOption.TopDirectoryOnly, SearchTarget.File))
                (ArchiveState as IAddFiles).AddFile(fileSystem, file);
        }

        #endregion

        #endregion
    }
}