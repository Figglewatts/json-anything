﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using IconFonts;
using ImGuiNET;
using JsonAnything.Util;

namespace JsonAnything.GUI.GUIComponents
{
    public class FileDialog : IImGuiComponent
    {
        public enum DialogType
        {
            Open,
            Save
        }

        public DialogType Type { get; }
        public string FilePath { get; private set; }

        public string FileSearchPattern
        {
            set
            {
                _fileSearchPattern = value;
                invalidateFileList();
            }
        }

        private string _fileSaveType = "";
        private string _fileSearchPattern = "*";
        private bool _open = true;
        private string _currentDir;
        private int _selectedFile = -1;
        private string _bottomBarText = "";
        private readonly Vector2 _dialogStartSize = new Vector2(400, 300);
        private readonly List<string> _directoriesInCurrentDir;
        private readonly List<string> _filesInCurrentDir;
        private event Action<string> OnDialogAccept;

        public FileDialog(string dir, DialogType type)
        {
            FilePath = dir;
            Type = type;
            _currentDir = Directory.GetCurrentDirectory();
            _directoriesInCurrentDir = new List<string>();
            _filesInCurrentDir = new List<string>();
            updateFilesInCurrentDir();
        }

        public void Show(Action<string> onDialogAccept, string fileSearchPattern = "*", string fileSaveType = "")
        {
            OnDialogAccept = onDialogAccept;
            _fileSearchPattern = fileSearchPattern;
            _fileSaveType = fileSaveType;
            FilePath = "";
            invalidateFileList();
            ImGui.OpenPopup(Type == DialogType.Open ? "Open file..." : "Save file...");
            _open = true;
        }
        
        public void Render()
        {
            if (Type == DialogType.Open)
            {
                renderFileOpenDialog();
            }
            else
            {
                renderFileSaveDialog();
            }
        }

        private void renderFileOpenDialog()
        {
            ImGui.SetNextWindowSize(_dialogStartSize, Condition.FirstUseEver);
            if (ImGui.BeginPopupModal("Open file...", ref _open))
            {
                renderTopBar();
                renderFileList();
                renderBottomBar();
                
                ImGui.EndPopup();
            }
        }

        private void renderFileSaveDialog()
        {
            ImGui.SetNextWindowSize(_dialogStartSize, Condition.FirstUseEver);
            if (ImGui.BeginPopupModal("Save file...", ref _open))
            {
                renderTopBar();
                renderFileList();
                renderBottomBar();
                
                ImGui.EndPopup();
            }
        }

        private void invalidateFileList()
        {
            _selectedFile = -1;
            updateDirectoriesInCurrentDir();
            updateFilesInCurrentDir();
        }

        private void goToParentDir()
        {
            DirectoryInfo parentDir = Directory.GetParent(_currentDir);
            _currentDir = parentDir?.ToString() ?? _currentDir;
            invalidateFileList();
        }

        private void updateDirectoriesInCurrentDir()
        {
            _directoriesInCurrentDir.Clear();

            if (!Directory.Exists(_currentDir)) return;
            string[] dirs = Directory.GetDirectories(_currentDir, "*", SearchOption.TopDirectoryOnly);

            foreach (string dir in dirs)
            {
                _directoriesInCurrentDir.Add(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)));
            }
        }

        private void updateFilesInCurrentDir()
        {
            _filesInCurrentDir.Clear();

            if (!Directory.Exists(_currentDir)) return;
            string[] files = Directory.GetFiles(_currentDir, _fileSearchPattern);

            foreach (string file in files)
            {
                _filesInCurrentDir.Add(Path.GetFileName(file));
            }
        }

        private bool indexInFilesListIsDirectory(int i) { return i < _directoriesInCurrentDir.Count; }

        private void renderTopBar()
        {
            Vector2 pos = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + 5));
            ImGui.Text(FontAwesome5.FolderOpen);
            ImGui.SameLine();
            Vector2 posAfter = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(posAfter.X, pos.Y));
            ImGui.PushItemWidth(-40);
            bool modified = false;
            modified = ImGuiNETExtensions.InputText("##current-dir", ref _currentDir);
            ImGui.SameLine();
            if (ImGui.Button("Up", new Vector2(30, 0)))
            {
                goToParentDir();
            }

            if (modified)
            {
                invalidateFileList();
            }
        }

        private void renderFileList()
        {
            // if the dialog is in save mode we can't select files
            if (Type == DialogType.Save) _selectedFile = -1;
            
            ImGui.BeginChild("fileSelect", new Vector2(-1, -24), true, WindowFlags.Default);
            if (!Directory.Exists(_currentDir))
            {
                ImGui.Text("Directory does not exist!");
            }
            else if (_filesInCurrentDir.Count <= 0 && _directoriesInCurrentDir.Count <= 0)
            {
                ImGui.Text("Directory is empty!");
            }
            else
            {
                int i = 0;
                foreach (string dir in _directoriesInCurrentDir)
                {
                    ImGui.PushID(i);
                    if (ImGui.Selectable($"{FontAwesome5.Folder} {dir}", _selectedFile == i, SelectableFlags.AllowDoubleClick))
                    {
                        _selectedFile = i;

                        if (Type == DialogType.Open) _bottomBarText = dir;

                        if (ImGui.IsMouseDoubleClicked(0))
                        {
                            _currentDir = Path.Combine(_currentDir, dir);
                            invalidateFileList();
                            ImGui.PopID();
                            break;
                        }
                    }
                    ImGui.PopID();
                    i++;
                }

                foreach (string file in _filesInCurrentDir)
                {
                    ImGui.PushID(i);
                    if (ImGui.Selectable($"{FontAwesome5.File} {file}", _selectedFile == i, SelectableFlags.AllowDoubleClick))
                    {
                        _selectedFile = i;

                        _bottomBarText = Path.GetFileNameWithoutExtension(file);

                        FilePath = Path.Combine(_currentDir, file);

                        if (ImGui.IsMouseDoubleClicked(0))
                        {
                            dialogAccept();
                            ImGui.PopID();
                            break;
                        }
                    }
                    ImGui.PopID();
                    i++;
                }
            }
            ImGui.EndChild();
        }

        private void renderBottomBar()
        {
            ImGui.PushItemWidth(-48 - (_fileSaveType.Length * 7) - 16);
            ImGuiNETExtensions.InputText("##bottombar", ref _bottomBarText);
            ImGui.SameLine();
            ImGui.Text(_fileSaveType);
            ImGui.SameLine();
            if (Type == DialogType.Open)
            {
                if (ImGui.Button("Open", new Vector2(48, 0)))
                {
                    if (_selectedFile >= _directoriesInCurrentDir.Count)
                    {
                        // selected file was a file
                        dialogAccept();
                    }
                    else
                    {
                        // it was a directory
                        _currentDir = Path.Combine(_currentDir, _bottomBarText);
                        invalidateFileList();
                    }
                }
            }
            else
            {
                if (ImGui.Button("Save", new Vector2(48, 0)))
                {
                    FilePath = Path.Combine(_currentDir, _bottomBarText + _fileSaveType);
                    dialogAccept();
                }
            }
        }

        private void dialogAccept()
        {
            OnDialogAccept?.Invoke(FilePath);
            _open = false;
        }
    }
}
