using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using M2Mod.Config;
using M2Mod.Interop;
using M2Mod.Interop.Structures;

namespace M2Mod
{
    // This partial class adds a "Batch" tab that lets the user pick a folder and
    // round-trip (M2 -> M2I -> M2) every .m2 file inside it, one after another.
    public partial class M2ModForm
    {
        private TabPage tabBatch;
        private TextBox textBoxBatchFolder;
        private Button buttonBatchBrowse;
        private CheckBox checkBoxBatchSubfolders;
        private CheckBox checkBoxBatchRemoveTxid;
        private CheckBox checkBoxBatchFixLodSkins;
        private CheckBox checkBoxBatchLegacySuffix;
        private Button buttonBatchStart;
        private ProgressBar progressBarBatch;
        private Label labelBatchStatus;

        private bool _batchRunning;

        private void InitializeBatchTab()
        {
            tabBatch = new TabPage
            {
                Name = "tabBatch",
                Text = "Batch M2I Round-trip",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3)
            };

            var labelFolder = new Label
            {
                Text = "Folder containing .m2 files:",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 15)
            };

            textBoxBatchFolder = new TextBox
            {
                Location = new System.Drawing.Point(10, 35),
                Size = new System.Drawing.Size(430, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            buttonBatchBrowse = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(446, 33),
                Size = new System.Drawing.Size(100, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            buttonBatchBrowse.Click += ButtonBatchBrowse_Click;

            checkBoxBatchSubfolders = new CheckBox
            {
                Text = "Include subfolders",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 65)
            };

            checkBoxBatchRemoveTxid = new CheckBox
            {
                Text = "Remove TXID chunk",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 88)
            };

            checkBoxBatchFixLodSkins = new CheckBox
            {
                Text = "Fold _LOD0x.skin files into base skin count (WotLK compatibility)",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 111)
            };

            checkBoxBatchLegacySuffix = new CheckBox
            {
                Text = "Rename _race_gender suffix to legacy _racegender (no underscore)",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 134)
            };

            buttonBatchStart = new Button
            {
                Text = "Start",
                Location = new System.Drawing.Point(10, 161),
                Size = new System.Drawing.Size(120, 28)
            };
            buttonBatchStart.Click += ButtonBatchStart_Click;

            progressBarBatch = new ProgressBar
            {
                Location = new System.Drawing.Point(10, 201),
                Size = new System.Drawing.Size(536, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Minimum = 0,
                Maximum = 1,
                Value = 0
            };

            labelBatchStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 226),
                Size = new System.Drawing.Size(536, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            tabBatch.Controls.Add(labelFolder);
            tabBatch.Controls.Add(textBoxBatchFolder);
            tabBatch.Controls.Add(buttonBatchBrowse);
            tabBatch.Controls.Add(checkBoxBatchSubfolders);
            tabBatch.Controls.Add(checkBoxBatchRemoveTxid);
            tabBatch.Controls.Add(checkBoxBatchFixLodSkins);
            tabBatch.Controls.Add(checkBoxBatchLegacySuffix);
            tabBatch.Controls.Add(buttonBatchStart);
            tabBatch.Controls.Add(progressBarBatch);
            tabBatch.Controls.Add(labelBatchStatus);

            tabControl.Controls.Add(tabBatch);
        }

        private void ButtonBatchBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = false;
                if (textBoxBatchFolder.Text.Length > 0 && Directory.Exists(textBoxBatchFolder.Text))
                    dialog.SelectedPath = textBoxBatchFolder.Text;

                if (dialog.ShowDialog() == DialogResult.OK)
                    textBoxBatchFolder.Text = dialog.SelectedPath;
            }
        }

        private void ButtonBatchStart_Click(object sender, EventArgs e)
        {
            if (_batchRunning)
                return;

            var folder = textBoxBatchFolder.Text;
            if (folder.Length == 0 || !Directory.Exists(folder))
            {
                MessageBox.Show("Please specify a valid folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var searchOption = checkBoxBatchSubfolders.Checked
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(folder, "*.m2", searchOption)
                .Where(f => !IsInsideDoneFolder(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0)
            {
                MessageBox.Show("No .m2 files were found in that folder.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"This will convert {files.Length} .m2 file(s) to .m2i and back, overwriting each " +
                "original file in place. This cannot be undone (unless you have backups). Continue?",
                "Confirm batch round-trip",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            RunBatchRoundTrip(files);
        }

        private static bool IsInsideDoneFolder(string filePath)
        {
            var directoryName = Path.GetFileName(Path.GetDirectoryName(filePath));
            return string.Equals(directoryName, "Done", StringComparison.OrdinalIgnoreCase);
        }

        private void RunBatchRoundTrip(string[] files)
        {
            _batchRunning = true;

            // Suppress the per-warning/error modal popups while running a batch job -
            // they would otherwise interrupt the loop after every single file.
            var previousIgnoreErrors = _ignoreErrors;
            var previousIgnoreWarnings = _ignoreWarnings;
            _ignoreErrors = true;
            _ignoreWarnings = true;

            buttonBatchStart.Enabled = false;
            buttonBatchBrowse.Enabled = false;
            textBoxBatchFolder.Enabled = false;
            checkBoxBatchSubfolders.Enabled = false;
            checkBoxBatchRemoveTxid.Enabled = false;
            checkBoxBatchFixLodSkins.Enabled = false;
            checkBoxBatchLegacySuffix.Enabled = false;

            var removeTxid = checkBoxBatchRemoveTxid.Checked;
            var fixLodSkins = checkBoxBatchFixLodSkins.Checked;
            var legacySuffix = checkBoxBatchLegacySuffix.Checked;

            progressBarBatch.Minimum = 0;
            progressBarBatch.Maximum = files.Length;
            progressBarBatch.Value = 0;

            var succeeded = 0;
            var failed = 0;

            try
            {
                foreach (var originalFile in files)
                {
                    var file = originalFile;

                    labelBatchStatus.Text = $"[{succeeded + failed + 1}/{files.Length}] {Path.GetFileName(file)}";
                    SetStatus($"Converting {Path.GetFileName(file)}...");
                    labelBatchStatus.Refresh();
                    statusStrip1.Refresh();

                    var error = ConvertM2ToM2AndBack(file, removeTxid);
                    if (error != M2LibError.OK)
                    {
                        failed++;
                        logTextBox.AppendLine(LogLevel.Error,
                            $"Failed to round-trip '{file}': {Imports.GetErrorText(error)}. Skipping to next file.");
                    }
                    else
                    {
                        if (fixLodSkins)
                        {
                            try
                            {
                                var renamed = FixLodSkins(file);
                                if (renamed > 0)
                                    logTextBox.AppendLine(LogLevel.Info,
                                        $"Folded {renamed} LOD skin(s) into the base skin count for '{Path.GetFileName(file)}'.");
                            }
                            catch (Exception lodEx)
                            {
                                logTextBox.AppendLine(LogLevel.Warning,
                                    $"Converted '{file}' successfully but failed to fold its LOD skins: {lodEx.Message}");
                            }
                        }

                        if (legacySuffix)
                        {
                            try
                            {
                                var renamedPath = FixRaceGenderSuffix(file);
                                if (renamedPath != file)
                                {
                                    logTextBox.AppendLine(LogLevel.Info,
                                        $"Renamed '{Path.GetFileName(file)}' to legacy suffix '{Path.GetFileName(renamedPath)}'.");
                                    file = renamedPath;
                                }
                            }
                            catch (Exception suffixEx)
                            {
                                logTextBox.AppendLine(LogLevel.Warning,
                                    $"Converted '{file}' successfully but failed to rename its race/gender suffix: {suffixEx.Message}");
                            }
                        }

                        try
                        {
                            MoveToDoneFolder(file);
                        }
                        catch (Exception moveEx)
                        {
                            logTextBox.AppendLine(LogLevel.Warning,
                                $"Converted '{file}' successfully but failed to move it to the Done folder: {moveEx.Message}");
                        }

                        succeeded++;
                    }

                    progressBarBatch.Value = succeeded + failed;

                    // Keep the UI responsive without needing a background thread
                    // (the native logger callback writes straight to logTextBox, which
                    // must stay on this, the UI, thread).
                    Application.DoEvents();
                }
            }
            finally
            {
                _ignoreErrors = previousIgnoreErrors;
                _ignoreWarnings = previousIgnoreWarnings;

                buttonBatchStart.Enabled = true;
                buttonBatchBrowse.Enabled = true;
                textBoxBatchFolder.Enabled = true;
                checkBoxBatchSubfolders.Enabled = true;
                checkBoxBatchRemoveTxid.Enabled = true;
                checkBoxBatchFixLodSkins.Enabled = true;
                checkBoxBatchLegacySuffix.Enabled = true;

                _batchRunning = false;
            }

            labelBatchStatus.Text = $"Done. {succeeded} succeeded, {failed} failed out of {files.Length}.";
            SetStatus("Batch round-trip finished.");

            MessageBox.Show(labelBatchStatus.Text, "Batch round-trip finished",
                MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>
        /// Folds any "&lt;stem&gt;_LOD01/02/03.skin" files sitting next to the .m2 into the main,
        /// numbered skin sequence (continuing on from the base skins, e.g. 04.skin, 05.skin,
        /// 06.skin) and bumps the "nSkinProfiles"/"nViews" count in the .m2 header to match, so a
        /// pre-Legion client (which only ever reads that count and only ever looks for
        /// "&lt;stem&gt;NN.skin" files) actually loads them instead of silently ignoring them.
        ///
        /// Supports both the classic non-chunked "MD20" header and an "MD21"-wrapped header (the
        /// same sub-header, offset by the 8-byte MD21 chunk wrapper). Any SFID chunk in an MD21
        /// file is left as-is and not inspected - this only touches nSkinProfiles and the sibling
        /// .skin filenames; reconciling anything else (e.g. an SFID chunk) is left to downstream
        /// tooling.
        ///
        /// This is pure file/header patching - it does not touch M2Lib - so it needs to run on a
        /// file that was actually saved with physical, numbered ".skin" siblings on disk (i.e.
        /// what M2Mod's own Save produces for a pre-Legion-targeting profile).
        /// Returns the number of LOD skin files that were folded in.
        /// </summary>
        private int FixLodSkins(string m2FilePath)
        {
            const int NSkinHeaderRelativeOffset = 68; // offset of nSkinProfiles/nViews within the classic sub-header
            const int MaxLodSkins = 3;                // _LOD01.skin .. _LOD03.skin

            var directory = Path.GetDirectoryName(m2FilePath);
            var stem = Path.GetFileNameWithoutExtension(m2FilePath);

            int headerBase;
            uint currentSkinCount;
            byte[] fileBytes;
            using (var stream = new FileStream(m2FilePath, FileMode.Open, FileAccess.Read))
            {
                if (stream.Length < 12)
                    return 0;

                fileBytes = new byte[stream.Length];
                var read = 0;
                while (read < fileBytes.Length)
                {
                    var n = stream.Read(fileBytes, read, fileBytes.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }
            }

            bool isMd20 = fileBytes[0] == 'M' && fileBytes[1] == 'D' && fileBytes[2] == '2' && fileBytes[3] == '0';
            bool isMd21 = fileBytes[0] == 'M' && fileBytes[1] == 'D' && fileBytes[2] == '2' && fileBytes[3] == '1';

            if (isMd20)
            {
                headerBase = 0;
            }
            else if (isMd21)
            {
                // MD21 wraps the same classic sub-header, offset by the "MD21" magic (4 bytes)
                // plus the chunk's own uint32 size field (4 bytes) = 8 bytes.
                headerBase = 8;
            }
            else
            {
                logTextBox.AppendLine(LogLevel.Warning,
                    $"'{Path.GetFileName(m2FilePath)}' has neither an MD20 nor MD21 header - skipping LOD skin fold.");
                return 0;
            }

            var nSkinOffset = headerBase + NSkinHeaderRelativeOffset;
            if (fileBytes.Length < nSkinOffset + 4)
                return 0;

            currentSkinCount = BitConverter.ToUInt32(fileBytes, nSkinOffset);

            // Find whichever _LOD0N.skin files actually exist, in order, case-insensitively.
            var lodFiles = new System.Collections.Generic.List<string>();
            for (var i = 1; i <= MaxLodSkins; i++)
            {
                var candidate = Path.Combine(directory, $"{stem}_LOD0{i}.skin");
                if (File.Exists(candidate))
                {
                    lodFiles.Add(candidate);
                    continue;
                }

                // Case-insensitive fallback (filesystem is usually case-insensitive on Windows
                // anyway, but be defensive in case Directory.GetFiles is needed instead).
                var match = Directory.GetFiles(directory, $"{stem}_LOD0{i}.skin")
                    .FirstOrDefault();
                if (match != null)
                    lodFiles.Add(match);
            }

            if (lodFiles.Count == 0)
                return 0;

            // Rename each LOD skin into the next slot after the existing base skins.
            for (var i = 0; i < lodFiles.Count; i++)
            {
                var newIndex = currentSkinCount + i;
                var newName = $"{stem}{newIndex:D2}.skin";
                var destination = Path.Combine(directory, newName);

                if (File.Exists(destination))
                    File.Delete(destination);

                File.Move(lodFiles[i], destination);
            }

            // Patch nSkinProfiles/nViews in the header to reflect the new total.
            var newSkinCount = currentSkinCount + (uint)lodFiles.Count;
            using (var stream = new FileStream(m2FilePath, FileMode.Open, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                stream.Seek(nSkinOffset, SeekOrigin.Begin);
                writer.Write(newSkinCount);
            }

            return lodFiles.Count;
        }

        /// <summary>
        /// Race/gender codes used in WoW character model filenames (Blood Elf, Draenei, Dwarf,
        /// Gnome, Human, Night Elf, Orc, Scourge/Undead, Tauren, Troll, Skeleton, Goblin), as
        /// used by MultiConverter's own helm-detection regex.
        /// </summary>
        private static readonly Regex RaceGenderSuffixRegex = new Regex(
            @"^(?<base>.+)_(?<race>be|dr|dw|gn|hu|ni|or|sc|ta|tr|sk|go)_(?<gender>[mf])$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Renames a "&lt;...&gt;_&lt;race&gt;_&lt;gender&gt;" style filename (e.g. "..._hu_m.m2") to the
        /// legacy "&lt;...&gt;_&lt;race&gt;&lt;gender&gt;" form (e.g. "..._hum.m2") that older tooling (and
        /// MultiConverter's own helm-detection regex) expects, and renames every sibling
        /// "&lt;stem&gt;*.*" file (skins, etc.) to match. Returns the new .m2 path, or the original
        /// path unchanged if the filename doesn't match that suffix pattern.
        /// </summary>
        private string FixRaceGenderSuffix(string m2FilePath)
        {
            var directory = Path.GetDirectoryName(m2FilePath);
            var stem = Path.GetFileNameWithoutExtension(m2FilePath);

            var match = RaceGenderSuffixRegex.Match(stem);
            if (!match.Success)
                return m2FilePath;

            var newStem = match.Groups["base"].Value + "_" +
                          match.Groups["race"].Value + match.Groups["gender"].Value;

            if (string.Equals(newStem, stem, StringComparison.OrdinalIgnoreCase))
                return m2FilePath;

            // Rename every sibling file that starts with the old stem (the .m2 itself, its
            // .skin files, and anything else sharing the same base name) to use the new stem.
            foreach (var siblingPath in Directory.GetFiles(directory, stem + "*"))
            {
                var siblingName = Path.GetFileName(siblingPath);
                var newSiblingName = newStem + siblingName.Substring(stem.Length);
                var newSiblingPath = Path.Combine(directory, newSiblingName);

                if (File.Exists(newSiblingPath))
                    File.Delete(newSiblingPath);

                File.Move(siblingPath, newSiblingPath);
            }

            return Path.Combine(directory, newStem + ".m2");
        }

        /// <summary>
        /// Moves a successfully-converted .m2 (and every sibling "&lt;stem&gt;*.skin" file next to it -
        /// i.e. skin00-03.skin and any _LOD01-03.skin files) into a "Done" subfolder created inside
        /// the file's own directory, so re-running the batch on the same folder won't reprocess it.
        /// </summary>
        private void MoveToDoneFolder(string m2FilePath)
        {
            var directory = Path.GetDirectoryName(m2FilePath);
            var stem = Path.GetFileNameWithoutExtension(m2FilePath);

            var doneFolder = Path.Combine(directory, "Done");
            if (!Directory.Exists(doneFolder))
                Directory.CreateDirectory(doneFolder);

            MoveFileIntoFolder(m2FilePath, doneFolder);

            foreach (var skinFile in Directory.GetFiles(directory, stem + "*.skin"))
                MoveFileIntoFolder(skinFile, doneFolder);
        }

        private static void MoveFileIntoFolder(string filePath, string destinationFolder)
        {
            var destination = Path.Combine(destinationFolder, Path.GetFileName(filePath));
            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(filePath, destination);
        }

        /// <summary>
        /// Converts a single .m2 file to .m2i and immediately back to .m2, overwriting the
        /// original file. Equivalent to doing Export -> Import by hand through the Export/Import
        /// tabs, but fully in-memory (the intermediate .m2i is written to a temp file and removed
        /// afterwards) and using the currently selected profile's settings/normalization rules.
        /// Optionally also flags the TXID chunk for removal before saving, same as the standalone
        /// TXID Remover tool does.
        /// </summary>
        private M2LibError ConvertM2ToM2AndBack(string m2FilePath, bool removeTxidChunk)
        {
            var tempM2I = Path.Combine(Path.GetTempPath(), "m2mod_batch_" + Guid.NewGuid().ToString("N") + ".m2i");

            IntPtr m2Export = IntPtr.Zero;
            IntPtr m2Import = IntPtr.Zero;
            try
            {
                // 1) Load the M2 (plus its .skin files) and export it to an intermediate .m2i file.
                m2Export = Imports.M2_Create(ref ProfileManager.CurrentProfile.Settings);

                var error = Imports.M2_Load(m2Export, m2FilePath);
                if (error != M2LibError.OK)
                    return error;

                error = Imports.M2_ExportM2Intermediate(m2Export, tempM2I);
                if (error != M2LibError.OK)
                    return error;

                Imports.M2_Free(m2Export);
                m2Export = IntPtr.Zero;

                // 2) Load the original M2 again as a fresh base, then re-import the .m2i into it -
                // this mirrors exactly what the Import tab does (Preload + Go).
                m2Import = Imports.M2_Create(ref ProfileManager.CurrentProfile.Settings);

                error = Imports.M2_Load(m2Import, m2FilePath);
                if (error != M2LibError.OK)
                    return error;

                foreach (var ruleSet in ProfileManager.CurrentProfile.Configuration.NormalizationConfig.GetRules())
                {
                    var sourceRules = ruleSet.SourceRules.Serialize().ToArray();
                    var targetRules = ruleSet.TargetRules.Serialize().ToArray();

                    error = Imports.M2_AddNormalizationRule(m2Import,
                        ruleSet.SourceType, sourceRules, sourceRules.Length,
                        ruleSet.TargetType, targetRules, targetRules.Length, ruleSet.PreferSourceDirection);
                    if (error != M2LibError.OK)
                        return error;
                }

                error = Imports.M2_ImportM2Intermediate(m2Import, tempM2I);
                if (error != M2LibError.OK)
                    return error;

                if (removeTxidChunk)
                {
                    error = Imports.M2_SetNeedRemoveTXIDChunk(m2Import);
                    if (error != M2LibError.OK)
                        return error;
                }

                // 3) Save back over the original file (and its .skin files).
                error = Imports.M2_Save(m2Import, m2FilePath, SaveMask.All);
                if (error != M2LibError.OK)
                    return error;

                return M2LibError.OK;
            }
            finally
            {
                if (m2Export != IntPtr.Zero)
                    Imports.M2_Free(m2Export);
                if (m2Import != IntPtr.Zero)
                    Imports.M2_Free(m2Import);

                try
                {
                    if (File.Exists(tempM2I))
                        File.Delete(tempM2I);
                }
                catch
                {
                    // ignored - leftover temp file isn't fatal
                }
            }
        }
    }
}
