﻿namespace RadeonResetBugFixService.ThirdParty.MonitorChanger
{
    // Code taken from https://github.com/Grunge/setDisplayRes
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    // Encapsulates access to the PInvoke functions
    public class Display
    {
        public static List<DISPLAY_DEVICE> GetDisplayList(bool RetrieveMonitorname = false)
        {
            //todo: EDD_GET_DEVICE_INTERFACE_NAME            
            //const int EDD_GET_DEVICE_INTERFACE_NAME = 0x1;

            List<DISPLAY_DEVICE> displays = new List<DISPLAY_DEVICE>();
            DISPLAY_DEVICE d = new DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);
            try
            {
                for (uint id = 0; NativeMethods.EnumDisplayDevices(null, id, ref d, 0); id++)
                {
                    if (d.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop))
                    {
                        //call again to get the monitor name (not only the graka name).
                        DISPLAY_DEVICE devWithName = new DISPLAY_DEVICE();
                        devWithName.cb = Marshal.SizeOf(devWithName);

                        NativeMethods.EnumDisplayDevices(d.DeviceName, 0, ref devWithName, 0);
                        //overwrite device string and id, keep the rest!
                        d.DeviceString = devWithName.DeviceString;
                        d.DeviceID = devWithName.DeviceID;

                        displays.Add(d);
                    }//if is display                        
                }//for
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("{0}", ex.ToString()));
            }

            return displays;
        }





        // Return a list of all possible display types for this computer
        public static List<DevMode> GetDisplaySettings(string strDevName)
        {
            List<DevMode> modes = new List<DevMode>();
            DevMode devmode = DevMode;

            int counter = 0;
            int returnValue = 1;

            // A return value of zero indicates that no more settings are available
            while (returnValue != 0)
            {
                returnValue = GetSettings(strDevName, ref devmode, counter++);

                modes.Add(devmode);
            }

            return modes;
        }

        // Return the current display setting
        public int GetCurrentSettings(string strDevName, ref DevMode devmode)
        {
            return GetSettings(strDevName, ref devmode, NativeMethods.ENUM_CURRENT_SETTINGS);
        }



        //todo: CDS_UPDATEREGISTRY

        // Change the settings to the values of the DEVMODE passed
        public static string ChangeSettings(DISPLAY_DEVICE a_dev, DevMode devmode, bool bSetPrimary)
        {
            string errorMessage = "";
            ChangeDisplaySettingsFlags flags = new ChangeDisplaySettingsFlags();
            flags = ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_GLOBAL;

            ReturnCodes iRet = NativeMethods.ChangeDisplaySettingsEx(a_dev.DeviceName, ref devmode, IntPtr.Zero, flags, IntPtr.Zero);

            //same again, but with PRIMARY
            if (bSetPrimary && iRet == ReturnCodes.DISP_CHANGE_SUCCESSFUL)
            {
                SetAsPrimaryMonitor(a_dev);
            }//if primary

            switch (iRet)
            {
                case ReturnCodes.DISP_CHANGE_SUCCESSFUL:
                    break;
                case ReturnCodes.DISP_CHANGE_RESTART:
                    errorMessage = "Please restart your system";
                    break;
                case ReturnCodes.DISP_CHANGE_FAILED:
                    errorMessage = "ChangeDisplaySettigns API failed";
                    break;
                case ReturnCodes.DISP_CHANGE_BADDUALVIEW:
                    errorMessage = "The settings change was unsuccessful because system is DualView capable.";
                    break;
                case ReturnCodes.DISP_CHANGE_BADFLAGS:
                    errorMessage = "An invalid set of flags was passed in.";
                    break;
                case ReturnCodes.DISP_CHANGE_BADPARAM:
                    errorMessage = "An invalid parameter was passed in. This can include an invalid flag or combination of flags.";
                    break;
                case ReturnCodes.DISP_CHANGE_NOTUPDATED:
                    errorMessage = "Unable to write settings to the registry.";
                    break;
                default:
                    errorMessage = "Unknown return value from ChangeDisplaySettings API";
                    break;
            }
            return errorMessage;
        }

        public static void SetAsPrimaryMonitor(DISPLAY_DEVICE a_dev)
        {
            var deviceMode = new DevMode();
            NativeMethods.EnumDisplaySettings(a_dev.DeviceName, -1, ref deviceMode);
            var offsetx = deviceMode.dmPositionX;
            var offsety = deviceMode.dmPositionY;
            deviceMode.dmPositionX = 0;
            deviceMode.dmPositionY = 0;

            NativeMethods.ChangeDisplaySettingsEx(
                a_dev.DeviceName,
                ref deviceMode,
                (IntPtr)null,
                (ChangeDisplaySettingsFlags.CDS_SET_PRIMARY | ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_NORESET),
                IntPtr.Zero);

            var device = new DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(device);

            // Update other devices
            for (uint otherid = 0; NativeMethods.EnumDisplayDevices(null, otherid, ref device, 0); otherid++)
            {
                if (device.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop) && device.DeviceID != a_dev.DeviceID)
                {
                    device.cb = Marshal.SizeOf(device);
                    var otherDeviceMode = new DevMode();

                    NativeMethods.EnumDisplaySettings(device.DeviceName, -1, ref otherDeviceMode);

                    otherDeviceMode.dmPositionX -= offsetx;
                    otherDeviceMode.dmPositionY -= offsety;

                    NativeMethods.ChangeDisplaySettingsEx(
                        device.DeviceName,
                        ref otherDeviceMode,
                        (IntPtr)null,
                        (ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_NORESET),
                        IntPtr.Zero);

                }

                device.cb = Marshal.SizeOf(device);
            }

            // Apply settings
            NativeMethods.ChangeDisplaySettingsEx(null, IntPtr.Zero, (IntPtr)null, ChangeDisplaySettingsFlags.CDS_NONE, (IntPtr)null);
        }//set as primary()

        // Return a properly configured DEVMODE
        public static DevMode DevMode
        {
            get
            {
                DevMode devmode = new DevMode();
                devmode.dmDeviceName = new String(new char[32]);
                devmode.dmFormName = new String(new char[32]);
                devmode.dmSize = (short)Marshal.SizeOf(devmode);
                return devmode;
            }
        }

        // call the external function inthe Win32 API
        private static int GetSettings(string strDevName, ref DevMode devmode, int iModeNum)
        {
            // helper to wrap EnumDisplaySettings Win32 API
            return NativeMethods.EnumDisplaySettings(strDevName, iModeNum, ref devmode);
        }
    }


    // Encapsulate the magic numbers for the return value in an enumeration
    public enum ReturnCodes : int
    {
        DISP_CHANGE_SUCCESSFUL = 0,
        DISP_CHANGE_BADDUALVIEW = -6,
        DISP_CHANGE_BADFLAGS = -4,
        DISP_CHANGE_BADMODE = -2,
        DISP_CHANGE_BADPARAM = -5,
        DISP_CHANGE_FAILED = -1,
        DISP_CHANGE_NOTUPDATED = -3,
        DISP_CHANGE_RESTART = 1
    }

    // To see how the DEVMODE struct was translated from the unmanaged to the managed see the Task 2 Declarations section

    // http://msdn.microsoft.com/library/default.asp?url=/library/en-us/gdi/prntspol_8nle.asp
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DevMode
    {
        // The MarshallAs attribute is covered in the Background section of the article
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public short dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;

        public override string ToString()
        {
            return dmPelsWidth.ToString() + " x " + dmPelsHeight.ToString();
        }


        public string[] GetInfoArray()
        {
            string[] items = new string[5];

            items[0] = dmDeviceName;
            items[1] = dmPelsWidth.ToString();
            items[2] = dmPelsHeight.ToString();
            items[3] = dmDisplayFrequency.ToString();
            items[4] = dmBitsPerPel.ToString();

            return items;
        }
    }


    //Display Listening
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags()]
    public enum DEVMODE_Flags : int
    {
        DM_BITSPERPEL = 0x40000,
        DM_DISPLAYFLAGS = 0x200000,
        DM_DISPLAYFREQUENCY = 0x400000,
        DM_PELSHEIGHT = 0x100000,
        DM_PELSWIDTH = 0x80000,
        DM_POSITION = 0x20
    }

    [Flags()]
    public enum DisplayDeviceStateFlags : int
    {
        /// <summary>The device is part of the desktop.</summary>
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,
        /// <summary>The device is part of the desktop.</summary>
        PrimaryDevice = 0x4,
        /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
        MirroringDriver = 0x8,
        /// <summary>The device is VGA compatible.</summary>
        VGACompatible = 0x10,
        /// <summary>The device is removable; it cannot be the primary display.</summary>
        Removable = 0x20,
        /// <summary>The device has more display modes than its output devices support.</summary>
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
    }

    [Flags()]
    public enum ChangeDisplaySettingsFlags : uint
    {
        CDS_NONE = 0,
        CDS_UPDATEREGISTRY = 0x00000001,
        CDS_TEST = 0x00000002,
        CDS_FULLSCREEN = 0x00000004,
        CDS_GLOBAL = 0x00000008,
        CDS_SET_PRIMARY = 0x00000010,
        CDS_VIDEOPARAMETERS = 0x00000020,
        CDS_ENABLE_UNSAFE_MODES = 0x00000100,
        CDS_DISABLE_UNSAFE_MODES = 0x00000200,
        CDS_RESET = 0x40000000,
        CDS_RESET_EX = 0x20000000,
        CDS_NORESET = 0x10000000
    }

    [Flags()]
    public enum ChangeDisplayConfigFlags : uint
    {
        SDC_TOPOLOGY_INTERNAL = 0x00000001,
        SDC_TOPOLOGY_CLONE = 0x00000002,
        SDC_TOPOLOGY_EXTEND = 0x00000004,
        SDC_TOPOLOGY_EXTERNAL = 0x00000008,
        SDC_APPLY = 0x00000080,
    }

    class NativeMethods
    {
        // http://msdn.microsoft.com/library/default.asp?url=/library/en-us/gdi/devcons_84oj.asp
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern int EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DevMode lpDevMode);

        // http://msdn.microsoft.com/library/default.asp?url=/library/en-us/gdi/devcons_7gz7.asp
        [DllImport("user32.dll", CharSet = CharSet.Ansi)] //CallingConvention = CallingConvention.Cdecl
        public static extern ReturnCodes ChangeDisplaySettingsEx(string lpszDeviceName, ref DevMode lpDevMode, IntPtr hwnd, ChangeDisplaySettingsFlags dwFlags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int ChangeDisplaySettingsEx(int lpszDeviceName, int lpDevMode, int hwnd, int dwFlags, int lParam);


        [DllImport("user32.dll")]
        // A signature for ChangeDisplaySettingsEx with a DEVMODE struct as the second parameter won't allow you to pass in IntPtr.Zero, so create an overload
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, ChangeDisplaySettingsFlags dwflags, IntPtr lParam);





        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern ReturnCodes ChangeDisplaySettings(ref DevMode lpDevMode, int dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);


        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern long SetDisplayConfig(uint numPathArrayElements, IntPtr pathArray, uint numModeArrayElements, IntPtr modeArray, ChangeDisplayConfigFlags flags);


        public const int ENUM_CURRENT_SETTINGS = -1;
    }
}
