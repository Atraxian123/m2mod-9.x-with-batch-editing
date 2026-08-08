using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace M2Mod.Interop.Structures
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Settings
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        [DefaultValue("")]
        public string OutputDirectory;

        [DefaultValue("")]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string WorkingDirectory;

        [DefaultValue("")]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string MappingsDirectory;

        public Expansion ForceLoadExpansion;
        public uint CustomFilesStartIndex;

        [MarshalAs(UnmanagedType.U1)] public bool MergeBones;
        [MarshalAs(UnmanagedType.U1)] public bool MergeAttachments;
        [MarshalAs(UnmanagedType.U1)] public bool MergeCameras;
        [MarshalAs(UnmanagedType.U1)] public bool FixSeams;
        [MarshalAs(UnmanagedType.U1)] public bool FixEdgeNormals;
        [MarshalAs(UnmanagedType.U1)] public bool IgnoreOriginalMeshIndexes;
        [MarshalAs(UnmanagedType.U1)] public bool FixAnimationsTest;

        /// <summary>
        /// When true, .skin file names are always derived from the classic
        /// "&lt;model&gt;0N.skin" / "&lt;model&gt;_LOD0N.skin" naming convention, even if the
        /// model has an SFID chunk and a listfile entry is available for its FileDataId.
        /// When false (default), the retail FileDataId/listfile lookup is tried first and
        /// this convention is only used as a fallback.
        /// </summary>
        [MarshalAs(UnmanagedType.U1)] public bool UseFallbackSkinNaming;
    }
}
