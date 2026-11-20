using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class DisplayLayout
{
    private const uint QueryFlags = 0x02 | 0x10 | 0x40; // Active paths, virtual modes and refresh rates.
    private const uint SuppliedConfig = 0x20 | 0x8000 | 0x20000;
    private const uint Validate = 0x40;
    private const uint Apply = 0x80;
    private const uint SaveToDatabase = 0x200;

    internal sealed class Display
    {
        internal int SourceIndex;
        internal long Adapter;
        internal uint TargetId;
        internal string Name;
        internal bool Internal;
        internal Rectangle Bounds;

        internal bool IsSameScreen(Display other)
        {
            return Adapter == other.Adapter && TargetId == other.TargetId;
        }
    }

    internal Display[] Displays { get; private set; }
    private PathInfo[] paths;
    private ModeInfo[] modes;

    internal static DisplayLayout Read()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            uint pathCount, modeCount;
            Check(GetDisplayConfigBufferSizes(QueryFlags, out pathCount, out modeCount));
            var paths = new PathInfo[pathCount];
            var modes = new ModeInfo[modeCount];
            int result = QueryDisplayConfig(QueryFlags, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result == 122) continue; // A monitor connected or disconnected during enumeration.
            Check(result);
            Array.Resize(ref paths, (int)pathCount);
            Array.Resize(ref modes, (int)modeCount);
            var displays = new List<Display>();
            foreach (PathInfo path in paths)
            {
                uint index = (path.Flags & 8) != 0 ? path.Source.ModeIndex >> 16 : path.Source.ModeIndex;
                if (index >= modes.Length || modes[index].Type != 1)
                    throw new InvalidOperationException("Windows did not return a position for every active display.");

                ModeInfo mode = modes[index];
                bool portrait = path.Target.Rotation == 2 || path.Target.Rotation == 4;
                bool builtIn = path.Target.Technology == 0x80000000 || path.Target.Technology == 6
                    || path.Target.Technology == 11 || path.Target.Technology == 13;
                var name = new TargetName();
                name.Header = new DeviceHeader { Type = 2, Size = (uint)Marshal.SizeOf(typeof(TargetName)),
                    Adapter = path.Target.Adapter, Id = path.Target.Id };
                int nameResult = DisplayConfigGetDeviceInfo(ref name);
                displays.Add(new Display {
                    SourceIndex = (int)index, Adapter = path.Target.Adapter, TargetId = path.Target.Id,
                    Internal = builtIn, Name = builtIn ? "Laptop" : (nameResult == 0 && !String.IsNullOrWhiteSpace(name.Name)
                        ? name.Name : "External display"),
                    Bounds = new Rectangle(mode.X, mode.Y, portrait ? mode.Height : mode.Width,
                        portrait ? mode.Width : mode.Height)
                });
            }
            return new DisplayLayout { paths = paths, modes = modes, Displays = displays.ToArray() };
        }
        throw new InvalidOperationException("The display connections are changing. Try the shortcut again in a moment.");
    }

    internal bool SharesLayout(DisplayLayout other)
    {
        return Displays.Length == other.Displays.Length && Displays.All(display => other.Displays.Any(candidate =>
            display.IsSameScreen(candidate) && candidate.Bounds == display.Bounds));
    }

    internal DisplayLayout SetPosition(Display laptop, Point position, bool save)
    {
        if (laptop.Bounds.Location == position && !save) return this;
        ModeInfo[] moved = (ModeInfo[])modes.Clone();
        moved[laptop.SourceIndex].X = position.X;
        moved[laptop.SourceIndex].Y = position.Y;
        uint applyFlags = SuppliedConfig | Apply | (save ? SaveToDatabase : 0);
        Update(moved, SuppliedConfig | Validate);
        Update(moved, applyFlags);

        DisplayLayout updated = Read();
        bool matched = Displays.Length == updated.Displays.Length && Displays.All(display =>
        {
            Rectangle expected = display.Bounds;
            if (display == laptop) expected.Location = position;
            return updated.Displays.Any(candidate => display.IsSameScreen(candidate) && candidate.Bounds == expected);
        });
        if (matched) return updated;
        if (Displays.Length == updated.Displays.Length && Displays.All(display => updated.Displays.Any(display.IsSameScreen)))
            Update(modes, applyFlags);
        throw new InvalidOperationException("Windows changed the display arrangement. Try again with the current layout.");
    }

    private void Update(ModeInfo[] config, uint flags)
    {
        Check(SetDisplayConfig((uint)paths.Length, paths, (uint)config.Length, config, flags));
    }

    private static void Check(int error)
    {
        if (error != 0) throw new Win32Exception(error);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PathSource
    {
        internal long Adapter;
        internal uint Id, ModeIndex, StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PathTarget
    {
        internal long Adapter;
        internal uint Id, ModeIndex, Technology, Rotation, Scaling;
        internal uint RefreshNumerator, RefreshDenominator, ScanLineOrdering, Available, StatusFlags;
    }

    // A Windows LUID is eight bytes but aligned to four bytes in these path structures.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PathInfo
    {
        internal PathSource Source;
        internal PathTarget Target;
        internal uint Flags;
    }

    // Only the source mode's desktop dimensions and position are edited. The other union data passes through.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct ModeInfo
    {
        [FieldOffset(0)] internal uint Type;
        [FieldOffset(16)] internal int Width;
        [FieldOffset(20)] internal int Height;
        [FieldOffset(28)] internal int X;
        [FieldOffset(32)] internal int Y;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct DeviceHeader
    {
        internal uint Type, Size;
        internal long Adapter;
        internal uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct TargetName
    {
        internal DeviceHeader Header;
        internal uint Flags, Technology;
        internal ushort EdidManufacturer, EdidProduct;
        internal uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DevicePath;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths,
        ref uint modeCount, [Out] ModeInfo[] modes, IntPtr topology);

    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(uint pathCount, [In] PathInfo[] paths, uint modeCount,
        [In] ModeInfo[] modes, uint flags);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref TargetName name);
}
