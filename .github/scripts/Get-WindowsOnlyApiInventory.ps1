#Requires -Version 7.0
<#
.SYNOPSIS
    Inventories the Windows-only APIs used by the PaperRoute.Core library
    (issue #160, "What stays"; core stage 2, step 0).

.DESCRIPTION
    Reads every PaperRoute.Core\**\*.vb under -Root except those under
    PaperRoute.Core\bin and PaperRoute.Core\obj, so a folder added to the core
    later is covered with no change here, and prints one row per hit: file, line, category, sub-category,
    status, pattern and the matched text. Then prints a per-category, per-pattern
    and per-file summary, and the project's NuGet packages with their platform
    support.

    The script is read-only, needs no build, and is deterministic: files are
    taken in ordinal order of their relative path, rows are sorted by file,
    line, category and pattern.

    How a line is read:
      * Comments are removed: a whole line starting with ' or REM, and the tail
        of a line after a ' that is outside a string literal.
      * Most patterns run on the code with the contents of string literals
        blanked out, so words inside messages, CSS or regular expressions do not
        count. Patterns marked Target=Full run on the line with its strings
        (library names in DllImport, "Local\" kernel-object names, drive letters,
        environment-variable names).
      * Matching is case-sensitive (VB is not, but the code base uses the
        declared casing of framework types; a lower-case local such as `form`
        or `icon` is not a type).
      * An Imports line is reported only by the Imports patterns.

    Status of a row:
      windows-only   The API does not exist on net10.0 without the -windows
                     suffix, or exists but throws or does nothing on Linux and
                     macOS. Each row needs a replacement before the file can move
                     to a cross-platform library.
      needs-reading  A heuristic pattern. The hit is plausible but a person must
                     read the line (the reading lists the verdicts).
      portable-note  A cross-platform API that the plan should know about (it
                     behaves differently per OS, or it is the portable form of a
                     Windows-only neighbour). It does not block the move.

    Limits: the reader is line-based. A VB statement split over several lines is
    matched line by line, so a pattern that needs two tokens on one line can
    miss a split statement (the case-insensitive path comparison pattern looks
    back 12 lines to soften this). Types reached only through type inference
    (Dim x = SomeCall()) are not seen unless their members are named.

    The script throws when it finds fewer than 160 .vb files in the core, so a
    moved or renamed folder cannot make the report pass by scanning nothing.

.PARAMETER Root
    The repository root (the folder that holds ManuscriptPipeline\ and
    PaperRoute.Core\). The Forms and Controls of ManuscriptPipeline\ are read
    only to learn the app's UI type names.

.PARAMETER AsMarkdown
    Print the summary tables as Markdown instead of the plain row listing.

.PARAMETER AsCsv
    Also write every row to this CSV path (UTF-8, no BOM).

.PARAMETER FailOnWindowsOnly
    After the report, exit with code 1 when any windows-only row was found.

.EXAMPLE
    pwsh -File Get-WindowsOnlyApiInventory.ps1 -Root C:\src\repo -AsMarkdown -AsCsv inventory.csv
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [switch] $AsMarkdown,
    [string] $AsCsv,
    [switch] $FailOnWindowsOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\', '/')
$project = Join-Path $Root 'ManuscriptPipeline'
if (-not (Test-Path -LiteralPath (Join-Path $project 'ManuscriptPipeline.vbproj'))) {
    throw "No ManuscriptPipeline\ManuscriptPipeline.vbproj under $Root."
}

$opts = [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
$ci = $opts -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase

function Rx([string] $pattern, [switch] $IgnoreCase) {
    if ($IgnoreCase) { return [regex]::new($pattern, $ci) }
    return [regex]::new($pattern, $opts)
}

# A type name in a type position: As X, As New X, New X, Of X, Inherits X,
# Implements X, Is X, GetType(X), and the type argument closing TryCast,
# DirectCast or CType. An optional namespace qualifier is allowed.
function TypePos([string[]] $names, [string] $qualifier = '(?:System\.Windows\.Forms\.|Windows\.Forms\.|Forms\.|System\.Drawing\.|Drawing\.|Drawing2D\.|Imaging\.)?') {
    $alt = ($names | Sort-Object -Unique) -join '|'
    return "(?:\bAs\s+(?:New\s+)?|\bNew\s+|\bOf\s+|\bInherits\s+|\bImplements\s+|\bIs\s+|\bGetType\(\s*|,\s*(?=(?:[\w.]+)\s*\)))$qualifier(?<api>$alt)\b"
}

# A static member access on a type, not on an instance member: X.Member where
# X is not itself preceded by a dot or a word character.
function Static([string[]] $names) {
    $alt = ($names | Sort-Object -Unique) -join '|'
    return "(?<![\.\w])(?<api>(?:$alt)\.\w+)"
}

# ---------------------------------------------------------------------------
# UI-project types (the Forms and Controls folders and the project root files).
# A Services or Models file that names one cannot move to a library that the
# UI project references: the reference would be circular.
# ---------------------------------------------------------------------------
$typeDecl = Rx '^(?<indent>\s*)(?:(?:Public|Private|Friend|Protected|Partial|NotInheritable|MustInherit|Shadows|Shared)\s+)*(?:Class|Module|Structure|Enum|Interface)\s+(?<name>\w+)'
$uiFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'Forms'), (Join-Path $project 'Controls') -Recurse -Filter *.vb) +
           @(Get-ChildItem -LiteralPath $project -Filter *.vb)
$coreRoot = Join-Path $Root 'PaperRoute.Core'
if (-not (Test-Path -LiteralPath (Join-Path $coreRoot 'PaperRoute.Core.vbproj'))) {
    throw "No PaperRoute.Core\PaperRoute.Core.vbproj under $Root."
}
# Ordinal sort of the relative paths, independent of the culture of the machine.
$coreList = [System.Collections.Generic.List[object]]::new()
foreach ($f in (Get-ChildItem -LiteralPath $coreRoot -Recurse -Filter *.vb)) {
    $top = $f.FullName.Substring($coreRoot.Length + 1).Split([char[]] @('\', '/'))[0]
    if ($top -in @('bin', 'obj')) { continue }
    $coreList.Add([pscustomobject]@{ Rel = $f.FullName.Substring($Root.Length + 1); File = $f })
}
$coreList.Sort([System.Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.Rel, $b.Rel) })
$coreFiles = @($coreList)
$minimumCoreFiles = 160
if ($coreFiles.Count -lt $minimumCoreFiles) {
    throw "Only $($coreFiles.Count) .vb files found under $coreRoot (at least $minimumCoreFiles expected); the scan would not cover the core."
}

function DeclaredTypes($files, [int] $maxIndent) {
    $names = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($f in $files) {
        foreach ($line in [System.IO.File]::ReadLines($f.FullName)) {
            $m = $typeDecl.Match($line)
            if ($m.Success -and $m.Groups['indent'].Value.Length -le $maxIndent) { [void]$names.Add($m.Groups['name'].Value) }
        }
    }
    return $names
}
$uiTypes = DeclaredTypes $uiFiles 4        # top-level types only; nested types are private to a form
$coreTypes = DeclaredTypes ($coreFiles.File) 64
$uiOnly = @($uiTypes | Where-Object { -not $coreTypes.Contains($_) -and $_ -notin @('Program', 'MyApplication') })

# ---------------------------------------------------------------------------
# Patterns. Target: Code (strings blanked) or Full (strings kept).
# ---------------------------------------------------------------------------
$Patterns = [System.Collections.Generic.List[object]]::new()
function Add-Pattern($id, $category, $sub, $status, $target, $regex, $replace) {
    $Patterns.Add([pscustomobject]@{ Id = $id; Category = $category; Sub = $sub; Status = $status; Target = $target; Regex = $regex; Replace = $replace })
}

$wf = 'System.Windows.Forms'
$dr = 'System.Drawing'

# --- Imports -------------------------------------------------------------
Add-Pattern 'imports-winforms'  $wf 'Imports' 'windows-only' 'Imports' (Rx '^\s*Imports\s+(?<api>System\.Windows\.Forms(?:\.\w+)*)') 'stays in UI project'
Add-Pattern 'imports-drawing'   $dr 'Imports' 'windows-only' 'Imports' (Rx '^\s*Imports\s+(?<api>System\.Drawing(?:\.\w+)*)') 'plain value types (ARGB) or stays in UI project'
Add-Pattern 'imports-win32'     'Microsoft.Win32' 'Imports' 'windows-only' 'Imports' (Rx '^\s*Imports\s+(?<api>Microsoft\.Win32(?!\.SafeHandles)(?:\.\w+)*)') 'interface the UI implements'
Add-Pattern 'imports-safehandles' 'P/Invoke and interop' 'Imports (portable namespace)' 'portable-note' 'Imports' (Rx '^\s*Imports\s+(?<api>Microsoft\.Win32\.SafeHandles)') 'portable (SafeFileHandle exists on every OS)'
Add-Pattern 'imports-interop'   'P/Invoke and interop' 'Imports' 'needs-reading' 'Imports' (Rx '^\s*Imports\s+(?<api>System\.Runtime\.InteropServices)') 'portable namespace; the rows below say what it is used for'
Add-Pattern 'imports-ui-project' 'App UI types (split blocker)' 'Imports' 'windows-only' 'Imports' (Rx '^\s*Imports\s+(?<api>ManuscriptPipeline\.(?:Forms|Controls))\b') 'move the caller to the UI project or invert through an interface'
Add-Pattern 'imports-vb-devices' 'VB My and Microsoft.VisualBasic' 'Imports' 'windows-only' 'Imports' (Rx '^\s*Imports\s+(?<api>Microsoft\.VisualBasic\.(?:Devices|ApplicationServices|MyServices|Logging|FileIO))') 'System.IO / interface'
Add-Pattern 'imports-other-win' 'Other Windows-only' 'Imports' 'windows-only' 'Imports' (Rx '^\s*Imports\s+(?<api>System\.(?:Management|Speech|DirectoryServices|ServiceProcess|Printing|Windows(?!\.Forms))(?:\.\w+)*|Windows\.\w+(?:\.\w+)*)') 'interface or drop'

# --- System.Windows.Forms -------------------------------------------------
Add-Pattern 'wf-qualified'   $wf 'Qualified name' 'windows-only' 'Code' (Rx '(?<api>\b(?:System\.)?Windows\.Forms\.\w+|(?<![\.\w])Forms\.Timer\b)') 'stays in UI project'
Add-Pattern 'wf-messagebox'  $wf 'MessageBox and dialogs' 'windows-only' 'Code' (Rx '(?<api>\bMessageBox(?:Buttons|Icon|DefaultButton|Options)?\b|\bDialogResult\b|\b(?:OpenFileDialog|SaveFileDialog|FolderBrowserDialog|ColorDialog|FontDialog|PrintDialog|CommonDialog|TaskDialog\w*)\b|\.ShowDialog\()') 'interface the UI implements (ask/confirm/report)'
Add-Pattern 'wf-clipboard'   $wf 'Clipboard' 'windows-only' 'Code' (Rx '(?<api>(?<![\.\w])Clipboard\.\w+|\bDataObject\b|\bDataFormats\.\w+)') 'interface the UI implements (IClipboard)'
Add-Pattern 'wf-application' $wf 'Application' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>Application\.\w+)') 'assembly metadata or interface (app identity / message loop stays in UI)'
Add-Pattern 'wf-sysinfo'     $wf 'SystemInformation' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>SystemInformation\.\w+)') 'interface the UI implements (platform settings)'
Add-Pattern 'wf-screen'      $wf 'Screen' 'windows-only' 'Code' (Rx ('(?<api>(?<![\.\w])Screen\.\w+)|' + (TypePos @('Screen')))) 'stays in UI project'
Add-Pattern 'wf-keys'        $wf 'Keys, mouse and cursors' 'windows-only' 'Code' (Rx ('(?<![\.\w])(?<api>(?:Keys|MouseButtons|Cursors|Cursor)\.\w+|Control\.ModifierKeys|Control\.MouseButtons)|' + (TypePos @('Keys', 'KeyEventArgs', 'KeyPressEventArgs', 'PreviewKeyDownEventArgs', 'MouseEventArgs', 'Cursor')))) 'stays in UI project'
Add-Pattern 'wf-textrenderer' $wf 'TextRenderer' 'windows-only' 'Code' (Rx '(?<api>\bTextRenderer\.\w+|\bTextFormatFlags\b)') 'stays in UI project (text measurement belongs to the toolkit)'
Add-Pattern 'wf-timer-forms' $wf 'Timer (WinForms)' 'needs-reading' 'Code' (Rx (TypePos @('Timer') '(?:System\.Windows\.Forms\.)?')) 'UI dispatcher timer stays in UI; a core timer uses System.Threading.Timer or PeriodicTimer'
Add-Pattern 'wf-controls'    $wf 'Form and Control types' 'windows-only' 'Code' (Rx (TypePos @(
    'Form', 'Control', 'UserControl', 'ContainerControl', 'ScrollableControl', 'Panel', 'FlowLayoutPanel', 'TableLayoutPanel',
    'SplitContainer', 'TabControl', 'TabPage', 'Label', 'LinkLabel', 'Button', 'ButtonBase', 'CheckBox', 'RadioButton', 'TextBox',
    'TextBoxBase', 'RichTextBox', 'ComboBox', 'ListBox', 'CheckedListBox', 'ListView', 'ListViewItem', 'ColumnHeader', 'TreeView', 'TreeNode',
    'DataGridView', 'PictureBox', 'ProgressBar', 'TrackBar', 'NumericUpDown', 'DateTimePicker', 'MonthCalendar', 'GroupBox',
    'MenuStrip', 'ToolStrip', 'ToolStripItem', 'ToolStripMenuItem', 'ToolStripButton', 'ToolStripRenderer', 'ToolStripProfessionalRenderer',
    'StatusStrip', 'ContextMenuStrip', 'ScrollBar', 'VScrollBar', 'HScrollBar', 'ErrorProvider', 'ImageList', 'IWin32Window',
    'NativeWindow', 'BindingSource', 'PaintEventArgs', 'DrawItemEventArgs', 'MeasureItemEventArgs', 'ToolTip', 'HelpProvider',
    'FormClosingEventArgs', 'FormClosedEventArgs', 'ItemCheckEventArgs', 'CreateParams', 'AccessibleObject', 'ControlCollection',
    'Message', 'IMessageFilter', 'VisualStyleRenderer', 'Splitter', 'Padding'))) 'stays in UI project'
Add-Pattern 'wf-enums'       $wf 'Layout and style enums' 'windows-only' 'Code' (Rx (Static @(
    'AnchorStyles', 'DockStyle', 'AutoScaleMode', 'AutoSizeMode', 'FormBorderStyle', 'FormStartPosition', 'FormWindowState',
    'BorderStyle', 'FlatStyle', 'ScrollBars', 'SizeType', 'ControlStyles', 'BoundsSpecified', 'HorizontalAlignment',
    'LeftRightAlignment', 'ImeMode', 'AccessibleRole', 'AccessibleStates', 'CloseReason', 'SystemColorMode', 'ColumnHeaderStyle',
    'SelectionMode', 'DrawMode', 'ToolStripItemDisplayStyle', 'ToolStripRenderMode', 'TabAlignment', 'TabAppearance',
    'ControlPaint', 'VisualStyleElement', 'VisualStyleState', 'MenuMerge', 'HighDpiMode', 'ArrangeDirection', 'View', 'Orientation'))) 'stays in UI project'
Add-Pattern 'wf-handle'      $wf 'Window handle and messages' 'windows-only' 'Code' (Rx '(?<api>\.IsHandleCreated\b|\.Handle\b(?!\w)|\bWndProc\b|\bHandleCreated\b|\.BeginInvoke\(|\.InvokeRequired\b)') 'stays in UI project'

# --- System.Drawing -------------------------------------------------------
Add-Pattern 'dr-qualified'   $dr 'Qualified name' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>(?:System\.)?Drawing\.(?:Drawing2D\.|Imaging\.|Text\.)?\w+)') 'plain value types or stays in UI project'
Add-Pattern 'dr-color'       $dr 'Color' 'windows-only' 'Code' (Rx ('(?<![\.\w])(?<api>Color\.\w+|ColorTranslator\.\w+|KnownColor\.\w+)|' + (TypePos @('Color')))) 'plain ARGB value (a UInt32 or a small ArgbColor structure) in the core; the UI maps it to its brush'
Add-Pattern 'dr-system-colors' $dr 'SystemColors, SystemFonts, SystemIcons' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>(?:SystemColors|SystemFonts|SystemIcons|SystemBrushes|SystemPens)\.\w+)') 'interface the UI implements (platform theme), or stays in UI project'
Add-Pattern 'dr-gdi'         $dr 'Font, Graphics, Bitmap, Image, Icon, brushes' 'windows-only' 'Code' (Rx ('(?<![\.\w])(?<api>(?:Image|Bitmap|Icon|Brushes|Pens|FontFamily|FontStyle|GraphicsUnit|ContentAlignment|StringAlignment|SmoothingMode|InterpolationMode|PixelFormat|ImageFormat|TextRenderingHint|CompositingQuality|PixelOffsetMode|ColorMatrix|ImageAttributes|ImageLockMode|GraphicsPath|StringFormat)\.\w+)|' + (TypePos @(
    'Font', 'FontFamily', 'FontStyle', 'Graphics', 'Bitmap', 'Image', 'Icon', 'Brush', 'SolidBrush', 'Pen', 'Region', 'GraphicsPath',
    'LinearGradientBrush', 'StringFormat', 'ImageAttributes', 'ColorMatrix', 'BitmapData', 'Matrix')))) 'stays in UI project (Avalonia has its own drawing types)'
Add-Pattern 'dr-geometry'    $dr 'Point, Size, Rectangle (easy)' 'windows-only' 'Code' (Rx ('(?<![\.\w])(?<api>(?:Point|PointF|Size|SizeF|Rectangle|RectangleF)\.(?:Empty|Round|Ceiling|Truncate|Inflate|Union|Intersect|Add|Subtract|FromLTRB))|' + (TypePos @('Point', 'PointF', 'Size', 'SizeF', 'Rectangle', 'RectangleF')))) 'own small record structures (or tuples) in the core; the UI maps them'

# --- Microsoft.Win32 ------------------------------------------------------
Add-Pattern 'win32-registry' 'Microsoft.Win32' 'Registry' 'windows-only' 'Code' (Rx '(?<api>\bMicrosoft\.Win32\.(?!SafeHandles)\w+|(?<![\.\w])Registry(?:Key|View|Hive|ValueKind)?\.\w+|\bAs\s+RegistryKey\b)') 'interface the UI implements, or settings file'
Add-Pattern 'win32-sysevents' 'Microsoft.Win32' 'SystemEvents' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>SystemEvents\.\w+|UserPreferenceChangedEventArgs|UserPreferenceCategory\.\w+)') 'interface the UI implements (theme or settings changed)'

# --- P/Invoke and interop -------------------------------------------------
Add-Pattern 'pinvoke-dllimport' 'P/Invoke and interop' 'DllImport / LibraryImport' 'windows-only' 'Full' (Rx '(?<api>(?:DllImport|LibraryImport)\(\s*"[^"]+")') 'per-OS implementation behind an interface, or stays in UI project'
Add-Pattern 'pinvoke-declare' 'P/Invoke and interop' 'Declare Function/Sub' 'windows-only' 'Full' (Rx '^\s*(?:(?:Public|Private|Friend|Protected|Shared)\s+)*(?<api>Declare\s+(?:Auto\s+|Ansi\s+|Unicode\s+)?(?:Function|Sub)\s+\w+\s+Lib\s+"[^"]+")') 'per-OS implementation behind an interface'
Add-Pattern 'pinvoke-marshal' 'P/Invoke and interop' 'Marshal' 'needs-reading' 'Code' (Rx '(?<![\.\w])(?<api>Marshal\.\w+|MarshalAs\(\s*UnmanagedType\.\w+)') 'goes with the P/Invoke it serves'
Add-Pattern 'pinvoke-struct' 'P/Invoke and interop' 'StructLayout / HandleRef' 'needs-reading' 'Code' (Rx '(?<api>\bStructLayout\(\s*LayoutKind\.\w+|\bHandleRef\b)') 'goes with the P/Invoke it serves'

# --- Process launch -------------------------------------------------------
Add-Pattern 'proc-start'     'Process launch' 'Process.Start' 'needs-reading' 'Code' (Rx '(?<![\.\w])(?<api>Process\.Start\b)') 'interface the UI implements (ILauncher) for documents and links; Process.Start of own executable is portable'
Add-Pattern 'proc-shell'     'Process launch' 'UseShellExecute = True' 'windows-only' 'Code' (Rx '(?<api>\.?UseShellExecute\s*=\s*True)') 'one launch helper per OS: Windows shell, macOS open, Linux xdg-open (Avalonia ILauncher)'
Add-Pattern 'proc-noshell'   'Process launch' 'UseShellExecute = False' 'portable-note' 'Code' (Rx '(?<api>\.?UseShellExecute\s*=\s*False)') 'portable'
Add-Pattern 'proc-verbs'     'Process launch' 'explorer.exe, cmd, shell verbs' 'windows-only' 'Full' (Rx '(?<api>\bexplorer(?:\.exe)?\b|\bcmd(?:\.exe)?\s|\brundll32\b|\bpowershell\.exe\b|\.Verb\s*=\s*"\w+"|"shell:\w+|"ms-settings:\w*|/select,)' -IgnoreCase) 'one launch helper per OS (reveal in file manager)'

# --- Windows folders and paths --------------------------------------------
$fp = 'Windows folders and paths'
Add-Pattern 'path-specialfolder' $fp 'Environment.SpecialFolder' 'needs-reading' 'Code' (Rx '(?<![\w])(?<api>(?:Environment\.)?SpecialFolder\.\w+)') 'Environment.SpecialFolder exists on every OS; map each folder per OS (see reading)'
Add-Pattern 'path-envvar'    $fp 'Windows environment variables' 'windows-only' 'Full' (Rx '(?<api>%(?:APPDATA|LOCALAPPDATA|USERPROFILE|PROGRAMDATA|ProgramFiles|TEMP|HOMEDRIVE|HOMEPATH|SystemRoot|windir)%|"(?:APPDATA|LOCALAPPDATA|USERPROFILE|PROGRAMDATA|ProgramFiles|HOMEDRIVE|HOMEPATH|SystemRoot|windir|COMPUTERNAME|USERNAME)")' -IgnoreCase) 'Environment.SpecialFolder or Path.GetTempPath'
Add-Pattern 'path-drive'     $fp 'Drive letter' 'windows-only' 'Full' (Rx '(?<api>"[A-Za-z]:\\[^"]*")') 'Path.Combine over a resolved root'
# A line that names "\"c together with "/"c accepts both separators, which is
# portable, so it is not reported.
Add-Pattern 'path-backslash'$fp 'Backslash as separator' 'needs-reading' 'Full' (Rx '^(?!.*"/"c).*?(?:(?<api>"\\"c(?=.*(?:Path\.|TrimEnd|LastIndexOf|IndexOfAny|Split\()))|(?<api2>(?:TrimEnd|LastIndexOf|IndexOfAny|Split)\([^)]*"\\"c))') 'Path.DirectorySeparatorChar / AltDirectorySeparatorChar, or accept both explicitly'
Add-Pattern 'path-invalidchars' $fp 'Path.GetInvalid*Chars (differs per OS)' 'needs-reading' 'Code' (Rx '(?<api>\bPath\.GetInvalid(?:FileName|Path)Chars\b)') 'an explicit portable set when the name must be valid on every OS'
Add-Pattern 'path-reserved'  $fp 'Windows reserved device names' 'portable-note' 'Full' (Rx '(?<api>"(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])")') 'keep: a Windows-safe name is safe everywhere'
Add-Pattern 'path-ignorecase' $fp 'Case-insensitive path comparison' 'needs-reading' 'Code' (Rx '(?<api>StringComparison\.OrdinalIgnoreCase|StringComparer\.OrdinalIgnoreCase)') 'compare paths with an OS-aware comparer (case-sensitive on Linux)'

# --- VB My and Microsoft.VisualBasic -------------------------------------
$vb = 'VB My and Microsoft.VisualBasic'
Add-Pattern 'vb-my'          $vb 'My namespace' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>My\.(?:Application|Computer|Settings|Resources|User|Forms|WebServices)\b(?:\.\w+)?)') 'System.Reflection / System.IO / settings file; My.Forms stays in UI'
Add-Pattern 'vb-interaction' $vb 'InputBox, MsgBox, Shell and settings' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>(?:InputBox|MsgBox|Shell|AppActivate|GetSetting|SaveSetting|DeleteSetting|GetAllSettings)\s*\(|Interaction\.\w+|MsgBoxResult\.\w+|MsgBoxStyle\.\w+)') 'interface the UI implements'
Add-Pattern 'vb-fileio'      $vb 'FileIO with RecycleOption/UIOption' 'windows-only' 'Code' (Rx '(?<api>\bFileIO\.FileSystem\.\w+|\bRecycleOption\.\w+|\bUIOption\.\w+|\bUICancelOption\.\w+)') 'System.IO, or an interface for the recycle bin'

# --- Named kernel objects and single instance -----------------------------
$ko = 'Named kernel objects and single instance'
Add-Pattern 'ko-mutex'       $ko 'Mutex' 'needs-reading' 'Code' (Rx '(?<![\.\w])(?<api>Mutex)\b') 'named Mutex works on Linux and macOS; the "Local\" prefix is accepted'
Add-Pattern 'ko-ewh'         $ko 'EventWaitHandle (named)' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>EventWaitHandle(?:\.\w+)?)\b') 'interface the UI implements (single instance), or a socket / pipe / file-lock protocol'
Add-Pattern 'ko-semaphore'   $ko 'Semaphore (named)' 'needs-reading' 'Code' (Rx '(?<![\.\w])(?<api>Semaphore)\b(?!Slim)') 'named Semaphore is Windows-only; unnamed is portable'
Add-Pattern 'ko-pipe'        $ko 'Named pipes / memory-mapped files' 'needs-reading' 'Code' (Rx '(?<api>\bNamedPipe(?:Server|Client)Stream\b|\bMemoryMappedFile\b)') 'portable with care (Unix domain sockets underneath)'
Add-Pattern 'ko-namespace'   $ko 'Local\ or Global\ object name' 'windows-only' 'Full' (Rx '(?<api>"(?:Local|Global)\\[^"]*")') 'goes with the named object above'

# --- Data protection and credentials --------------------------------------
$dp = 'Data protection and credentials'
Add-Pattern 'dp-dpapi'       $dp 'ProtectedData (DPAPI)' 'windows-only' 'Code' (Rx '(?<![\.\w])(?<api>ProtectedData\.\w+|DataProtectionScope\.\w+|ProtectedMemory\.\w+)') 'interface the UI implements (secret store): DPAPI on Windows, libsecret on Linux, Keychain on macOS'
Add-Pattern 'dp-credman'     $dp 'Credential Manager / PasswordVault' 'windows-only' 'Code' (Rx '(?<api>\bCred(?:Read|Write|Delete|Enumerate)W?\b|\bPasswordVault\b|\bPasswordCredential\b|\bWindows\.Security\.\w+)') 'interface the UI implements (secret store)'

# --- Windows notifications ------------------------------------------------
$wn = 'Windows notifications'
Add-Pattern 'wn-notifyicon'  $wn 'NotifyIcon balloon' 'windows-only' 'Code' (Rx '(?<api>\bNotifyIcon\b|\bBalloonTip\w*|\bToolTipIcon\.\w+)') 'interface the UI implements (INotifier); Linux via the desktop notification service, macOS via the user-notification service'
Add-Pattern 'wn-toast'       $wn 'Toast / AppNotification' 'windows-only' 'Code' (Rx '(?<api>\bToastNotification\w*|\bAppNotification\w*|\bWindows\.UI\.Notifications\b)') 'interface the UI implements'

# --- Other Windows-only on net10.0 ---------------------------------------
$ow = 'Other Windows-only'
Add-Pattern 'ow-apis'        $ow 'Management, EventLog, Speech, identity, ACLs' 'windows-only' 'Code' (Rx '(?<api>\bSystem\.Management\b|\bManagementObject\w*|(?<![\.\w])EventLog\b|\bSystem\.Speech\b|\bSpeechSynthesizer\b|\bWindowsIdentity\b|\bWindowsPrincipal\b|\bWindowsBuiltInRole\b|\bSystem\.DirectoryServices\b|\bServiceController\b|\bPerformanceCounter\b|\bFileSecurity\b|\bDirectorySecurity\b|\bFileSystemAclExtensions\b|(?<![\.\w])File\.(?:Encrypt|Decrypt)\b|\bPrintDocument\b)') 'interface or drop'
Add-Pattern 'ow-timezone'    $ow 'Windows time-zone IDs' 'needs-reading' 'Full' (Rx '(?<api>FindSystemTimeZoneById\("[^"]*Standard Time")') 'IANA IDs, or TimeZoneInfo.TryConvertWindowsIdToIanaId'
Add-Pattern 'ow-osdesc'      $ow 'OS description / OSVersion' 'portable-note' 'Code' (Rx '(?<![\.\w])(?<api>RuntimeInformation\.OSDescription|Environment\.OSVersion|OperatingSystem\.Is\w+)') 'portable; check the label around it'
Add-Pattern 'ow-wording'     $ow 'User text that names Windows' 'portable-note' 'Full' (Rx '(?<api>"[^"]*\bWindows\b[^"]*")') 'reword per OS, or name the OS through RuntimeInformation'
Add-Pattern 'ow-fileattr'   $ow 'Hidden/System file attributes' 'needs-reading' 'Code' (Rx '(?<api>\bFileAttributes\.(?:Hidden|System|Archive|Encrypted|Compressed|NotContentIndexed)\b)') 'on Unix hidden means a leading dot'

# --- Line endings and byte identity ---------------------------------------
# Not Windows-only, but the parity rule of #160 (a session on each OS leaves the
# data files byte-identical) fails wherever the platform newline reaches a file:
# Environment.NewLine is CR LF on Windows and LF elsewhere, StringBuilder.AppendLine
# uses it, and System.Text.Json's indented output uses it unless
# JsonSerializerOptions.NewLine is set.
$le = 'Line endings and byte identity'
Add-Pattern 'le-newline'     $le 'Environment.NewLine' 'needs-reading' 'Code' (Rx '(?<![\.\w])(?<api>Environment\.NewLine)\b') 'a fixed newline (vbCrLf or vbLf) wherever the text is written to a file; keep it for on-screen text'
Add-Pattern 'le-appendline'  $le 'StringBuilder.AppendLine' 'needs-reading' 'Code' (Rx '(?<api>\.AppendLine\()') 'Append(text).Append(fixed newline) wherever the text is written to a file'
Add-Pattern 'le-json'        $le 'JSON WriteIndented' 'needs-reading' 'Code' (Rx '(?<api>\bWriteIndented\s*:?=\s*True)') 'set JsonSerializerOptions.NewLine (or JsonWriterOptions.NewLine) to the Windows value so files stay byte-identical'
Add-Pattern 'le-explicit'    $le 'Explicit NewLine setting' 'portable-note' 'Code' (Rx '(?<api>\.NewLine\s*=\s*\w+|\bNewLine\s*:=\s*\w+)') 'already fixed'

# --- App UI types (split blocker) -----------------------------------------
$ua = 'App UI types (split blocker)'
Add-Pattern 'ui-namespace'   $ua 'Forms / Controls namespace' 'windows-only' 'Code' (Rx '(?<api>\bManuscriptPipeline\.(?:Forms|Controls)\.\w+|(?<![\.\w])(?:Forms|Controls)\.(?!Timer\b)[A-Z]\w+)') 'move the caller to the UI project or invert through an interface'
if ($uiOnly.Count -gt 0) {
    $uiAlt = ($uiOnly | ForEach-Object { [regex]::Escape($_) }) -join '|'
    Add-Pattern 'ui-type'    $ua 'UI-project type' 'windows-only' 'Code' (Rx "(?<![\.\w])(?<api>(?:$uiAlt))\b") 'move the caller to the UI project or invert through an interface'
}

# ---------------------------------------------------------------------------
# Line reader
# ---------------------------------------------------------------------------
# Returns @(fullWithoutComment, codeWithStringsBlanked).
function Split-VbLine([string] $line) {
    $trim = $line.TrimStart()
    if ($trim.StartsWith("'") -or $trim -match '^(?i:REM)(\s|$)') { return @('', '') }
    $full = [System.Text.StringBuilder]::new()
    $code = [System.Text.StringBuilder]::new()
    $inString = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $ch = $line[$i]
        if ($inString) {
            if ($ch -eq '"') {
                if ($i + 1 -lt $line.Length -and $line[$i + 1] -eq '"') {
                    [void]$full.Append('""'); [void]$code.Append('  '); $i++; continue
                }
                $inString = $false
                [void]$full.Append($ch); [void]$code.Append($ch); continue
            }
            [void]$full.Append($ch); [void]$code.Append(' '); continue
        }
        if ($ch -eq '"') { $inString = $true; [void]$full.Append($ch); [void]$code.Append($ch); continue }
        if ($ch -eq "'" -or $ch -eq [char]0x2018 -or $ch -eq [char]0x2019) { break }
        [void]$full.Append($ch); [void]$code.Append($ch)
    }
    return @($full.ToString(), $code.ToString())
}

$pathContext = Rx '\bPath\.GetFullPath\b|\bGetFullPath\(|DirectorySeparatorChar|\bPath\.GetDirectoryName\b|\bGetTempPath\(|\bPath\.Combine\b'

$rows = [System.Collections.Generic.List[object]]::new()
$fileStats = [ordered]@{}
$codeByFile = @{}
$rawByFile = @{}
$totalLines = 0
foreach ($cf in $coreFiles) {
    $rel = $cf.Rel
    $lines = [System.IO.File]::ReadAllLines($cf.File.FullName)
    $totalLines += $lines.Length
    $fileStats[$rel] = $lines.Length
    $codeLines = [string[]]::new($lines.Length)
    $codeByFile[$rel] = $codeLines
    $rawByFile[$rel] = $lines
    for ($n = 0; $n -lt $lines.Length; $n++) {
        $parts = Split-VbLine $lines[$n]
        $codeLines[$n] = $parts[1]
        $full = $parts[0]; $code = $parts[1]
        if ([string]::IsNullOrWhiteSpace($code)) { continue }
        $isImports = $code -match '^\s*Imports\s'
        foreach ($p in $Patterns) {
            if ($isImports -ne ($p.Target -eq 'Imports')) { continue }
            $text = if ($p.Target -eq 'Full') { $full } else { $code }
            $found = [System.Collections.Generic.List[string]]::new()
            foreach ($m in $p.Regex.Matches($text)) {
                $g = $m.Groups['api']
                $v = if ($g.Success) { $g.Value } elseif ($m.Groups['api2'].Success) { $m.Groups['api2'].Value } else { $m.Value }
                $v = $v.Trim()
                if (-not $found.Contains($v)) { $found.Add($v) }
            }
            if ($found.Count -eq 0) { continue }
            if ($p.Id -eq 'path-ignorecase') {
                # Only when a path API is on this line or in the 12 lines before it.
                $hit = $false
                for ($k = [Math]::Max(0, $n - 12); $k -le $n; $k++) {
                    if ($null -ne $codeLines[$k] -and $pathContext.IsMatch($codeLines[$k])) { $hit = $true; break }
                }
                if (-not $hit) { continue }
            }
            $rows.Add([pscustomobject]@{
                File = $rel; Line = $n + 1; Category = $p.Category; Sub = $p.Sub; Status = $p.Status
                Pattern = $p.Id; Api = ($found -join '; '); Replace = $p.Replace
            })
        }
    }
}

# ---------------------------------------------------------------------------
# Second pass: a file that names a top-level type declared in a file with a
# windows-only row depends on that file. The member it calls may itself be
# portable, so these rows are needs-reading; they show which files cannot move
# before their dependency is split or given a seam.
# ---------------------------------------------------------------------------
$bound = [System.Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::Ordinal)   # type -> file
$boundFiles = @($rows | Where-Object Status -EQ 'windows-only' | ForEach-Object File | Sort-Object -Unique)
foreach ($bf in $boundFiles) {
    foreach ($line in $rawByFile[$bf]) {
        $m = $typeDecl.Match($line)
        if ($m.Success -and $m.Groups['indent'].Value.Length -le 4) { $bound[$m.Groups['name'].Value] = $bf }
    }
}
if ($bound.Count -gt 0) {
    $boundRx = Rx ('(?<![\.\w])(?<api>' + (($bound.Keys | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')\b')
    # One row per (file, type): the first line that names it, and how many lines do.
    foreach ($cf in $coreFiles) {
        $codeLines = $codeByFile[$cf.Rel]
        $first = [System.Collections.Generic.SortedDictionary[string, int[]]]::new([StringComparer]::Ordinal)   # type -> (first line, line count)
        for ($n = 0; $n -lt $codeLines.Length; $n++) {
            $code = $codeLines[$n]
            if ([string]::IsNullOrWhiteSpace($code) -or $code -match '^\s*Imports\s') { continue }
            if ($typeDecl.IsMatch($code)) { continue }
            $seen = [System.Collections.Generic.HashSet[string]]::new()
            foreach ($m in $boundRx.Matches($code)) {
                $t = $m.Groups['api'].Value
                if ($bound[$t] -eq $cf.Rel -or -not $seen.Add($t)) { continue }
                if ($first.ContainsKey($t)) { $first[$t][1]++ } else { $first[$t] = @(($n + 1), 1) }
            }
        }
        foreach ($t in $first.Keys) {
            $rows.Add([pscustomobject]@{
                File = $cf.Rel; Line = $first[$t][0]; Category = 'Depends on a Windows-bound file'; Sub = 'Names a type from a file with windows-only rows'
                Status = 'needs-reading'; Pattern = 'dep-bound-type'
                Api = "$t ($([IO.Path]::GetFileName($bound[$t])), $($first[$t][1]) line$(if ($first[$t][1] -ne 1) { 's' }))"
                Replace = 'split the dependency (portable part moves, Windows part stays) or call it through an interface'
            })
        }
    }
}

# Deterministic order: file (ordinal), line, category, pattern.
$sorted = [System.Collections.Generic.List[object]]::new($rows)
$sorted.Sort([System.Comparison[object]] {
    param($a, $b)
    $c = [string]::CompareOrdinal($a.File, $b.File); if ($c -ne 0) { return $c }
    $c = $a.Line.CompareTo($b.Line); if ($c -ne 0) { return $c }
    $c = [string]::CompareOrdinal($a.Category, $b.Category); if ($c -ne 0) { return $c }
    return [string]::CompareOrdinal($a.Pattern, $b.Pattern)
})
$rows = @($sorted)

if ($AsCsv) {
    $csvPath = [System.IO.Path]::GetFullPath($AsCsv, (Get-Location).Path)
    $rows | Select-Object File, Line, Category, Sub, Status, Pattern, Api, Replace |
        ConvertTo-Csv -UseQuotes AsNeeded |
        Set-Content -LiteralPath $csvPath -Encoding utf8NoBOM
}

# ---------------------------------------------------------------------------
# Summaries
# ---------------------------------------------------------------------------
$categories = @(@($Patterns | ForEach-Object Category) + 'Depends on a Windows-bound file' | Select-Object -Unique)
$statuses = @('windows-only', 'needs-reading', 'portable-note')

function Count-By($items, [scriptblock] $where) { @($items | Where-Object $where).Count }

$catSummary = foreach ($c in $categories) {
    $r = @($rows | Where-Object Category -EQ $c)
    [pscustomobject]@{
        Category = $c
        Rows = $r.Count
        WindowsOnly = Count-By $r { $_.Status -eq 'windows-only' }
        NeedsReading = Count-By $r { $_.Status -eq 'needs-reading' }
        PortableNote = Count-By $r { $_.Status -eq 'portable-note' }
        Files = @($r | ForEach-Object File | Select-Object -Unique).Count
    }
}

$patSummary = foreach ($p in $Patterns) {
    $r = @($rows | Where-Object Pattern -EQ $p.Id)
    if ($r.Count -eq 0) { continue }
    [pscustomobject]@{
        Category = $p.Category; Sub = $p.Sub; Pattern = $p.Id; Status = $p.Status; Rows = $r.Count
        Files = (@($r | ForEach-Object { [IO.Path]::GetFileName($_.File) } | Select-Object -Unique) -join ', ')
    }
}

$fileSummary = foreach ($f in $fileStats.Keys) {
    $r = @($rows | Where-Object File -EQ $f)
    [pscustomobject]@{
        File = $f; Lines = $fileStats[$f]; Rows = $r.Count
        WindowsOnly = Count-By $r { $_.Status -eq 'windows-only' }
        NeedsReading = Count-By $r { $_.Status -eq 'needs-reading' }
        PortableNote = Count-By $r { $_.Status -eq 'portable-note' }
        Categories = (@($r | Where-Object Status -NE 'portable-note' | ForEach-Object Category | Select-Object -Unique) -join '; ')
    }
}

# NuGet packages, read from the project file. Platform support is a fixed
# table checked against each package's public documentation (see the reading);
# a package not in the table is reported as "not in the table".
$packageSupport = @{
    'Velopack'  = @{ Platforms = 'Windows, macOS, Linux'; Note = 'Velopack docs (packaging overview) apply to all three; Windows gets Setup.exe, Linux a self-updating .AppImage with no installer, macOS an .app bundle. UpdateService also uses its update UI flow through WinForms dialogs, which stays in the UI.' }
    'ClosedXML' = @{ Platforms = 'Windows, macOS, Linux'; Note = 'nuget.org: targets netstandard2.0 and 2.1; managed dependencies only (DocumentFormat.OpenXml, SixLabors.Fonts, ...). Text measurement for column widths goes through SixLabors.Fonts, so fonts on Linux matter (not checked).' }
    'Anthropic' = @{ Platforms = 'Windows, macOS, Linux'; Note = 'nuget.org: targets netstandard2.0, net8.0, net9.0; managed dependencies only.' }
}
$vbproj = [xml](Get-Content -LiteralPath (Join-Path $project 'ManuscriptPipeline.vbproj') -Raw)
$packages = @($vbproj.SelectNodes('//PackageReference') | ForEach-Object {
    $s = $packageSupport[$_.Include]
    [pscustomobject]@{
        Package = $_.Include; Version = $_.Version
        Platforms = if ($s) { $s.Platforms } else { 'not in the table' }
        Note = if ($s) { $s.Note } else { 'needs reading' }
    }
} | Sort-Object Package)
$tfm = $vbproj.SelectSingleNode('//TargetFramework').InnerText
$useWfNode = $vbproj.SelectSingleNode('//UseWindowsForms')
$useWf = if ($useWfNode) { $useWfNode.InnerText } else { 'false' }
$globalImports = @($vbproj.SelectNodes('//ItemGroup/Import') | ForEach-Object { $_.GetAttribute('Include') })

$filesWithBlocking = @($fileSummary | Where-Object { $_.WindowsOnly + $_.NeedsReading -gt 0 })
$filesClean = @($fileSummary | Where-Object { $_.WindowsOnly + $_.NeedsReading -eq 0 })
$windowsOnlyFiles = @($fileSummary | Where-Object { $_.WindowsOnly -gt 0 })
$windowsOnlyCount = Count-By $rows { $_.Status -eq 'windows-only' }
$windowsOnlyLine = "Windows-only rows: $windowsOnlyCount in $($windowsOnlyFiles.Count) files" +
    $(if ($windowsOnlyFiles.Count -gt 0) { ' (' + (($windowsOnlyFiles | ForEach-Object { "$([IO.Path]::GetFileName($_.File)) $($_.WindowsOnly)" }) -join ', ') + ')' })

if ($AsMarkdown) {
    function Md-Cell([object] $v) { ([string]$v).Replace('|', '\|') }
    function Md-Table($items, [string[]] $cols) {
        '| ' + ($cols -join ' | ') + ' |'
        '|' + (($cols | ForEach-Object { '---' }) -join '|') + '|'
        foreach ($i in $items) { '| ' + (($cols | ForEach-Object { Md-Cell $i.$_ }) -join ' | ') + ' |' }
    }
    '# Windows-only API inventory: PaperRoute.Core'
    ''
    "Generated by ``Get-WindowsOnlyApiInventory.ps1`` over ``$($coreFiles.Count)`` files ($totalLines lines) in ``PaperRoute.Core`` (not ``bin`` or ``obj``)."
    $giText = ($globalImports | ForEach-Object { '`' + $_ + '`' }) -join ', '
    "App project (``ManuscriptPipeline.vbproj``, which references the core): TargetFramework ``$tfm``, UseWindowsForms ``$useWf``, project-wide imports: $giText."
    'The project-wide imports mean a file can use System.Drawing and System.Windows.Forms types with no Imports line of its own; the patterns therefore match the type names, not only the Imports.'
    ''
    "Rows: **$($rows.Count)** (windows-only $(Count-By $rows { $_.Status -eq 'windows-only' }), needs-reading $(Count-By $rows { $_.Status -eq 'needs-reading' }), portable-note $(Count-By $rows { $_.Status -eq 'portable-note' })). Files with a windows-only or needs-reading row: **$($filesWithBlocking.Count)**; files with none: **$($filesClean.Count)**."
    ''
    $windowsOnlyLine
    ''
    '## By category'
    ''
    Md-Table $catSummary @('Category', 'Rows', 'WindowsOnly', 'NeedsReading', 'PortableNote', 'Files')
    ''
    '## By pattern'
    ''
    Md-Table $patSummary @('Category', 'Sub', 'Pattern', 'Status', 'Rows', 'Files')
    ''
    '## By file (files with at least one row)'
    ''
    Md-Table @($fileSummary | Where-Object Rows -GT 0) @('File', 'Lines', 'Rows', 'WindowsOnly', 'NeedsReading', 'PortableNote', 'Categories')
    ''
    "## Files with no windows-only or needs-reading row ($($filesClean.Count))"
    ''
    (($filesClean | ForEach-Object { [IO.Path]::GetFileName($_.File) }) -join ', ')
    ''
    '## NuGet packages of the app project'
    ''
    Md-Table $packages @('Package', 'Version', 'Platforms', 'Note')
    ''
    '## Every windows-only and needs-reading row'
    ''
    Md-Table @($rows | Where-Object Status -NE 'portable-note' | ForEach-Object {
        [pscustomobject]@{ Where = "$($_.File.Replace('\','/')):$($_.Line)"; Category = $_.Category; Sub = $_.Sub; Status = $_.Status; Api = $_.Api }
    }) @('Where', 'Category', 'Sub', 'Status', 'Api')
}
else {
    $rows | Format-Table @{ n = 'Where'; e = { "$($_.File):$($_.Line)" } }, Category, Sub, Status, Api -AutoSize -Wrap | Out-String -Width 400
    $catSummary | Format-Table -AutoSize | Out-String -Width 300
    $fileSummary | Where-Object Rows -GT 0 | Format-Table File, Lines, Rows, WindowsOnly, NeedsReading, PortableNote, Categories -AutoSize | Out-String -Width 400
    $packages | Format-Table -AutoSize -Wrap | Out-String -Width 300
    "Files scanned: $($coreFiles.Count); lines: $totalLines; rows: $($rows.Count); files with windows-only or needs-reading rows: $($filesWithBlocking.Count)"
    $windowsOnlyLine
}

if ($FailOnWindowsOnly -and $windowsOnlyCount -gt 0) {
    [Console]::Error.WriteLine("The core still uses a Windows-only API: $windowsOnlyLine.")
    exit 1
}
