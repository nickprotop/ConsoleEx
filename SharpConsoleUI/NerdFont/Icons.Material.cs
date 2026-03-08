// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

namespace SharpConsoleUI.NerdFont
{
    public static partial class Icons
    {
        /// <summary>
        /// Material Design icons from the nf-md- range.
        /// Nerd Fonts v3 maps these starting at U+F0001+.
        /// Uses surrogate pairs via \U000Fxxxx format.
        /// </summary>
        public static class Material
        {
            #region File Types

            public const string File = "\U000F0214";
            public const string FileDocument = "\U000F0219";
            public const string FileCode = "\U000F022E";
            public const string FilePdf = "\U000F0226";
            public const string FileImage = "\U000F0224";
            public const string FileMusic = "\U000F0223";
            public const string FileVideo = "\U000F022A";
            public const string FileExcel = "\U000F021B";
            public const string FileWord = "\U000F022C";
            public const string FileCabinet = "\U000F0AB6";
            public const string FileTree = "\U000F0645";

            #endregion

            #region Actions

            public const string ContentSave = "\U000F0193";
            public const string ContentCopy = "\U000F0190";
            public const string ContentPaste = "\U000F0192";
            public const string ContentCut = "\U000F0191";
            public const string Undo = "\U000F054C";
            public const string Redo = "\U000F044E";
            public const string Magnify = "\U000F0349";
            public const string Refresh = "\U000F0450";
            public const string Download = "\U000F01DA";
            public const string Upload = "\U000F0552";
            public const string Delete = "\U000F01B4";
            public const string Pencil = "\U000F03EB";
            public const string Plus = "\U000F0415";
            public const string Minus = "\U000F0374";
            public const string Close = "\U000F0156";
            public const string Check = "\U000F012C";
            public const string Sort = "\U000F04BA";
            public const string Filter = "\U000F0232";
            public const string Share = "\U000F0496";
            public const string OpenInNew = "\U000F03CC";

            #endregion

            #region UI

            public const string Home = "\U000F02DC";
            public const string Cog = "\U000F0493";
            public const string Account = "\U000F0004";
            public const string AccountGroup = "\U000F0849";
            public const string Star = "\U000F04CE";
            public const string StarOutline = "\U000F04D2";
            public const string Heart = "\U000F02D1";
            public const string HeartOutline = "\U000F02D5";
            public const string Bell = "\U000F009A";
            public const string BellOutline = "\U000F009C";
            public const string Bookmark = "\U000F00C0";
            public const string BookmarkOutline = "\U000F00C3";
            public const string Tag = "\U000F04F9";
            public const string Flag = "\U000F0239";
            public const string Eye = "\U000F0208";
            public const string EyeOff = "\U000F0209";
            public const string Calendar = "\U000F00ED";
            public const string Clock = "\U000F0954";
            public const string ClockOutline = "\U000F0150";
            public const string Map = "\U000F034D";
            public const string MapMarker = "\U000F034E";
            public const string Email = "\U000F01EE";
            public const string EmailOpen = "\U000F01EF";
            public const string Menu = "\U000F035C";
            public const string DotsVertical = "\U000F01D9";
            public const string DotsHorizontal = "\U000F01D8";
            public const string Comment = "\U000F017A";
            public const string ThumbUp = "\U000F0513";
            public const string ThumbDown = "\U000F0511";
            public const string Wrench = "\U000F0594";
            public const string TuneVertical = "\U000F066A";
            public const string Palette = "\U000F03D8";

            #endregion

            #region Arrows

            public const string ArrowUp = "\U000F005D";
            public const string ArrowDown = "\U000F0045";
            public const string ArrowLeft = "\U000F004D";
            public const string ArrowRight = "\U000F0054";
            public const string ChevronUp = "\U000F0143";
            public const string ChevronDown = "\U000F0140";
            public const string ChevronLeft = "\U000F0141";
            public const string ChevronRight = "\U000F0142";
            public const string ChevronDoubleUp = "\U000F013D";
            public const string ChevronDoubleDown = "\U000F013C";
            public const string ChevronDoubleLeft = "\U000F013B";
            public const string ChevronDoubleRight = "\U000F013A";
            public const string MenuUp = "\U000F035D";
            public const string MenuDown = "\U000F035E";
            public const string MenuLeft = "\U000F035F";
            public const string MenuRight = "\U000F0360";
            public const string SwapHorizontal = "\U000F04E1";
            public const string SwapVertical = "\U000F04E2";

            #endregion

            #region Status

            public const string CheckCircle = "\U000F05E0";
            public const string CloseCircle = "\U000F0159";
            public const string Alert = "\U000F0026";
            public const string AlertCircle = "\U000F0028";
            public const string Information = "\U000F02FC";
            public const string InformationOutline = "\U000F02FD";
            public const string HelpCircle = "\U000F02D6";
            public const string Bug = "\U000F00E4";
            public const string Shield = "\U000F0498";
            public const string ShieldCheck = "\U000F0565";
            public const string Lock = "\U000F033E";
            public const string LockOpen = "\U000F033F";
            public const string Key = "\U000F0306";
            public const string Cancel = "\U000F073A";
            public const string Loading = "\U000F0772";
            public const string ProgressCheck = "\U000F0996";

            #endregion

            #region Media

            public const string Play = "\U000F040A";
            public const string Pause = "\U000F03E4";
            public const string Stop = "\U000F04DB";
            public const string SkipNext = "\U000F04AD";
            public const string SkipPrevious = "\U000F04AE";
            public const string VolumeHigh = "\U000F057E";
            public const string VolumeLow = "\U000F0580";
            public const string VolumeOff = "\U000F0581";
            public const string MusicNote = "\U000F0387";

            #endregion

            #region Miscellaneous

            public const string Folder = "\U000F024B";
            public const string FolderOpen = "\U000F0770";
            public const string Database = "\U000F01BC";
            public const string Cloud = "\U000F015F";
            public const string CloudUpload = "\U000F0167";
            public const string CloudDownload = "\U000F0162";
            public const string Console = "\U000F018D";
            public const string CodeTags = "\U000F0174";
            public const string SourceBranch = "\U000F062C";
            public const string Table = "\U000F04EB";
            public const string ViewList = "\U000F0569";
            public const string Lightning = "\U000F0330";
            public const string Fire = "\U000F0238";
            public const string Earth = "\U000F01E7";
            public const string Wifi = "\U000F05A9";
            public const string Monitor = "\U000F0379";
            public const string Laptop = "\U000F0322";
            public const string Cellphone = "\U000F011C";
            public const string Harddisk = "\U000F02CA";
            public const string Power = "\U000F0425";
            public const string Battery = "\U000F0079";
            public const string BatteryHalf = "\U000F007E";
            public const string Package = "\U000F03D3";
            public const string Rocket = "\U000F0463";
            public const string Lightbulb = "\U000F0335";
            public const string Circle = "\U000F0765";
            public const string Square = "\U000F0764";
            public const string CheckboxMarked = "\U000F0132";
            public const string CheckboxBlank = "\U000F0131";
            public const string RadioboxMarked = "\U000F043E";
            public const string RadioboxBlank = "\U000F043D";
            public const string Puzzle = "\U000F0431";
            public const string Trophy = "\U000F0548";

            #endregion
        }
    }
}
