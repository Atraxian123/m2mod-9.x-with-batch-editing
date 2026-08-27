using System;
using System.Collections.Generic;
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
        private CheckBox checkBoxBatchSkipConversion;
        private CheckBox checkBoxBatchMirrorRootDone;
        private CheckBox checkBoxBatchCopyTextures;
        private Button buttonBatchStart;
        private ProgressBar progressBarBatch;
        private Label labelBatchStatus;

        private bool _batchRunning;

        private void InitializeBatchTab()
        {
            tabBatch = new TabPage
            {
                Name = "tabBatch",
                Text = "Batch",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3)
            };

            var labelFolder = new Label
            {
                Text = "Folder containing .m2 files:",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 15)
            };

            const int leftMargin = 10;
            const int rightMargin = 10;
            const int browseButtonWidth = 100;
            const int browseButtonGap = 6;

            // Compute widths from the tab's own client width (rather than a hardcoded pixel
            // value tuned for one specific window size) so the folder textbox and the progress
            // bar correctly fill the available space regardless of how wide the window actually
            // is. The Anchor settings below then keep them filling that space as the window is
            // resized afterwards.
            var tabContentWidth = tabBatch.ClientSize.Width > 0 ? tabBatch.ClientSize.Width : 568;

            textBoxBatchFolder = new TextBox
            {
                Location = new System.Drawing.Point(leftMargin, 35),
                Size = new System.Drawing.Size(
                    Math.Max(50, tabContentWidth - leftMargin - browseButtonWidth - browseButtonGap - rightMargin),
                    20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            buttonBatchBrowse = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(tabContentWidth - rightMargin - browseButtonWidth, 33),
                Size = new System.Drawing.Size(browseButtonWidth, 24),
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

            checkBoxBatchSkipConversion = new CheckBox
            {
                Text = "Skip M2 <-> M2I conversion (only apply the operations checked above, e.g. TXID/suffix/LOD fixing)",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 157)
            };
            checkBoxBatchSkipConversion.CheckedChanged += CheckBoxBatchSkipConversion_CheckedChanged;

            checkBoxBatchMirrorRootDone = new CheckBox
            {
                Text = "Collect processed files into a mirrored \"<root folder>_done\" tree instead of per-folder \"Done\" subfolders",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 180)
            };
            checkBoxBatchMirrorRootDone.CheckedChanged += CheckBoxBatchMirrorRootDone_CheckedChanged;

            checkBoxBatchCopyTextures = new CheckBox
            {
                Text = "Also copy all .blp textures under the root folder into the mirrored tree",
                AutoSize = true,
                Location = new System.Drawing.Point(28, 203),
                Enabled = false
            };

            buttonBatchStart = new Button
            {
                Text = "Start",
                Location = new System.Drawing.Point(10, 233),
                Size = new System.Drawing.Size(120, 28)
            };
            buttonBatchStart.Click += ButtonBatchStart_Click;

            progressBarBatch = new ProgressBar
            {
                Location = new System.Drawing.Point(leftMargin, 273),
                Size = new System.Drawing.Size(tabContentWidth - leftMargin - rightMargin, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Minimum = 0,
                Maximum = 1,
                Value = 0
            };

            labelBatchStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Location = new System.Drawing.Point(leftMargin, 298),
                Size = new System.Drawing.Size(tabContentWidth - leftMargin - rightMargin, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            tabBatch.Controls.Add(labelFolder);
            tabBatch.Controls.Add(textBoxBatchFolder);
            tabBatch.Controls.Add(buttonBatchBrowse);
            tabBatch.Controls.Add(checkBoxBatchSubfolders);
            tabBatch.Controls.Add(checkBoxBatchRemoveTxid);
            tabBatch.Controls.Add(checkBoxBatchFixLodSkins);
            tabBatch.Controls.Add(checkBoxBatchLegacySuffix);
            tabBatch.Controls.Add(checkBoxBatchSkipConversion);
            tabBatch.Controls.Add(checkBoxBatchMirrorRootDone);
            tabBatch.Controls.Add(checkBoxBatchCopyTextures);
            tabBatch.Controls.Add(buttonBatchStart);
            tabBatch.Controls.Add(progressBarBatch);
            tabBatch.Controls.Add(labelBatchStatus);

            tabControl.Controls.Add(tabBatch);
        }

        private void CheckBoxBatchMirrorRootDone_CheckedChanged(object sender, EventArgs e)
        {
            // Texture copying only makes sense together with the mirrored done tree - disable
            // (without clearing) the checkbox otherwise, so the user's preference is remembered
            // if they toggle mirroring back on later.
            checkBoxBatchCopyTextures.Enabled = checkBoxBatchMirrorRootDone.Checked;
        }

        private void CheckBoxBatchSkipConversion_CheckedChanged(object sender, EventArgs e)
        {
            // When the round-trip conversion is skipped, TXID removal still needs a native
            // load/save pass, but it's the only remaining reason to touch M2Lib at all - so
            // make that dependency visible in the label instead of silently no-oping.
            checkBoxBatchRemoveTxid.Text = checkBoxBatchSkipConversion.Checked
                ? "Remove TXID chunk (still requires a load/save pass)"
                : "Remove TXID chunk";
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

            var skipConversion = checkBoxBatchSkipConversion.Checked;

            var confirmText = skipConversion
                ? $"This will process {files.Length} .m2 file(s) in place using only the operations " +
                  "checked above (no M2 <-> M2I conversion will be performed). This cannot be undone " +
                  "(unless you have backups). Continue?"
                : $"This will convert {files.Length} .m2 file(s) to .m2i and back, overwriting each " +
                  "original file in place. This cannot be undone (unless you have backups). Continue?";

            var confirm = MessageBox.Show(
                confirmText,
                "Confirm batch operation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            RunBatchRoundTrip(files, folder);
        }

        private static bool IsInsideDoneFolder(string filePath)
        {
            var directoryName = Path.GetFileName(Path.GetDirectoryName(filePath));
            return string.Equals(directoryName, "Done", StringComparison.OrdinalIgnoreCase);
        }

        private void RunBatchRoundTrip(string[] files, string rootFolder)
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
            checkBoxBatchSkipConversion.Enabled = false;
            checkBoxBatchMirrorRootDone.Enabled = false;
            checkBoxBatchCopyTextures.Enabled = false;

            var removeTxid = checkBoxBatchRemoveTxid.Checked;
            var fixLodSkins = checkBoxBatchFixLodSkins.Checked;
            var legacySuffix = checkBoxBatchLegacySuffix.Checked;
            var skipConversion = checkBoxBatchSkipConversion.Checked;
            var mirrorRootDone = checkBoxBatchMirrorRootDone.Checked;
            var copyTextures = mirrorRootDone && checkBoxBatchCopyTextures.Checked;

            if (copyTextures)
            {
                SetStatus("Copying .blp textures...");
                statusStrip1.Refresh();
                CopyAllTexturesToMirroredDone(rootFolder);
            }

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
                    SetStatus(skipConversion
                        ? $"Processing {Path.GetFileName(file)}..."
                        : $"Converting {Path.GetFileName(file)}...");
                    labelBatchStatus.Refresh();
                    statusStrip1.Refresh();

                    var error = ProcessFile(file, removeTxid, skipConversion);
                    if (error != M2LibError.OK)
                    {
                        failed++;
                        logTextBox.AppendLine(LogLevel.Error,
                            $"Failed to process '{file}': {Imports.GetErrorText(error)}. Skipping to next file.");
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
                                    $"Processed '{file}' successfully but failed to fold its LOD skins: {lodEx.Message}");
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
                                    $"Processed '{file}' successfully but failed to rename its race/gender suffix: {suffixEx.Message}");
                            }
                        }

                        try
                        {
                            if (mirrorRootDone)
                                MoveToMirroredDoneFolder(file, rootFolder);
                            else
                                MoveToDoneFolder(file);
                        }
                        catch (Exception moveEx)
                        {
                            logTextBox.AppendLine(LogLevel.Warning,
                                $"Processed '{file}' successfully but failed to move it to the Done folder: {moveEx.Message}");
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
                checkBoxBatchSkipConversion.Enabled = true;
                checkBoxBatchMirrorRootDone.Enabled = true;
                checkBoxBatchCopyTextures.Enabled = checkBoxBatchMirrorRootDone.Checked;

                _batchRunning = false;
            }

            labelBatchStatus.Text = $"Done. {succeeded} succeeded, {failed} failed out of {files.Length}.";
            SetStatus("Batch operation finished.");

            MessageBox.Show(labelBatchStatus.Text, "Batch operation finished",
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
        /// True siblings of a given .m2 stem follow one of exactly three patterns after the stem:
        /// nothing at all (the .m2/.skel file itself), a two-digit skin number ("00".."99"), or an
        /// un-folded "_LODNN" skin suffix. Anything else appearing right after the stem (e.g. an
        /// underscore followed by more name, as in "..._spirit") means the match is actually a
        /// *different* model whose name happens to start with this stem as a plain text prefix,
        /// not a sibling file of this model - this is what distinguishes
        /// "knife_1h_ulatek_d_01.m2" from "knife_1h_ulatek_d_01_spirit.m2".
        /// </summary>
        private static readonly Regex SiblingSuffixRegex = new Regex(
            @"\A(?:\d{2}|_LOD0\d)?\z",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Finds files in <paramref name="directory"/> that are true siblings of a model whose .m2
        /// stem is <paramref name="stem"/> - i.e. share that exact stem, not just a common text
        /// prefix (see <see cref="SiblingSuffixRegex"/>). <paramref name="searchPattern"/> is the
        /// Directory.GetFiles pattern appended to the stem for the initial (coarse) filesystem
        /// search, e.g. "*.skin" or "*"; the precise boundary check is then applied on top of it.
        /// </summary>
        private static IEnumerable<string> GetTrueSiblingFiles(string directory, string stem, string searchPattern)
        {
            foreach (var file in Directory.GetFiles(directory, stem + searchPattern))
            {
                var fileStem = Path.GetFileNameWithoutExtension(file);
                if (fileStem.Length < stem.Length ||
                    !fileStem.Substring(0, stem.Length).Equals(stem, StringComparison.OrdinalIgnoreCase))
                    continue;

                var suffix = fileStem.Substring(stem.Length);
                if (SiblingSuffixRegex.IsMatch(suffix))
                    yield return file;
            }
        }

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

            // Rename every TRUE sibling file (the .m2 itself, its .skin/.skel files - not files
            // belonging to some other model that merely starts with the same text) to use the
            // new stem.
            foreach (var siblingPath in GetTrueSiblingFiles(directory, stem, "*").ToArray())
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
        /// Moves a successfully-converted .m2 (and every true sibling "&lt;stem&gt;*.skin" file next
        /// to it - i.e. skin00-03.skin and any _LOD01-03.skin files, but never a *different*
        /// model's skins that merely start with the same text, e.g. "..._01_spirit00.skin" next to
        /// "..._01.m2") into a "Done" subfolder created inside the file's own directory, so
        /// re-running the batch on the same folder won't reprocess it.
        /// </summary>
        private void MoveToDoneFolder(string m2FilePath)
        {
            var directory = Path.GetDirectoryName(m2FilePath);
            var stem = Path.GetFileNameWithoutExtension(m2FilePath);

            var doneFolder = Path.Combine(directory, "Done");
            if (!Directory.Exists(doneFolder))
                Directory.CreateDirectory(doneFolder);

            MoveFileIntoFolder(m2FilePath, doneFolder);

            foreach (var skinFile in GetTrueSiblingFiles(directory, stem, "*.skin").ToArray())
                MoveFileIntoFolder(skinFile, doneFolder);
        }

        /// <summary>
        /// Alternative to <see cref="MoveToDoneFolder"/> used when the "mirrored root done tree"
        /// option is checked: instead of dropping a "Done" subfolder next to each processed file,
        /// every processed file (and its true sibling "&lt;stem&gt;*.skin" files - see
        /// <see cref="GetTrueSiblingFiles"/>) is collected into a sibling folder named
        /// "&lt;root folder name&gt;_done" that reproduces the same relative subfolder path the file
        /// had under the batch's root folder. E.g. batching "C:\Models" (with "Include subfolders"
        /// checked) and processing "C:\Models\Creatures\Wolf.m2" moves it to
        /// "C:\Models_done\Creatures\Wolf.m2", creating "C:\Models_done\Creatures" as needed.
        /// Files processed directly in the root folder land straight in "&lt;root&gt;_done" itself.
        ///
        /// Unlike <see cref="MoveToDoneFolder"/>, files here are COPIED rather than moved: the
        /// original .m2/.skin files are left in place under the root folder, since the whole point
        /// of the mirrored tree is to produce a separate, parallel copy of the processed output
        /// without disturbing the source files. Note that this means re-running the batch on the
        /// same root folder WILL reprocess the same files again (nothing here marks them as done),
        /// unlike the per-folder "Done" option.
        /// </summary>
        private void MoveToMirroredDoneFolder(string m2FilePath, string rootFolder)
        {
            var directory = Path.GetDirectoryName(m2FilePath);
            var stem = Path.GetFileNameWithoutExtension(m2FilePath);

            var mirroredRoot = rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "_done";

            var relativeDir = GetRelativeDirectory(rootFolder, directory);
            var destinationFolder = string.IsNullOrEmpty(relativeDir)
                ? mirroredRoot
                : Path.Combine(mirroredRoot, relativeDir);

            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            CopyFileIntoFolder(m2FilePath, destinationFolder);

            foreach (var skinFile in GetTrueSiblingFiles(directory, stem, "*.skin").ToArray())
                CopyFileIntoFolder(skinFile, destinationFolder);
        }

        /// <summary>
        /// Copies every ".blp" texture found anywhere under <paramref name="rootFolder"/> into the
        /// mirrored "&lt;root&gt;_done" tree, preserving each texture's own relative folder path.
        /// Run once, up front, for the whole batch (rather than per-model) since textures aren't
        /// necessarily named after, or even referenced only by, the specific .m2 files being
        /// processed - this simply reproduces the whole texture tree alongside the processed
        /// models. Files are copied (not moved) and overwrite any existing copy already there.
        /// </summary>
        private void CopyAllTexturesToMirroredDone(string rootFolder)
        {
            var mirroredRoot = rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "_done";

            string[] textureFiles;
            try
            {
                textureFiles = Directory.GetFiles(rootFolder, "*.blp", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                logTextBox.AppendLine(LogLevel.Warning, $"Failed to scan for .blp textures under '{rootFolder}': {ex.Message}");
                return;
            }

            var copied = 0;
            foreach (var textureFile in textureFiles)
            {
                var relativeDir = GetRelativeDirectory(rootFolder, Path.GetDirectoryName(textureFile));
                var destinationFolder = string.IsNullOrEmpty(relativeDir)
                    ? mirroredRoot
                    : Path.Combine(mirroredRoot, relativeDir);

                try
                {
                    if (!Directory.Exists(destinationFolder))
                        Directory.CreateDirectory(destinationFolder);

                    var destinationFile = Path.Combine(destinationFolder, Path.GetFileName(textureFile));
                    File.Copy(textureFile, destinationFile, true);
                    copied++;
                }
                catch (Exception copyEx)
                {
                    logTextBox.AppendLine(LogLevel.Warning,
                        $"Failed to copy texture '{textureFile}' into the mirrored done tree: {copyEx.Message}");
                }
            }

            if (copied > 0)
                logTextBox.AppendLine(LogLevel.Info, $"Copied {copied} .blp texture(s) into the mirrored done tree.");
        }

        /// <summary>
        /// Returns <paramref name="fullDirectory"/>'s path relative to <paramref name="rootFolder"/>
        /// (empty string if they're the same folder). Both paths are resolved to their full,
        /// trailing-separator-free form first so the comparison works regardless of how the user
        /// typed/browsed to the root folder.
        /// </summary>
        private static string GetRelativeDirectory(string rootFolder, string fullDirectory)
        {
            var rootFull = Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dirFull = Path.GetFullPath(fullDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(rootFull, dirFull, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (dirFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return dirFull.Substring(rootFull.Length + 1);

            // Shouldn't happen (every processed file comes from under the root folder), but fall
            // back to just the leaf folder name rather than throwing.
            return Path.GetFileName(dirFull);
        }

        private static void MoveFileIntoFolder(string filePath, string destinationFolder)
        {
            var destination = Path.Combine(destinationFolder, Path.GetFileName(filePath));
            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(filePath, destination);
        }

        /// <summary>
        /// Like <see cref="MoveFileIntoFolder"/>, but copies instead of moving - used for the
        /// mirrored "&lt;root&gt;_done" tree so the original file is left untouched in place.
        /// Overwrites any existing copy already at the destination.
        /// </summary>
        private static void CopyFileIntoFolder(string filePath, string destinationFolder)
        {
            var destination = Path.Combine(destinationFolder, Path.GetFileName(filePath));
            File.Copy(filePath, destination, true);
        }

        /// <summary>
        /// Dispatches a single file to either the full M2 -&gt; M2I -&gt; M2 round-trip, or - when
        /// <paramref name="skipConversion"/> is set - a lighter-weight path that leaves the mesh
        /// data completely untouched. This is what lets the batch tab double as a mass TXID
        /// fixer or a plain suffix/LOD-skin converter without forcing every file through a full
        /// conversion it doesn't need.
        /// </summary>
        private M2LibError ProcessFile(string m2FilePath, bool removeTxidChunk, bool skipConversion)
        {
            if (!skipConversion)
                return ConvertM2ToM2AndBack(m2FilePath, removeTxidChunk);

            // Conversion skipped: only touch M2Lib at all if there's actually a TXID chunk to
            // remove. If not, this file needs no native load/save - the remaining batch
            // operations (LOD folding, suffix renaming, moving to Done) all work directly on
            // the file on disk and are applied by the caller regardless of this return value.
            return removeTxidChunk
                ? RemoveTxidChunkOnly(m2FilePath)
                : M2LibError.OK;
        }

        /// <summary>
        /// Loads a single .m2 (no .m2i round-trip), flags its TXID chunk for removal, and saves
        /// it straight back over the original file. Used when the batch tab's "Skip M2 &lt;-&gt; M2I
        /// conversion" option is checked, so TXID stripping alone doesn't require a full
        /// export/import cycle.
        /// </summary>
        private M2LibError RemoveTxidChunkOnly(string m2FilePath)
        {
            IntPtr m2 = IntPtr.Zero;
            try
            {
                m2 = Imports.M2_Create(ref ProfileManager.CurrentProfile.Settings);

                var error = Imports.M2_Load(m2, m2FilePath);
                if (error != M2LibError.OK)
                    return error;

                error = Imports.M2_SetNeedRemoveTXIDChunk(m2);
                if (error != M2LibError.OK)
                    return error;

                return Imports.M2_Save(m2, m2FilePath, SaveMask.All);
            }
            finally
            {
                if (m2 != IntPtr.Zero)
                    Imports.M2_Free(m2);
            }
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
